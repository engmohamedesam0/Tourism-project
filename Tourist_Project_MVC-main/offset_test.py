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

urls = {
    'proxy no offset': proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=5&f=json&token=' + tok,
    'proxy offset=0': proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset=0&resultRecordCount=5&f=json&token=' + tok,
    'proxy offset=0 (2nd attempt)': proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultOffset=0&resultRecordCount=5&f=json&token=' + tok,
}
for label, url in urls.items():
    try:
        d = json.loads(urllib.request.urlopen(url, timeout=60).read().decode())
        print(label, '-> features:', len(d.get('features', [])), '| keys:', sorted(d.keys())[:6])
    except Exception as e:
        print(label, '-> FAILED', type(e).__name__, str(e)[:120])
