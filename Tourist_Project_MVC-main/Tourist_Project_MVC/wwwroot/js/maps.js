var EGYMaps = (function () {
  "use strict";

  var _maps = {};
  var _mapConfig = null;
  function _round6(n) {
    return Math.round(n * 1e6) / 1e6;
  }

  function _showNotice(el, msg) {
    if (!el) return;
    var notice = el.querySelector(".map-notice");
    if (!notice) {
      notice = document.createElement("div");
      notice.className = "map-notice";
      notice.style.cssText =
        "position:absolute;top:8px;left:50%;transform:translateX(-50%);z-index:999;background:rgba(255,255,255,.92);border:1px solid #C8832A;border-radius:8px;padding:6px 14px;font-size:.85rem;color:#6b4226;box-shadow:0 2px 8px rgba(0,0,0,.08);pointer-events:none;white-space:nowrap;";
      el.appendChild(notice);
    }
    notice.textContent = msg || "\u26A0 Couldn\u2019t load live layer";
  }

  function _firstKey(attrs, keys) {
    for (var i = 0; i < keys.length; i++) {
      if (attrs[keys[i]] !== undefined) return keys[i];
    }
    return keys[0];
  }

  function _ensureConfig() {
    if (_mapConfig) return Promise.resolve(_mapConfig);
    return fetch("/Map/GetMapConfig")
      .then(function (r) {
        return r.ok ? r.json() : Promise.reject(r.status);
      })
      .then(function (cfg) {
        _mapConfig = cfg;
        return cfg;
      })
      .catch(function (err) {
        console.warn("Failed to load ArcGIS map config", err);
        return {};
      });
  }

  async function _ensureApiKey(cfg) {
    if (!cfg || !cfg.apiKey) return;
    var esriConfig = await $arcgis.import("@arcgis/core/config.js");
    esriConfig.apiKey = cfg.apiKey;
  }

  function _waitForArcgisLoader() {
    if (window.$arcgis) return Promise.resolve(window.$arcgis);
    return new Promise(function (resolve, reject) {
      var started = Date.now();
      var interval = setInterval(function () {
        if (window.$arcgis) {
          clearInterval(interval);
          resolve(window.$arcgis);
        } else if (Date.now() - started > 15000) {
          clearInterval(interval);
          reject(new Error("ArcGIS loader timed out."));
        }
      }, 20);
    });
  }

  function initWfsMap(opts) {
    opts = opts || {};
    var mapEl = document.getElementById(opts.mapElId);
    if (!mapEl || _maps[opts.mapElId]) return null;

    var map, view, sourceLayer, overlayGraphicsLayer, graphicsByFeature;
    var _lastFitPromise = null;
    var EsriMap,
      MapView,
      FeatureLayer,
      GraphicsLayer,
      Graphic,
      Point,
      TextSymbol,
      Extent;

    var propMap = opts.propMap || {};
    var useLayerStyle = !!opts.useLayerStyle;

    var markerStyle = opts.markerStyle || {
      radius: 8,
      fillColor: "#C8832A",
      color: "#fff",
      weight: 2,
      opacity: 1,
      fillOpacity: 0.85,
    };
    var onLayerReady = opts.onLayerReady || null;
    var onFeatureClick = opts.onFeatureClick || null;

    var handle = {
      get map() {
        return map;
      },
      get view() {
        return view;
      },
      layer: function () {
        return sourceLayer;
      },
      overlayLayer: function () {
        return overlayGraphicsLayer;
      },
      filterMarkers: function (predicate) {
        if (useLayerStyle) {
          // When using the layer's own ArcGIS renderer, filter by definitionExpression.
          if (!sourceLayer) return;
          if (typeof predicate !== "function") {
            sourceLayer.definitionExpression = "1=1";
            return;
          }
          // Use the layer's OBJECTID field to build the expression.
          // visibleOids collects OBJECTID values (numeric) from sentinel attributes
          // for every feature that passes the predicate — NOT the map keys.
          var oidField = sourceLayer.objectIdField || "OBJECTID";
          var passingOids = [];
          graphicsByFeature.forEach(function (sentinel) {
            var attrs = sentinel.attributes || {};
            var passes = predicate({ attributes: attrs, properties: attrs }, sentinel);
            if (passes) {
              var oid = attrs[oidField];
              if (oid !== undefined && oid !== null) passingOids.push(oid);
            }
          });
          if (!passingOids.length) {
            sourceLayer.definitionExpression = "1=0";
          } else {
            // OBJECTID is numeric — no quoting needed.
            sourceLayer.definitionExpression = oidField + " IN (" + passingOids.join(",") + ")";
          }
          return;
        }
        // Default: toggle graphic visibility on overlay layer.
        if (!graphicsByFeature || !graphicsByFeature.size) return;
        graphicsByFeature.forEach(function (graphic) {
          var show = true;
          if (typeof predicate === "function") {
            var attrs = graphic.attributes || {};
            show = predicate({ attributes: attrs, properties: attrs }, graphic);
          }
          graphic.visible = show;
        });
      },
      addStopOverlay: function (lat, lng, label) {
        if (!overlayGraphicsLayer || !Point || !Graphic || !TextSymbol)
          return null;
        var point = new Point({
          longitude: lng,
          latitude: lat,
          spatialReference: { wkid: 4326 },
        });
        var circle = new Graphic({
          geometry: point,
          symbol: {
            type: "simple-marker",
            style: "circle",
            color: "#0d6efd",
            size: 24,
            outline: { color: "#fff", width: 3 },
          },
        });
        var text = new Graphic({
          geometry: point,
          symbol: new TextSymbol({
            text: String(label),
            color: "#fff",
            font: { size: 12, weight: "bold" },
            yoffset: 0,
            haloColor: "#000",
            haloSize: 1.5,
          }).toJSON(),
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
        var extent = latlngs.reduce(
          function (acc, ll) {
            acc[0] = Math.min(acc[0], ll[1]);
            acc[1] = Math.min(acc[1], ll[0]);
            acc[2] = Math.max(acc[2], ll[1]);
            acc[3] = Math.max(acc[3], ll[0]);
            return acc;
          },
          [Infinity, Infinity, -Infinity, -Infinity],
        );

        var EPS = 0.001;
        var xmin = extent[0],
          ymin = extent[1],
          xmax = extent[2],
          ymax = extent[3];

        // Degenerate (zero-size) extent: single point or coincident coords.
        // Zoom to a sane, consistent level instead of an extreme one.
        if (xmax - xmin < EPS && ymax - ymin < EPS) {
          var goToPoint = view.goTo(
            { center: [xmin, ymin], zoom: 14 },
            { duration: 1000 },
          );
          _lastFitPromise = goToPoint;
          goToPoint.catch(function () {
            if (goToPoint !== _lastFitPromise) return;
          });
          return;
        }

        var goToPromise = view.goTo(
          {
            target: new Extent({
              xmin: xmin,
              ymin: ymin,
              xmax: xmax,
              ymax: ymax,
              spatialReference: { wkid: 4326 },
            }),
          },
          {
            duration: 1000,
            padding: { top: 40, bottom: 40, left: 40, right: 40 },
          },
        );
        _lastFitPromise = goToPromise;
        // A superseded goTo rejects by design; ignore stale rejections so they
        // never break a subsequent fitBounds call.
        goToPromise.catch(function () {
          if (goToPromise !== _lastFitPromise) return;
        });
      },
      openPopupAt: function (lat, lng, title) {
        if (!view) return;
        var point = new Point({
          longitude: lng,
          latitude: lat,
          spatialReference: { wkid: 4326 },
        });

        // Find graphic at this location
        var targetGraphic = null;
        if (graphicsByFeature) {
          graphicsByFeature.forEach(function (g) {
            if (
              g.geometry &&
              Math.abs(g.geometry.latitude - lat) < 0.0001 &&
              Math.abs(g.geometry.longitude - lng) < 0.0001
            ) {
              targetGraphic = g;
            }
          });
        }

        if (targetGraphic && targetGraphic.popupTemplate) {
          view.popup.open({
            location: point,
            features: [targetGraphic],
          });
        } else {
          view.popup.open({
            location: point,
            title: title || "",
            content: "No additional details available.",
          });
        }
      },
    };

    _maps[opts.mapElId] = handle;

    function layerUrlFor(optsLayer) {
      if (!optsLayer) return null;
      if (typeof optsLayer === "string") {
        if (_mapConfig) {
          var lower = optsLayer.toLowerCase();
          if (lower === "destinations" && _mapConfig.destinationsLayerUrl)
            return _mapConfig.destinationsLayerUrl + "/0";
          if (lower === "branches" && _mapConfig.branchesLayerUrl)
            return _mapConfig.branchesLayerUrl + "/0";
        }
        if (
          optsLayer.indexOf("/FeatureServer") > -1 &&
          optsLayer.indexOf("/0") === -1
        ) {
          return optsLayer + "/0";
        }
        return optsLayer;
      }
      return null;
    }

    function portalItemIdFor(optsLayer) {
      if (!optsLayer || !_mapConfig) return null;
      if (typeof optsLayer === "string") {
        var lower = optsLayer.toLowerCase();
        if (lower === "destinations") return _mapConfig.destinationsItemId || _mapConfig.portalId || null;
        if (lower === "branches") return _mapConfig.branchesItemId || null;
      }
      return _mapConfig.portalId || null;
    }

    async function loadLayer() {
      var layerUrl = layerUrlFor(opts.layer || opts.proxyUrl);
      if (!layerUrl) {
        return;
      }

      var featureLayerOpts = {
        url: layerUrl,
        outFields: ["*"]
      };
      var itemId = portalItemIdFor(opts.layer || opts.proxyUrl);
      if (itemId) {
        featureLayerOpts.portalItem = { id: itemId };
      }
      sourceLayer = new FeatureLayer(featureLayerOpts);

      map.add(sourceLayer);
      // When useLayerStyle is true, keep the FeatureLayer visible so it renders
      // with its own ArcGIS Online / portal renderer instead of custom graphics.
      sourceLayer.visible = useLayerStyle;

      try {
        await sourceLayer.load();
        // whenLayerView returns a Promise resolving to the LayerView; no .ready chaining needed.
        await view.whenLayerView(sourceLayer);

        // If the layer didn't bring its own Arcade popup from ArcGIS Online, generate a default one
        if (!sourceLayer.popupTemplate) {
          sourceLayer.popupTemplate = sourceLayer.createPopupTemplate();
        }

        var query = sourceLayer.createQuery();
        query.where = "1=1";
        query.outFields = ["*"];
        query.returnGeometry = true;
        var result = await sourceLayer.queryFeatures(query);
        if (result && result.features) {
          graphicsByFeature.clear();
          result.features.forEach(function (f) {
            var idKey = _firstKey(f.attributes, propMap.id || ["id"]);
            if (useLayerStyle) {
              // Store a lightweight sentinel graphic (invisible) so filterMarkers,
              // onFeatureClick, fitBounds and openPopupAt still have feature data.
              var sentinel = new Graphic({
                geometry: f.geometry,
                symbol: {
                  type: "simple-marker",
                  style: "circle",
                  color: [0, 0, 0, 0],
                  size: (markerStyle.radius || 8) * 2,
                  outline: { color: [0, 0, 0, 0], width: 0 },
                },
                attributes: f.attributes,
                popupTemplate: sourceLayer.popupTemplate,
              });
              sentinel.visible = false;
              overlayGraphicsLayer.add(sentinel);
              graphicsByFeature.set(f.attributes[idKey], sentinel);
            } else {
              var graphic = new Graphic({
                geometry: f.geometry,
                symbol: {
                  type: "simple-marker",
                  style: "circle",
                  color: markerStyle.fillColor || "#C8832A",
                  size: (markerStyle.radius || 8) * 2,
                  outline: {
                    color: markerStyle.color || "#fff",
                    width: markerStyle.weight || 2,
                  },
                },
                attributes: f.attributes,
                popupTemplate: sourceLayer.popupTemplate,
              });
              overlayGraphicsLayer.add(graphic);
              graphicsByFeature.set(f.attributes[idKey], graphic);
            }
          });
        }

        if (typeof onLayerReady === "function") {
          onLayerReady({
            layer: function () {
              return overlayGraphicsLayer;
            },
            features: result
              ? result.features.map(function (f) {
                  return { attributes: f.attributes, geometry: f.geometry };
                })
              : [],
          });
        }
      } catch (e) {
        console.warn("ArcGIS layer query failed", e);
        _showNotice(mapEl);
        if (typeof onLayerReady === "function") {
          onLayerReady({
            layer: function () {
              return overlayGraphicsLayer;
            },
            features: [],
          });
        }
      }
    }

    (async function () {
      await _waitForArcgisLoader();

      var cfg = await _ensureConfig();
      await _ensureApiKey(cfg);

      EsriMap = await $arcgis.import("@arcgis/core/Map.js");
      MapView = await $arcgis.import("@arcgis/core/views/MapView.js");
      FeatureLayer = await $arcgis.import(
        "@arcgis/core/layers/FeatureLayer.js",
      );
      GraphicsLayer = await $arcgis.import(
        "@arcgis/core/layers/GraphicsLayer.js",
      );
      Graphic = await $arcgis.import("@arcgis/core/Graphic.js");
      Point = await $arcgis.import("@arcgis/core/geometry/Point.js");
      TextSymbol = await $arcgis.import("@arcgis/core/symbols/TextSymbol.js");
      Extent = await $arcgis.import("@arcgis/core/geometry/Extent.js");

      map = new EsriMap({
        basemap: "osm",
      });

      mapEl.innerHTML = "";

      view = new MapView({
        container: mapEl,
        map: map,
        center: [
          opts.center ? opts.center[1] : 30.8,
          opts.center ? opts.center[0] : 27.0,
        ],
        zoom: opts.zoom || 6,
      });

      var localLoader = document.createElement("div");
      localLoader.style.cssText =
        "position:absolute;top:0;left:0;width:100%;height:100%;background:rgba(26, 15, 0, 0.75);backdrop-filter:blur(8px);-webkit-backdrop-filter:blur(8px);z-index:1000;display:flex;align-items:center;justify-content:center;transition:opacity 0.4s;";
      localLoader.innerHTML =
        '<div class="loader-container"><img src="/assets/img/scarab.png" alt="Loading map..." class="loader-logo-fixed" /><div class="spinner-ring"></div></div>';
      mapEl.appendChild(localLoader);

      sourceLayer = null;
      overlayGraphicsLayer = new GraphicsLayer({ title: "overlays" });
      map.add(overlayGraphicsLayer);

      graphicsByFeature = new Map();

      loadLayer().finally(function () {
        localLoader.style.opacity = "0";
        setTimeout(function () {
          if (localLoader.parentNode)
            localLoader.parentNode.removeChild(localLoader);
        }, 400);
      });

      // Wire up feature-click → onFeatureClick callback for card highlighting.
      if (typeof onFeatureClick === "function") {
        view.on("click", function (event) {
          // When using the layer's own renderer, hit-test against the sourceLayer;
          // otherwise test against the overlay graphics layer.
          // Guard: sourceLayer may still be null if layer hasn't loaded yet.
          var includeTarget = (useLayerStyle && sourceLayer) ? sourceLayer : overlayGraphicsLayer;
          view.hitTest(event, { include: includeTarget }).then(function (response) {
            if (!response || !response.results || !response.results.length) return;
            var firstHit = response.results[0];
            var graphic = firstHit.graphic;
            if (graphic && graphic.attributes) {
              onFeatureClick(graphic.attributes);
            }
          });
        });
      }
    })();

    return handle;
  }

  function initLocationPicker(opts) {
    opts = opts || {};
    var mapEl = document.getElementById(opts.mapElId);
    if (!mapEl || _maps[opts.mapElId]) return;

    var latInput = document.getElementById(opts.latInputId);
    var lngInput = document.getElementById(opts.lngInputId);
    var onLocationSelected = typeof opts.onLocationSelected === "function" ? opts.onLocationSelected : null;

    var initialLat =
      opts.initialLat !== undefined && opts.initialLat !== null
        ? opts.initialLat
        : 30.0444;
    var initialLng =
      opts.initialLng !== undefined && opts.initialLng !== null
        ? opts.initialLng
        : 31.2357;

    (async function () {
      await _waitForArcgisLoader();

      var cfg = await _ensureConfig();
      await _ensureApiKey(cfg);

      var EsriMap = await $arcgis.import("@arcgis/core/Map.js");
      var MapView = await $arcgis.import("@arcgis/core/views/MapView.js");
      var FeatureLayer = await $arcgis.import(
        "@arcgis/core/layers/FeatureLayer.js",
      );
      var GraphicsLayer = await $arcgis.import(
        "@arcgis/core/layers/GraphicsLayer.js",
      );
      var Graphic = await $arcgis.import("@arcgis/core/Graphic.js");
      var Point = await $arcgis.import("@arcgis/core/geometry/Point.js");
      var SimpleMarkerSymbol = await $arcgis.import(
        "@arcgis/core/symbols/SimpleMarkerSymbol.js",
      );
      var TextSymbol = await $arcgis.import(
        "@arcgis/core/symbols/TextSymbol.js",
      );
      var webMercatorUtils = await $arcgis.import("@arcgis/core/geometry/support/webMercatorUtils.js");
      var Search = await $arcgis.import("@arcgis/core/widgets/Search.js");

      var map = new EsriMap({
        basemap: "osm",
      });

      mapEl.innerHTML = "";

      var view = new MapView({
        container: mapEl,
        map: map,
        center: [initialLng, initialLat],
        zoom: 13,
      });

      var localLoader = document.createElement("div");
      localLoader.style.cssText =
        "position:absolute;top:0;left:0;width:100%;height:100%;background:rgba(26, 15, 0, 0.75);backdrop-filter:blur(8px);-webkit-backdrop-filter:blur(8px);z-index:1000;display:flex;align-items:center;justify-content:center;transition:opacity 0.4s;";
      localLoader.innerHTML =
        '<div class="loader-container"><img src="/assets/img/scarab.png" alt="Loading map..." class="loader-logo-fixed" /><div class="spinner-ring"></div></div>';
      mapEl.appendChild(localLoader);

      _maps[opts.mapElId] = view;

      var overlayLayer = new GraphicsLayer();
      map.add(overlayLayer);

      var pickerPoint = new Point({
        longitude: initialLng,
        latitude: initialLat,
        spatialReference: { wkid: 4326 },
      });
      var pickerGraphic = new Graphic({
        geometry: pickerPoint,
        symbol: new SimpleMarkerSymbol({
          style: "circle",
          color: "#C8832A",
          size: 16,
          outline: { color: "#fff", width: 3 },
        }).toJSON(),
      });
      overlayLayer.add(pickerGraphic);

      function syncFromLatLng(lng, lat) {
        if (latInput) latInput.value = _round6(lat);
        if (lngInput) lngInput.value = _round6(lng);
        if (onLocationSelected) onLocationSelected({ latitude: _round6(lat), longitude: _round6(lng) });
      }

      view.on("pointer-down", function (event) {
        view.hitTest(event).then(function (response) {
          if (response.results.length && response.results[0].graphic === pickerGraphic) {
            view._dragging = true;
            event.stopPropagation();
          } else {
            view._dragging = false;
          }
        });
      });

      view.on("pointer-move", function (event) {
        if (!view._dragging) return;
        view.hitTest(event).then(function (response) {
          if (response.results.length) {
            var pt = response.results[0].graphic.geometry;
            if (pt && pt.longitude !== undefined && pt.latitude !== undefined) {
              pickerGraphic.geometry = new Point({
                longitude: pt.longitude,
                latitude: pt.latitude,
                spatialReference: { wkid: 4326 },
              });
              syncFromLatLng(pt.longitude, pt.latitude);
            }
          }
        });
      });

      view.on("pointer-up", function () {
        view._dragging = false;
        var geo = pickerGraphic.geometry;
        if (geo) syncFromLatLng(geo.longitude, geo.latitude);
      });

      view.on("click", function (event) {
        if (view._dragging) return;
        pickerGraphic.geometry = new Point({
          longitude: event.mapPoint.longitude,
          latitude: event.mapPoint.latitude,
          spatialReference: { wkid: 4326 },
        });
        syncFromLatLng(event.mapPoint.longitude, event.mapPoint.latitude);
      });

      var proxyUrl = opts.contextLayer || "";
      if (proxyUrl) {
        var ctxLayerUrl = proxyUrl;
        if (_mapConfig) {
          var ctxKey = proxyUrl.toLowerCase();
          if (ctxKey === "destinations" && _mapConfig.destinationsLayerUrl)
            ctxLayerUrl = _mapConfig.destinationsLayerUrl + "/0";
          else if (ctxKey === "branches" && _mapConfig.branchesLayerUrl)
            ctxLayerUrl = _mapConfig.branchesLayerUrl + "/0";
        }
        var ctxLayer = new FeatureLayer({ url: ctxLayerUrl, outFields: ["*"] });
        ctxLayer
          .queryFeatures({
            where: "1=1",
            outFields: ["*"],
            returnGeometry: true,
          })
          .then(function (result) {
            if (!result || !result.features) return;
            var ctxStyle = opts.contextStyle || {
              radius: 6,
              fillColor: "#888",
              color: "#555",
              weight: 1,
              opacity: 0.6,
              fillOpacity: 0.35,
            };
            result.features.forEach(function (f) {
              if (!f.geometry) return;
              var g = new Graphic({
                geometry: f.geometry,
                symbol: new SimpleMarkerSymbol({
                  style: "circle",
                  color: ctxStyle.fillColor || "#888",
                  size: (ctxStyle.radius || 6) * 2,
                  outline: {
                    color: ctxStyle.color || "#555",
                    width: ctxStyle.weight || 1,
                  },
                }).toJSON(),
              });
              overlayLayer.add(g);
            });
          })
          .catch(function () {
            _showNotice(mapEl);
          });
      }

      var search = new Search({ view: view, includeDefaultSources: true });
      view.ui.add(search, "top-right");
      search.on("select-result", function (event) {
        var point = event.result && event.result.feature && event.result.feature.geometry;
        if (!point) return;
        var geo = point.spatialReference && point.spatialReference.wkid === 4326
          ? point
          : point.longitude !== undefined ? point : webMercatorUtils.webMercatorToGeographic(point);
        if (geo && geo.longitude !== undefined && geo.latitude !== undefined) {
          pickerGraphic.geometry = new Point({ longitude: geo.longitude, latitude: geo.latitude, spatialReference: { wkid: 4326 } });
          view.goTo({ center: [geo.longitude, geo.latitude], zoom: 16 }, { duration: 600 });
          syncFromLatLng(geo.longitude, geo.latitude);
        }
      });

      view.when(function () {
        localLoader.style.opacity = "0";
        setTimeout(function () {
          if (localLoader.parentNode)
            localLoader.parentNode.removeChild(localLoader);
        }, 400);
      }).catch(function (error) {
        if (localLoader) localLoader.innerHTML = '<div class="p-3 text-center text-white">The ArcGIS map could not be initialized. Refresh and try again.</div>';
        console.warn("ArcGIS location picker failed", error);
      });

      if (latInput) {
        latInput.addEventListener("input", function () {
          var lat = parseFloat(latInput.value);
          var lng = parseFloat(
            lngInput ? lngInput.value : pickerGraphic.geometry.longitude,
          );
          if (!isNaN(lat) && !isNaN(lng)) {
            pickerGraphic.geometry = new Point({
              longitude: lng,
              latitude: lat,
              spatialReference: { wkid: 4326 },
            });
            view.goTo({ center: [lng, lat], zoom: 13 }, { duration: 500 });
          }
        });
      }
      if (lngInput) {
        lngInput.addEventListener("input", function () {
          var lng = parseFloat(lngInput.value);
          var lat = parseFloat(
            latInput ? latInput.value : pickerGraphic.geometry.latitude,
          );
          if (!isNaN(lat) && !isNaN(lng)) {
            pickerGraphic.geometry = new Point({
              longitude: lng,
              latitude: lat,
              spatialReference: { wkid: 4326 },
            });
            view.goTo({ center: [lng, lat], zoom: 13 }, { duration: 500 });
          }
        });
      }

      return { map: map, view: view, marker: pickerGraphic };
    })().catch(function (error) {
      console.warn("ArcGIS location picker initialization failed", error);
      mapEl.innerHTML = '<div class="p-4 text-center text-muted">ArcGIS map unavailable. You can still enter latitude and longitude manually.</div>';
    });
  }

  function resize(mapElId) {
    var view = _maps[mapElId];
    if (view && view.resize) view.resize();
  }

  /**
   * Renders a single-location map (OSM basemap) centered on a point with a
   * pin marker — used on detail pages such as the sponsor/branch details so
   * the tourist sees exactly where the branch is. No-op when the element is
   * missing, already initialized, or the coordinates are invalid/zero.
   */
  function initPointMap(opts) {
    opts = opts || {};
    var mapEl = document.getElementById(opts.mapElId);
    if (!mapEl || _maps[opts.mapElId]) return null;
    var lat = parseFloat(opts.lat);
    var lng = parseFloat(opts.lng);
    if (isNaN(lat) || isNaN(lng) || (lat === 0 && lng === 0)) return null;

    (async function () {
      await _waitForArcgisLoader();

      var cfg = await _ensureConfig();
      await _ensureApiKey(cfg);

      var EsriMap = await $arcgis.import("@arcgis/core/Map.js");
      var MapView = await $arcgis.import("@arcgis/core/views/MapView.js");
      var Graphic = await $arcgis.import("@arcgis/core/Graphic.js");
      var GraphicsLayer = await $arcgis.import(
        "@arcgis/core/layers/GraphicsLayer.js",
      );

      var map = new EsriMap({ basemap: "osm" });
      mapEl.innerHTML = "";

      var view = new MapView({
        container: mapEl,
        map: map,
        center: [lng, lat],
        zoom: opts.zoom || 14,
        constraints: { minZoom: 6 },
      });
      _maps[opts.mapElId] = view;

      var markerLayer = new GraphicsLayer();
      map.add(markerLayer);
      markerLayer.add(
        new Graphic({
          geometry: {
            type: "point",
            longitude: lng,
            latitude: lat,
            spatialReference: { wkid: 4326 },
          },
          attributes: { title: opts.title || "" },
          symbol: {
            type: "simple-marker",
            style: "pin",
            color: [200, 131, 42, 1],
            size: 26,
            outline: { color: [255, 255, 255, 0.95], width: 2 },
          },
          popupTemplate: opts.title
            ? { title: opts.title, content: opts.title }
            : null,
        }),
      );

      return { map: map, view: view };
    })().catch(function (error) {
      console.warn("ArcGIS point map initialization failed", error);
      mapEl.innerHTML =
        '<div class="p-4 text-center text-muted">Map unavailable.</div>';
    });
  }

  return {
    initWfsMap: initWfsMap,
    initLocationPicker: initLocationPicker,
    initPointMap: initPointMap,
    resize: resize,
  };
})();
