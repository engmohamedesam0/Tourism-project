"""Kills the stale proxy, starts a fresh one, waits for it, then re-runs the sync."""
import os
import socket
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.abspath(__file__))

# 1) kill any python proxy processes
import signal
try:
    out = subprocess.run(['powershell', '-NoProfile', '-Command',
                          'Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*arcgis-proxy.py*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }'],
                         capture_output=True, timeout=60)
    print('proxy processes killed:', out.returncode)
except Exception as e:
    print('kill failed:', e)

time.sleep(1)

# 2) start a fresh proxy
log = open(os.path.join(ROOT, 'proxy.log'), 'ab', buffering=0)
p = subprocess.Popen([sys.executable, os.path.join(ROOT, 'arcgis-proxy.py'), '8765'],
                     stdout=log, stderr=log,
                     creationflags=subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS,
                     close_fds=True)
ok = False
for _ in range(30):
    try:
        s = socket.create_connection(('127.0.0.1', 8765), timeout=1)
        s.close()
        ok = True
        break
    except OSError:
        time.sleep(0.5)
print('fresh proxy listening:', ok, '| pid', p.pid)

# 3) verify a query returns features NOW
import json
import urllib.parse
import urllib.request

with open(os.path.join(ROOT, 'Tourist_Project_MVC', 'appsettings.json'), encoding='utf-8') as f:
    t = f.read()
cfg = json.loads(t[:t.rindex('}') + 1])['ArcGIS']
key = cfg['ApiKey']
tok = urllib.parse.quote(key, safe='')
base = 'https://services3.arcgis.com/UDCw00RKDRKPqASe/arcgis/rest/services/tourists_layer_data/FeatureServer/0'
proxy = 'http://127.0.0.1:8765/https/' + base[len('https://'):]
d = json.loads(urllib.request.urlopen(proxy + '/query?where=1%3D1&outFields=TouristId,ObjectId&returnGeometry=false&resultRecordCount=5&f=json&token=' + tok, timeout=60).read().decode())
print('fresh proxy query features:', len(d.get('features', [])))

# 4) run the sync
r = subprocess.run([sys.executable, os.path.join(ROOT, 'sync_after_remove.py')], capture_output=True, text=True, timeout=600)
print('--- sync output ---')
print(r.stdout[-2000:])
if r.stderr:
    print('stderr:', r.stderr[-500:])
