# -*- coding: utf-8 -*-
"""A stand-in for the plugin, so the viewer can be worked on without Rhino.

Serves the routes SyncServer serves - the viewer page, /data.json, the two
dataset scripts, and the project endpoints - backed by a small synthetic model
instead of Grasshopper. Run it, open the URL it prints, and pick the one recent
project.

The model is built to exercise the cases that are awkward to stage by hand in
Rhino: a surface-only group, a solid-only group, a group holding one of each
(the mixed case the viewer has to refuse to price), and one object sent under
two colours (the double-count the viewer has to report).

    python tools/mock_plugin.py

It serves on a different port from the plugin, so both can run at once.
"""
import io, json, os
from http.server import BaseHTTPRequestHandler, HTTPServer

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VIEWER = os.path.join(ROOT, 'src', 'Sitewise', 'WebAssets', 'viewer.html')
DATA = os.path.join(ROOT, 'data')
PORT = 51999


# Both arrays are flat, matching MeshExport: vertices as x,y,z triples and
# faces as groups of four indices, a triangle marked by repeating the third.


def box(x, y, z, sx, sy, sz):
    """A closed unit box scaled and placed, so it reports a volume."""
    v = [x, y, z, x + sx, y, z, x + sx, y + sy, z, x, y + sy, z,
         x, y, z + sz, x + sx, y, z + sz, x + sx, y + sy, z + sz, x, y + sy, z + sz]
    f = [0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 5, 4, 1, 2, 6, 5, 2, 3, 7, 6, 3, 0, 4, 7]
    return {'vertices': v, 'faces': f}


def slab(x, y, sx, sy):
    """One open quad, so it reports an area and no volume."""
    return {'vertices': [x, y, 0, x + sx, y, 0, x + sx, y + sy, 0, x, y + sy, 0],
            'faces': [0, 1, 2, 3]}


def wedge(x, y, sx, sy):
    """One triangle, to exercise the d == c marker."""
    return {'vertices': [x, y, 0, x + sx, y, 0, x, y + sy, 0],
            'faces': [0, 1, 2, 2]}


def obj(i, mesh, area=0.0, volume=0.0):
    return {'id': 'mock%02d' % i,
            'quantity': {'area': area, 'count': 0, 'length': 0.0, 'volume': volume},
            'mesh': mesh}


MODEL = {
    'meta': {'units': 'm', 'source_units': 'Meters', 'unit_scale': 1.0, 'units_trusted': True,
             'origin_offset': [0, 0, 0], 'timestamp': '2026-09-14T00:00:00Z', 'mesh_tolerance': 0.01,
             'mesh_format': 2},
    'groups': [
        {'color': '#4C9F70', 'objects': [obj(1, slab(0, 0, 20, 12), area=240.0),
                                         obj(5, wedge(0, -10, 12, 8), area=48.0)]},
        {'color': '#C46A4E', 'objects': [obj(2, box(24, 0, 0, 4, 4, 3), volume=48.0, area=80.0)]},
        # the case under test: a solid and a surface sharing one colour
        {'color': '#3E6FB0', 'objects': [obj(3, box(0, 16, 0, 5, 5, 2), volume=50.0, area=90.0),
                                         obj(4, slab(8, 16, 14, 6), area=84.0)]},
        # obj(4) again, under a second colour: the double-count case
        {'color': '#8E6BB0', 'objects': [obj(4, slab(8, 16, 14, 6), area=84.0)]},
    ],
}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def send(self, ctype, body):
        raw = body.encode('utf-8')
        self.send_response(200)
        self.send_header('Content-Type', ctype)
        self.send_header('Content-Length', str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def read(self, name):
        return io.open(os.path.join(DATA, name), encoding='utf-8').read()

    def do_GET(self):
        p = self.path.split('?')[0]
        if p == '/':
            self.send('text/html; charset=utf-8', io.open(VIEWER, encoding='utf-8').read())
        elif p == '/data.json':
            self.send('application/json', json.dumps(MODEL))
        elif p == '/data/cc.js':
            self.send('application/javascript', 'const CC=' + self.read('cc_dataset.json') + ';')
        elif p == '/data/materials.js':
            self.send('application/javascript', 'const MAT=' + self.read('materials.json') + ';')
        else:
            self.send_error(404)

    def do_POST(self):
        p = self.path.split('?')[0]
        length = int(self.headers.get('Content-Length') or 0)
        if length:
            self.rfile.read(length)
        if p == '/project/recent':
            self.send('application/json', json.dumps({'recent': [
                {'name': 'Mixed group test', 'path': 'C:/mock', 'schemes': 0, 'saved': '2026-09-14T00:00:00Z'}]}))
        elif p in ('/project/open-recent', '/project/open', '/project/new'):
            self.send('application/json', json.dumps({
                'ok': True, 'name': 'Mixed group test', 'path': 'C:/mock',
                'project': {'name': 'Mixed group test', 'projectName': 'Mixed group test',
                            'disp': 'm', 'format': 1},
                'schemes': []}))
        elif p == '/project/save':
            self.send('application/json', json.dumps({'ok': True}))
        else:
            self.send_error(404)


if __name__ == '__main__':
    print('mock plugin on http://localhost:%d/  (Ctrl+C to stop)' % PORT)
    HTTPServer(('127.0.0.1', PORT), Handler).serve_forever()
