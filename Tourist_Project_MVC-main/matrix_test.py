import json
import urllib.parse
import urllib.request

p = r'E:\ITI\Graduation_Project\Tourist_Project_MVC-main\Tourist_Project_MVC\appsettings.json'
t = open(p, encoding='utf-8').read()
cfg = json.loads(t[:t.rindex('}') + 1])['ArcGIS']
key = cfg['ApiKey']
tok = urllib.parse.quote(key, safe='')
base = 'https://services3.arcgis.com/UDCw00RKDRKPqASe/arcgis/rest/services/tourists_layer_data/FeatureServer/0'
proxy = 'http://127.0.0.1:8765/https/' + base[len('https://'):]

cases = [
    ('no-ref rrc5', {}),
    ('no-ref rrc1000', {}),
    ('ref rrc5', {'Referer': 'http://localhost:5217/'}),
    ('ref rrc1000', {'Referer': 'http://localhost:5217/'}),
]
for label, headers in cases:
    rrc = '5' if 'rrc5' in label else '1000'
    url = proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount={}&f=json&token={}'.format(rrc, tok)
    try:
        req = urllib.request.Request(url, headers=headers)
        d = json.loads(urllib.request.urlopen(req, timeout=60).read().decode())
        print(label, '-> features:', len(d.get('features', [])), '| name:', d.get('name'), '| type:', d.get('type'))
    except Exception as e:
        print(label, '-> FAILED', type(e).__name__, str(e)[:120])
