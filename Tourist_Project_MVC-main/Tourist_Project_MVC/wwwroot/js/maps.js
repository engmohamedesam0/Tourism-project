var EGYMaps = (function () {
    'use strict';

    var _maps = {};
    var _mapConfig = null;

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
        var p = feature.attributes || {};
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

    function _ensureConfig() {
        if (_mapConfig) return Promise.resolve(_mapConfig);
        return fetch('/Map/GetMapConfig')
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (cfg) {
                _mapConfig = cfg;
                return cfg;
            })
            .catch(function (err) {
                console.warn('Failed to load ArcGIS map config', err);
                return {};
            });
    }

    function _ensureApiKey(cfg) {
    }

    function _waitForArcgisLoader() {
        if (window.$arcgis) return Promise.resolve(window.$arcgis);
        return new Promise(function (resolve) {
            var interval = setInterval(function () {
                if (window.$arcgis) {
                    clearInterval(interval);
                    resolve(window.$arcgis);
                }
            }, 20);
        });
    }

    function initWfsMap(opts) {
        opts = opts || {};
        var mapEl = document.getElementById(opts.mapElId);
        if (!mapEl || _maps[opts.mapElId]) return null;

        var map, view, sourceLayer, overlayGraphicsLayer, graphicsByFeature;
        var EsriMap, MapView, FeatureLayer, GraphicsLayer, Graphic, Point, PopupTemplate, TextSymbol, Extent;

        var propMap = opts.propMap || {};
        var popupHtml = opts.popupHtml || _buildPopupHtml;
        var markerStyle = opts.markerStyle || { radius: 8, fillColor: '#C8832A', color: '#fff', weight: 2, opacity: 1, fillOpacity: 0.85 };
        var onLayerReady = opts.onLayerReady || null;

        var handle = {
            get map() { return map; },
            get view() { return view; },
            layer: function () { return sourceLayer; },
            overlayLayer: function () { return overlayGraphicsLayer; },
            filterMarkers: function (predicate) {
                if (!graphicsByFeature || !graphicsByFeature.size) return;
                graphicsByFeature.forEach(function (graphic) {
                    var show = true;
                    if (typeof predicate === 'function') {
                        var attrs = graphic.attributes || {};
                        show = predicate({ attributes: attrs, properties: attrs }, graphic);
                    }
                    graphic.visible = show;
                });
            },
            addStopOverlay: function (lat, lng, label) {
                if (!overlayGraphicsLayer || !Point || !Graphic || !TextSymbol) return null;
                var point = new Point({ longitude: lng, latitude: lat, spatialReference: { wkid: 4326 } });
                var circle = new Graphic({
                    geometry: point,
                    symbol: {
                        type: 'simple-marker',
                        style: 'circle',
                        color: '#0d6efd',
                        size: 24,
                        outline: { color: '#fff', width: 3 }
                    }
                });
                var text = new Graphic({
                    geometry: point,
                    symbol: new TextSymbol({
                        text: String(label),
                        color: '#fff',
                        font: { size: 12, weight: 'bold' },
                        yoffset: 0,
                        haloColor: '#000',
                        haloSize: 1.5
                    }).toJSON()
                });
                overlayGraphicsLayer.addMany([circle, text]);
                return { marker: circle, label: text };
            },
            clearOverlays: function () {
                if (overlayGraphicsLayer) overlayGraphicsLayer.removeAll();
                if (graphicsByFeature) graphicsByFeature.clear();
            },
            fitBounds: function (latlngs) {
                if (!view) return;
                if (!latlngs || !latlngs.length) return;
                var extent = latlngs.reduce(function (acc, ll) {
                    acc[0] = Math.min(acc[0], ll[1]);
                    acc[1] = Math.min(acc[1], ll[0]);
                    acc[2] = Math.max(acc[2], ll[1]);
                    acc[3] = Math.max(acc[3], ll[0]);
                    return acc;
                }, [Infinity, Infinity, -Infinity, -Infinity]);
                view.goTo({
                    target: new Extent({
                        xmin: extent[0], ymin: extent[1], xmax: extent[2], ymax: extent[3],
                        spatialReference: { wkid: 4326 }
                    })
                }, { duration: 1000, padding: { top: 40, bottom: 40, left: 40, right: 40 } });
            },
            openPopupAt: function (lat, lng, title) {
                if (!view) return;
                var point = new Point({ longitude: lng, latitude: lat, spatialReference: { wkid: 4326 } });
                view.popup = {
                    location: point,
                    title: title || '',
                    content: ''
                };
                view.openPopup();
            }
        };

        _maps[opts.mapElId] = handle;

        function layerUrlFor(optsLayer) {
            if (!optsLayer) return null;
            if (typeof optsLayer === 'string') {
                if (_mapConfig) {
                    var lower = optsLayer.toLowerCase();
                    if (lower === 'destinations' && _mapConfig.destinationsLayerUrl) return _mapConfig.destinationsLayerUrl + '/0';
                    if (lower === 'branches' && _mapConfig.branchesLayerUrl) return _mapConfig.branchesLayerUrl + '/0';
                }
                if (optsLayer.indexOf('/FeatureServer') > -1 && optsLayer.indexOf('/0') === -1) {
                    return optsLayer + '/0';
                }
                return optsLayer;
            }
            return null;
        }

        async function loadLayer() {
            var layerUrl = layerUrlFor(opts.layer || opts.proxyUrl);
            if (!layerUrl) {
                return;
            }

            sourceLayer = new FeatureLayer({
                url: layerUrl,
                outFields: ['*'],
                popupTemplate: new PopupTemplate({
                    title: '{name}',
                    content: function (event) {
                        var feature = event.graphic;
                        var html = popupHtml({ attributes: feature.attributes, properties: feature.attributes }, propMap);
                        return html || '';
                    }
                })
            });

            map.add(sourceLayer);
            sourceLayer.visible = false;

            try {
                await view.whenLayerView(sourceLayer).ready;
                var query = sourceLayer.createQuery();
                query.where = '1=1';
                query.outFields = ['*'];
                query.returnGeometry = true;
                var result = await sourceLayer.queryFeatures(query);
                if (result && result.features) {
                    graphicsByFeature.clear();
                    result.features.forEach(function (f) {
                        var graphic = new Graphic({
                            geometry: f.geometry,
                            symbol: {
                                type: 'simple-marker',
                                style: 'circle',
                                color: markerStyle.fillColor || '#C8832A',
                                size: (markerStyle.radius || 8) * 2,
                                outline: {
                                    color: markerStyle.color || '#fff',
                                    width: markerStyle.weight || 2
                                }
                            },
                            attributes: f.attributes,
                            popupTemplate: sourceLayer.popupTemplate
                        });
                        overlayGraphicsLayer.add(graphic);
                        graphicsByFeature.set(f.attributes[Object.keys(propMap.id || ['id'])[0] || 'id'], graphic);
                    });
                }

                if (typeof onLayerReady === 'function') {
                    onLayerReady({
                        layer: function () { return overlayGraphicsLayer; },
                        features: result ? result.features.map(function (f) { return { attributes: f.attributes, geometry: f.geometry }; }) : []
                    });
                }
            } catch (e) {
                console.warn('ArcGIS layer query failed', e);
                _showNotice(mapEl);
                if (typeof onLayerReady === 'function') {
                    onLayerReady({ layer: function () { return overlayGraphicsLayer; }, features: [] });
                }
            }
        }

        (async function () {
            await _waitForArcgisLoader();

            var cfg = await _ensureConfig();
            _ensureApiKey(cfg);

            EsriMap = await $arcgis.import('@arcgis/core/Map.js');
            MapView = await $arcgis.import('@arcgis/core/views/MapView.js');
            FeatureLayer = await $arcgis.import('@arcgis/core/layers/FeatureLayer.js');
            GraphicsLayer = await $arcgis.import('@arcgis/core/layers/GraphicsLayer.js');
            Graphic = await $arcgis.import('@arcgis/core/Graphic.js');
            Point = await $arcgis.import('@arcgis/core/geometry/Point.js');
            PopupTemplate = await $arcgis.import('@arcgis/core/PopupTemplate.js');
            TextSymbol = await $arcgis.import('@arcgis/core/symbols/TextSymbol.js');
            Extent = await $arcgis.import('@arcgis/core/geometry/Extent.js');

            map = new EsriMap({
                basemap: 'osm'
            });

            mapEl.innerHTML = '';

            view = new MapView({
                container: mapEl,
                map: map,
                center: [opts.center ? opts.center[1] : 31.2357, opts.center ? opts.center[0] : 30.0444],
                zoom: opts.zoom || 7
            });

            sourceLayer = null;
            overlayGraphicsLayer = new GraphicsLayer({ title: 'overlays' });
            map.add(overlayGraphicsLayer);

            graphicsByFeature = new Map();

            loadLayer();
        })();

        return handle;
    }

    function initLocationPicker(opts) {
        opts = opts || {};
        var mapEl = document.getElementById(opts.mapElId);
        if (!mapEl || _maps[opts.mapElId]) return;

        var latInput = document.getElementById(opts.latInputId);
        var lngInput = document.getElementById(opts.lngInputId);

        var initialLat = (opts.initialLat !== undefined && opts.initialLat !== null) ? opts.initialLat : 30.0444;
        var initialLng = (opts.initialLng !== undefined && opts.initialLng !== null) ? opts.initialLng : 31.2357;

        (async function () {
            await _waitForArcgisLoader();

            var cfg = await _ensureConfig();
            _ensureApiKey(cfg);

            var EsriMap = await $arcgis.import('@arcgis/core/Map.js');
            var MapView = await $arcgis.import('@arcgis/core/views/MapView.js');
            var FeatureLayer = await $arcgis.import('@arcgis/core/layers/FeatureLayer.js');
            var GraphicsLayer = await $arcgis.import('@arcgis/core/layers/GraphicsLayer.js');
            var Graphic = await $arcgis.import('@arcgis/core/Graphic.js');
            var Point = await $arcgis.import('@arcgis/core/geometry/Point.js');
            var SimpleMarkerSymbol = await $arcgis.import('@arcgis/core/symbols/SimpleMarkerSymbol.js');
            var TextSymbol = await $arcgis.import('@arcgis/core/symbols/TextSymbol.js');

        var map = new EsriMap({
                basemap: 'osm'
            });

            mapEl.innerHTML = '';

            var view = new MapView({
                container: mapEl,
                map: map,
                center: [initialLng, initialLat],
                zoom: 13
            });

            _maps[opts.mapElId] = view;

            var overlayLayer = new GraphicsLayer();
            map.add(overlayLayer);

            var pickerPoint = new Point({ longitude: initialLng, latitude: initialLat, spatialReference: { wkid: 4326 } });
            var pickerGraphic = new Graphic({
                geometry: pickerPoint,
                symbol: new SimpleMarkerSymbol({
                    style: 'circle',
                    color: '#C8832A',
                    size: 16,
                    outline: { color: '#fff', width: 3 }
                }).toJSON()
            });
            overlayLayer.add(pickerGraphic);

            function syncFromLatLng(lng, lat) {
                if (latInput) latInput.value = _round6(lat);
                if (lngInput) lngInput.value = _round6(lng);
            }

            view.on('pointer-down', function (event) {
                view.hitTest(event).then(function (response) {
                    if (response.results.length) {
                        view._dragging = true;
                        event.stopPropagation();
                    }
                });
            });

            view.on('pointer-move', function (event) {
                if (!view._dragging) return;
                view.hitTest(event).then(function (response) {
                    if (response.results.length) {
                        var pt = response.results[0].graphic.geometry;
                        if (pt && pt.longitude !== undefined && pt.latitude !== undefined) {
                            pickerGraphic.geometry = new Point({ longitude: pt.longitude, latitude: pt.latitude, spatialReference: { wkid: 4326 } });
                            syncFromLatLng(pt.longitude, pt.latitude);
                        }
                    }
                });
            });

            view.on('pointer-up', function () {
                view._dragging = false;
                var geo = pickerGraphic.geometry;
                if (geo) syncFromLatLng(geo.longitude, geo.latitude);
            });

            view.on('click', function (event) {
                if (view._dragging) return;
                pickerGraphic.geometry = new Point({ longitude: event.mapPoint.longitude, latitude: event.mapPoint.latitude, spatialReference: { wkid: 4326 } });
                syncFromLatLng(event.mapPoint.longitude, event.mapPoint.latitude);
            });

            var proxyUrl = opts.contextLayer || '';
            if (proxyUrl) {
                var ctxLayerUrl = proxyUrl;
                if (_mapConfig) {
                    var ctxKey = proxyUrl.toLowerCase();
                    if (ctxKey === 'destinations' && _mapConfig.destinationsLayerUrl) ctxLayerUrl = _mapConfig.destinationsLayerUrl + '/0';
                    else if (ctxKey === 'branches' && _mapConfig.branchesLayerUrl) ctxLayerUrl = _mapConfig.branchesLayerUrl + '/0';
                }
                var ctxLayer = new FeatureLayer({ url: ctxLayerUrl, outFields: ['*'] });
                ctxLayer.queryFeatures({ where: '1=1', outFields: ['*'], returnGeometry: true })
                    .then(function (result) {
                        if (!result || !result.features) return;
                        var ctxStyle = opts.contextStyle || { radius: 6, fillColor: '#888', color: '#555', weight: 1, opacity: 0.6, fillOpacity: 0.35 };
                        result.features.forEach(function (f) {
                            if (!f.geometry) return;
                            var g = new Graphic({
                                geometry: f.geometry,
                                symbol: new SimpleMarkerSymbol({
                                    style: 'circle',
                                    color: ctxStyle.fillColor || '#888',
                                    size: (ctxStyle.radius || 6) * 2,
                                    outline: { color: ctxStyle.color || '#555', width: ctxStyle.weight || 1 }
                                }).toJSON()
                            });
                            overlayLayer.add(g);
                        });
                    })
                    .catch(function () { _showNotice(mapEl); });
            }

            if (latInput) {
                latInput.addEventListener('input', function () {
                    var lat = parseFloat(latInput.value);
                    var lng = parseFloat(lngInput ? lngInput.value : pickerGraphic.geometry.longitude);
                    if (!isNaN(lat) && !isNaN(lng)) {
                        pickerGraphic.geometry = new Point({ longitude: lng, latitude: lat, spatialReference: { wkid: 4326 } });
                        view.goTo({ center: [lng, lat], zoom: 13 }, { duration: 500 });
                    }
                });
            }
            if (lngInput) {
                lngInput.addEventListener('input', function () {
                    var lng = parseFloat(lngInput.value);
                    var lat = parseFloat(latInput ? latInput.value : pickerGraphic.geometry.latitude);
                    if (!isNaN(lat) && !isNaN(lng)) {
                        pickerGraphic.geometry = new Point({ longitude: lng, latitude: lat, spatialReference: { wkid: 4326 } });
                        view.goTo({ center: [lng, lat], zoom: 13 }, { duration: 500 });
                    }
                });
            }

            return { map: map, view: view, marker: pickerGraphic };
        })();
    }

    return { initWfsMap: initWfsMap, initLocationPicker: initLocationPicker };
})();
