"""Local TLS-bridge proxy for the Tourist Project.

Why: on some Windows machines the Schannel TLS stack is broken ("No
credentials are available in the security package" / curl SSL error 35),
so .NET cannot make ANY https call. Python uses OpenSSL, which still works.
This proxy receives PLAIN HTTP requests whose path starts with /https/ or
/http/ and forwards them to the real HTTPS/HTTP endpoint using OpenSSL,
returning the response over plain HTTP. The .NET app never performs TLS
itself, so the broken Schannel no longer blocks ArcGIS syncs.

Usage:  python arcgis-proxy.py [port]     (default 8765)
Example request the app sends:
    GET http://127.0.0.1:8765/https/services3.arcgis.com/.../FeatureServer/0/query?f=json
"""
import http.client
import socket
import socketserver
import ssl
import sys
import threading
import urllib.parse

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8765
LISTEN = '127.0.0.1'
BUFSIZE = 65536
ssl_ctx = ssl.create_default_context()  # OpenSSL -> works even when Schannel is broken


def pipe(src, dst):
    try:
        while True:
            data = src.recv(BUFSIZE)
            if not data:
                break
            dst.sendall(data)
    except OSError:
        pass
    finally:
        for s in (src, dst):
            try:
                s.close()
            except OSError:
                pass


class ProxyHandler(socketserver.StreamRequestHandler):
    def handle(self):
        try:
            request_line = self.rfile.readline()
            if not request_line:
                return
            request_line = request_line.decode('latin-1').strip()
            parts = request_line.split(' ', 2)
            if len(parts) < 3:
                return
            method, target = parts[0], parts[1]

            headers = {}
            while True:
                line = self.rfile.readline()
                if line in (b'\r\n', b'\n', b''):
                    break
                k, _, v = line.decode('latin-1').partition(':')
                headers[k.strip().lower()] = v.strip()

            body = b''
            length = int(headers.get('content-length') or 0)
            if length > 0:
                body = self.rfile.read(length)

            if method.upper() == 'CONNECT':
                host, _, port = target.partition(':')
                port = int(port or 443)
                print(f'[proxy] CONNECT {host}:{port}')
                upstream = socket.create_connection((host, port), timeout=30)
                try:
                    upstream = ssl_ctx.wrap_socket(upstream, server_hostname=host)
                except Exception as e:
                    print(f'[proxy] TLS to {host} failed: {e}')
                    upstream.close()
                    return
                self.request.sendall(b'HTTP/1.1 200 Connection Established\r\n\r\n')
                t1 = threading.Thread(target=pipe, args=(self.request, upstream), daemon=True)
                t2 = threading.Thread(target=pipe, args=(upstream, self.request), daemon=True)
                t1.start(); t2.start()
                t1.join(); t2.join()
                return

            # Plain HTTP absolute-URI -> rebuild the upstream URL from the path
            parsed = urllib.parse.urlsplit(target)
            path = parsed.path
            if path.startswith('/https/'):
                upstream = 'https://' + path[len('/https/'):]
            elif path.startswith('/http/'):
                upstream = 'http://' + path[len('/http/'):]
            else:
                print(f'[proxy] unsupported target: {target}')
                self.request.sendall(b'HTTP/1.1 400 Bad Request\r\n\r\n')
                return
            upstream_url = upstream + ('?' + parsed.query if parsed.query else '')
            up = urllib.parse.urlsplit(upstream_url)
            host = up.hostname
            port = up.port or (443 if up.scheme == 'https' else 80)
            # redact the API-key token so it never lands in proxy.log
            log_url = upstream_url
            if 'token=' in log_url:
                log_url = log_url.split('token=')[0] + 'token=***REDACTED***'
            print(f'[proxy] {method} {log_url}', flush=True)

            forward_headers = {k: v for k, v in headers.items()
                               if k not in ('host', 'accept-encoding', 'connection',
                                            'proxy-connection', 'content-length')}
            if body:
                forward_headers['content-length'] = str(len(body))
            forward_headers['host'] = host

            if up.scheme == 'https':
                conn = http.client.HTTPSConnection(host, port, timeout=90, context=ssl_ctx)
            else:
                conn = http.client.HTTPConnection(host, port, timeout=90)
            conn.request(method, up.path + ('?' + up.query if up.query else ''),
                         body=body if body else None, headers=forward_headers)
            resp = conn.getresponse()
            data = resp.read()
            out = f'HTTP/1.1 {resp.status} {resp.reason}\r\n'.encode('latin-1')
            for k, v in resp.getheaders():
                if k.lower() not in ('transfer-encoding', 'connection', 'content-length'):
                    out += f'{k}: {v}\r\n'.encode('latin-1')
            out += f'Content-Length: {len(data)}\r\n\r\n'.encode('latin-1') + data
            try:
                self.request.sendall(out)
            except OSError as e:
                print(f'[proxy] response send failed: {e}', flush=True)
            conn.close()
        except Exception as e:
            print(f'[proxy] error: {type(e).__name__}: {e}', flush=True)
            try:
                self.request.sendall(b'HTTP/1.1 502 Bad Gateway\r\n\r\n')
            except OSError:
                pass
        finally:
            try:
                self.request.close()
            except OSError:
                pass


class ThreadingProxyServer(socketserver.ThreadingMixIn, socketserver.TCPServer):
    allow_reuse_address = True
    daemon_threads = True


if __name__ == '__main__':
    server = ThreadingProxyServer((LISTEN, PORT), ProxyHandler)
    print(f'[proxy] listening on {LISTEN}:{PORT} (OpenSSL bridge)', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print('[proxy] stopped', flush=True)
