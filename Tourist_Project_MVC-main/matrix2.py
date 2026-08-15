import json
import urllib.parse
import urllib.request

p = r'E:\ITI\Graduation_Project\Tourist_Project_MVC-main\Tourist_Project_MVC\appsettings.json'
t = open(p, encoding='utf-8').read()
cfg = json.loads(t[:t.rindex('}') + 1])['ArcGIS']
key = cfg['ApiKey']
TOKEN = urllib.parse.quote(key, safe='')
base = 'https://services3.arcgis.com/UDCw00RKDRKPqASe/arcgis/rest/services/tourists_layer_data/FeatureServer/0'
proxy = 'http://127.0.0.1:8765/https/' + base[len('https://'):]


def q(params):
    req = urllib.request.Request(proxy + '/query?' + params + '&f=json&token=' + TOKEN,
                                 headers={'Referer': 'http://localhost:5217/'})
    d = json.loads(urllib.request.urlopen(req, timeout=60).read().decode())
    return len(d.get('features', [])), d.get('name')


cases = [
    ('rrc=5', 'where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=5'),
    ('rrc=1000', 'where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=1000'),
    ('offset0+rrc1000', 'where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset=0&resultRecordCount=1000'),
    ('offset1000+rrc1000', 'where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset=1000&resultRecordCount=1000'),
    ('countOnly', 'where=1%3D1&returnCountOnly=true'),
]
for label, params in cases:
    try:
        feats, name = q(params)
        print('{} -> features: {} | name: {}'.format(label, feats, name))
    except Exception as e:
        print('{} -> FAILED {}: {}'.format(label, type(e).__name__, str(e)[:120]))
