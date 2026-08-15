import json
import urllib.parse
import urllib.request

t = open(r'E:\ITI\Graduation_Project\Tourist_Project_MVC-main\Tourist_Project_MVC\appsettings.json', encoding='utf-8').read()
cfg = json.loads(t[:t.rindex('}') + 1])['ArcGIS']
key = cfg['ApiKey']
tok = urllib.parse.quote(key, safe='')

for name, url in [('tourists', cfg['TouristsTableUrl']), ('nationality', cfg['TouristNationalityLayerUrl']),
                  ('branches', cfg['BranchesLayerUrl']), ('destinations', cfg['DestinationsLayerUrl'])]:
    d = json.loads(urllib.request.urlopen(
        url.rstrip('/') + '/0/query?where=1%3D1&returnCountOnly=true&f=json&token=' + tok, timeout=30).read().decode())
    print(name, ':', d.get('count'))
