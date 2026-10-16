(function () {
    function tell(dotnet, value) {
        try { return dotnet.invokeMethodAsync("OnGeo", value); }
        catch (e) { return Promise.resolve(); }
    }

    /**
     * مهم: getCurrentPosition يُستدعى فوراً داخل ضغطة المستخدم.
     * أي انتظار (permissions.query) يلغي إيماءة النظام على iOS/Android فلا تظهر شاشة الإذن.
     */
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

        var hi = { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 };
        var soft = { enableHighAccuracy: false, timeout: 16000, maximumAge: 0 };

        navigator.geolocation.getCurrentPosition(send, function (err) {
            // GPS مطفأ أو مهلة — إعادة طلب بدقة أخرى لتظهر شاشة تشغيل الموقع على أندرويد.
            if (err && (err.code === 2 || err.code === 3)) {
                navigator.geolocation.getCurrentPosition(send, fail, soft);
                return;
            }
            fail(err);
        }, hi);
    }

    function bindButton(btn, dotnet) {
        if (!btn || !dotnet) return;
        if (btn._geoRun) {
            btn.removeEventListener("click", btn._geoRun);
            btn.removeEventListener("touchend", btn._geoTouch);
        }
        var lockUntil = 0;
        function run() {
            var now = Date.now();
            if (now < lockUntil) return;
            lockUntil = now + 1000;
            locate(dotnet);
        }
        btn._geoRun = run;
        btn._geoTouch = function () { run(); };
        btn.addEventListener("click", btn._geoRun);
        btn.addEventListener("touchend", btn._geoTouch, { passive: true });
    }

    window.mahgoubGeo = {
        bind: bindButton,
        bindMany: function (buttons, dotnet) {
            if (!buttons || !dotnet) return;
            for (var i = 0; i < buttons.length; i++)
                bindButton(buttons[i], dotnet);
        },
        request: function (dotnet) {
            if (dotnet) locate(dotnet);
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
