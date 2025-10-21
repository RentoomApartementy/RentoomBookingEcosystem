window.leafletMap = {
    map: null,
    markerCluster: null,
    mediaCache: new Map(),

    createMap: function (id, lat, lng, zoom) {
        if (!this.map) {
            this.map = L.map(id, { attributionControl: false }).setView([lat, lng], zoom);

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '',
            }).addTo(this.map);

            this.defaultView = { lat: lat, lng: lng, zoom: zoom };

            this.map.on('click', () => {
                this.map.flyTo([this.defaultView.lat, this.defaultView.lng], this.defaultView.zoom);
            });
        }
    },

    addMarkers: function (markers) {
        if (!this.map || !Array.isArray(markers)) return;

        if (this.markerCluster) {
            this.map.removeLayer(this.markerCluster);
        }

        this.markerCluster = L.markerClusterGroup({ showCoverageOnHover: false });

        markers.forEach(m => {
            const lat = parseFloat(m.lat);
            const lng = parseFloat(m.lng);
            if (isNaN(lat) || isNaN(lng)) return;

            const defaultIcon = `
                <div class="marker">
                    <img src="/assets/svgs/marker.svg" alt="marker" style="width: 35px; height: 35px;" />
                </div>
            `;

            const priceIcon = `
                <div class="marker-offer">
                    <span class="marker-price">${Math.round(m.price)} zł</span>
                </div>
            `;

            const htmlContent = m.hasOffer ? priceIcon : defaultIcon;

            const customIcon = L.divIcon({
                className: 'custom-marker',
                html: htmlContent,
                iconSize: m.hasOffer ? [70, 35] : [35, 35],
                iconAnchor: m.hasOffer ? [35, 35] : [17, 35],
                popupAnchor: [0, -40]
            });

            const marker = L.marker([lat, lng], { icon: customIcon });

            if (m.popup)
                marker.bindPopup(m.popup);

            marker.on('click', async function () {
                if (m.objRef && m.id) {
                    if (window.leafletMap.mediaCache.has(m.id)) {
                        const cachedUrl = window.leafletMap.mediaCache.get(m.id);
                        updatePopupImage(m.id, cachedUrl);
                        return;
                    }

                    try {
                        const imageUrl = await m.objRef.invokeMethodAsync('OnMarkerClicked', m.id);
                        if (imageUrl) {
                            window.leafletMap.mediaCache.set(m.id, imageUrl);
                            updatePopupImage(m.id, imageUrl);
                        }
                    } catch (err) {
                        console.error('Error:', err);
                    }
                }
            });

            this.markerCluster.addLayer(marker);
        });

        this.map.addLayer(this.markerCluster);

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
    },

    clearMarkers: function () {
        if (this.map && this.markerCluster) {
            this.map.removeLayer(this.markerCluster);
            this.markerCluster = null;
        }
    },

    refreshMap: function() {
        if (this.map) {
            this.map.invalidateSize();
        }
    },
};
