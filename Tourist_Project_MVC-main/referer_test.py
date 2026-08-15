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
url = proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=5&f=json&token=' + tok

for label, headers in [('no referer', {}), ('referer localhost:5217', {'Referer': 'http://localhost:5217/'}),
                       ('referer arcgis.com', {'Referer': 'https://www.arcgis.com/'})]:
    try:
        req = urllib.request.Request(url, headers=headers)
        d = json.loads(urllib.request.urlopen(req, timeout=60).read().decode())
        print(label, '-> features:', len(d.get('features', [])), '| error:', 'error' in d)
        if 'error' in d:
            print('   ', json.dumps(d['error'])[:200])
    except urllib.error.HTTPError as e:
        print(label, '-> HTTP', e.code, '|', e.read(200))
    except Exception as e:
        print(label, '-> FAILED', type(e).__name__, str(e)[:120])
