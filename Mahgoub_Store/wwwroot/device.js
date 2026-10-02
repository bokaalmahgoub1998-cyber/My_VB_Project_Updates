window.mahgoubGeo = {
    bind: function (btn, dotnet) {
        if (!btn) return;
        if (btn._geoClick) btn.removeEventListener("click", btn._geoClick);
        btn._geoClick = function () {
            if (!window.isSecureContext) { dotnet.invokeMethodAsync("OnGeo", "insecure"); return; }
            if (!navigator.geolocation) { dotnet.invokeMethodAsync("OnGeo", "unsupported"); return; }
            function send(pos) {
                var lat = Number(pos.coords.latitude);
                var lng = Number(pos.coords.longitude);
                if (!isFinite(lat) || !isFinite(lng)) { dotnet.invokeMethodAsync("OnGeo", "failed"); return; }
                dotnet.invokeMethodAsync("OnGeo", "https://maps.google.com/?q=" + lat.toFixed(6) + "," + lng.toFixed(6));
            }
            function fail(err) {
                var code = err && err.code;
                dotnet.invokeMethodAsync("OnGeo", code === 1 ? "denied" : code === 2 ? "off" : "failed");
            }
            navigator.geolocation.getCurrentPosition(send, function (err) {
                if (err && err.code === 2) {
                    navigator.geolocation.getCurrentPosition(send, fail, { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 });
                    return;
                }
                fail(err);
            }, { enableHighAccuracy: false, timeout: 12000, maximumAge: 0 });
        };
        btn.addEventListener("click", btn._geoClick);
    }
};

window.mahgoubNotify = {
    ask: function () {
        if (!window.isSecureContext || !window.Notification) return;
        try { Notification.requestPermission(); } catch (e) { }
    },
    show: function (title, body) {
        if (!window.Notification || Notification.permission !== "granted") return;
        var opts = { body: body || "", icon: "logo.svg", lang: "ar", dir: "rtl", tag: "mahgoub-order", renotify: true };
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
