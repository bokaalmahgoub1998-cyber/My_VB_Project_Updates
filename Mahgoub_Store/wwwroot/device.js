(function () {
    function tell(dotnet, value) {
        try { return dotnet.invokeMethodAsync("OnGeo", value); }
        catch (e) { return Promise.resolve(); }
    }

    /**
     * طلب الموقع يجب أن يبدأ داخل ضغطة المستخدم مباشرة.
     * أي نداء Blazor أو permissions.query قبل getCurrentPosition
     * قد يمنع ظهور نافذة النظام على Chrome/Safari في الملف الشخصي العادي.
     */
    function locate(dotnet) {
        if (!window.isSecureContext) {
            if (dotnet) tell(dotnet, "insecure");
            return;
        }
        if (!navigator.geolocation) {
            if (dotnet) tell(dotnet, "unsupported");
            return;
        }

        function send(pos) {
            var lat = Number(pos.coords.latitude);
            var lng = Number(pos.coords.longitude);
            if (!isFinite(lat) || !isFinite(lng)) {
                if (dotnet) tell(dotnet, "failed");
                return;
            }
            if (dotnet)
                tell(dotnet, "https://maps.google.com/?q=" + lat.toFixed(6) + "," + lng.toFixed(6));
        }

        function fail(err) {
            if (!dotnet) return;
            var code = err && err.code;
            if (code === 1) tell(dotnet, "denied");
            else if (code === 2) tell(dotnet, "off");
            else tell(dotnet, "failed");
        }

        var opts = { enableHighAccuracy: true, timeout: 25000, maximumAge: 0 };

        // مهم: أول سطر فعّال بعد التحقق هو getCurrentPosition — داخل نفس إيماءة الضغطة.
        try {
            navigator.geolocation.getCurrentPosition(send, function (err) {
                // موقع مطفأ / مهلة: محاولة ثانية أخف لتظهر شاشة تشغيل الموقع على أندرويد.
                if (err && (err.code === 2 || err.code === 3)) {
                    navigator.geolocation.getCurrentPosition(send, fail, {
                        enableHighAccuracy: false,
                        timeout: 20000,
                        maximumAge: 0
                    });
                    return;
                }
                fail(err);
            }, opts);
        } catch (e) {
            fail({ code: 1 });
            return;
        }

        if (dotnet) tell(dotnet, "busy");
    }

    function bindButton(btn, dotnet) {
        if (!btn || !dotnet) return;
        if (btn._geoRun) {
            btn.removeEventListener("click", btn._geoRun);
            // إزالة مستمع قديم إن وُجد من نسخة سابقة
            if (btn._geoTouch)
                btn.removeEventListener("touchend", btn._geoTouch);
        }
        var lockUntil = 0;
        function run(ev) {
            // نتجاهل النقر الاصطناعي المكرر بعد touch على بعض الأجهزة إن لزم
            var now = Date.now();
            if (now < lockUntil) return;
            lockUntil = now + 1500;
            locate(dotnet);
        }
        btn._geoRun = run;
        btn._geoTouch = null;
        // click فقط: إيماءة معتمدة لطلب الموقع على أندرويد وآيفون.
        // touchend + click كانا يطلقان طلبين ويُلغيان نافذة النظام أحياناً في المتصفح العادي.
        btn.addEventListener("click", btn._geoRun);
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
