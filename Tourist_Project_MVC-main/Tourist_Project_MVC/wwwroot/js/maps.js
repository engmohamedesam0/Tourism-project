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
      zoomTo: function (lat, lng) {
        if (view && !isNaN(lat) && !isNaN(lng)) {
          view.goTo({ center: [lng, lat], zoom: 15 }, { duration: 800 });
        }
      },
      closePopup: function () {
        if (view && view.popup) view.popup.close();
      },
      openPopupAt: function (lat, lng, title, cardData) {
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
              Math.abs(g.geometry.latitude - lat) < 0.001 &&
              Math.abs(g.geometry.longitude - lng) < 0.001
            ) {
              targetGraphic = g;
            }
          });
        }

        if (targetGraphic && targetGraphic.popupTemplate) {
          if (cardData) {
            targetGraphic.attributes = Object.assign({}, targetGraphic.attributes || {}, {
              Id: cardData.id || targetGraphic.attributes.Id || targetGraphic.attributes.id,
              Name: cardData.name || targetGraphic.attributes.Name || targetGraphic.attributes.name,
              PhotoUrls: cardData.photos || targetGraphic.attributes.PhotoUrls || targetGraphic.attributes.photoUrls,
              Description: cardData.description || targetGraphic.attributes.Description,
              Category: cardData.category || targetGraphic.attributes.Category,
              Rating: cardData.rating || targetGraphic.attributes.Rating,
              City: cardData.city || targetGraphic.attributes.City
            });
          }
          view.popup.open({
            location: point,
            features: [targetGraphic],
          });
        } else {
          var tempGraphic = new Graphic({
            geometry: point,
            attributes: {
              Id: cardData ? cardData.id : 0,
              Name: title || (cardData ? cardData.name : "Destination"),
              PhotoUrls: cardData ? cardData.photos : "",
              Description: cardData ? cardData.description : "",
              Category: cardData ? cardData.category : "Explore",
              Rating: cardData ? cardData.rating : 4.5,
              City: cardData ? cardData.city : "Cairo"
            },
            popupTemplate: sourceLayer ? sourceLayer.popupTemplate : null
          });
          view.popup.open({
            location: point,
            features: [tempGraphic],
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

    async function loadLayer() {
      var layerUrl = layerUrlFor(opts.layer || opts.proxyUrl);
      if (!layerUrl) {
        return;
      }

      var featureLayerOpts = {
        url: layerUrl,
        outFields: ["*"]
      };
      if (_mapConfig && _mapConfig.portalId) {
        featureLayerOpts.portalItem = { id: _mapConfig.portalId };
      }
      sourceLayer = new FeatureLayer(featureLayerOpts);

      map.add(sourceLayer);
      sourceLayer.visible = false;

      try {
        await sourceLayer.load();
        // whenLayerView returns a Promise resolving to the LayerView; no .ready chaining needed.
        await view.whenLayerView(sourceLayer);

        // Build custom premium EGYXPLORE popup template — Specification-compliant v3 (2-column details card)
        function _safeEsc(s) {
          return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
        }

        function _renderStars(rating) {
          var r = parseFloat(rating) || 4.9;
          var html = '';
          for (var s = 1; s <= 5; s++) {
            if (s <= Math.floor(r)) {
              html += '<i class="bi bi-star-fill"></i>';
            } else if (s - r <= 0.5 && s - r > 0) {
              html += '<i class="bi bi-star-half"></i>';
            } else {
              html += '<i class="bi bi-star" style="opacity:0.35"></i>';
            }
          }
          return html;
        }

        function _getId(attrs) {
          if (!attrs) return 0;

          // 1. Direct database ID keys (SQL Destination Id)
          var dbKeys = ['DatabaseId', 'databaseId', 'destination_id', 'Id', 'id', 'ID', 'Id_1'];
          for (var i = 0; i < dbKeys.length; i++) {
            var v = attrs[dbKeys[i]];
            if (v !== undefined && v !== null && String(v) !== '' && String(v) !== '0') {
              var num = parseInt(v);
              if (!isNaN(num) && num > 0) return num;
            }
          }

          // 2. DOM card lookup by Name to find the exact SQL Destination.Id
          var name = attrs.Name || attrs.name || attrs.English_Name || attrs.english_name || attrs.TITLE || attrs.Title;
          if (name) {
            var card = document.querySelector('#exploreList .explore-card[data-name="' + String(name).replace(/"/g, '\\"') + '"]');
            if (card) {
              var cid = parseInt(card.getAttribute('data-id'));
              if (!isNaN(cid) && cid > 0) return cid;
            }
          }

          // 3. Fallback: GIS ObjectId
          var gisKeys = ['ObjectId', 'OBJECTID', 'FID', 'fid'];
          for (var j = 0; j < gisKeys.length; j++) {
            var gv = attrs[gisKeys[j]];
            if (gv !== undefined && gv !== null && String(gv) !== '' && String(gv) !== '0') {
              var gnum = parseInt(gv);
              if (!isNaN(gnum) && gnum > 0) return gnum;
            }
          }

          return 0;
        }

        function _fixImgUrl(url) {
          if (!url) return "/assets/img/egypt_feature1.jpg";
          var u = String(url).trim();
          if (!u || u === "/images/placeholder-destination.jpg") return "/assets/img/egypt_feature1.jpg";
          if (u.indexOf("~/") === 0) u = u.substring(1);
          if (u.indexOf("http://") !== 0 && u.indexOf("https://") !== 0 && u.indexOf("/") !== 0) {
            u = "/" + u;
          }
          return u;
        }

        function createCustomPopupTemplate(graphic) {
          var attrs = graphic.attributes || {};
          var rawName = attrs.Name || attrs.name || attrs.English_Name || attrs.english_name || attrs.TITLE || attrs.Title || "";
          var id = _getId(attrs);

          var cardEl = null;
          if (id > 0) {
            cardEl = document.querySelector('#exploreList .explore-card[data-id="' + id + '"]');
          }
          if (!cardEl && rawName) {
            cardEl = document.querySelector('#exploreList .explore-card[data-name="' + String(rawName).replace(/"/g, '\\"') + '"]');
          }
          if (!cardEl && graphic.geometry) {
            var gLat = graphic.geometry.latitude;
            var gLng = graphic.geometry.longitude;
            if (gLat && gLng) {
              var allCards = document.querySelectorAll('#exploreList .explore-card');
              for (var c = 0; c < allCards.length; c++) {
                var cLat = parseFloat(allCards[c].getAttribute('data-lat'));
                var cLng = parseFloat(allCards[c].getAttribute('data-lng'));
                if (!isNaN(cLat) && !isNaN(cLng) && Math.abs(cLat - gLat) < 0.005 && Math.abs(cLng - gLng) < 0.005) {
                  cardEl = allCards[c];
                  break;
                }
              }
            }
          }

          if (cardEl) {
            if (!id || id <= 0) id = parseInt(cardEl.getAttribute('data-id')) || 0;
            if (!rawName) rawName = cardEl.getAttribute('data-name') || "";
          }

          var name     = rawName || "Destination";
          var category = attrs.Category || attrs.category || (cardEl ? cardEl.getAttribute('data-category') : null) || "Explore";
          var city     = attrs.City || attrs.city || attrs.Governorate || attrs.governorate || (cardEl ? cardEl.getAttribute('data-city') : null) || "Cairo";
          var desc     = attrs.Description || attrs.description || (cardEl ? cardEl.getAttribute('data-description') : null) || "";
          
          var photoUrls = attrs.PhotoUrls || attrs.photoUrls || attrs.PhotoUrl || attrs.photoUrl ||
                          attrs.Photo_Urls || attrs.Photo_Url || attrs.Image || attrs.image ||
                          attrs.ImageUrl || attrs.imageUrl || attrs.Photos || attrs.photos ||
                          attrs.URL || attrs.url || attrs.photosData || (cardEl ? cardEl.getAttribute('data-photos') : "");

          if (!photoUrls && cardEl) {
            var cardImgs = Array.from(cardEl.querySelectorAll('img')).map(function(i){ return i.src || i.getAttribute('data-src'); }).filter(Boolean);
            if (cardImgs.length > 0) photoUrls = cardImgs.join('|');
          }

          var rating       = attrs.Rating || attrs.rating || (cardEl ? cardEl.getAttribute('data-rating') : null) || 4.5;
          var reviewsCount = attrs.ReviewCount || attrs.reviewCount || attrs.Visits || 21;
          var openAt       = attrs.OpenAt || attrs.openAt;
          var closeAt      = attrs.CloseAt || attrs.closeAt;

          var lat = graphic.geometry ? graphic.geometry.latitude : (attrs.Y || 30.0444);
          var lng = graphic.geometry ? graphic.geometry.longitude : (attrs.X || 31.2357);

          var rawCat = category && category !== "Explore" ? category : "";
          var typeVal = attrs.Type || attrs.type || rawCat || "Museum";
          var estVisitVal = attrs.EstimatedVisit || (typeVal.indexOf("Museum") > -1 ? "2–3 Hours" : typeVal.indexOf("Pharaonic") > -1 ? "2–4 Hours" : "1–2 Hours");

          // Parse photo URLs
          var images = [];
          if (photoUrls) {
            String(photoUrls).split(/[\r\n\|]+/).forEach(function(p) {
              var t = _fixImgUrl(p);
              if (t && images.indexOf(t) === -1) images.push(t);
            });
          }
          if (images.length === 0) images.push("/assets/img/egypt_feature1.jpg");

          var uid = 'pop_' + Math.random().toString(36).substr(2, 7);
          var isFavorited = (window.EGY_FAVORITED_IDS || []).indexOf(parseInt(id)) > -1;
          var targetUrl = "/Destination/Details/" + encodeURIComponent(id);

          // Build Left Column: Image Gallery
          var galleryArrowsHtml = '';
          var galleryDotsHtml = '';
          var galleryThumbsHtml = '';

          if (images.length > 1) {
            galleryArrowsHtml =
              '<button type="button" class="popup-gallery-arrow popup-gallery-arrow--prev" id="' + uid + '-prev" aria-label="Previous image"><i class="bi bi-chevron-left"></i></button>' +
              '<button type="button" class="popup-gallery-arrow popup-gallery-arrow--next" id="' + uid + '-next" aria-label="Next image"><i class="bi bi-chevron-right"></i></button>';

            galleryDotsHtml = '<div class="popup-gallery-dots" id="' + uid + '-dots">';
            images.forEach(function(_, i) {
              galleryDotsHtml += '<button type="button" class="popup-gallery-dot' + (i === 0 ? ' active' : '') + '" data-index="' + i + '"></button>';
            });
            galleryDotsHtml += '</div>';

            galleryThumbsHtml = '<div class="popup-gallery-thumbs" id="' + uid + '-thumbs">';
            images.forEach(function(src, i) {
              galleryThumbsHtml += '<img src="' + _safeEsc(src) + '" class="popup-thumb-img' + (i === 0 ? ' active' : '') + '" data-index="' + i + '" alt="Thumb ' + (i+1) + '" onerror="this.onerror=null;this.src=\'/assets/img/egypt_feature1.jpg\';" />';
            });
            galleryThumbsHtml += '</div>';
          }

          var galleryHtml =
            '<div class="egy-popup-gallery">' +
              '<div class="popup-gallery-main">' +
                '<img id="' + uid + '-main-img" src="' + _safeEsc(images[0]) + '" alt="' + _safeEsc(name) + '" onerror="this.onerror=null;this.src=\'/assets/img/egypt_feature1.jpg\';" />' +
                galleryArrowsHtml +
                galleryDotsHtml +
              '</div>' +
              galleryThumbsHtml +
            '</div>';

          // Build Right Column: Destination Info
          var infoHtml =
            '<div class="egy-popup-info">' +
              '<div class="popup-top-badge-row">' +
                '<span class="popup-cat-badge"><i class="bi bi-compass me-1"></i>' + _safeEsc(category) + '</span>' +
              '</div>' +
              '<h4 class="popup-dest-title">' + _safeEsc(name) + '</h4>' +
              '<div class="popup-cat-loc-row">' +
                '<span class="popup-loc-text"><i class="bi bi-geo-alt-fill me-1"></i>' + _safeEsc(city) + ', Egypt</span>' +
              '</div>' +
              '<div class="popup-rating-row">' +
                '<div class="popup-stars">' + _renderStars(rating) + '</div>' +
                '<strong class="popup-rating-num">' + parseFloat(rating).toFixed(1) + '</strong>' +
                '<span class="popup-review-count">(' + reviewsCount + ')</span>' +
              '</div>' +
              '<p class="popup-dest-desc">' + _safeEsc(desc || (category + " destination in " + city + ".")) + '</p>' +
              '<div class="popup-info-grid">' +
                '<div class="popup-grid-item">' +
                  '<div class="popup-grid-header"><i class="bi bi-bank"></i> TYPE</div>' +
                  '<div class="popup-grid-val">' + _safeEsc(typeVal) + '</div>' +
                '</div>' +
                '<div class="popup-grid-item">' +
                  '<div class="popup-grid-header"><i class="bi bi-hourglass-split"></i> EST. VISIT</div>' +
                  '<div class="popup-grid-val">' + _safeEsc(estVisitVal) + '</div>' +
                '</div>' +
              '</div>' +
              '<div class="popup-actions-row">' +
                '<a href="' + _safeEsc(targetUrl) + '" class="btn-popup-primary" id="' + uid + '-details-btn">VIEW DETAILS <i class="bi bi-arrow-right ms-1"></i></a>' +
                '<button type="button" class="btn-popup-favorite ' + (isFavorited ? 'favorited' : '') + '" id="' + uid + '-fav" data-id="' + id + '">' +
                  '<i class="bi ' + (isFavorited ? 'bi-heart-fill' : 'bi-heart') + ' me-1"></i>' +
                  '<span>' + (isFavorited ? 'Favorited' : 'Favorite') + '</span>' +
                '</button>' +
              '</div>' +
            '</div>';

          // Interactive Script IIFE
          var scriptHtml = '<script>(function(){' +
            'setTimeout(function(){' +
              'var cNode = document.querySelector(".esri-popup__content"); if(cNode) cNode.scrollTop = 0;' +
              'var mNode = document.querySelector(".esri-popup__main-container"); if(mNode) mNode.scrollTop = 0;' +
            '}, 10);' +
            'var imgs = ' + JSON.stringify(images) + ';' +
            'var curIdx = 0;' +
            'var autoTimer = null;' +
            'var mainImg = document.getElementById("' + uid + '-main-img");' +
            'function setImage(idx) {' +
              'curIdx = (idx + imgs.length) % imgs.length;' +
              'if (mainImg) { mainImg.style.opacity = "0.3"; setTimeout(function(){ mainImg.src = imgs[curIdx]; mainImg.style.opacity = "1"; }, 120); }' +
              'document.querySelectorAll("#' + uid + '-dots .popup-gallery-dot").forEach(function(d, i){ d.classList.toggle("active", i === curIdx); });' +
              'document.querySelectorAll("#' + uid + '-thumbs .popup-thumb-img").forEach(function(t, i){ t.classList.toggle("active", i === curIdx); });' +
              'resetAutoRotate();' +
            '}' +
            'function resetAutoRotate() {' +
              'if (imgs.length <= 1) return;' +
              'clearInterval(autoTimer);' +
              'autoTimer = setInterval(function(){ setImage(curIdx + 1); }, 10000);' +
            '}' +
            'var detailsBtn = document.getElementById("' + uid + '-details-btn");' +
            'if (detailsBtn) {' +
              'detailsBtn.addEventListener("click", function(e){' +
                'e.preventDefault(); e.stopPropagation();' +
                'var destId = ' + JSON.stringify(id) + ';' +
                'if (destId && destId !== 0 && destId !== "0") {' +
                  'window.location.href = "/Destination/Details/" + encodeURIComponent(destId);' +
                '} else {' +
                  'window.location.href = "' + targetUrl + '";' +
                '}' +
              '});' +
            '}' +
            'var prevBtn = document.getElementById("' + uid + '-prev");' +
            'var nextBtn = document.getElementById("' + uid + '-next");' +
            'if (prevBtn) prevBtn.addEventListener("click", function(e){ e.stopPropagation(); setImage(curIdx - 1); });' +
            'if (nextBtn) nextBtn.addEventListener("click", function(e){ e.stopPropagation(); setImage(curIdx + 1); });' +
            'document.querySelectorAll("#' + uid + '-dots .popup-gallery-dot").forEach(function(d){' +
              'd.addEventListener("click", function(e){ e.stopPropagation(); setImage(parseInt(d.getAttribute("data-index"))); });' +
            '});' +
            'document.querySelectorAll("#' + uid + '-thumbs .popup-thumb-img").forEach(function(t){' +
              't.addEventListener("click", function(e){ e.stopPropagation(); setImage(parseInt(t.getAttribute("data-index"))); });' +
            '});' +
            'resetAutoRotate();' +
            'var zoomBtn = document.getElementById("' + uid + '-zoom");' +
            'if (zoomBtn) zoomBtn.addEventListener("click", function(e){ e.stopPropagation(); if(window.EGYMaps && window.EGYMaps.zoomTo){ window.EGYMaps.zoomTo(' + lat + ',' + lng + '); } });' +
            'var closeBtn = document.getElementById("' + uid + '-close");' +
            'if (closeBtn) closeBtn.addEventListener("click", function(e){ e.stopPropagation(); if(window.EGYMaps && window.EGYMaps.closePopup){ window.EGYMaps.closePopup(); } });' +
            'var favBtn = document.getElementById("' + uid + '-fav");' +
            'if (favBtn) favBtn.addEventListener("click", function(e){' +
              'e.preventDefault(); e.stopPropagation();' +
              'var itemId = parseInt(favBtn.getAttribute("data-id"));' +
              'if (!itemId || isNaN(itemId) || itemId <= 0) {' +
                'var cardEl = document.querySelector("#exploreList .explore-card[data-name=\'" + ' + JSON.stringify(name) + '\' ]");' +
                'if (cardEl) itemId = parseInt(cardEl.getAttribute("data-id"));' +
              '}' +
              'if (!itemId || isNaN(itemId) || itemId <= 0) return;' +
              'fetch("/Favorites/Toggle", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ itemType: 0, itemId: itemId }) })' +
              '.then(function(r){ return r.status === 401 ? (window.location.href="/Account/Login?returnUrl=" + encodeURIComponent(window.location.pathname)) : r.json(); })' +
              '.then(function(data){' +
                'if (!data || data.error) return;' +
                'var icon = favBtn.querySelector("i");' +
                'var txt = favBtn.querySelector("span");' +
                'if (data.isFavorited) {' +
                  'favBtn.classList.add("favorited");' +
                  'if (icon) icon.className = "bi bi-heart-fill me-1";' +
                  'if (txt) txt.textContent = "Favorited";' +
                  'if (!window.EGY_FAVORITED_IDS) window.EGY_FAVORITED_IDS = [];' +
                  'if (window.EGY_FAVORITED_IDS.indexOf(itemId) === -1) window.EGY_FAVORITED_IDS.push(itemId);' +
                '} else {' +
                  'favBtn.classList.remove("favorited");' +
                  'if (icon) icon.className = "bi bi-heart me-1";' +
                  'if (txt) txt.textContent = "Favorite";' +
                  'if (window.EGY_FAVORITED_IDS) { var idx = window.EGY_FAVORITED_IDS.indexOf(itemId); if (idx > -1) window.EGY_FAVORITED_IDS.splice(idx, 1); }' +
                '}' +
                'var cardHeart = document.querySelector(".btn-favorite-heart[data-id=\'" + itemId + "\']");' +
                'if (cardHeart) {' +
                  'cardHeart.classList.toggle("favorited", data.isFavorited);' +
                  'var chIcon = cardHeart.querySelector("i");' +
                  'if (chIcon) { chIcon.className = "bi " + (data.isFavorited ? "bi-heart-fill" : "bi-heart"); }' +
                '}' +
              '});' +
            '});' +
          '})();<\/script>';

          var fullCardHtml =
            '<div class="egy-popup-card">' +
              '<div class="egy-popup-header">' +
                '<button type="button" class="btn-popup-zoom" id="' + uid + '-zoom"><i class="bi bi-search"></i><span>Zoom to</span></button>' +
                '<button type="button" class="btn-popup-close" id="' + uid + '-close" aria-label="Close"><i class="bi bi-x-lg"></i></button>' +
              '</div>' +
              '<div class="egy-popup-body">' +
                galleryHtml +
                infoHtml +
              '</div>' +
            '</div>' +
            scriptHtml;

          return fullCardHtml;
        }

        sourceLayer.popupTemplate = {
          title: "",
          content: createCustomPopupTemplate
        };

        var query = sourceLayer.createQuery();
        query.where = "1=1";
        query.outFields = ["*"];
        query.returnGeometry = true;
        var result = await sourceLayer.queryFeatures(query);
        if (result && result.features) {
          graphicsByFeature.clear();
          result.features.forEach(function (f) {
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
            var idKey = _firstKey(f.attributes, propMap.id || ["id"]);
            graphicsByFeature.set(f.attributes[idKey], graphic);
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
          opts.center ? opts.center[1] : 31.2357,
          opts.center ? opts.center[0] : 30.0444,
        ],
        zoom: opts.zoom || 7,
        popup: {
          dockEnabled: false,
          dockOptions: {
            buttonEnabled: false,
            breakpoint: false
          },
          alignment: "top-center"
        }
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
          view.hitTest(event, { include: overlayGraphicsLayer }).then(function (response) {
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
    zoomTo: function (lat, lng) {
      for (var k in _maps) {
        if (_maps[k] && typeof _maps[k].zoomTo === "function") {
          _maps[k].zoomTo(lat, lng);
        }
      }
    },
    closePopup: function () {
      for (var k in _maps) {
        if (_maps[k] && typeof _maps[k].closePopup === "function") {
          _maps[k].closePopup();
        }
      }
    }
  };
})();
