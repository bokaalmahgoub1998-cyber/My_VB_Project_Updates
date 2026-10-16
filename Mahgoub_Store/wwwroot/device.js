(function () {
    function tell(dotnet, value) {
        try { return dotnet.invokeMethodAsync("OnGeo", value); }
        catch (e) { return Promise.resolve(); }
    }

    function readGeoPermission() {
        if (!navigator.permissions || !navigator.permissions.query)
            return Promise.resolve("prompt");
        try {
            return navigator.permissions.query({ name: "geolocation" }).then(function (st) {
                return (st && st.state) || "prompt";
            }).catch(function () { return "prompt"; });
        } catch (e) {
            return Promise.resolve("prompt");
        }
    }

    function locate(dotnet) {
        tell(dotnet, "busy");
        if (!window.isSecureContext) { tell(dotnet, "insecure"); return; }
        if (!navigator.geolocation) { tell(dotnet, "unsupported"); return; }

        function send(pos) {
            var lat = Number(pos.coords.latitude);
            var lng = Number(pos.coords.longitude);
            if (!isFinite(lat) || !isFinite(lng)) { tell(dotnet, "failed"); return; }
            tell(dotnet, "https://maps.google.com/?q=" + lat.toFixed(6) + "," + lng.toFixed(6));
        }

        function fail(err) {
            var code = err && err.code;
            if (code === 1) tell(dotnet, "denied");
            else if (code === 2) tell(dotnet, "off");
            else tell(dotnet, "failed");
        }

        var opts = { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 };
        var optsSoft = { enableHighAccuracy: false, timeout: 15000, maximumAge: 0 };

        // يُستدعى دائماً من ضغطة المستخدم — يطلب الإذن أو يعيد قراءة الموقع.
        function start() {
            navigator.geolocation.getCurrentPosition(send, function (err) {
                if (err && err.code === 2) {
                    navigator.geolocation.getCurrentPosition(send, fail, optsSoft);
                    return;
                }
                if (err && err.code === 3) {
                    navigator.geolocation.getCurrentPosition(send, fail, optsSoft);
                    return;
                }
                fail(err);
            }, opts);
        }

        readGeoPermission().then(function (state) {
            if (state === "denied") {
                // ما زلنا نطلب من الجهاز؛ إن رفض المتصفح يظهر denied.
                start();
                return;
            }
            start();
        }, start);
    }

    window.mahgoubGeo = {
        bind: function (btn, dotnet) {
            if (!btn || !dotnet) return;
            if (btn._geoClick) {
                btn.removeEventListener("click", btn._geoClick);
                btn.removeEventListener("touchend", btn._geoTouch);
            }
            var lockUntil = 0;
            function run(ev) {
                var now = Date.now();
                if (now < lockUntil) return;
                lockUntil = now + 900;
                locate(dotnet);
            }
            btn._geoClick = run;
            btn._geoTouch = function (ev) {
                // على بعض أجهزة iOS يسبق touchend الـ click؛ ننفّذ مرة واحدة عبر القفل.
                if (ev && ev.cancelable) { /* لا preventDefault حتى لا يُلغى الإيماء */ }
                run(ev);
            };
            btn.addEventListener("click", btn._geoClick);
            btn.addEventListener("touchend", btn._geoTouch, { passive: true });
        },
        request: function (dotnet) {
            if (!dotnet) return;
            locate(dotnet);
        }
    };

    window.mahgoubNotify = {
        ask: function () {
            if (!window.isSecureContext || !("Notification" in window))
                return Promise.resolve("unsupported");
            var cur = Notification.permission;
            if (cur === "granted" || cur === "denied")
                return Promise.resolve(cur);
            try {
                var p = Notification.requestPermission();
                return p && typeof p.then === "function" ? p : Promise.resolve(Notification.permission);
            } catch (e) {
                return Promise.resolve("denied");
            }
        },
        show: function (title, body) {
            if (!window.Notification || Notification.permission !== "granted") return;
            var opts = {
                body: body || "",
                icon: "logo.svg",
                badge: "logo.svg",
                lang: "ar",
                dir: "rtl",
                tag: "mahgoub-order",
                renotify: true
            };
            var shown = false;
            function pageNote() {
                if (shown) return;
                shown = true;
                try { new Notification(title, opts); } catch (e) { }
            }
            if (navigator.serviceWorker && navigator.serviceWorker.controller) {
                navigator.serviceWorker.ready.then(function (reg) {
                    if (shown || !reg || !reg.showNotification) { pageNote(); return; }
                    shown = true;
                    return reg.showNotification(title, opts);
                }).catch(function () { shown = false; pageNote(); });
                setTimeout(pageNote, 1200);
                return;
            }
            pageNote();
        }
    };

    // طلب الإشعار من إيماءة المستخدم قبل أن يبدأ Blazor الإرسال.
    document.addEventListener("click", function (e) {
        var el = e.target && e.target.closest ? e.target.closest("[data-send-order]") : null;
        if (!el || el.disabled) return;
        window.mahgoubNotify.ask();
    }, true);
    document.addEventListener("touchend", function (e) {
        var el = e.target && e.target.closest ? e.target.closest("[data-send-order]") : null;
        if (!el || el.disabled) return;
        window.mahgoubNotify.ask();
    }, { capture: true, passive: true });

    window.mahgoubMore = {
        observe: function (el, dotnet) {
            if (!el || !window.IntersectionObserver) return;
            if (el._io) el._io.disconnect();
            var io = new IntersectionObserver(function (ents) {
                if (ents.some(function (e) { return e.isIntersecting; }))
                    dotnet.invokeMethodAsync("LoadMore");
            }, { rootMargin: "280px" });
            io.observe(el);
            el._io = io;
        }
    };
})();
