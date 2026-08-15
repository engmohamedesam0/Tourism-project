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

# EXACT url the sync script builds (rrc=1000, resultOffset=0)
url = proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset=0&resultRecordCount=1000&f=json&token=' + tok
print('url length:', len(url))
d = json.loads(urllib.request.urlopen(url, timeout=60).read().decode())
print('keys:', sorted(d.keys()))
print('has features:', 'features' in d, '| count:', len(d.get('features', [])))
print('name field:', d.get('name'))

# same but resultRecordCount=5
url2 = proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset=0&resultRecordCount=5&f=json&token=' + tok
d2 = json.loads(urllib.request.urlopen(url2, timeout=60).read().decode())
print('rrc=5: has features:', 'features' in d2, '| count:', len(d2.get('features', [])), '| name:', d2.get('name'))

# same but no token in the query (token only) - check where=1=1 vs where=1%3D1
url3 = proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=5&f=json&token=' + tok
d3 = json.loads(urllib.request.urlopen(url3, timeout=60).read().decode())
print('rrc=5 no offset: has features:', 'features' in d3, '| count:', len(d3.get('features', [])), '| name:', d3.get('name'))
