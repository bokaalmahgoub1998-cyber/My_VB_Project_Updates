// Network-first PWA: live POS must not be served from a stale HTML/JSON cache.
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'mahgoub-store-v2-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html$/, /\.js$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.svg$/, /\.blat$/, /\.dat$/ ];
const offlineAssetsExclude = [
    /^service-worker\.js$/,
    /^service-worker-assets\.js$/,
    /^_redirects$/,
    /^_headers$/,
    /^_routes\.json$/
];

const base = "/";
const baseUrl = new URL(base, self.origin);

async function onInstall() {
    self.skipWaiting();
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !asset.url.includes('appsettings'))
        .map(asset => new Request(asset.url, { cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    for (const req of assetsRequests) {
        try {
            const resp = await fetch(req);
            if (resp.ok) await cache.put(req, resp);
        } catch {
            /* أصل واحد فاشل لا يُسقط التثبيت كله */
        }
    }
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(
        cacheKeys
            .filter(key => key !== cacheName)
            .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetch(event) {
    const req = event.request;
    if (req.method !== 'GET')
        return fetch(req);

    const url = new URL(req.url);
    if (url.origin === self.origin && url.pathname.startsWith('/api/'))
        return fetch(req);

    if (req.mode === 'navigate') {
        try {
            const fresh = await fetch(req);
            if (fresh.ok) {
                const cache = await caches.open(cacheName);
                await cache.put('index.html', fresh.clone());
            }
            return fresh;
        } catch {
            const cache = await caches.open(cacheName);
            return (await cache.match('index.html')) || Response.error();
        }
    }

    try {
        const fresh = await fetch(req);
        if (fresh.ok && url.origin === self.origin) {
            const cache = await caches.open(cacheName);
            await cache.put(req, fresh.clone());
        }
        return fresh;
    } catch {
        const cache = await caches.open(cacheName);
        const cached = await cache.match(req);
        return cached || Response.error();
    }
}
