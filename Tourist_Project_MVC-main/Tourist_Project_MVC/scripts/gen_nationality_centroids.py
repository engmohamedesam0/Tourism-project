"""Generate NationalityCentroids.cs (demonym -> country center) + starter CSV for the
ArcGIS tourists-by-nationality point layer. Data: world-countries npm package v5.0.0
(via jsdelivr CDN) — country approximate centers keyed by English demonym.
"""
import json
import urllib.request
import psycopg2
import csv
import os

# --- 1. DB aggregates -------------------------------------------------------
conn = psycopg2.connect(host='localhost', port=5432, dbname='Tourist_PostGIS_DB_MVC',
                        user='postgres', password='admin')
cur = conn.cursor()
cur.execute('''
    SELECT u."Nationality", COUNT(*)
    FROM "Tourists" t
    JOIN "AspNetUsers" u ON t."ApplicationUserId" = u."Id"
    WHERE u."Nationality" IS NOT NULL AND btrim(u."Nationality") <> ''
    GROUP BY u."Nationality"
    ORDER BY 2 DESC
''')
aggs = cur.fetchall()
conn.close()
print('DB nationalities:', aggs)

# --- 2. Country data --------------------------------------------------------
url = 'https://cdn.jsdelivr.net/npm/world-countries@v5.0.0/countries.json'
req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
with urllib.request.urlopen(req, timeout=120) as r:
    data = json.load(r)

# Common demonym variants used by the app's register list but not present
# verbatim in the dataset (value = the dataset key to reuse).
ALIASES = {
    'Saudi': 'Saudi Arabian',
    'Nepalese': 'Nepali',
    'Burmese': 'Burmese',          # dataset key for Myanmar
    'Macedonian': 'North Macedonian',
    'Kosovan': 'Kosovar',
}

source = 'world-countries v5.0.0 (country approximate centers, keyed by English demonym)'
by_demonym = {}
for c in data:
    latlng = c.get('latlng') or [None, None]
    dem = c.get('demonyms')
    if isinstance(dem, dict):
        eng = dem.get('eng') or {}
        dem = eng.get('m') or eng.get('f')
    name = (c.get('name') or {}).get('common', '?')
    if dem and latlng[0] is not None:
        by_demonym[dem] = (float(latlng[0]), float(latlng[1]), name)
for alias, target in ALIASES.items():
    if alias not in by_demonym and target in by_demonym:
        by_demonym[alias] = by_demonym[target]

print('source=%s, entries=%d' % (source, len(by_demonym)))
for nat, cnt in aggs:
    hit = by_demonym.get(nat)
    print('  %s x%d -> %s' % (nat, cnt, hit if hit else 'NO MATCH'))

# --- 3. Generate NationalityCentroids.cs ------------------------------------
base = os.path.join('Tourist_Project_MVC', 'Services')
os.makedirs(base, exist_ok=True)
lines = [
    '// AUTO-GENERATED — do not edit by hand.',
    '// Source: %s.' % source,
    '// Regenerate: python Tourist_Project_MVC/scripts/gen_nationality_centroids.py',
    'namespace Tourist_Project_MVC.Services',
    '{',
    '    public static class NationalityCentroids',
    '    {',
    '        public static readonly IReadOnlyDictionary<string, (double Latitude, double Longitude)> Map =',
    '            new Dictionary<string, (double Latitude, double Longitude)>(StringComparer.OrdinalIgnoreCase)',
    '            {',
]
for dem in sorted(by_demonym):
    lat, lng, name = by_demonym[dem]
    k = dem.replace('\\', '\\\\').replace('"', '\\"')
    lines.append('                ["%s"] = (%.4f, %.4f), // %s' % (k, lat, lng, name))
lines += [
    '            };',
    '',
    '        public static (double Latitude, double Longitude)? Get(string? nationality)',
    '        {',
    '            if (string.IsNullOrWhiteSpace(nationality)) return null;',
    '            return Map.TryGetValue(nationality.Trim(), out var c) ? c : null;',
    '        }',
    '    }',
    '}',
]
cs_path = os.path.join(base, 'NationalityCentroids.cs')
with open(cs_path, 'w', encoding='utf-8-sig') as f:
    f.write('\n'.join(lines))
print('wrote', os.path.abspath(cs_path))

# --- 4. Starter CSV for the point layer -------------------------------------
out_dir = os.path.join('Tourist_Project_MVC', 'Docs', 'arcgis')
os.makedirs(out_dir, exist_ok=True)
csv_path = os.path.join(out_dir, 'tourists-nationality-layer-data.csv')
with open(csv_path, 'w', newline='', encoding='utf-8-sig') as f:
    w = csv.writer(f)
    w.writerow(['Nationality', 'TouristCount', 'Latitude', 'Longitude'])
    for nat, cnt in aggs:
        hit = by_demonym.get(nat)
        if not hit:
            print('  SKIP (no centroid):', nat)
            continue
        w.writerow([nat, cnt, '%.4f' % hit[0], '%.4f' % hit[1]])
print('wrote', os.path.abspath(csv_path))
