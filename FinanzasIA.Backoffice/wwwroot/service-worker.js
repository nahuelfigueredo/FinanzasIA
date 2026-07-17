const cacheName = 'finanzas-ia-v5';
const offlineAssets = [
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
      // Cachear cada asset por separado: si uno falla, el service worker se instala igual.
      .then(cache => Promise.allSettled(offlineAssets.map(asset => cache.add(asset))))
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
        // Solo cachear respuestas válidas y no redirigidas (la Cache API rechaza redirecciones).
        if (response.ok && !response.redirected && response.type === 'basic') {
          const responseCopy = response.clone();
          caches.open(cacheName).then(cache => cache.put(event.request, responseCopy)).catch(() => { });
        }
        return response;
      })
      .catch(() => caches.match(event.request).then(response => response || caches.match('/manifest.webmanifest')))
  );
});
