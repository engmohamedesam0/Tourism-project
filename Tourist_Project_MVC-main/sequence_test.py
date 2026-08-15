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
layer = proxy

def get(url):
    req = urllib.request.Request(url, headers={'Referer': 'http://localhost:5217/'})
    return json.loads(urllib.request.urlopen(req, timeout=60).read().decode())

# 1) metadata first (like the sync script)
m = get('{}?f=json&token={}'.format(layer, TOKEN))
print('metadata -> name:', m.get('name'), '| type:', m.get('type'))

# 2) query after metadata (exact sync pattern)
d = get('{}?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset=0&resultRecordCount=1000&f=json&token={}'.format(layer, TOKEN))
print('query after metadata -> features:', len(d.get('features', [])), '| name:', d.get('name'), '| type:', d.get('type'))

# 3) query again
d2 = get('{}?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset=0&resultRecordCount=1000&f=json&token={}'.format(layer, TOKEN))
print('query again -> features:', len(d2.get('features', [])), '| name:', d2.get('name'))
