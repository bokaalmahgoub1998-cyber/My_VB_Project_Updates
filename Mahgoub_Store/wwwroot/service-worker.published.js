// يخزّن ملفات التشغيل فقط. لا يخزّن الإعدادات ولا واجهة الطلبات ولا HTML.
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('notificationclick', event => {
    event.notification.close();
    event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
        for (const client of list) {
            if ('focus' in client)
                return client.focus();
        }
        return self.clients.openWindow('./');
    }));
});

const cacheName = 'mahgoub-store-shell-v4';
const shell = /\.(wasm|dll|dat|blat|pdb|css|woff2?|svg|png|jpe?g|webp|ico|js)$/i;

async function onInstall() {
    self.skipWaiting();
}

async function onActivate() {
    const keys = await caches.keys();
    await Promise.all(keys.filter(key => key !== cacheName).map(key => caches.delete(key)));
    await self.clients.claim();
}

function isShell(url) {
    if (url.origin !== self.origin)
        return false;
    const path = url.pathname;
    if (path.startsWith('/api/') || path.includes('appsettings') || path.endsWith('.json') || path.endsWith('.html'))
        return false;
    if (path.endsWith('/service-worker.js') || path.endsWith('/device.js'))
        return false;
    return shell.test(path);
}

async function onFetch(event) {
    const req = event.request;
    if (req.method !== 'GET')
        return fetch(req);

    const url = new URL(req.url);
    if (!isShell(url))
        return fetch(req);

    const cache = await caches.open(cacheName);
    try {
        const fresh = await fetch(req);
        if (fresh.ok)
            await cache.put(req, fresh.clone());
        return fresh;
    } catch {
        return (await cache.match(req)) || Response.error();
    }
}
