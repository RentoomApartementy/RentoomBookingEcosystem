window.leafletMap = {
    createMap: function (id, lat, lng, zoom) {
        const map = L.map(id).setView([lat, lng], zoom);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19
        }).addTo(map);

        window._currentMap = map;
        window._defaultView = { lat: lat, lng: lng, zoom: zoom };

        map.on('click', function(e) {
            map.flyTo([window._defaultView.lat, window._defaultView.lng], window._defaultView.zoom);
        });
    },

    addMarkers: function (markers) {
        if (!window._currentMap) return;

        if (!Array.isArray(markers)) {
            console.error("addMarkers: markers is not an array", markers);
            return;
        }

        markers.forEach(m => {
            const lat = parseFloat(m.lat);
            const lng = parseFloat(m.lng);
            if (isNaN(lat) || isNaN(lng)) return;

            const marker = L.marker([lat, lng]).addTo(window._currentMap);

            if (m.popup)
                marker.bindPopup(m.popup);

            marker.on('click', function () {
                const zoomLevel = window._currentMap.getZoom() < 16 ? 16 : window._currentMap.getZoom();
                window._currentMap.flyTo([lat, lng], zoomLevel);

                if (window.DotNet && m.id) {
                    DotNet.invokeMethodAsync('RentoomBookingWeb', 'OnMarkerClicked', m.id);
                }
            });

            marker.on('popupclose', function () {
                window._currentMap.flyTo([window._defaultView.lat, window._defaultView.lng], window._defaultView.zoom);
            });
        });
    }
};
