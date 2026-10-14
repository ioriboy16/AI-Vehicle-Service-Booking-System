const CACHE_NAME = "serish-autocare-v2";


// =============================================
// INSTALL
// =============================================

self.addEventListener("install", (event) => {
    self.skipWaiting();
});


// =============================================
// ACTIVATE
// =============================================

self.addEventListener("activate", (event) => {

    event.waitUntil(
        self.clients.claim()
    );

});


// =============================================
// FETCH
// Always prefer live network data.
// =============================================

self.addEventListener("fetch", (event) => {

    event.respondWith(
        fetch(event.request)
            .catch(() =>
                caches.match(event.request)
            )
    );

});


// =============================================
// WEB PUSH NOTIFICATION
// Runs even when the PWA is not currently open.
// =============================================

self.addEventListener("push", (event) => {

    let data = {
        title: "Serish AutoCare",
        message: "You have a new notification.",
        url: "/"
    };


    if (event.data) {

        try {

            const receivedData =
                event.data.json();

            data = {
                ...data,
                ...receivedData
            };

        }
        catch {

            data.message =
                event.data.text();

        }

    }


    const options = {

        body: data.message,

        icon: "/icons/icon-192.png",

        badge: "/icons/icon-192.png",

        data: {
            url: data.url || "/"
        }

    };


    event.waitUntil(
        self.registration.showNotification(
            data.title || "Serish AutoCare",
            options
        )
    );

});


// =============================================
// NOTIFICATION CLICK
// Open Serish AutoCare when user taps notification.
// =============================================

self.addEventListener(
    "notificationclick",
    (event) => {

        event.notification.close();


        const targetUrl =
            event.notification.data?.url || "/";


        event.waitUntil(

            clients.matchAll({
                type: "window",
                includeUncontrolled: true
            })
                .then((clientList) => {

                    // If Serish AutoCare is already open,
                    // focus the existing window.

                    for (const client of clientList) {

                        if (
                            client.url.startsWith(
                                self.location.origin
                            ) &&
                            "focus" in client
                        ) {

                            if ("navigate" in client) {
                                client.navigate(targetUrl);
                            }

                            return client.focus();
                        }

                    }


                    // Otherwise open a new PWA/browser window.

                    if (clients.openWindow) {

                        return clients.openWindow(
                            targetUrl
                        );

                    }

                })

        );

    }
);