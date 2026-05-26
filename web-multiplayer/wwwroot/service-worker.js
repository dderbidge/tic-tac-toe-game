// Tombstone service worker for users who previously visited
// tictactoe.derbysquad.info while it served the Blazor WASM PWA (CT142, deprecated 2026-05-08).
//
// The old PWA registered a service worker that aggressively cached the static
// build. Now that the URL serves the new Blazor Server app, those caches must be
// cleared and the old worker unregistered so users see the live app instead of
// a cached snapshot.
//
// This script:
//   1. Skips the "waiting" phase so it activates immediately on install.
//   2. Deletes every cache the browser has for this origin.
//   3. Navigates every open client to its current URL (forces a full reload
//      from the network — bypassing the dying service worker).
//   4. Unregisters itself so subsequent visits go straight to the network.

self.addEventListener('install', (event) => {
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil((async () => {
        const cacheNames = await caches.keys();
        await Promise.all(cacheNames.map((c) => caches.delete(c)));

        const clients = await self.clients.matchAll({ type: 'window' });
        for (const client of clients) {
            try { client.navigate(client.url); } catch { /* ignore */ }
        }

        await self.registration.unregister();
    })());
});

// No fetch handler — every request goes straight to the network.
