const DEFAULT_MAP_LABELS = {
    apartmentsPrefix: "Apartments",
    offersPrefix: "Offers",
    noOffers: "no offers",
    currency: "PLN"
};

function getMapLabels(rawLabels) {
    return { ...DEFAULT_MAP_LABELS, ...(rawLabels || {}) };
}

window.ensureLeafletLoaded = async function () {
    if (!window.L) {
        throw new Error("Leaflet assets are not loaded.");
    }

    if (typeof L.markerClusterGroup !== "function") {
        throw new Error("Leaflet MarkerCluster assets are not loaded.");
    }
};

window.leafletMap = {
    map: null,
    markerCluster: null,
    mediaCache: new Map(),

    createMap: async function (id, lat, lng, zoom) {
        await window.ensureLeafletLoaded();

        if (this.map) {
            this.map.remove();
            this.map = null;
        }

        var container = document.getElementById(id);
        if (container && container._leaflet_id) {
            container._leaflet_id = null; 
        }

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

    addMarkers: function (markers, isSearch, labels) {
        if (!this.map || !Array.isArray(markers)) return;
        const mapLabels = getMapLabels(labels);

        if (this.markerCluster) {
            this.map.removeLayer(this.markerCluster);
        }

        this.markerCluster = L.markerClusterGroup({
            showCoverageOnHover: false,
            iconCreateFunction: (cluster) => {
                const markers = cluster.getAllChildMarkers();
                const totalCount = cluster.getChildCount();
                const offersCount = markers.filter(m => m.options.hasOffer).length;
                const hasOffer = offersCount > 0;

                const apartments = L.divIcon({
                    html: `<small style="font-size: 10px;">${mapLabels.apartmentsPrefix}: ${totalCount}</small>`,
                    className: !hasOffer ? 'custom-cluster' : 'custom-cluster with-offers',
                    iconSize: L.point(90, 21)
                });

                const offers = L.divIcon({
                    html: `<small style="font-size: 10px;">${offersCount ? `${mapLabels.offersPrefix}: ${offersCount}` : mapLabels.noOffers}</small>`,
                    className: !hasOffer ? 'custom-cluster' : 'custom-cluster with-offers',
                    iconSize: L.point(70, 21)
                });

                return isSearch ? offers : apartments;
            }
        });

        markers.forEach(m => {
            const lat = parseFloat(m.lat);
            const lng = parseFloat(m.lng);
            if (isNaN(lat) || isNaN(lng)) return;

            const defaultIcon = `<div class="marker"><span class="gicon map-marker-icon" aria-label="marker">location_on</span></div>`;
            const priceIcon = `<div class="marker-offer"><span class="marker-price">${Math.round(m.price)} ${mapLabels.currency}</span></div>`;
            const htmlContent = m.hasOffer ? priceIcon : defaultIcon;

            let iconSettings;

            if (m.hasOffer) {
                iconSettings = {
                    size: [70, 30],
                    anchor: [35, 38],
                    popupAnchor: [0, -38]
                };
            } else {
                iconSettings = {
                    size: [35, 35],
                    anchor: [17, 35],
                    popupAnchor: [0, -40]
                };
            }

            const customIcon = L.divIcon({
                className: 'custom-marker',
                html: htmlContent,
                iconSize: iconSettings.size,
                iconAnchor: iconSettings.anchor,
                popupAnchor: iconSettings.popupAnchor
            });

            const marker = L.marker([lat, lng], { icon: customIcon, hasOffer: m.hasOffer });

            if (m.popup) marker.bindPopup(m.popup);

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

            const imgContainer = popupEl.querySelector(".popup-img-target");

            if (imgContainer) {
                imgContainer.innerHTML = `<img src="${url}" style="height: 130px; width: 100%; object-fit: cover; border-radius: .5rem .5rem 0 0;" />`;
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

//copy
window.leafletPopupMap = {
    map: null,
    markerCluster: null,
    selectedLayer: null, 
    mediaCache: new Map(),

    createMap: async function (id, lat, lng, zoom) {
        await window.ensureLeafletLoaded();

        if (this.map) {
            this.map.remove();
            this.map = null;
            this.selectedLayer = null;
        }

        var container = document.getElementById(id);
        if (container && container._leaflet_id) {
            container._leaflet_id = null;
        }

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

    addMarkers: function (markers, isSearch, labels) {
        if (!this.map || !Array.isArray(markers)) return;
        const mapLabels = getMapLabels(labels);

        if (this.markerCluster) {
            this.map.removeLayer(this.markerCluster);
        }

        if (this.selectedLayer) {
            this.map.removeLayer(this.selectedLayer);
            this.selectedLayer = null;
        }

        this.markerCluster = L.markerClusterGroup({
            showCoverageOnHover: false,
            iconCreateFunction: (cluster) => {
                const markers = cluster.getAllChildMarkers();
                const totalCount = cluster.getChildCount();
                const offersCount = markers.filter(m => m.options.hasOffer).length;
                const hasOffer = offersCount > 0;

                const apartments = L.divIcon({
                    html: `<small style="font-size: 10px;">${mapLabels.apartmentsPrefix}: ${totalCount}</small>`,
                    className: !hasOffer ? 'custom-cluster' : 'custom-cluster with-offers',
                    iconSize: L.point(90, 21)
                });

                const offers = L.divIcon({
                    html: `<small style="font-size: 10px;">${offersCount ? `${mapLabels.offersPrefix}: ${offersCount}` : mapLabels.noOffers}</small>`,
                    className: !hasOffer ? 'custom-cluster' : 'custom-cluster with-offers',
                    iconSize: L.point(70, 21)
                });

                return isSearch ? offers : apartments;
            }
        });

        markers.forEach(m => {
            const lat = parseFloat(m.lat);
            const lng = parseFloat(m.lng);
            if (isNaN(lat) || isNaN(lng)) return;

            const extraClass = m.isSelected ? " selected-marker" : "";

            const zIndexVal = m.isSelected ? 10000 : 0;

            const defaultIcon = `<div class="marker${extraClass}"><span class="gicon map-marker-icon" aria-label="marker">location_on</span></div>`;

            const priceIcon = `<div class="marker-offer${extraClass}"><span class="marker-price">${Math.round(m.price)} ${mapLabels.currency}</span></div>`;

            const htmlContent = m.hasOffer ? priceIcon : defaultIcon;

            let iconSettings;
            if (m.hasOffer) {
                iconSettings = {
                    size: [70, 30],
                    anchor: [35, 38],
                    popupAnchor: [0, -38]
                };
            } else {
                iconSettings = {
                    size: [35, 35],
                    anchor: [17, 35],
                    popupAnchor: [0, -40]
                };
            }

            const customIcon = L.divIcon({
                className: 'custom-marker',
                html: htmlContent,
                iconSize: iconSettings.size,
                iconAnchor: iconSettings.anchor,
                popupAnchor: iconSettings.popupAnchor
            });

            const marker = L.marker([lat, lng], {
                icon: customIcon,
                hasOffer: m.hasOffer,
                zIndexOffset: zIndexVal
            });

            if (m.popup) marker.bindPopup(m.popup);

            marker.on('click', async function () {
                if (m.objRef && m.id) {
                    if (window.leafletPopupMap.mediaCache.has(m.id)) {
                        const cachedUrl = window.leafletPopupMap.mediaCache.get(m.id);
                        updatePopupImage(m.id, cachedUrl);
                        return;
                    }
                    try {
                        const imageUrl = await m.objRef.invokeMethodAsync('OnMarkerClicked', m.id);
                        if (imageUrl) {
                            window.leafletPopupMap.mediaCache.set(m.id, imageUrl);
                            updatePopupImage(m.id, imageUrl);
                        }
                    } catch (err) {
                        console.error('Error:', err);
                    }
                }
            });

            if (m.isSelected) {
                marker.addTo(this.map);
                this.selectedLayer = marker;
            } else {
                this.markerCluster.addLayer(marker);
            }
        });

        this.map.addLayer(this.markerCluster);

        function updatePopupImage(id, url) {
            const popupEl = document.getElementById(`popup-${id}`);
            if (!popupEl) return;
            const imgContainer = popupEl.querySelector(".popup-img-target");
            if (imgContainer) {
                imgContainer.innerHTML = `<img src="${url}" style="height: 130px; width: 100%; object-fit: cover; border-radius: .5rem .5rem 0 0;" />`;
            }
        }
    },

    clearMarkers: function () {
        if (this.map && this.markerCluster) {
            this.map.removeLayer(this.markerCluster);
            this.markerCluster = null;
        }
        if (this.selectedLayer) {
            this.map.removeLayer(this.selectedLayer);
            this.selectedLayer = null;
        }
    },

    refreshMap: function() {
        if (this.map) {
            this.map.invalidateSize();
        }
    },
};
