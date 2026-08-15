import json
import urllib.parse
import urllib.request

p = r'E:\ITI\Graduation_Project\Tourist_Project_MVC-main\Tourist_Project_MVC\appsettings.json'
t = open(p, encoding='utf-8').read()
cfg = json.loads(t[:t.rindex('}') + 1])['ArcGIS']
key = cfg['ApiKey']
tok = urllib.parse.quote(key, safe='')
base = 'https://services3.arcgis.com/UDCw00RKDRKPqASe/arcgis/rest/services/tourists_layer_data/FeatureServer/0'

# exact failing query (direct)
url = base + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=5&f=json&token=' + tok
d = json.loads(urllib.request.urlopen(url, timeout=60).read().decode())
print('response keys:', sorted(d.keys()))
print('features count:', len(d.get('features', [])))
print('exceededTransferLimit:', d.get('exceededTransferLimit'))
print('first feature:', d.get('features', [{}])[0])
print('error present:', 'error' in d)

# count-only through the same path
url2 = base + '/query?where=1%3D1&returnCountOnly=true&f=json&token=' + tok
d2 = json.loads(urllib.request.urlopen(url2, timeout=60).read().decode())
print('count-only keys:', sorted(d2.keys()), '| count:', d2.get('count'))
