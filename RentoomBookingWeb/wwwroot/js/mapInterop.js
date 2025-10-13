window.leafletMap = {
    createMap: function (id, lat, lng, zoom) {
        const map = L.map(id).setView([lat, lng], zoom);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19
        }).addTo(map);

        window._currentMap = map;
        window._defaultView = { lat: lat, lng: lng, zoom: zoom };

        map.on('click', function() {
            map.flyTo([window._defaultView.lat, window._defaultView.lng], window._defaultView.zoom);
        });
    },

    addMarkers: function (markers) {
        if (!window._currentMap) return;
        if (!Array.isArray(markers)) {
            console.error("markers is not an array", markers);
            return;
        }

        const mediaCache = new Map();

        markers.forEach(m => {
            const lat = parseFloat(m.lat);
            const lng = parseFloat(m.lng);
            if (isNaN(lat) || isNaN(lng)) return;

            const marker = L.marker([lat, lng]).addTo(window._currentMap);

            if (m.popup)
                marker.bindPopup(m.popup);

            marker.on('click', async function () {
                const zoomLevel = window._currentMap.getZoom() < 16 ? 16 : window._currentMap.getZoom();
                window._currentMap.flyTo([lat, lng], zoomLevel);

                if (m.objRef && m.id) {
                    if (mediaCache.has(m.id)) {
                        const cachedUrl = mediaCache.get(m.id);
                        updatePopupImage(m.id, cachedUrl);
                        return;
                    }

                    try {
                        const imageUrl = await m.objRef.invokeMethodAsync('OnMarkerClicked', m.id);
                        if (imageUrl) {
                            mediaCache.set(m.id, imageUrl);
                            updatePopupImage(m.id, imageUrl);
                        }
                    } catch (err) {
                        console.error('Error:', err);
                    }
                }
            });
        });

        function updatePopupImage(id, url) {
            const popupEl = document.getElementById(`popup-${id}`);
            if (!popupEl) return;

            const imgContainer = popupEl.querySelector("div");
            if (imgContainer) {
                imgContainer.innerHTML = `
                    <img src="${url}" style="height: 130px; width: 250px; object-fit: cover; border-radius: .5rem;" />
                `;
            }
        }
    }
};
