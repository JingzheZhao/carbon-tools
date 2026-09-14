# -*- coding: utf-8 -*-
"""Structural checks for viewer.html. Run after any edit to the file.

The page is a single document with one global class pool, so a short class name
added by a new component can silently inherit rules from an unrelated one. That
is not a visible error - it shows up as a few pixels of drift. These checks make
it loud instead.
"""
import io, re, sys, subprocess, os

P = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                 'src', 'Sitewise', 'WebAssets', 'viewer.html')
s = io.open(P, encoding='utf-8').read()
fail = []

# ---- 1. class collisions -------------------------------------------------
css = s[s.index('<style>'):s.index('</style>')]
css = re.sub(r'/\*.*?\*/', '', css, flags=re.S)

# a class is "owned" by the prefix before its first dash; unprefixed classes
# have no owner and are the ones that collide
defined = {}
for rule in re.finditer(r'([^{}]+)\{', css):
    sel = rule.group(1).strip()
    if sel.startswith('@') or sel.startswith(':root') or sel.startswith('['):
        continue
    for cls in re.findall(r'\.([A-Za-z][\w-]*)', sel):
        defined.setdefault(cls, []).append(sel.split('\n')[0][:70])

# bare, single-token rules (".foo{...}" with no ancestor) are global reach
bare = set()
for rule in re.finditer(r'(?:^|\})\s*(\.[\w-]+)\s*\{', css):
    bare.add(rule.group(1)[1:])

# every class actually put on an element, and where
used = {}
for m in re.finditer(r'class="([^"]+)"', s):
    for cls in m.group(1).split():
        if '+' in cls or "'" in cls:
            continue
        used.setdefault(cls, 0)
        used[cls] += 1

# deliberate shared utilities: reused on purpose, styled in exactly one place
SHARED = {'empty', 'h2', 'kpi', 'kpis', 'meta', 'pv', 'sub2',
          # the report overlay deliberately reuses the comparison's frame
          'cmp', 'cmpH', 'cmpX'}

risky = sorted(c for c in bare
               if used.get(c, 0) > 1 and '-' not in c and len(c) <= 5
               and c not in SHARED)
if risky:
    for c in risky:
        fail.append('.%s is unprefixed and lands on %d elements (rules: %s) - '
                    'prefix it per component, e.g. .dm-bar, or add it to SHARED '
                    'if the reuse is deliberate' % (c, used[c], defined[c]))

orphan = sorted(c for c in used if c not in defined and '-' in c)
if orphan:
    print('  ! class on an element but never styled: ' + ', '.join(orphan))

# ---- 2. markup balance ---------------------------------------------------
body = s[s.index('</style>'):s.index('<script>')]
if body.count('<div') != body.count('</div>'):
    fail.append('div imbalance: %d open, %d close' % (body.count('<div'), body.count('</div>')))

# ---- 3. hidden elements must not be forced visible by a display rule ------
for m in re.finditer(r'id="(\w+)"[^>]*\shidden', s):
    eid = m.group(1)
    cls = re.search(r'class="([^"]+)"[^>]*id="%s"' % eid, s)
    if not cls:
        continue
    for c in cls.group(1).split():
        r = re.search(r'\.%s\{([^}]*)\}' % re.escape(c), css)
        if r and 'display:' in r.group(1) and ('.%s[hidden]' % c) not in css:
            fail.append('#%s can be hidden but .%s sets display: with no [hidden] guard' % (eid, c))

# ---- 4. the script must parse --------------------------------------------
js = s.split('<script>')[-1].split('</script>')[0]
tmp = os.path.join(os.path.dirname(P), '_check.js')
io.open(tmp, 'w', encoding='utf-8').write(js)
r = subprocess.run(['node', '--check', tmp], capture_output=True, text=True)
os.remove(tmp)
if r.returncode:
    fail.append('JS syntax: ' + (r.stderr.strip().split('\n')[0] if r.stderr else '?'))

for f in fail:
    print('  FAIL: ' + f)
print('  ' + ('FAILED' if fail else 'all checks passed'))
sys.exit(1 if fail else 0)
