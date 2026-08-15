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

queries = [
    ('proxy rrc=1000', proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=1000&f=json&token=' + tok),
    ('proxy rrc=5', proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=5&f=json&token=' + tok),
    ('direct rrc=5', base + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=5&f=json&token=' + tok),
]
for label, url in queries:
    try:
        r = urllib.request.urlopen(url, timeout=60)
        body = r.read().decode()
        print(label, '-> HTTP', r.status, '| len', len(body), '| head:', body[:180].replace('\n', ' '))
    except urllib.error.HTTPError as e:
        print(label, '-> HTTP', e.code, '|', e.read(200))
    except Exception as e:
        print(label, '-> FAILED', type(e).__name__, str(e)[:150])
