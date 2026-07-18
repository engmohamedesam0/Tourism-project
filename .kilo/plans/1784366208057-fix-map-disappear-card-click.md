# Plan: Fix card-click map-disappear bug in NearMe and Explore

## Verified diagnosis

Cross-checked the user's claim against source. It is correct.

- **`Views/NearMe/Index.cshtml:685`** — inside the card click handler, when a matching graphic is found, `view.goTo({ center: [graphic.geometry.x, graphic.geometry.y], zoom: 15 })` is called. The `graphic.geometry` comes from an ArcGIS FeatureLayer query (`maps.js:262`, `geometry: f.geometry`). For hosted ArcGIS Online services, `f.geometry` is in Web Mercator (wkid 102100 / 3857). Passing the raw x/y values as a plain array to `view.goTo` causes ArcGIS to interpret them as WGS84 degrees — wildly incorrect coordinates — and the view silently goes blank.
- **`Views/Explore/Index.cshtml:589`** — identical pattern (`view.goTo({ center: [graphic.geometry.x, graphic.geometry.y], zoom: 15 })`).
- Both files already parse `data-lat`/`data-lng` into `lat`/`lng` earlier in the same handler, and `fitNearMeBounds()` / `fitExploreBounds()` already successfully use those same attributes. The fix can simply use them.
- `EGYMaps.initWfsMap` returns a handle that exposes `openPopupAt(lat, lng, title)` (`maps.js:199`), which correctly constructs a WGS84 `Point` and opens the popup.

Only two locations in the repo use `graphic.geometry.x/y` for view navigation — both are these card-click handlers. No other callers are affected.

## Proposed changes

### 1) `Tourist_Project_MVC/Views/NearMe/Index.cshtml`

Replace lines 679–701 (the `var markerFound = false;` block through the closing `});` of the card click handler):

```javascript
                    var markerFound = false;
                    if (nearMeMap && nearMeMap.overlayLayer) {
                        nearMeMap.overlayLayer().graphics.forEach(function (graphic) {
                            var p = graphic.attributes || {};
                            var id = _firstDefined(p, nearMePropMap.id, []);
                            if (String(id) === String(cardId)) {
                                nearMeMap.view.goTo({ center: [graphic.geometry.x, graphic.geometry.y], zoom: 15 }, { duration: 1000 });
                                nearMeMap.view.popup = {
                                    location: graphic.geometry,
                                    title: _firstDefined(p, nearMePropMap.name, ['']),
                                    content: buildPopup({ attributes: p })
                                };
                                nearMeMap.view.openPopup();
                                markerFound = true;
                            }
                        });
                    }

                    if (!markerFound && nearMeMap && nearMeMap.view && !isNaN(lat) && !isNaN(lng)) {
                        nearMeMap.view.goTo({ center: [lng, lat], zoom: 15 }, { duration: 1000 });
                    }
```

With:

```javascript
                    var name = card.getAttribute('data-name') || '';
                    if (nearMeMap && nearMeMap.view && !isNaN(lat) && !isNaN(lng)) {
                        nearMeMap.view.goTo({ center: [lng, lat], zoom: 15 }, { duration: 1000 });
                        if (nearMeMap.openPopupAt) nearMeMap.openPopupAt(lat, lng, name);
                    }
```

### 2) `Tourist_Project_MVC/Views/Explore/Index.cshtml`

Replace lines 582–604 (the `var markerFound = false;` block through the closing `});` of the card click handler):

```javascript
                var markerFound = false;
                if (mapInstance && mapInstance.overlayLayer) {
                    mapInstance.overlayLayer().graphics.forEach(function (graphic) {
                        var attrs = graphic.attributes || {};
                        var id = _firstDefined(attrs, explorePropMap.id, []);
                        if (String(id) === String(cardId)) {
                            view = mapInstance.view;
                            view.goTo({ center: [graphic.geometry.x, graphic.geometry.y], zoom: 15 }, { duration: 1000 });
                            view.popup = {
                                location: graphic.geometry,
                                title: _firstDefined(attrs, explorePropMap.name, ['']),
                                content: buildPopup({ attributes: attrs })
                            };
                            view.openPopup();
                            markerFound = true;
                        }
                    });
                }

                if (!markerFound && mapInstance && mapInstance.map && !isNaN(lat) && !isNaN(lng)) {
                    mapInstance.view.goTo({ center: [lng, lat], zoom: 15 }, { duration: 1000 });
                }
```

With:

```javascript
                var name = card.getAttribute('data-name') || '';
                if (mapInstance && mapInstance.view && !isNaN(lat) && !isNaN(lng)) {
                    mapInstance.view.goTo({ center: [lng, lat], zoom: 15 }, { duration: 1000 });
                    if (mapInstance.openPopupAt) mapInstance.openPopupAt(lat, lng, name);
                }
```

## Risk / tradeoff to confirm

The current code builds a rich popup via `buildPopup({ attributes: p })` which includes:
- NearMe: destination name, category badge, and a "View Details" link (`/NearMe/Details/{id}`).
- Explore: destination name, city, category badge, and a "Details" link (`/Destination/Details/{id}`).

The proposed `openPopupAt(lat, lng, name)` sets `content: ''` (`maps.js:205`), so the popup will display **title only** (no category, city, or details button).

**Question:** Is the title-only popup acceptable, or should the popup retain the rich HTML content? If content should be preserved, the implementation would instead need to create the ArcGIS `Point` directly and set `view.popup = { location: point, title: name, content: buildPopup({ attributes: p }) }` rather than using `openPopupAt`.

## Validation plan

1. Run the app and navigate to `/NearMe`.
2. Click at least 3 different destination cards.
   - Verify the map pans smoothly to each destination (zoom ~15).
   - Verify a popup opens at the destination with the correct title.
   - Verify the map view does not go blank or distort.
3. Navigate to `/Explore`.
4. Repeat the card-click test (3+ cards).
5. Confirm that cards without valid `data-lat`/`data-lng` gracefully skip the pan (the `!isNaN` guard).
