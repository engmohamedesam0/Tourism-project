"""Full reset of the tourists + nationality layers: delete ALL features, then
re-add exactly what the DB has now (544 tourists, 148 nationalities)."""
import json
import os
import re
import urllib.parse
import urllib.request

import psycopg2

ROOT = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(ROOT, 'Tourist_Project_MVC', 'appsettings.json'), encoding='utf-8') as f:
    t = f.read()
cfg = json.loads(t[:t.rindex('}') + 1])['ArcGIS']
API_KEY = cfg['ApiKey']
PROXY = 'http://127.0.0.1:8765'
TOKEN = urllib.parse.quote(API_KEY, safe='')


def proxy_layer(base_url):
    base = base_url.rstrip('/') + '/0'
    return PROXY + '/https/' + base[len('https://'):]


def http_get(url, timeout=120):
    req = urllib.request.Request(url, headers={'Referer': 'http://localhost:5217/'})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode())


def http_post(url, data, timeout=180):
    body = urllib.parse.urlencode(data).encode('utf-8')
    req = urllib.request.Request(url, data=body,
                                 headers={'Referer': 'http://localhost:5217/', 'Content-Type': 'application/x-www-form-urlencoded'})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode())


def all_object_ids(layer, oid_field):
    ids = []
    offset = 0
    while True:
        d = http_get('{}/query?where=1%3D1&outFields={}&returnGeometry=false&resultOffset={}&resultRecordCount=1000&f=json&token={}'.format(layer, oid_field, offset, TOKEN))
        page = d.get('features', [])
        for f in page:
            ids.append(int(f['attributes'][oid_field]))
        if len(page) < 1000:
            break
        offset += len(page)
    return ids


def delete_all(layer, oid_field, label):
    ids = all_object_ids(layer, oid_field)
    print('{}: clearing {} features'.format(label, len(ids)))
    for i in range(0, len(ids), 500):
        chunk = ids[i:i + 500]
        res = http_post(layer + '/applyEdits?token=' + TOKEN, {'f': 'json', 'deletes': ','.join(map(str, chunk))})
        if 'error' in res:
            raise RuntimeError('{} delete error: {}'.format(label, json.dumps(res['error'])[:300]))
    print('{}: cleared'.format(label))


conn = psycopg2.connect(host='localhost', port=5432, dbname='Tourist_PostGIS_DB_MVC',
                        user='postgres', password='admin')
cur = conn.cursor()

# ============ TOURISTS ============
print('=== tourists reset ===')
cur.execute("""SELECT t."Id", u."Id", u."Email", u."FirstName", u."LastName", u."Nationality",
                      u."PhoneNumber", t."IdNumber", t."Passport", t."point_Balance",
                      t."RegisterDate", t."Status"
               FROM "Tourists" t JOIN "AspNetUsers" u ON t."ApplicationUserId" = u."Id"
               ORDER BY t."Id" """)
tourists = cur.fetchall()

layer = proxy_layer(cfg['TouristsTableUrl'])
meta = http_get('{}?f=json&token={}'.format(layer, TOKEN))
oid_field = meta.get('objectIdFieldName') or 'ObjectId'
delete_all(layer, oid_field, 'tourists')

adds = []
for r in tourists:
    tid, uid, email, first, last, nat, phone, idnum, passport, bal, regdate, status = r
    adds.append({'attributes': {
        'TouristId': tid, 'UserId': uid, 'Email': email or '', 'FirstName': first, 'LastName': last,
        'FullName': ('{} {}'.format(first, last)).strip(), 'Nationality': nat or '',
        'PhoneNumber': phone or '', 'IdNumber': idnum or '', 'Passport': passport or '',
        'PointBalance': bal, 'RegisterDate': regdate.strftime('%Y-%m-%d'), 'Status': status or 'Active',
    }})
for i in range(0, len(adds), 500):
    res = http_post(layer + '/applyEdits?token=' + TOKEN, {'f': 'json', 'adds': json.dumps(adds[i:i + 500], ensure_ascii=False)})
    if 'error' in res:
        raise RuntimeError('tourists add error: {}'.format(json.dumps(res['error'])[:300]))
print('tourists re-added:', len(adds))

# ============ NATIONALITY ============
print('\n=== nationality reset ===')
cs = open(os.path.join(ROOT, 'Tourist_Project_MVC', 'Services', 'NationalityCentroids.cs'), encoding='utf-8').read()
centroids = {}
for m in re.finditer(r'\["([^"]+)"\] = \((-?[\d.]+), (-?[\d.]+)\)', cs):
    centroids[m.group(1).lower()] = (float(m.group(2)), float(m.group(3)))

cur.execute("""SELECT u."Nationality", COUNT(*) FROM "Tourists" t
               JOIN "AspNetUsers" u ON t."ApplicationUserId" = u."Id"
               WHERE u."Nationality" IS NOT NULL AND u."Nationality" <> ''
               GROUP BY u."Nationality" ORDER BY u."Nationality" """)
aggregates = cur.fetchall()
conn.close()

layer = proxy_layer(cfg['TouristNationalityLayerUrl'])
meta = http_get('{}?f=json&token={}'.format(layer, TOKEN))
oid_field = meta.get('objectIdFieldName') or 'ObjectId'
delete_all(layer, oid_field, 'nationality')

adds = []
for nat, count in aggregates:
    c = centroids.get(nat.lower())
    if c is None:
        print('skip (no centroid):', nat)
        continue
    adds.append({'attributes': {'Nationality': nat, 'TouristCount': count, 'Latitude': c[0], 'Longitude': c[1]},
                 'geometry': {'x': c[1], 'y': c[0], 'spatialReference': {'wkid': 4326}}})
for i in range(0, len(adds), 500):
    res = http_post(layer + '/applyEdits?token=' + TOKEN, {'f': 'json', 'adds': json.dumps(adds[i:i + 500], ensure_ascii=False)})
    if 'error' in res:
        raise RuntimeError('nationality add error: {}'.format(json.dumps(res['error'])[:300]))
print('nationalities re-added:', len(adds))
print('\nDONE')
