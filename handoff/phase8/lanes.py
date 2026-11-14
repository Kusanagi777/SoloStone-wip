#!/usr/bin/env python
"""Parallel lane mirrors for phase 8.  Run as:  python -I lanes.py <command>

  sync              main -> LB (verbatim base) and L1..Ln (lane copies; scratch files path-rewritten).
                    REFUSES (exit 6, nothing changed) while a lane holds work that is not in main: a file
                    that `merge` would still apply or that is still in conflict.  Merge it, settle it
                    (`resolved`), or run `sync --discard-unmerged` to drop it (the lane versions are kept
                    under L<k>\\stale\\<stamp>\\).
  status            what changed in main since the last sync (should be empty until the first merge)
  diff <k>          what lane k changed against the base (dry run of merge)
  merge <k> --apply 3-way merge lane k into main; conflicts are never written to main
                    (--atomic: write nothing at all unless the whole lane merges without a conflict)
  resolved <k> <repo|sp> <path>   record that the conflict the last `merge <k> --apply` reported for this
                    path was settled by hand in main.  The lane version that conflicted counts as merged;
                    lane edits made after it are merged on top by the next merge.  For a file the lane
                    deleted: main keeps it, stop reporting.  A path without a reported conflict is refused.
  rewrite <k> <file> / unrewrite <k> <file>   print a file with lane paths applied / removed

Exit codes: 0 clean, 1 conflicts or lane deletions/renames that need a hand (nothing conflicting was
written), 2 usage, 3 some writes to main failed (rerun the merge), 4 another sync/merge holds the lock,
5 the last sync did not finish, 6 sync refused because of unmerged lane work, 70 crash.

A lane copy whose editor flipped the BOM, the line ends or the encoding (UTF-8 saved as UTF-16 or ANSI)
of a file is first given back the convention of the base version (the item carries a note); the merge is
then made on real content only.
A CONFLICT is: overlapping edits, a binary or new file changed on both sides, a file another lane deleted
or renamed, a damaged base snapshot, or a result that fails a check (invalid JSON, a duplicate key or id
the two sides did not have, conflict markers, a BOM, line-end or encoding change that survived, a lane
path or a ..\\repo relative path left in the text, a write into L<n>/LB).  The lane version (.lane), and
where they exist the diff3 text (.diff3), the union candidate (.union) and the rejected clean merge
(.merged), are kept under conflicts\\<stamp>-L<k>\\.  Merging the same lane again is a no-op for files
already merged; if the lane edits such a file further (or reverts it), the 3-way base is the lane version
merged before, so only the new edits are merged.
This tool never deletes from main.  A repo file the lane deleted is reported `lane-deleted (not applied)`
and the merge exits 1 until main deletes it by hand or `resolved` says main keeps it; a deletion that
pairs with a new file is reported as RENAMED.  Scratch deletions are reported only.

Layout (SP = the session scratchpad):
  SP\\LB\\repo, SP\\LB\\sp      base snapshot taken by the last sync (never edited by anyone)
  SP\\L<k>\\repo                lane copy of C:\\SoloStone (tracked + untracked-not-ignored files + references)
  SP\\L<k>\\sp                  lane copy of the mirrored scratchpad folders (paths inside text files rewritten)
Mirrored scratch = lanes.json spInclude plus every scratch folder or file a merge created in main (kept in
state\\sp-extra.json), so that a family's new p8f<nn> folder reaches every lane at the next sync.
Nothing is ever deleted: a file that left the source, and anything in a lane's sp that is not mirrored
(except obj/bin/out build folders inside a mirrored folder), is moved to <target>\\stale\\<stamp>\\, and a
lane file that sync replaces is first copied to <lane>\\stale\\<stamp>\\overwritten\\.  Main is only written by
`merge <k> --apply`.  After settling conflicts by hand, run `merge <k> --apply` again: what merges now is
recorded; what still says CONFLICT needs `resolved`, or the next sync refuses.

Sandbox testing: set LANES_MAIN and LANES_SP (both, or the tool refuses) to throwaway roots; reports,
conflict files and merge state then go to LANES_OUT (default LANES_SP\\lanes-out), never next to this file.
Path rewriting always uses the real root names.  LANES_INDEX_MARGIN (seconds, default 2): sync's check for
unmerged work trusts the size+mtime it recorded for a lane file (LB\\lane-index.json) unless the file was
written in the last <margin> seconds of that sync; such files, and any file whose size or time differs, are read.
"""
import sys, os, re, json, hashlib, shutil, subprocess, time, fnmatch, glob, difflib

REAL_MAIN = r'C:\SoloStone'
REAL_SP = r'C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad'
HERE = os.path.dirname(os.path.abspath(__file__))
_em, _es = os.environ.get('LANES_MAIN'), os.environ.get('LANES_SP')
if bool(_em) != bool(_es):
    # one without the other would merge a sandbox lane into the real main, or the reverse
    raise SystemExit('LANES_MAIN and LANES_SP must be set together (sandbox) or not at all')
SANDBOX = bool(_em)
MAIN = os.path.abspath(_em) if SANDBOX else REAL_MAIN
SP = os.path.abspath(_es) if SANDBOX else REAL_SP
if SANDBOX and (os.path.normcase(MAIN) == os.path.normcase(REAL_MAIN) or os.path.normcase(SP) == os.path.normcase(REAL_SP)):
    raise SystemExit('LANES_MAIN / LANES_SP point at the real roots; unset them instead')
WORK = os.path.abspath(os.environ.get('LANES_OUT') or (os.path.join(SP, 'lanes-out') if SANDBOX else HERE))
# LANES_CFG (sandbox only) swaps the config, so that the tests can exercise other settings
CFG = json.load(open((SANDBOX and os.environ.get('LANES_CFG')) or os.path.join(HERE, 'lanes.json'), encoding='utf-8'))
OUT = sys.stdout.buffer
FOLD = os.path.normcase('A') == 'a'      # a file system that does not tell Foo.cs from foo.cs


def say(s=''):
    OUT.write((s + '\n').encode('utf-8'))
    OUT.flush()


def fold(s):
    return s.lower() if FOLD else s


# ---------------------------------------------------------------- path rewriting
SP_PARTS = ['Users', 'user', 'AppData', 'Local', 'Temp', 'claude', '0a564ffc8743aeec',
            'd2a4e245-ca37-4433-9b55-c35ed325f081', 'scratchpad']
# one path separator: \\\\ (a C#/JSON string inside a Python/JSON string), \\ (C#/JSON string), \ or /
SEP = rb'(?:\\\\\\\\|\\\\|\\|/)'
# a root name ends here: not SoloStoneX, SoloStone-old, scratchpad2, SoloStone.sln (a sentence's full stop is fine)
END = rb'(?![A-Za-z0-9_\-]|\.[A-Za-z0-9_])'
BEG = rb'(?<![A-Za-z0-9_])'              # not XC:\SoloStone
PBEG = rb'(?<![A-Za-z0-9_.\-~])'         # not src/c/SoloStone
PPRE = rb'(?:/mnt|/cygdrive)?'           # WSL and Cygwin spell the drive /mnt/c and /cygdrive/c
_sp_tail = b''.join(SEP + re.escape(p.encode()) for p in SP_PARTS[:-1])
_sp_posix = b'/'.join(re.escape(p.encode()) for p in SP_PARTS)
# Git Bash spells %TEMP% as /tmp, so `pwd` in a lane prints /tmp/claude/<id>/<session>/scratchpad/L<k>/...
_sp_tmp = b'/tmp/' + b'/'.join(re.escape(p.encode()) for p in SP_PARTS[5:])
# <scratch>\p8lanes (this tool, the harness lock wrapper) is shared by all lanes on purpose: never a lane path
NOTOOL = rb'(?!' + SEP + re.escape(os.path.basename(HERE).encode()) + END + rb')'
# windows style: drive, then every separator may be any SEP; the last separator is captured as the style
FWD = re.compile(
    rb'(?P<wsp>' + BEG + rb'(?P<d1>[Cc]):' + _sp_tail + rb'(?P<s1>' + SEP + rb')scratchpad' + END + NOTOOL + rb')'
    rb'|(?P<psp>' + PBEG + PPRE + rb'/(?P<d2>[Cc])/' + _sp_posix + END + NOTOOL + rb')'
    rb'|(?P<tsp>' + PBEG + _sp_tmp + END + NOTOOL + rb')'
    rb'|(?P<wrepo>' + BEG + rb'(?P<d3>[Cc]):(?P<s3>' + SEP + rb')SoloStone' + END + rb')'
    rb'|(?P<prepo>' + PBEG + rb'(?P<pp>' + PPRE + rb')/(?P<d4>[Cc])/SoloStone' + END + rb')')
# A lane mention that survived unrewrite (another lane's, a bare lane root, another letter case, a mixed
# spelling, a relative one) must never be merged silently.  Two shapes: <anything>scratchpad\L<n> whatever
# follows, and L<n>\repo or L<n>\sp whatever precedes.
ABSLEAK = re.compile(rb'(?i)scratchpad' + SEP + rb'(?:L\d+|LB)(?![A-Za-z0-9_])')
LEAK = re.compile(rb'(?i)(?<![A-Za-z0-9_.\-])(?:L\d+|LB)' + SEP + rb'(?:repo|sp)' + END)
# a relative path that climbs out of one half of a lane into the other (..\..\repo\x, ../sp, "..\repo")
CROSS = re.compile(rb'(?:\.\.' + SEP + rb')+(?:repo|sp)' + END)
# a root spelled in a way FWD does not rewrite: other case, other drive, URL-encoded, 3+ slashes, the
# session id on its own, a rooted path without a drive (\SoloStone\x), a root assembled from quoted parts
SUSP = re.compile(rb'(?i)(?:[a-z](?::|%3a)(?:\\|/|%5c|%2f)+|/[a-z]/)solostone' + END
                  + rb'|0a564ffc8743aeec'
                  + rb'|(?<![a-z0-9_.:\-/\\~%}])[\\/]+solostone[\\/]'
                  + rb'|[a-z]:[\\/]*["\'][^\r\n]{0,60}?["\']solostone["\']')


def _sp(drive, sep, posix, pre=b''):
    if posix:
        return pre + b'/' + drive + b'/' + b'/'.join(p.encode() for p in SP_PARTS)
    return drive + b':' + b''.join(sep + p.encode() for p in SP_PARTS)


def rewrite(data, k):
    if b'SoloStone' not in data and b'scratchpad' not in data:
        return data
    lane = ('L%s' % k).encode()

    def sub(m):
        if m.group('wsp'):
            return m.group('wsp') + m.group('s1') + lane + m.group('s1') + b'sp'
        if m.group('psp'):
            return m.group('psp') + b'/' + lane + b'/sp'
        if m.group('tsp'):
            return m.group('tsp') + b'/' + lane + b'/sp'
        if m.group('wrepo'):
            s = m.group('s3')
            return _sp(m.group('d3'), s, False) + s + lane + s + b'repo'
        return _sp(m.group('d4'), b'/', True, m.group('pp')) + b'/' + lane + b'/repo'
    return FWD.sub(sub, data)


def unrewrite(data, k):
    if b'scratchpad' not in data:
        return data
    lane = re.escape(('L%s' % k).encode())
    # the separators around L<k> may differ from each other (a lane path glued together by a script)
    rev = re.compile(
        rb'(?P<w>' + BEG + rb'(?P<d1>[Cc]):' + _sp_tail + SEP + rb'scratchpad)(?P<s1>' + SEP + rb')' + lane + SEP + rb'(?P<k1>repo|sp)' + END
        + rb'|(?P<p>' + PBEG + rb'(?P<pp>' + PPRE + rb')/(?P<d2>[Cc])/' + _sp_posix + rb')/' + lane + rb'/(?P<k2>repo|sp)' + END
        + rb'|(?P<t>' + PBEG + _sp_tmp + rb')/' + lane + rb'/(?P<k3>repo|sp)' + END)

    def sub(m):
        if m.group('w'):
            if m.group('k1') == b'sp':
                return m.group('w')
            return m.group('d1') + b':' + m.group('s1') + b'SoloStone'
        if m.group('p'):
            if m.group('k2') == b'sp':
                return m.group('p')
            return m.group('pp') + b'/' + m.group('d2') + b'/SoloStone'
        if m.group('k3') == b'sp':
            return m.group('t')
        return b'/c/SoloStone'      # /tmp is %TEMP%; the repo has no /tmp spelling
    return rev.sub(sub, data)


def root_candidate(data):
    """Cheap and never wrong the other way: False means neither FWD nor SUSP can match.  Every pattern needs
    the session id, or `solostone` right after a separator, a quote or a URL escape (%5c, %2f); the name
    alone (a namespace, SoloStone.Plugin.dll) is in nearly every file and is no path."""
    low = data.lower()
    if b'0a564ffc8743aeec' in low:
        return True
    i = low.find(b'solostone')
    while i >= 0:
        if i and low[i - 1] in b'\\/"\'cf':
            return True
        i = low.find(b'solostone', i + 1)
    return False


def unhandled_spellings(data):
    """Root mentions that rewrite() leaves alone (so in a lane they still point at main)."""
    if not root_candidate(data):
        return []
    cov = [m.span() for m in FWD.finditer(data)]
    res, i = [], 0
    for m in SUSP.finditer(data):
        s = m.start()
        while i < len(cov) and cov[i][1] <= s:
            i += 1
        if i < len(cov) and cov[i][0] <= s:
            continue
        res.append(data[max(0, s - 30):m.end() + 20].decode('utf-8', 'replace').replace('\n', ' ').replace('\r', ' '))
    return res


CTRL = re.compile(rb'[\x00-\x08\x0e-\x1a\x1c-\x1f]')


def is_text(data):
    """Valid UTF-8 text: what git merge-file may be given."""
    if b'\0' in data[:8192]:
        return False
    try:
        data.decode('utf-8')
        return True
    except UnicodeDecodeError:
        return False


def rewritable(data):
    """Text as far as the path patterns care: no NUL near the start.  The patterns are ASCII, so a file in
    CP1252 or an OEM code page (console output, a PowerShell 5.1 Set-Content) is rewritten like UTF-8."""
    return b'\0' not in data[:8192]


def texty(data):
    """Text as far as BOM and line-end repair care: valid UTF-8, or other bytes without any control
    character.  A small binary fixture often has no NUL, and its 0D 0A pairs are not line ends."""
    return is_text(data) or (rewritable(data) and CTRL.search(data) is None)


def views(data):
    """The bytes, and for a file that holds NULs (UTF-16, a NUL-separated list) the bytes without them,
    so that ASCII patterns also see UTF-16 text of either byte order."""
    yield data, ''
    if b'\0' in data:
        yield data.replace(b'\0', b''), ' (UTF-16 or NUL-separated text, which is never rewritten)'


# ---------------------------------------------------------------- file sets
def read(p):
    try:
        with open(p, 'rb') as f:
            return f.read()
    except (FileNotFoundError, NotADirectoryError):
        return None


WARM = 256


def warm(paths):
    """Open and read each file once, eight at a time, and drop the bytes.  It changes nothing: every caller
    still reads its files itself afterwards.  Why: on this machine the first open of a file costs 10-20 ms
    (the on-access virus scanner) and a repeat soon after costs 0.05 ms, so a loop that reads fifty thousand
    files one by one takes ten minutes (measured on the real tree: sync, 584 of 660 seconds inside open),
    longer than a foreground shell call may run, while the same opens made eight at a time take about a
    tenth.  Callers warm one batch of WARM files right before they read it: the scanner forgets a file
    again after some thousands of other opens, so warming everything up front would not last."""
    from concurrent.futures import ThreadPoolExecutor

    def touch(p):
        try:
            with open(p, 'rb') as f:
                f.read()
        except OSError:
            pass
    with ThreadPoolExecutor(max_workers=8) as ex:
        for _ in ex.map(touch, paths):
            pass


def write(p, data):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, 'wb') as f:
        f.write(data)


def write_atomic(p, data):
    """Whole file or nothing: a crash or a locked target never leaves a half-written file behind."""
    os.makedirs(os.path.dirname(p), exist_ok=True)
    tmp = p + '.lanes-tmp'
    try:
        with open(tmp, 'wb') as f:
            f.write(data)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp, p)
    except BaseException:
        try:
            os.remove(tmp)
        except OSError:
            pass
        raise


def h(data):
    return hashlib.sha256(data).hexdigest()


def git(args, cwd=MAIN, inp=None):
    r = subprocess.run(['git'] + args, cwd=cwd, input=inp, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    return r.returncode, r.stdout, r.stderr


def main_repo_files():
    rc, out, err = git(['ls-files', '-co', '--exclude-standard', '-z'])
    if rc != 0:
        raise SystemExit('git ls-files failed: ' + err.decode('utf-8', 'replace'))
    files = set(x.decode('utf-8') for x in out.split(b'\0') if x)
    files = set(f for f in files if os.path.isfile(os.path.join(MAIN, f)) and not f.endswith('.lanes-tmp'))
    for d in CFG['repoExtraDirs']:
        files |= set(walk(os.path.join(MAIN, d), CFG['repoSkipDirNames'], prefix=d))
    return files


def walk(root, skip_dirs, prefix=''):
    res = []
    if not os.path.isdir(root):
        return res
    for dp, dn, fn in os.walk(root):
        dn[:] = [d for d in dn if d not in skip_dirs]
        rel = os.path.relpath(dp, root).replace('\\', '/')
        for f in fn:
            if f.endswith('.lanes-tmp'):     # the leftover of a write_atomic that was killed
                continue
            r = f if rel == '.' else rel + '/' + f
            res.append((prefix + '/' + r) if prefix else r)
    return res


def source_skipdirs(files, skip):
    """Folders with a build-output name (bin, out, ...) that hold mirrored files: they are source folders."""
    keep = set()
    for f in files:
        parts = f.split('/')
        for i, p in enumerate(parts[:-1]):
            if p in skip:
                keep.add(fold('/'.join(parts[:i + 1])))
    return keep


def walk_lane(root, skip, keep, hidden=()):
    """Every file of a lane tree, and what was left out by folder name:
    (files, {folder: [count, samples, a project file sits beside it]}).
    A folder named in `skip` is not entered unless it is in `keep`; its files are counted, not dropped unseen."""
    files, skipped = [], {}
    if not os.path.isdir(root):
        return files, skipped
    for dp, dn, fn in os.walk(root):
        rel = os.path.relpath(dp, root).replace('\\', '/')
        go = []
        for d in dn:
            r = d if rel == '.' else rel + '/' + d
            if d in hidden:
                continue
            if d in skip and fold(r) not in keep:
                n, sample = 0, []
                for dp2, _, fn2 in os.walk(os.path.join(dp, d)):
                    for f in fn2:
                        n += 1
                        if len(sample) < 3:
                            sample.append(os.path.relpath(os.path.join(dp2, f), os.path.join(dp, d)).replace('\\', '/'))
                if n:
                    skipped[r] = [n, sample, d == '__pycache__' or any(f.lower().endswith(PROJ) for f in fn)]
                continue
            go.append(d)
        dn[:] = go
        for f in fn:
            if not f.endswith('.lanes-tmp'):
                files.append(f if rel == '.' else rel + '/' + f)
    return files, skipped


PROJ = ('.csproj', '.fsproj', '.vbproj', '.sln', '.slnx')
_GITDIR = []


def ignored(paths, worktree=None):
    """Which of `paths` git ignores.  worktree None: by main's current rules.  Otherwise by the .gitignore
    files of that copy of the repo (the base snapshot, a lane), so that a rule merged into main from one
    lane does not change what another lane's merge takes."""
    if not paths:
        return set()
    pre, cwd = [], MAIN
    if worktree:
        if not _GITDIR:
            rc, out, err = git(['rev-parse', '--absolute-git-dir'])
            if rc != 0:
                raise SystemExit('git rev-parse failed: ' + err.decode('utf-8', 'replace'))
            _GITDIR.append(out.decode('utf-8').strip())
        pre, cwd = ['--git-dir=' + _GITDIR[0], '--work-tree=' + worktree], worktree
    rc, out, err = git(pre + ['check-ignore', '--no-index', '-z', '--stdin'], cwd=cwd,
                       inp=b'\0'.join(c.encode('utf-8') for c in paths))
    if rc not in (0, 1):
        raise SystemExit('git check-ignore failed: ' + err.decode('utf-8', 'replace'))
    return set(x.decode('utf-8') for x in out.split(b'\0') if x)


def copy_repo_files(root):
    """Files in a repo copy that count as source: not in a skipped dir and not git-ignored (extra dirs excepted)."""
    files = [f for f in walk(root, CFG['repoSkipDirNames']) if not f.endswith('.lanes-tmp')]
    extra = tuple(d + '/' for d in CFG['repoExtraDirs'])
    ign = ignored([f for f in files if not f.startswith(extra)])
    return set(f for f in files if f not in ign)


def covered(rel, includes):
    return any(rel == d or rel.startswith(d + '/') for d in includes)


def sp_extra(man=None):
    """Scratch paths mirrored on top of spInclude: where earlier merges put lane files into main."""
    res = set((man or {}).get('spExtra', []))
    data = read(os.path.join(WORK, 'state', 'sp-extra.json'))
    if data is not None:
        res |= set(json.loads(data.decode('utf-8')).get('paths', []))
    return sorted(res)


def sp_includes(man=None):
    return list(CFG['spInclude']) + [e for e in sp_extra(man) if not covered(e, CFG['spInclude'])]


def sp_files(root, includes):
    res = set()
    for d in includes:               # a folder, a nested folder (p6f12/engine-out) or a single file
        if os.path.isfile(os.path.join(root, d)):
            res.add(d)
        res |= set(walk(os.path.join(root, d), CFG['spSkipDirNames'], prefix=d))
    return res


def lane_root(k):
    return os.path.join(SP, 'L%s' % k)


BASE = os.path.join(SP, 'LB')
MARKER = os.path.join(BASE, 'sync-in-progress')
RESERVED = re.compile(r'(?i)^(L\d+|LB)$')


def unique_stamp(taken):
    stamp = base = time.strftime('%Y%m%d-%H%M%S')
    n = 1
    while taken(stamp):
        n += 1
        stamp = '%s-%d' % (base, n)
    return stamp


class Lock(object):
    """One sync or applying merge at a time: two at once would race on main and on the base."""

    def __init__(self, what):
        self.path = os.path.join(WORK, 'lanes.lock')
        self.what = what

    def __enter__(self):
        os.makedirs(WORK, exist_ok=True)
        deadline = time.time() + float(os.environ.get('LANES_LOCK_WAIT', '300'))
        while True:
            try:
                fd = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
                os.write(fd, ('%s pid %d since %s\n' % (self.what, os.getpid(), time.strftime('%Y-%m-%d %H:%M:%S'))).encode())
                os.close(fd)
                return self
            except FileExistsError:
                if time.time() >= deadline:
                    say('LOCKED: %s (%s). Nothing was written. If no lanes.py is running, remove that file.'
                        % (self.path, (read(self.path) or b'').decode('utf-8', 'replace').strip()))
                    raise SystemExit(4)
                time.sleep(0.5)

    def __exit__(self, *a):
        try:
            os.remove(self.path)
        except OSError:
            pass


def load_manifest():
    if os.path.exists(MARKER):
        say('The last sync did not finish (%s exists): LB and the lanes are inconsistent. Run sync again.' % MARKER)
        raise SystemExit(5)
    data = read(os.path.join(BASE, 'manifest.json'))
    if data is None:
        raise SystemExit('no base snapshot: run sync first')
    return json.loads(data.decode('utf-8'))


PENDING = ('take-lane', 'new-file', 'merged-clean', 'CONFLICT')
INDEX = os.path.join(BASE, 'lane-index.json')


def lane_untouched(k, man):
    """Mirrored files of lane k that are still what the last sync wrote: {(kind, path)}.
    Like git's index: a file whose size and modification time are what sync recorded is not opened; any other
    file is read and compared with the manifest (so a lane synced before the index existed costs one read
    per file, not a wrong answer)."""
    idx, racy = {}, 0
    data = read(INDEX)
    if data is not None:
        data = json.loads(data.decode('utf-8'))
        if data.get('stamp') == man['stamp']:
            idx = data['lanes'].get(str(k), {})
            # a file written in the last moments of that sync could have been changed again within the
            # same clock tick: such entries are not trusted (git calls them racy)
            racy = data['written_ns'] - int(float(os.environ.get('LANES_INDEX_MARGIN', '2')) * 1e9)
    res, todo = set(), []
    for kind in ('repo', 'sp'):
        root = os.path.join(lane_root(k), kind)
        rec = idx.get(kind, {})
        for rel, sha in man[kind].items():
            p = os.path.join(root, rel)
            try:
                st = os.stat(p)
            except OSError:
                continue
            if rec.get(rel) == [st.st_mtime_ns, st.st_size] and st.st_mtime_ns < racy:
                res.add((kind, rel))
            else:
                todo.append((kind, rel, p, sha))

    def same(t):
        raw = read(t[2])
        return raw is not None and h(unrewrite(raw, k) if (t[0] == 'sp' and rewritable(raw)) else raw) == t[3]
    if todo:
        # opening ten thousand files is what takes the time (minutes with a virus scanner): do it eight at a time
        from concurrent.futures import ThreadPoolExecutor
        with ThreadPoolExecutor(max_workers=8) as ex:
            for t, ok in zip(todo, ex.map(same, todo)):
                if ok:
                    res.add((t[0], t[1]))
    return res


# ---------------------------------------------------------------- sync
def sync(discard=False):
    with Lock('sync'):
        return _sync(discard)


def _sync(discard):
    lanes = [str(k) for k in range(1, CFG['lanes'] + 1)]
    roots = [('B', BASE)] + [(k, lane_root(k)) for k in lanes]
    old_raw = read(os.path.join(BASE, 'manifest.json'))
    old_man = json.loads(old_raw.decode('utf-8')) if old_raw is not None else None
    # never the stamp of the base before: the merge state of a round is filed under its stamp
    stamp = unique_stamp(lambda s: any(os.path.exists(os.path.join(r, 'stale', s)) for _, r in roots)
                         or (old_man is not None and old_man.get('stamp') == s)
                         or any(os.path.exists(os.path.join(WORK, 'state', 'L%s' % k, s)) for k in lanes))

    # 1. Lane work that is not in main would be lost by the reset below: find it before touching anything.
    unmerged = dict((k, {}) for k in lanes)
    if old_man is not None and not os.path.exists(MARKER):
        for k in lanes:
            if os.path.isdir(lane_root(k)):
                rep = _merge(k, False, False, presync=True, untouched=lane_untouched(k, old_man))
                for it in rep['items']:
                    if it['action'].split(' ')[0] in PENDING and not it.get('lane_untouched'):
                        unmerged[k][(it['kind'], fold(it['path']))] = '%s %s   (%s%s)' % (
                            it['kind'], it['path'], it['action'], (': ' + it['why']) if it.get('why') else '')
                unmerged[k + ' report'] = rep['report']
        if any(unmerged[k] for k in lanes):
            say('%s: lane work that is NOT in main (a merge would still apply it, or it is in conflict):'
                % ('sync --discard-unmerged' if discard else 'sync REFUSED, nothing was changed'))
            for k in lanes:
                if unmerged[k]:
                    say('  lane %s: %d files   (dry-run report: %s)' % (k, len(unmerged[k]), unmerged[k + ' report']))
                    for line in sorted(unmerged[k].values()):
                        say('    ' + line)
            if not discard:
                say('Run `merge <k> --apply` and settle the conflicts (then `resolved`), or run `sync --discard-unmerged` '
                    'to drop this work (the lane versions are then kept under L<k>\\stale\\<stamp>\\).')
                return 6
    elif os.path.exists(MARKER):
        say('the last sync did not finish: completing it (the check for unmerged lane work was made by that run)')

    # 2. what is mirrored
    repo = main_repo_files()
    sticky = []
    if old_man is not None:
        # A file that was mirrored, is still in main, but is no longer listed has become git-ignored (a rule
        # that arrived with a merge).  Nearly all of main is untracked: dropping it would lose it from the lanes.
        was = set(old_man['repo'])
        for k in lanes:
            was |= set(key[5:] for key in load_state(k, old_man['stamp'])['done'] if key.startswith('repo '))
        sticky = sorted(f for f in was - repo if os.path.isfile(os.path.join(MAIN, f)))
        repo |= set(sticky)
    repo = sorted(repo)
    includes = sp_includes(old_man)
    spf = sorted(sp_files(SP, includes))
    say('main: %d repo files, %d scratch files' % (len(repo), len(spf)))
    extra = [e for e in includes if e not in CFG['spInclude']]
    if extra:
        say('mirrored on top of lanes.json spInclude (merges created these in main scratch): ' + ', '.join(extra))
    manifest = {'stamp': stamp, 'repo': {}, 'sp': {}, 'spExtra': extra}
    copied = dict((n, 0) for n, _ in roots)
    moved = dict(copied)
    saved = dict(copied)
    lost = dict(copied)
    bad_roundtrip, odd = [], []
    index = dict((k, {'repo': {}, 'sp': {}}) for k in lanes)
    write(MARKER, stamp.encode())
    for kind, files, src_root in (('repo', repo, MAIN), ('sp', spf, SP)):
        for n, rel in enumerate(files):
            if n % WARM == 0:       # the main, base and lane copies of the next batch (see warm)
                warm([p for r in files[n:n + WARM]
                      for p in [os.path.join(src_root, r), os.path.join(BASE, kind, r)]
                      + [os.path.join(lane_root(k), kind, r) for k in lanes]])
            data = read(os.path.join(src_root, rel))
            if data is None:
                continue
            manifest[kind][rel] = h(data)
            text = kind == 'sp' and rewritable(data)
            bp = os.path.join(BASE, kind, rel)
            old = read(bp)
            # every root mention that a lane copy will still carry unchanged, whatever the encoding
            if kind == 'repo' or os.path.splitext(rel)[1].lower() not in CFG['spNeverMergeExt']:
                if text:
                    for snip in unhandled_spellings(data)[:3]:
                        odd.append('sp %s: ...%s...' % (rel, snip))
                else:
                    for view, how in views(data):
                        if not root_candidate(view):
                            continue
                        m = FWD.search(view)
                        snips = [view[max(0, m.start() - 30):m.end() + 20].decode('utf-8', 'replace').replace('\n', ' ').replace('\r', ' ')] if m else []
                        snips += unhandled_spellings(view)
                        if snips:
                            odd.append('%s %s: %s ...%s...' % (kind, rel, 'repo files are never rewritten' if kind == 'repo'
                                                              else 'holds NUL bytes (UTF-16 or binary), copied without rewriting', snips[0]))
                            break
            for k in lanes:
                out = rewrite(data, k) if text else data
                if text and unrewrite(out, k) != data:
                    bad_roundtrip.append('L%s %s' % (k, rel))
                dst = os.path.join(lane_root(k), kind, rel)
                cur = read(dst)
                if cur == out:
                    st = os.stat(dst)
                    index[k][kind][rel] = [st.st_mtime_ns, st.st_size]
                    continue
                if cur is not None:
                    was = rewrite(old, k) if (old is not None and kind == 'sp' and rewritable(old)) else old
                    if cur != was:
                        # the lane edited this file and the edit is not (or not verbatim) what main has now
                        write(os.path.join(lane_root(k), 'stale', stamp, 'overwritten', kind, rel), cur)
                        saved[k] += 1
                        lost[k] += (kind, fold(rel)) in unmerged[k]
                write(dst, out)
                copied[k] += 1
                st = os.stat(dst)
                index[k][kind][rel] = [st.st_mtime_ns, st.st_size]
            if old != data:
                write(bp, data)
                copied['B'] += 1
        # anything the target has that the source no longer has is moved aside, never deleted
        fset = set(files)
        ffold = dict((fold(f), f) for f in files)
        for name, root in roots:
            troot = os.path.join(root, kind)
            aside = os.path.join(root, 'stale', stamp, kind)
            if kind == 'repo':
                have = copy_repo_files(troot)
                if old_man is not None:
                    have |= set(f for f in old_man['repo'] if os.path.isfile(os.path.join(troot, f)))
                whole = {}
            else:
                # the whole of a lane's scratch: a folder the lane made that is not mirrored must not survive
                # into the next wave, where the merge would take it for new lane work
                have, whole = set(walk(troot, ())), {}
            for rel in sorted(have - fset):
                parts = rel.split('/')
                if fold(rel) in ffold:
                    # only the letter case differs (a lane renamed Foo.cs to foo.cs): same file, give the name back
                    if parts[-1] != ffold[fold(rel)].split('/')[-1]:
                        try:
                            os.rename(os.path.join(troot, rel), os.path.join(troot, ffold[fold(rel)]))
                        except OSError:
                            pass
                    continue
                if kind == 'sp':
                    if not any(d == parts[0] or d.startswith(parts[0] + '/') for d in includes):
                        whole[parts[0]] = whole.get(parts[0], 0) + 1
                        lost[name] += (kind, fold(rel)) in unmerged.get(name, {})
                        continue
                    if any(p in CFG['spSkipDirNames'] for p in parts[:-1]):
                        continue        # build outputs inside a mirrored folder stay, so that a lane need not rebuild all
                src = os.path.join(troot, rel)
                if not os.path.isfile(src):
                    continue
                dst = os.path.join(aside, rel)
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.move(src, dst)
                moved[name] += 1
                lost[name] += (kind, fold(rel)) in unmerged.get(name, {})
            for top, n in sorted(whole.items()):
                os.makedirs(aside, exist_ok=True)
                shutil.move(os.path.join(troot, top), os.path.join(aside, top))
                moved[name] += n
    for name, root in roots:
        line = 'L%s: %d files written, %d stale files moved aside' % (name, copied[name], moved[name])
        if saved[name]:
            line += ', %d lane-edited files replaced (their lane versions are kept in %s)' % (
                saved[name], os.path.join(root, 'stale', stamp, 'overwritten'))
        if lost[name]:
            line += '; %d of these replaced or moved files were UNMERGED lane work, discarded on request' % lost[name]
        say(line)
    write_atomic(INDEX, json.dumps({'stamp': stamp, 'written_ns': time.time_ns(), 'lanes': index}, separators=(',', ':')).encode('utf-8'))
    write_atomic(os.path.join(BASE, 'manifest.json'), json.dumps(manifest, indent=0, sort_keys=True).encode('utf-8'))
    os.remove(MARKER)
    if bad_roundtrip:
        say('WARNING: %d files do not survive rewrite+unrewrite; merge will refuse lane edits to them (first 20):' % len(bad_roundtrip))
        for b in bad_roundtrip[:20]:
            say('  ' + b)
    if odd:
        say('WARNING: %d root mentions are NOT rewritten in the lane copies; there they still point at main (first 20):' % len(odd))
        for b in odd[:20]:
            say('  ' + b)
    if sticky:
        say('WARNING: %d repo files are now git-ignored in main but were mirrored (or merged from a lane) before; they STAY mirrored (first 20):' % len(sticky))
        for b in sticky[:20]:
            say('  ' + b)
    if bad_roundtrip or odd or sticky:
        wp = os.path.join(WORK, 'reports', 'sync-%s-warnings.txt' % stamp)
        write(wp, ('\n'.join(['[roundtrip] ' + b for b in bad_roundtrip] + ['[not rewritten] ' + b for b in odd]
                             + ['[git-ignored, still mirrored] ' + b for b in sticky]) + '\n').encode('utf-8'))
        say('all warnings: ' + wp)
    say('sync %s done' % stamp)
    return 0


# ---------------------------------------------------------------- status
def status(quiet=False):
    man = load_manifest()
    drift = []
    for kind, files, root in (('repo', main_repo_files(), MAIN), ('sp', sp_files(SP, sp_includes(man)), SP)):
        base = man[kind]
        rels = sorted(set(files) | set(base))
        for n, rel in enumerate(rels):
            if n % WARM == 0:
                warm([os.path.join(root, r) for r in rels[n:n + WARM]])
            data = read(os.path.join(root, rel))
            cur = h(data) if data is not None else None
            if cur != base.get(rel):
                drift.append('%s %s %s' % (kind, 'added' if rel not in base else ('removed' if cur is None else 'changed'), rel))
    if not quiet:
        say('base stamp %s; main differs from base in %d files' % (man['stamp'], len(drift)))
        for d in drift:
            say('  ' + d)
    return drift


# ---------------------------------------------------------------- merge
def merge_file(mainp, basep, lanep, union=False):
    args = ['merge-file', '-p', '-L', 'main', '-L', 'base', '-L', 'lane']
    args += ['--union'] if union else ['--diff3']
    rc, out, err = git(args + [mainp, basep, lanep], cwd=os.path.dirname(mainp))
    return rc, out, err


BOM = b'\xef\xbb\xbf'
REPL = b'\xef\xbf\xbd'
LOGEXT = ('.txt', '.log')
ACK = 'deleted-in-lane: main keeps the file'
IDISH = re.compile(r'(?i)^(id|key|name|.*id|.*key)$')
LINE = re.compile(rb'[^\n]*\n|[^\n]+\Z')


def _eol(line):
    return b'\r\n' if line.endswith(b'\r\n') else (b'\n' if line.endswith(b'\n') else b'')


def _mixed(data):
    """Some lines end CRLF and some end LF: there is no convention to give back wholesale."""
    return 0 < data.count(b'\r\n') < data.count(b'\n')


def _json(data):
    def pairs(p):
        keys = [k for k, _ in p]
        if len(set(keys)) != len(keys):
            raise ValueError('duplicate keys ' + ', '.join(sorted(set(k for k in keys if keys.count(k) > 1))))
        return dict(p)

    def constant(name):     # Python reads NaN and Infinity; System.Text.Json and every strict reader do not
        raise ValueError('%s is not JSON' % name)
    return json.loads(data.decode('utf-8-sig'), object_pairs_hook=pairs, parse_constant=constant)


def _unique_keys(doc):
    """{(array path, id-like key): every array of objects at that path has distinct values for the key}"""
    res = {}

    def rec(x, path):
        if isinstance(x, dict):
            for k, v in x.items():
                rec(v, path + '/' + k)
        elif isinstance(x, list):
            if len(x) > 1 and all(isinstance(e, dict) for e in x):
                common = set(x[0])
                for o in x[1:]:
                    common &= set(o)
                for key in common:
                    vals = [o[key] for o in x]
                    if IDISH.match(key) and all(isinstance(v, (str, int)) and not isinstance(v, bool) for v in vals):
                        res[(path, key)] = res.get((path, key), True) and len(set(vals)) == len(vals)
            for e in x:
                rec(e, path + '/*')
    rec(doc, '')
    return res


def _flags(rel, new):
    """Properties of one version of a file that a merge must not introduce."""
    f = set()
    if rel.lower().endswith('.json'):
        try:
            _json(new)
        except UnicodeDecodeError:
            f.add('JSON INVALID: not UTF-8 text')
        except (ValueError, RecursionError) as e:
            f.add('JSON INVALID: %s' % e)
    if re.search(rb'^(<<<<<<< |>>>>>>> |\|\|\|\|\|\|\| )', new, re.M):
        f.add('CONFLICT MARKERS')
    return f


def leak_lines(data):
    """{(what, line): the match} for every line that names a lane or climbs across a lane root."""
    res = {}
    for view, how in views(data):
        for what, rx in (('LANE PATH LEFT IN FILE', LEAK), ('LANE PATH LEFT IN FILE', ABSLEAK), ('RELATIVE PATH ACROSS THE LANE ROOT', CROSS)):
            for m in rx.finditer(view):
                e = view.find(b'\n', m.end())
                line = view[view.rfind(b'\n', 0, m.start()) + 1:len(view) if e < 0 else e].rstrip(b'\r')
                res.setdefault((what, line), m.group(0).decode('utf-8', 'replace') + how)
    return res


def _enc(data):
    return 'utf8' if is_text(data) else ('nul' if b'\0' in data else 'bytes')


def checks(kind, rel, new, base, parents=()):
    """Why `new` must not be written over the file main has now, `base` (None: a new file).
    Only what `base` does not already have counts.  parents: the two sides of a text merge.
    Returns (blocking flags, notes)."""
    old = _flags(rel, base) if base is not None else set()
    oldcat = set(x.split(':')[0] for x in old)
    flags = [x for x in sorted(_flags(rel, new)) if x.split(':')[0] not in oldcat]
    notes = []
    # lane paths: every line that carries one must already be in main's version, byte for byte (a count
    # would let a lane swap one mention for another); raw bytes, whatever the encoding
    hits = leak_lines(new)
    if hits:
        have = set()
        if base is not None:
            for view, _ in views(base):
                have |= set(x.rstrip(b'\r') for x in view.split(b'\n'))
        seen = set()
        for (what, line), snip in sorted(hits.items()):
            if line not in have and what not in seen:
                seen.add(what)
                flags.append('%s: %s' % (what, snip))
    if base is not None:
        eb, en = _enc(base), _enc(new)
        if not base:
            pass
        elif eb == 'utf8' and en != 'utf8':
            if kind == 'sp' and en == 'bytes' and base.isascii() and os.path.splitext(rel)[1].lower() in LOGEXT:
                notes.append('now holds bytes that are not UTF-8 (console output in an OEM/ANSI code page)')
            else:
                flags.append('ENCODING CHANGED: main has UTF-8 text, this version %s' % (
                    'holds NUL bytes (UTF-16, or binary)' if en == 'nul' else 'is not valid UTF-8 (saved as ANSI/OEM?)'))
        elif eb == 'bytes' and en == 'nul':
            flags.append('ENCODING CHANGED: main has single-byte text, this version holds NUL bytes (UTF-16, or binary)')
        if new.count(REPL) > base.count(REPL):
            flags.append('REPLACEMENT CHARACTERS ADDED (U+FFFD): characters were lost in an encoding round trip')
        if new.startswith(BOM) != base.startswith(BOM):
            flags.append('BOM ADDED' if new.startswith(BOM) else 'BOM REMOVED')
        nb, nn = base.count(b'\n'), new.count(b'\n')
        cb, cn = base.count(b'\r\n'), new.count(b'\r\n')
        if texty(base) and texty(new):        # in a binary file 0D 0A is not a line end
            if cn and not cb:
                flags.append('CRLF ADDED')
            if cb and cb == nb and cn != nn:
                flags.append('LF ADDED TO A CRLF FILE')
        if _mixed(base) and texty(base) and texty(new) and max(base.count(b'\n'), new.count(b'\n')) >= 20000:
            # A file with mixed line ends that is too big for conform() to align line by line: refuse a
            # version in which a line whose text main has appears only with the other line end.
            exact = set(LINE.findall(base))
            body = set(x[:len(x) - len(_eol(x))] for x in exact)
            n = sum(1 for x in LINE.findall(new) if x not in exact and _eol(x) and x[:len(x) - len(_eol(x))] in body)
            if n:
                flags.append('LINE ENDS CHANGED ON %d UNEDITED LINES of a file with mixed line ends' % n)
    if parents and rel.lower().endswith('.json') and not any(x.startswith('JSON INVALID') for x in flags):
        try:
            maps = [_unique_keys(_json(p)) for p in parents]
            for (path, key), uniq in sorted(_unique_keys(_json(new)).items()):
                have = [m[(path, key)] for m in maps if (path, key) in m]
                if not uniq and have and all(have):
                    flags.append('JSON DUPLICATE "%s" VALUES in array %s (distinct on both sides before the merge)' % (key, path or '/'))
        except ValueError:
            pass
    return flags, notes


def _reencode(l, ref):
    """A UTF-8 file that came back from the lane in another encoding, as UTF-8 again; None if that
    cannot be done without guessing."""
    if l[:2] in (b'\xff\xfe', b'\xfe\xff'):
        try:        # what PowerShell 5.1 `>` and Out-File write
            return l.decode('utf-16').encode('utf-8'), 'encoding restored to UTF-8 (the lane copy was UTF-16)'
        except UnicodeError:
            return None
    if not rewritable(l):
        return None
    # ANSI (PowerShell 5.1 Set-Content, Python open() without encoding=): a line is restored only when it is
    # exactly the CP1252 form of a line the base has; a line the lane itself wrote in ANSI stays a conflict
    table = {}
    lines = ref[len(BOM):].split(b'\n') if ref.startswith(BOM) else ref.split(b'\n')
    for x in lines:
        x = x.rstrip(b'\r')
        if not x.isascii():
            try:
                table.setdefault(x.decode('utf-8').encode('cp1252'), x)
            except UnicodeError:
                pass
    have = set(x.rstrip(b'\r') for x in lines)
    out = []
    for x in l.split(b'\n'):
        cr = b'\r' if x.endswith(b'\r') else b''
        body = x[:len(x) - len(cr)]
        if body in table and body not in have:
            out.append(table[body] + cr)
            continue
        try:
            body.decode('utf-8')
        except UnicodeDecodeError:
            return None
        out.append(x)
    return b'\n'.join(out), 'encoding restored to UTF-8 (the lane copy had been saved as ANSI/CP1252)'


def _line_ends(l, ref):
    """For a base with mixed line ends: every lane line that matches a base line gets that line's end back."""
    if max(ref.count(b'\n'), l.count(b'\n')) >= 20000:
        return l, 0         # too big to align; checks() then refuses a version whose unedited lines changed
    bl, ll = LINE.findall(ref), LINE.findall(l)
    strip = lambda x: x[:len(x) - len(_eol(x))]
    uniform = not _mixed(l)         # the lane's editor wrote one line end throughout
    out, n, prev = [], 0, None
    for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, [strip(x) for x in bl], [strip(x) for x in ll], autojunk=False).get_opcodes():
        for j in range(j1, j2):
            x = ll[j]
            want = b''
            if tag == 'equal' or (tag == 'replace' and i2 - i1 == j2 - j1):
                want = _eol(bl[i1 + j - j1])
            elif uniform:
                # a line the lane added: in a normalised copy its line end says nothing, so it follows the
                # line it replaces or the line before it
                want = _eol(bl[i1]) if i1 < i2 else (prev if prev is not None else (_eol(bl[i1]) if i1 < len(bl) else b''))
            if want and _eol(x) and want != _eol(x):     # never glue: a missing final newline is left as the lane has it
                x = strip(x) + want
                n += 1
            out.append(x)
            prev = _eol(x) or prev
    return b''.join(out), n


def conform(l, ref):
    """The lane version with the encoding, the BOM and the line ends of the version it started from.
    Editors and shells flip these without being asked (PowerShell 5.1 writes UTF-16 or ANSI, a BOM and
    CRLF; other tools drop a BOM), and such a flip would turn every line into a change.
    Returns (bytes, notes)."""
    notes = []
    if ref is None:
        return l, notes
    if is_text(ref) and not is_text(l):
        fixed = _reencode(l, ref)
        if fixed is not None:
            l = fixed[0]
            notes.append(fixed[1])
    if not (texty(l) and texty(ref)):
        return l, notes
    if ref.startswith(BOM) and not l.startswith(BOM):
        l = BOM + l
        notes.append('BOM restored (the lane copy had lost it)')
    elif l.startswith(BOM) and not ref.startswith(BOM):
        l = l[len(BOM):]
        notes.append('BOM removed again (the lane copy had gained one)')
    rn, rc = ref.count(b'\n'), ref.count(b'\r\n')
    ln, lc = l.count(b'\n'), l.count(b'\r\n')
    if rn and rc == rn and lc != ln:        # every line of the original ends CRLF; the lane copy has bare LFs
        l = l.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        notes.append('line ends restored to CRLF (the lane copy had LF)')
    elif rn and rc == 0 and lc:             # the original has no CRLF at all; the lane copy has some
        l = l.replace(b'\r\n', b'\n')
        notes.append('line ends restored to LF (the lane copy had CRLF)')
    elif _mixed(ref) and l != ref:
        l, n = _line_ends(l, ref)
        if n:
            notes.append('line ends restored on %d lines (the file has mixed line ends; the lane copy had changed them)' % n)
    return l, notes


def state_path(k):
    return os.path.join(WORK, 'state', 'merged-L%s.json' % k)


def load_state(k, stamp):
    """What lane k already has in main since the sync `stamp`: done {'repo x/y.cs': sha of the lane version},
    deleted {'repo x/y.cs': the path the lane renamed it to, or ''}."""
    data = read(state_path(k))
    if data is not None:
        st = json.loads(data.decode('utf-8'))
        if st.get('stamp') == stamp:
            st.setdefault('deleted', {})
            return st
    return {'stamp': stamp, 'done': {}, 'deleted': {}}


def prev_path(k, stamp, kind, rel):
    """The lane version of a file (in main form) that the last merge of lane k put into main."""
    return os.path.join(WORK, 'state', 'L%s' % k, stamp, kind, *rel.split('/'))


def merge(k, apply, atomic=False):
    k = str(k)
    if not os.path.isdir(lane_root(k)):
        say('no lane %s' % k)
        raise SystemExit(2)
    if apply:
        with Lock('merge %s' % k):
            return _merge(k, True, atomic)
    return _merge(k, False, atomic)


def _merge(k, apply, atomic, presync=False, untouched=()):
    # untouched (sync's check for unmerged work only): mirrored lane files known to be what sync wrote; not opened
    man = load_manifest()
    os.makedirs(os.path.join(WORK, 'reports'), exist_ok=True)
    suffix = '' if apply else ('-presync' if presync else '-dryrun')
    out = (lambda s='': None) if presync else say

    def rpath(s):
        return os.path.join(WORK, 'reports', 'merge-L%s-%s%s.json' % (k, s, suffix))
    def cpath(s):
        return os.path.join(WORK, 'conflicts', '%s-L%s%s' % (s, k, suffix))
    stamp = unique_stamp(lambda s: os.path.exists(rpath(s)) or os.path.exists(cpath(s)))
    root = lane_root(k)
    cdir = cpath(stamp)
    rep = {'lane': k, 'stamp': stamp, 'base_stamp': man['stamp'], 'applied': apply, 'atomic': atomic, 'items': []}
    tmp = os.path.join(WORK, 'tmp')
    state = load_state(k, man['stamp'])
    # what the other lanes deleted or renamed in this sync round: an edit to such a file must not land quietly
    others = {}
    for j in range(1, CFG['lanes'] + 1):
        if str(j) != k:
            for key, to in load_state(str(j), man['stamp'])['deleted'].items():
                if to or key.startswith('repo '):       # a scratch file another lane merely deleted is no obstacle
                    others[key] = (str(j), to)
    plan = []       # (item, dst, bytes to write, main bytes the decision was based on)
    lane_bytes = {}  # 'kind rel' -> the lane version in main form, for the state
    deletions = []  # (item, base bytes) of files the lane no longer has
    agreed = []     # keys deleted in the lane and gone from main as well
    newfiles = []   # (item, lane bytes) of files the base does not have
    restored = []   # keys the lane has (again)

    def conflict(item, kind, rel, why, l, diff3=None, union=None, merged=None):
        item['action'] = 'CONFLICT'
        item['why'] = why
        keep = os.path.join(cdir, kind, rel)
        if l is not None:
            write(keep + '.lane', l)
            item['lane_version'] = keep + '.lane'
            item['lane_sha'] = h(l)
        for ext, name, data in (('.diff3', 'diff3', diff3), ('.union', 'union_candidate', union), ('.merged', 'merged_candidate', merged)):
            if data is not None:
                write(keep + ext, data)
                item[name] = keep + ext

    def handle(kind, rel, b, l, m, dst):
        """b base bytes, l lane bytes in main form, m main bytes now."""
        item = {'kind': kind, 'path': rel}
        rep['items'].append(item)
        key = kind + ' ' + rel
        if kind == 'repo' and rel in CFG['generated']:
            item['action'] = 'generated-skip'
            return
        done = state['done'].get(key)
        if l is None:
            if done == ACK or m is None:
                rep['items'].pop()          # settled: main keeps it on purpose, or main has no such file either
                if m is None:
                    agreed.append(key)
                return
            item['action'] = 'lane-deleted (not applied)'
            if b is not None and m != b:
                item['notes'] = ['main\'s copy has changed since the sync (another lane\'s merge or a hand edit)']
            if kind == 'repo':
                item['blocks'] = True
            deletions.append((item, b))
            return
        restored.append(key)
        if b is None and rel in man[kind]:
            conflict(item, kind, rel, 'the base file is missing from LB although sync recorded it (LB was damaged); '
                     'merge every other lane first, then run sync', l)
            item['lane_untouched'] = h(l) == man[kind][rel]
            return
        if b is None:
            newfiles.append((item, l))
        l, fixed = conform(l, b)
        if fixed:
            item['notes'] = fixed
        item['lane_sha'] = h(l)
        lane_bytes[key] = l
        if done == item['lane_sha']:
            item['action'] = 'already-merged'
            return
        if b is not None and h(b) != man[kind].get(rel):
            conflict(item, kind, rel, 'the base snapshot of this file is not what sync recorded (LB was edited?); '
                     'merge every other lane first, then run sync (a sync before that would drop their work)', l)
            item['lane_untouched'] = item['lane_sha'] == man[kind].get(rel)
            return
        if m == l:
            item['action'] = 'same-as-main'
            return
        if key in others:
            j, to = others[key]
            conflict(item, kind, rel, 'lane %s %s in this sync round and this lane changed it: carry the change over by hand%s, '
                     'then run `resolved %s %s %s`' % (j, ('renamed this file to ' + to) if to else 'deleted this file',
                                                       (' into ' + to) if to else ' or keep the file', k, kind, rel), l)
            return
        if done is not None and done != ACK:
            # The lane went on working after an earlier merge of this file.  What main and the lane have
            # in common now is the lane version merged then, not the sync base.
            p = read(prev_path(k, man['stamp'], kind, rel))
            if p is not None and h(p) == done:
                b = p
                item['base'] = 'the lane version merged earlier'
        if m == b:
            flags, notes = checks(kind, rel, l, m)
            notes = item.get('notes', []) + notes
            if b is None and kind == 'sp':
                # a new scratch file is the lane's own: a fixture may be malformed JSON on purpose.  Say so, apply it.
                soft = [x for x in flags if x.startswith('JSON INVALID')]
                notes += soft
                flags = [x for x in flags if x not in soft]
            if flags:
                conflict(item, kind, rel, 'the lane version failed a check: ' + '; '.join(flags), l)
                return
            item['action'] = 'take-lane' if b is not None else 'new-file'
            if b is None:
                notes += [n for n, c in (('has a BOM', l.startswith(BOM)), ('has CRLF line ends', b'\r\n' in l and texty(l)),
                                         ('is UTF-16', l[:2] in (b'\xff\xfe', b'\xfe\xff')),
                                         ('is text but not valid UTF-8', _enc(l) == 'bytes')) if c]
                if kind == 'sp':
                    item['top_existed'] = os.path.lexists(os.path.join(SP, rel.split('/')[0]))
            if notes:
                item['notes'] = notes
            plan.append((item, dst, l, m))
            return
        # both sides changed
        if b is None or m is None or not (is_text(b) and is_text(l) and is_text(m)):
            conflict(item, kind, rel, 'added on both sides' if b is None else ('removed in main' if m is None else
                     'binary' if not (rewritable(b) and rewritable(l) and rewritable(m)) else 'not UTF-8 text on one side, changed on both'), l)
            return
        # per-process names: a dry run takes no lock and must not swap these files under an applying merge
        pm, pb, pl = (os.path.join(tmp, '%s-%d' % (n, os.getpid())) for n in ('main', 'base', 'lane'))
        try:
            write(pm, m); write(pb, b); write(pl, l)
            rc, mout, err = merge_file(pm, pb, pl)
            rc2, out2, _ = merge_file(pm, pb, pl, union=True) if rc != 0 else (0, b'', b'')
        finally:
            for p in (pm, pb, pl):
                try:
                    os.remove(p)
                except OSError:
                    pass
        if rc == 0:
            # judged against what main has now: its BOM, its line ends, its JSON must survive the merge
            flags, notes = checks(kind, rel, mout, m, parents=(m, l))
            if flags:
                conflict(item, kind, rel, 'the clean text merge failed a check: ' + '; '.join(flags), l, merged=mout)
            else:
                item['action'] = 'merged-clean'
                if notes:
                    item['notes'] = item.get('notes', []) + notes
                plan.append((item, dst, mout, m))
            return
        conflict(item, kind, rel, '%s overlapping hunks' % rc if 0 < rc < 127 else 'git merge-file error: ' + err.decode('utf-8', 'replace'),
                 l, diff3=mout, union=out2)
        item['union_flags'] = checks(kind, rel, out2, m, parents=(m, l))[0]

    def note_case(kind, old, new):
        rep['items'].append({'kind': kind, 'path': old, 'action': 'case-rename (not applied)', 'renamed_to': new,
                             'notes': ['the lane spells it %s; main keeps its spelling and the content is merged under it' % new]})

    left = []       # 'kind rel' of lane files that are not merged by rule
    folders = []    # folders left out by name, with file counts

    # repo side: everything sync put in the base, plus whatever source the lane added
    lrepo = os.path.join(root, 'repo')
    base_repo = set(man['repo'])
    lowmap = dict((fold(f), f) for f in base_repo)
    skip = set(CFG['repoSkipDirNames'])
    lane_all, skipped_dirs = walk_lane(lrepo, skip, source_skipdirs(base_repo, skip), hidden=('.git',))
    for d, (n, sample, proj) in sorted(skipped_dirs.items()):
        folders.append({'kind': 'repo', 'folder': d, 'files': n, 'sample': sample, 'beside_project_file': proj})
    spelled, cand = {}, []      # manifest path -> the lane's spelling ; lane files the base does not have
    for f in lane_all:
        if f in base_repo:
            spelled.setdefault(f, f)
        elif fold(f) in lowmap:
            spelled[lowmap[fold(f)]] = f
            note_case('repo', lowmap[fold(f)], f)
        else:
            cand.append(f)
    # New lane files that git ignores (a loose *.dll, a *.zip) are never merged: name them, do not hide them.
    # Judged by the base snapshot's rules (and the lane's own, if it edited a .gitignore), never by a rule
    # that another lane's merge has just put into main.
    extra = tuple(d + '/' for d in CFG['repoExtraDirs'])
    ask = [f for f in cand if not f.startswith(extra)]
    ign = ignored(ask, os.path.join(BASE, 'repo'))
    if any(f.split('/')[-1] == '.gitignore' and read(os.path.join(lrepo, f)) != read(os.path.join(BASE, 'repo', spelled_f))
           for spelled_f, f in list(spelled.items()) + [(c, c) for c in cand]):
        ign |= ignored(ask, lrepo)
    left += sorted('repo ' + f for f in cand if f in ign)
    new_repo = set(f for f in cand if f not in ign)
    rels = sorted(base_repo | new_repo)
    for wi, rel in enumerate(rels):
        if wi % WARM == 0:      # the base and lane copies of the next batch (see warm)
            warm([p for r in rels[wi:wi + WARM] if ('repo', r) not in untouched
                  for p in (os.path.join(BASE, 'repo', r), os.path.join(lrepo, spelled.get(r, r)))])
        key = 'repo ' + rel
        if ('repo', rel) in untouched and state['done'].get(key) in (None, man['repo'][rel]):
            continue
        b = read(os.path.join(BASE, 'repo', rel))
        l = read(os.path.join(lrepo, spelled.get(rel, rel)))
        if b is not None and l == b and h(b) == man['repo'].get(rel) and state['done'].get(key) in (None, h(b)):
            continue
        handle('repo', rel, b, l, read(os.path.join(MAIN, rel)), os.path.join(MAIN, rel))

    # scratch side: every file under the lane's sp, mirrored or new
    lsp = os.path.join(root, 'sp')
    mirrored = tuple(d + '/' for d in CFG['spInclude'])
    splow = dict((fold(f), f) for f in man['sp'])
    spskip = set(CFG['spSkipDirNames'])
    sp_all, skipped_dirs = walk_lane(lsp, spskip, source_skipdirs(man['sp'], spskip))
    skipped = 0
    for d, (n, sample, proj) in sorted(skipped_dirs.items()):
        folders.append({'kind': 'sp', 'folder': d, 'files': n, 'sample': sample, 'beside_project_file': proj})
        skipped += n
    seen = set()
    sp_sorted = sorted(sp_all)
    for wi, lrel in enumerate(sp_sorted):
        if wi % WARM == 0:      # the lane and base copies of the next batch, but for what is skipped unread below
            warm([p for r in sp_sorted[wi:wi + WARM]
                  if ('sp', r) not in untouched and os.path.splitext(r)[1].lower() not in CFG['spNeverMergeExt']
                  and not any(x in CFG['spNeverMergeDirNames'] for x in r.split('/'))
                  for p in (os.path.join(lsp, r), os.path.join(BASE, 'sp', r))])
        parts = lrel.split('/')
        if any(p in CFG['spNeverMergeDirNames'] for p in parts) or os.path.splitext(lrel)[1].lower() in CFG['spNeverMergeExt']:
            skipped += 1
            seen.add(splow.get(fold(lrel), lrel))
            continue
        rel = lrel
        if rel not in man['sp'] and fold(rel) in splow:
            rel = splow[fold(rel)]
            note_case('sp', rel, lrel)
        seen.add(rel)
        key = 'sp ' + rel
        if ('sp', rel) in untouched and state['done'].get(key) in (None, man['sp'][rel]):
            continue
        raw = read(os.path.join(lsp, lrel))
        b = read(os.path.join(BASE, 'sp', rel))
        btext = b is not None and rewritable(b)
        if b is not None and raw == (rewrite(b, k) if btext else b) and h(b) == man['sp'].get(rel) and state['done'].get(key) in (None, h(b)):
            continue
        if (b is None and rel not in man['sp'] and rel.startswith(mirrored) and any(fnmatch.fnmatch(parts[-1], g) for g in CFG['spSkipNewLogGlobs'])) \
                or any(fnmatch.fnmatch(parts[-1], g) for g in CFG.get('spNeverMergeGlobs', [])):
            # spNeverMergeGlobs (optional): logs every lane rewrites, new or not, that would only ever conflict
            skipped += 1
            left.append('sp ' + rel)
            continue
        l = unrewrite(raw, k) if rewritable(raw) else raw
        if RESERVED.match(parts[0]) or parts[0].lower() == os.path.basename(HERE).lower():
            # L1\sp\L2\... would be written into another lane (or the base, or this tool)
            item = {'kind': 'sp', 'path': rel}
            rep['items'].append(item)
            conflict(item, 'sp', rel, 'reserved top-level name %s: a lane must not write into a lane, the base or the lane tool' % parts[0], l)
            continue
        if btext and unrewrite(rewrite(b, k), k) != b:
            item = {'kind': 'sp', 'path': rel}
            rep['items'].append(item)
            conflict(item, 'sp', rel, 'the base file does not survive rewrite+unrewrite, so the lane edit cannot be told from path noise', l)
            continue
        handle('sp', rel, b, l, read(os.path.join(SP, rel)), os.path.join(SP, rel))
    for rel in sorted(set(man['sp']) - seen):
        handle('sp', rel, read(os.path.join(BASE, 'sp', rel)), None, read(os.path.join(SP, rel)), os.path.join(SP, rel))

    # a file the lane created, merged, and then deleted is in neither the base nor the lane any more
    for key in sorted(state['done']):
        kind, rel = key.split(' ', 1)
        if rel in man[kind] or state['done'][key] == ACK or os.path.isfile(os.path.join(lrepo if kind == 'repo' else lsp, rel)):
            continue
        m = read(os.path.join(MAIN if kind == 'repo' else SP, rel))
        if m is None:
            agreed.append(key)
            continue
        item = {'kind': kind, 'path': rel, 'action': 'lane-deleted after an earlier merge (not applied)'}
        if kind == 'repo':
            item['blocks'] = True
        rep['items'].append(item)
        deletions.append((item, read(prev_path(k, man['stamp'], kind, rel))))

    # a deletion and a new file with (nearly) the same content are one rename; the old name is still in main
    exact = {}
    for nit, nl in newfiles:
        exact.setdefault((nit['kind'], h(nl)), nit)
    for n, (item, b) in enumerate(deletions):
        if not b:               # no base bytes to compare, or an empty file (which would pair with any empty new file)
            continue
        best = exact.get((item['kind'], h(b)))
        if best is None and rewritable(b) and n < 50:
            bl, score = LINE.findall(b), 0.8
            for nit, nl in newfiles[:200]:
                if nit['kind'] != item['kind'] or not rewritable(nl) or not len(b) // 2 <= len(nl) <= len(b) * 2 or len(bl) > 5000:
                    continue
                sm = difflib.SequenceMatcher(None, bl, LINE.findall(nl), autojunk=False)
                if sm.quick_ratio() >= score and sm.ratio() >= score:
                    best, score = nit, sm.ratio()
        if best is not None:
            item['action'] = 'RENAMED (not applied: the old name is still in main)'
            item['renamed_to'] = best['path']
            item['blocks'] = True

    def count():
        c = {}
        for it in rep['items']:
            a = it['action'].split(' ')[0]
            c[a] = c.get(a, 0) + 1
        return c

    def blocked():
        return [it for it in rep['items'] if it.get('blocks') and it['action'].split(' ')[0] in ('lane-deleted', 'RENAMED')]

    # apply: nothing above wrote to main.  Each write re-checks that main still is what the decision saw.
    wrote = failed = 0
    held = apply and atomic and (count().get('CONFLICT') or blocked())
    if apply and not held:
        new_extra, mirrored_now = set(), sp_includes(man)
        for item, dst, data, seen_main in plan:
            key = item['kind'] + ' ' + item['path']
            try:
                if read(dst) != seen_main:
                    conflict(item, item['kind'], item['path'], 'main changed while this merge was running; rerun the merge', lane_bytes[key])
                    continue
                write_atomic(dst, data)
                wrote += 1
                if item['kind'] == 'sp' and not covered(item['path'], mirrored_now):
                    # mirror what the lane brought into main scratch, so that the next sync resets it in every lane
                    new_extra.add(item['path'].split('/')[0] if (item.get('top_existed') is False and '/' in item['path']) else item['path'])
            except Exception as e:      # a locked file, a full disk: say which files did and did not land
                item['intended'] = item['action']
                item['action'] = 'FAILED-WRITE'
                item['why'] = '%s: %s' % (type(e).__name__, e)
                failed += 1
        for it in rep['items']:
            if it['action'] in ('take-lane', 'new-file', 'merged-clean', 'same-as-main'):
                key = it['kind'] + ' ' + it['path']
                write(prev_path(k, man['stamp'], it['kind'], it['path']), lane_bytes[key])
                state['done'][key] = it['lane_sha']
        for key in restored:
            state['deleted'].pop(key, None)
        for item, _ in deletions:
            state['deleted'][item['kind'] + ' ' + item['path']] = item.get('renamed_to', '')
        for key in agreed:
            state['deleted'].setdefault(key, '')
        write_atomic(state_path(k), json.dumps(state, indent=0, sort_keys=True).encode('utf-8'))
        if new_extra:
            write_atomic(os.path.join(WORK, 'state', 'sp-extra.json'),
                         json.dumps({'paths': sorted(set(sp_extra()) | new_extra)}, indent=0).encode('utf-8'))
    elif held:
        for item, dst, data, seen_main in plan:
            item['action'] = 'held (--atomic: lane has conflicts) ' + item['action']

    counts = count()
    rep['counts'] = counts
    rep['written_to_main'] = wrote
    rep['scratch_build_products_skipped'] = skipped
    rep['not_merged_ignored_or_log'] = left
    rep['skipped_folders'] = folders
    need = blocked()
    rep['exit'] = 3 if failed else (1 if counts.get('CONFLICT') or need else 0)
    rp = rpath(stamp)
    rep['report'] = rp
    write(rp, json.dumps(rep, indent=1).encode('utf-8'))
    out('lane %s %s: %s' % (k, ('APPLIED to main (%d files written)' % wrote) if apply else 'dry run (nothing written)',
                            json.dumps(counts, sort_keys=True)))
    for it in rep['items']:
        line = '  %-28s %s %s' % (it['action'], it['kind'], it['path'])
        if it.get('renamed_to'):
            line += ' -> ' + it['renamed_to']
        if it.get('notes'):
            line += '   (' + '; '.join(it['notes']) + ')'
        if it.get('base'):
            line += '   (3-way base: ' + it['base'] + ')'
        if it['action'] in ('CONFLICT', 'FAILED-WRITE'):
            line += '   (' + it.get('why', '') + ')'
        out(line)
    if left:
        out('not merged, by rule (git-ignored by the base snapshot\'s rules, or a new build/run log in a mirrored scratch folder): %d files%s'
            % (len(left), ''.join('\n  ' + x for x in left[:15]) + ('\n  ... the report lists all' if len(left) > 15 else '')))
    if folders:
        odd = [f for f in folders if not f['beside_project_file']]
        out('not merged, by folder name (%s; %s in scratch): %d files in %d folders the base has nothing in; %d of the folders are build '
            'output beside a project file' % (', '.join(sorted(skip)), ', '.join(sorted(spskip)), sum(f['files'] for f in folders), len(folders),
                                              len(folders) - len(odd)))
        if odd:
            out('  %d of the folders have NO project file beside them; if one holds source, move it or merge it by hand:%s'
                % (len(odd), ''.join('\n  %s %s/   %d files (%s%s)' % (f['kind'], f['folder'], f['files'], ', '.join(f['sample']), ', ...' if f['files'] > len(f['sample']) else '')
                                     for f in odd[:15]) + ('\n  ... the report lists all' if len(odd) > 15 else '')))
    fixed = [it for it in rep['items'] if any('restored' in n or 'removed again' in n for n in it.get('notes', []))]
    if fixed:
        out('%d lane files had flipped a BOM, their line ends or their encoding; the merge used main\'s convention for them (see the notes above). '
            'If a lane meant such a change, make it by hand.' % len(fixed))
    gone = [it for it in rep['items'] if it['action'].split(' ')[0] in ('lane-deleted', 'RENAMED')]
    if gone:
        out('%d files the lane deleted are still in main (this tool never deletes): delete them by hand if the lane meant it.' % len(gone))
    if need:
        out('%d of them need a decision (exit 1): repo files, and files the lane RENAMED (the content now sits under both names). '
            'Delete the old file in main, or run `resolved %s <repo|sp> <path>` to keep it.' % (len(need), k))
    out('report: ' + rp)
    if counts.get('CONFLICT'):
        out('CONFLICTS were NOT written to main%s. Lane versions, diff3 output and union candidates: %s'
            % (' and --atomic held back every other file too' if held else '', cdir))
        if apply and not held and wrote:
            out('Main now holds only part of lane %s: resolve the conflicts above before building or syncing.' % k)
    if failed:
        out('FAILED: %d files could not be written to main (see FAILED-WRITE above); main holds only part of lane %s. Rerun the merge.' % (failed, k))
    return rep


def _stamp_key(s):
    return (s[:15], int(s[16:] or 1))


def resolved(k, kind, rel):
    """The conflict the last applied merge of lane k reported for this file was settled by hand in main:
    the lane version that conflicted (not whatever the lane holds now) counts as merged."""
    k = str(k)
    rel = rel.replace('\\', '/')
    man = load_manifest()
    if kind not in ('repo', 'sp'):
        say('usage: resolved <k> <repo|sp> <path>')
        return 2
    reps = []
    for p in glob.glob(os.path.join(glob.escape(os.path.join(WORK, 'reports')), 'merge-L%s-*.json' % k)):
        if p.endswith(('-dryrun.json', '-presync.json')):
            continue
        r = json.loads(read(p).decode('utf-8'))
        if r.get('applied') and r.get('base_stamp') == man['stamp'] and str(r.get('lane')) == k:
            reps.append(r)
    last = max(reps, key=lambda r: _stamp_key(r['stamp'])) if reps else None
    it = None
    for x in (last or {}).get('items', []):
        if x['kind'] == kind and fold(x['path']) == fold(rel) and (x['action'] == 'CONFLICT' or x.get('blocks')):
            it = x
    if it is None:
        say('lane %s: the last `merge %s --apply` since the sync (%s) reports no conflict for %s %s; nothing was recorded. '
            'Run `merge %s --apply` first; only a path it reports as CONFLICT (or as a deletion that needs a decision) can be resolved.'
            % (k, k, last['stamp'] if last else 'there is none', kind, rel, k))
        return 2
    rel = it['path']
    key = kind + ' ' + rel
    with Lock('resolved %s' % k):
        state = load_state(k, man['stamp'])
        if it['action'] != 'CONFLICT':
            state['done'][key] = ACK
            state['deleted'].pop(key, None)
            msg = 'main keeps the file the lane deleted; it is no longer reported'
        else:
            l = read(it.get('lane_version', '')) if it.get('lane_version') else None
            if l is None:
                say('lane %s: the lane version kept for that conflict is gone (%s); nothing was recorded' % (k, it.get('lane_version')))
                return 2
            write(prev_path(k, man['stamp'], kind, rel), l)
            state['done'][key] = h(l)
            msg = 'recorded as merged by hand (the lane version of merge %s)' % last['stamp']
            raw = read(os.path.join(lane_root(k), kind, rel))
            if raw is not None:
                cur = unrewrite(raw, k) if (kind == 'sp' and rewritable(raw)) else raw
                cur = conform(cur, read(os.path.join(BASE, kind, rel)))[0]
                if h(cur) != h(l):
                    msg += '. NOTE: the lane file has changed since that conflict; the next merge will merge those newer edits on top'
        write_atomic(state_path(k), json.dumps(state, indent=0, sort_keys=True).encode('utf-8'))
    say('lane %s: %s %s %s' % (k, kind, rel, msg))
    return 0


def main():
    a = sys.argv[1:]
    if not a:
        say(__doc__)
        return 2
    if a[0] == 'sync' and all(x == '--discard-unmerged' for x in a[1:]):
        return sync('--discard-unmerged' in a)
    elif a[0] == 'status' and len(a) == 1:
        status()
    elif a[0] == 'diff' and len(a) == 2:
        return merge(a[1], False)['exit']
    elif a[0] == 'merge' and len(a) > 1 and all(x in ('--apply', '--atomic') for x in a[2:]):
        return merge(a[1], '--apply' in a, '--atomic' in a)['exit']
    elif a[0] == 'resolved' and len(a) == 4:
        return resolved(a[1], a[2], a[3])
    elif a[0] in ('rewrite', 'unrewrite') and len(a) == 3:
        data = read(a[2])
        OUT.write(rewrite(data, a[1]) if a[0] == 'rewrite' else unrewrite(data, a[1]))
    else:
        say(__doc__)
        return 2
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (SystemExit, KeyboardInterrupt):
        raise
    except Exception:
        # not exit 1: that means "conflicts".  A merge decides everything before its first write to main,
        # so a crash while reading leaves main untouched; a crash later is covered by rerunning.
        import traceback
        traceback.print_exc()
        say('lanes.py CRASHED (exit 70). If this was `merge --apply`, run `status`, then rerun the merge.')
        sys.exit(70)
