const cacheName = 'finanzas-ia-v4';
const offlineAssets = [
  '/',
  '/manifest.webmanifest',
  '/app-icon.svg',
  '/app-icon-512.png',
  '/app-icon-192.png',
  '/app-icon-maskable-512.png',
  '/app-icon-maskable-192.png',
  '/brand-icon.svg',
  '/app.css'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(cacheName)
      .then(cache => cache.addAll(offlineAssets))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.map(key => key === cacheName ? undefined : caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') {
    return;
  }

  event.respondWith(
    fetch(event.request)
      .then(response => {
        const responseCopy = response.clone();
        caches.open(cacheName).then(cache => cache.put(event.request, responseCopy));
        return response;
      })
      .catch(() => caches.match(event.request).then(response => response || caches.match('/')))
  );
});
