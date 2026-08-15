"""Syncs the tourists + nationality layers after removing 3,500 accounts:
updates the 544 kept tourists, deletes the 3,500 removed (chunked), and
recomputes the nationality layer. Through the local proxy."""
import json
import os
import re
import time
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


def http_get(url, timeout=90):
    req = urllib.request.Request(url, headers={'Referer': 'http://localhost:5217/'})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode())


def http_post(url, data, timeout=120):
    body = urllib.parse.urlencode(data).encode('utf-8')
    req = urllib.request.Request(url, data=body,
                                 headers={'Referer': 'http://localhost:5217/', 'Content-Type': 'application/x-www-form-urlencoded'})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode())


def apply_chunked(layer, adds, updates, deletes, label):
    added = updated = deleted = 0
    for i in range(0, len(adds), 500):
        res = http_post(layer + '/applyEdits?token=' + TOKEN, {'f': 'json', 'adds': json.dumps(adds[i:i + 500], ensure_ascii=False)})
        if 'error' in res:
            raise RuntimeError('{} adds error: {}'.format(label, json.dumps(res['error'])[:300]))
        added += sum(1 for r in res.get('addResults', []) if r.get('success'))
    for i in range(0, len(updates), 500):
        res = http_post(layer + '/applyEdits?token=' + TOKEN, {'f': 'json', 'updates': json.dumps(updates[i:i + 500], ensure_ascii=False)})
        if 'error' in res:
            raise RuntimeError('{} updates error: {}'.format(label, json.dumps(res['error'])[:300]))
        updated += sum(1 for r in res.get('updateResults', []) if r.get('success'))
    for i in range(0, len(deletes), 500):
        chunk = deletes[i:i + 500]
        res = http_post(layer + '/applyEdits?token=' + TOKEN, {'f': 'json', 'deletes': ','.join(map(str, chunk))})
        if 'error' in res:
            raise RuntimeError('{} deletes error: {}'.format(label, json.dumps(res['error'])[:300]))
        deleted += sum(1 for r in res.get('deleteResults', []) if r.get('success'))
    print('{}: +{} added, ~{} updated, -{} deleted'.format(label, added, updated, deleted))


conn = psycopg2.connect(host='localhost', port=5432, dbname='Tourist_PostGIS_DB_MVC',
                        user='postgres', password='admin')
cur = conn.cursor()

# ================= TOURISTS =================
print('=== tourists ===')
cur.execute("""SELECT t."Id", u."Id", u."Email", u."FirstName", u."LastName", u."Nationality",
                      u."PhoneNumber", t."IdNumber", t."Passport", t."point_Balance",
                      t."RegisterDate", t."Status"
               FROM "Tourists" t JOIN "AspNetUsers" u ON t."ApplicationUserId" = u."Id"
               ORDER BY t."Id" """)
tourists = cur.fetchall()
db_ids = {r[0] for r in tourists}
print('DB tourists:', len(tourists))

layer = proxy_layer(cfg['TouristsTableUrl'])
meta = http_get('{}?f=json&token={}'.format(layer, TOKEN))
oid_field = meta.get('objectIdFieldName') or 'ObjectId'
field_names = {f['name'] for f in meta.get('fields', [])}

remote = {}
offset = 0
while True:
    qurl = '{}/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset={}&resultRecordCount=1000&f=json&token={}'.format(layer, offset, TOKEN)
    d = http_get(qurl)
    if 'features' not in d or len(d.get('features', [])) == 0:
        print('!! query offset={} response: {}'.format(offset, json.dumps(d)[:300]))
    page = d.get('features', [])
    for f in page:
        a = f['attributes']
        if a.get('TouristId') is not None and a.get(oid_field) is not None:
            remote[int(a['TouristId'])] = int(a[oid_field])
    if len(page) < 1000:
        break
    offset += len(page)
print('existing on layer:', len(remote))

adds, updates = [], []
for r in tourists:
    tid, uid, email, first, last, nat, phone, idnum, passport, bal, regdate, status = r
    attrs = {
        'TouristId': tid, 'UserId': uid, 'Email': email or '', 'FirstName': first, 'LastName': last,
        'FullName': ('{} {}'.format(first, last)).strip(), 'Nationality': nat or '',
        'PhoneNumber': phone or '', 'IdNumber': idnum or '', 'Passport': passport or '',
        'PointBalance': bal, 'RegisterDate': regdate.strftime('%Y-%m-%d'), 'Status': status or 'Active',
    }
    if tid in remote:
        attrs[oid_field] = remote[tid]
        updates.append({'attributes': attrs})
    else:
        adds.append({'attributes': attrs})
deletes = [oid for tid, oid in remote.items() if tid not in db_ids]
print('adds:', len(adds), '| updates:', len(updates), '| deletes:', len(deletes))
t0 = time.time()
apply_chunked(layer, adds, updates, deletes, 'tourists')
print('tourists sync {:.1f}s'.format(time.time() - t0))

# ================= NATIONALITY =================
print('\n=== nationality ===')
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
print('nationality aggregates:', len(aggregates))

layer = proxy_layer(cfg['TouristNationalityLayerUrl'])
meta = http_get('{}?f=json&token={}'.format(layer, TOKEN))
oid_field = meta.get('objectIdFieldName') or 'ObjectId'
remote_nat = {}
d = http_get('{}/query?where=1%3D1&outFields=Nationality,ObjectId&returnGeometry=false&resultRecordCount=1000&f=json&token={}'.format(layer, TOKEN))
for f in d.get('features', []):
    a = f['attributes']
    if a.get('Nationality') is not None and a.get(oid_field) is not None:
        remote_nat[a['Nationality'].lower()] = int(a[oid_field])
print('existing nationalities on layer:', len(remote_nat))

adds, updates, deletes, newset = [], [], [], set()
for nat, count in aggregates:
    c = centroids.get(nat.lower())
    if c is None:
        continue
    newset.add(nat.lower())
    attrs = {'Nationality': nat, 'TouristCount': count, 'Latitude': c[0], 'Longitude': c[1]}
    geometry = {'x': c[1], 'y': c[0], 'spatialReference': {'wkid': 4326}}
    if nat.lower() in remote_nat:
        attrs[oid_field] = remote_nat[nat.lower()]
        updates.append({'attributes': attrs, 'geometry': geometry})
    else:
        adds.append({'attributes': attrs, 'geometry': geometry})
deletes = [oid for nat, oid in remote_nat.items() if nat not in newset]
print('adds:', len(adds), '| updates:', len(updates), '| deletes:', len(deletes))
t0 = time.time()
apply_chunked(layer, adds, updates, deletes, 'nationality')
print('nationality sync {:.1f}s'.format(time.time() - t0))
