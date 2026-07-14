var EGYMaps = (function () {
    'use strict';

    var _maps = {};

    function _debounce(fn, delay) {
        var timer;
        return function () {
            var args = arguments;
            var ctx = this;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(ctx, args); }, delay);
        };
    }

    function _round6(n) {
        return Math.round(n * 1e6) / 1e6;
    }

    function _showNotice(el, msg) {
        if (!el) return;
        var notice = el.querySelector('.map-notice');
        if (!notice) {
            notice = document.createElement('div');
            notice.className = 'map-notice';
            notice.style.cssText = 'position:absolute;top:8px;left:50%;transform:translateX(-50%);z-index:999;background:rgba(255,255,255,.92);border:1px solid #C8832A;border-radius:8px;padding:6px 14px;font-size:.85rem;color:#6b4226;box-shadow:0 2px 8px rgba(0,0,0,.08);pointer-events:none;white-space:nowrap;';
            el.appendChild(notice);
        }
        notice.textContent = msg || '\u26A0 Couldn\u2019t load live layer';
    }

    function _buildPopupHtml(feature, propMap) {
        var p = feature.properties || {};
        var id = _firstDefined(p, propMap.id, []);
        var name = _firstDefined(p, propMap.name, ['Unnamed']);
        var city = _firstDefined(p, propMap.city, ['']);
        var cat = _firstDefined(p, propMap.category, ['']);
        var lines = [];
        if (name) lines.push('<strong>' + _esc(name) + '</strong>');
        if (city) lines.push('<span style="color:#666">' + _esc(city) + '</span>');
        if (cat) lines.push('<br><span class="badge bg-secondary">' + _esc(cat) + '</span>');
        if (id && propMap.detailsUrl) {
            lines.push('<br><a href="' + propMap.detailsUrl.replace('{id}', encodeURIComponent(id)) + '" class="btn btn-sm btn-outline-primary mt-1">Details</a>');
        }
        return lines.join('<br>');
    }

    function _firstDefined(p, keys, fallback) {
        for (var i = 0; i < keys.length; i++) {
            var val = p[keys[i]];
            if (val !== undefined && val !== null && String(val) !== '') return String(val);
        }
        return fallback[0] || '';
    }

    function _esc(s) {
        return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function initWfsMap(opts) {
        opts = opts || {};
        var mapEl = document.getElementById(opts.mapElId);
        if (!mapEl || _maps[opts.mapElId]) return;

        var map = L.map(mapEl, { zoomControl: true, scrollWheelZoom: true }).setView(opts.center || [30.0444, 31.2357], 7);
        _maps[opts.mapElId] = map;

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 19
        }).addTo(map);

        var proxyUrl = opts.proxyUrl || '';
        var propMap = opts.propMap || {};
        var popupHtml = opts.popupHtml || _buildPopupHtml;
        var markerStyle = opts.markerStyle || { radius: 8, fillColor: '#C8832A', color: '#fff', weight: 2, opacity: 1, fillOpacity: 0.85 };
        var onLayerReady = opts.onLayerReady || null;

        var geojsonLayer = null;
        var markerEntries = [];

        function loadWfs() {
            if (!proxyUrl) return;
            fetch(proxyUrl)
                .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
                .then(function (data) {
                    if (!data || data.type !== 'FeatureCollection' || !Array.isArray(data.features)) {
                        _showNotice(mapEl);
                        return;
                    }
                    if (geojsonLayer) map.removeLayer(geojsonLayer);
                    markerEntries = [];
                    geojsonLayer = L.geoJSON(data, {
                        pointToLayer: function (feature, latlng) {
                            return L.circleMarker(latlng, markerStyle);
                        },
                        onEachFeature: function (feature, layer) {
                            markerEntries.push({ layer: layer, feature: feature });
                            var html = popupHtml(feature, propMap);
                            if (html) layer.bindPopup(html);
                        }
                    }).addTo(map);

                    if (data.features.length > 0) {
                        var group = L.featureGroup([geojsonLayer]);
                        map.fitBounds(group.getBounds().pad(0.15));
                    }

                    if (typeof onLayerReady === 'function') onLayerReady(geojsonLayer, data.features);
                })
                .catch(function () {
                    _showNotice(mapEl);
                });
        }

        loadWfs();

        map.invalidateSize();
        window.addEventListener('resize', _debounce(function () { map.invalidateSize(); }, 150));

        return {
            map: map,
            layer: function () { return geojsonLayer; },
            filterMarkers: function (predicate) {
                if (!markerEntries.length) return;
                markerEntries.forEach(function (entry) {
                    var show = (typeof predicate !== 'function') ? true : predicate(entry.feature, entry.layer);
                    if (show) { if (!map.hasLayer(entry.layer)) entry.layer.addTo(map); }
                    else { if (map.hasLayer(entry.layer)) map.removeLayer(entry.layer); }
                });
            }
        };
    }

    function initLocationPicker(opts) {
        opts = opts || {};
        var mapEl = document.getElementById(opts.mapElId);
        if (!mapEl || _maps[opts.mapElId]) return;

        var latInput = document.getElementById(opts.latInputId);
        var lngInput = document.getElementById(opts.lngInputId);

        var initialLat = (opts.initialLat !== undefined && opts.initialLat !== null) ? opts.initialLat : 30.0444;
        var initialLng = (opts.initialLng !== undefined && opts.initialLng !== null) ? opts.initialLng : 31.2357;

        var map = L.map(mapEl).setView([initialLat, initialLng], 13);
        _maps[opts.mapElId] = map;

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 19
        }).addTo(map);

        var marker = L.marker([initialLat, initialLng], { draggable: true }).addTo(map);

        function syncFromLatLng(latlng) {
            var lat = _round6(latlng.lat);
            var lng = _round6(latlng.lng);
            if (latInput) latInput.value = lat;
            if (lngInput) lngInput.value = lng;
        }

        marker.on('dragend', function () { syncFromLatLng(marker.getLatLng()); });

        map.on('click', function (e) {
            marker.setLatLng(e.latlng);
            syncFromLatLng(e.latlng);
        });

        if (latInput) {
            latInput.addEventListener('input', function () {
                var lat = parseFloat(latInput.value);
                if (!isNaN(lat)) { marker.setLatLng([lat, marker.getLatLng().lng]); map.panTo([lat, marker.getLatLng().lng]); }
            });
        }
        if (lngInput) {
            lngInput.addEventListener('input', function () {
                var lng = parseFloat(lngInput.value);
                if (!isNaN(lng)) { marker.setLatLng([marker.getLatLng().lat, lng]); map.panTo([marker.getLatLng().lat, lng]); }
            });
        }

        var proxyUrl = opts.contextProxyUrl || '';
        if (proxyUrl) {
            fetch(proxyUrl)
                .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
                .then(function (data) {
                    if (!data || data.type !== 'FeatureCollection') return;
                    var ctxStyle = opts.contextStyle || { radius: 6, fillColor: '#888', color: '#555', weight: 1, opacity: 0.6, fillOpacity: 0.35 };
                    L.geoJSON(data, {
                        pointToLayer: function (f, ll) { return L.circleMarker(ll, ctxStyle); }
                    }).addTo(map);
                })
                .catch(function () { _showNotice(mapEl); });
        }

        map.invalidateSize();
        window.addEventListener('resize', _debounce(function () { map.invalidateSize(); }, 150));

        return { map: map, marker: marker };
    }

    return { initWfsMap: initWfsMap, initLocationPicker: initLocationPicker };
})();
