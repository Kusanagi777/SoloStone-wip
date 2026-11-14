"""verify/tools/port_paths.py: the mechanical part of porting a suite copied from the old scratchpad.

    python -I verify/tools/port_paths.py <suite folder> [<suite folder> ...]

Rewrites, in place, every *.csproj and *.cs of each folder (Tests/ included; obj/ and out/ skipped):
  .csproj  C:\\SoloStone\\<path>              -> $(SoloStoneRoot)<path with />
           <HintPath> to any engine-out\\SabberStoneCore.dll -> $(EngineDll)
           $(EngineDir)\\SabberStoneCore.dll   -> $(EngineDir)/SabberStoneCore.dll
  .cs      @"C:\\SoloStone"                    -> VerifyPaths.Root
           @"C:\\SoloStone\\data"              -> VerifyPaths.Data
           @"C:\\SoloStone\\<path>"            -> VerifyPaths.Repo("<path with />")
           @"C:\\Users\\...\\scratchpad\\<p>"   -> VerifyPaths.Fixture("<p with />")
           @"a\\b\\c.json" (a relative path)   -> "a/b/c.json"   (a file with an extension, or inside Path.Combine / R(...))
Then lists what is left for a person to look at: any remaining drive path, "scratchpad", "..\\", cwd-relative
Path.Combine("..", ...), AppContext.BaseDirectory, ProcessStartInfo, .exe, cygpath. Line ends are written as LF.
It never touches a check: only string literals that are paths. Read the diff before building.
"""
import os
import re
import sys

BS = chr(92)
# a path ends at a quote, at ';' (MSBuild item lists) and at '<'
SCRATCH = re.compile(r'C:' + re.escape(BS) + r'Users' + re.escape(BS) + r'[^";<]*?' + re.escape(BS) + r'scratchpad(?:' + re.escape(BS) + r'([^";<]*))?')
REPO = re.compile(r'C:' + re.escape(BS) + r'SoloStone(?:' + re.escape(BS) + r'([^";<]*))?')
REL = re.compile(r'^[A-Za-z0-9_.\-]+(?:' + re.escape(BS) + r'[A-Za-z0-9_.\-*]+)+$')


def fwd(p):
    return p.replace(BS, '/')


def port_csproj(text):
    def hint(m):
        inner = m.group(1)
        if inner.lower().endswith('sabberstonecore.dll') and ('engine-out' in inner or 'EngineDir' in inner or 'EngineDll' in inner):
            if '$(EngineDir)' in inner:
                return '<HintPath>$(EngineDir)/SabberStoneCore.dll</HintPath>'
            if '$(EngineDll)' in inner:
                return m.group(0)
            return '<HintPath>$(EngineDll)</HintPath>'
        return m.group(0)
    text = re.sub(r'<HintPath>([^<]*)</HintPath>', hint, text)
    text = REPO.sub(lambda m: '$(SoloStoneRoot)' + fwd(m.group(1) or ''), text)
    return text


PATHISH = re.compile(r'\.(json|txt|csv|xml|md|cs|dll|exe|log)$', re.I)


def port_cs(text):
    def lit(m):
        v = m.group(2)
        line_start = text.rfind('\n', 0, m.start()) + 1
        line = text[line_start:text.find('\n', m.end()) if text.find('\n', m.end()) >= 0 else len(text)]
        s = SCRATCH.fullmatch(v)
        if s:
            return 'VerifyPaths.Fixture("' + fwd(s.group(1) or '') + '")'
        r = REPO.fullmatch(v)
        if r:
            rest = fwd(r.group(1) or '')
            if rest == '':
                return 'VerifyPaths.Root'
            if rest == 'data':
                return 'VerifyPaths.Data'
            return 'VerifyPaths.Repo("' + rest + '")'
        # a relative path: only when it names a file with an extension, or sits in a Path.Combine / R(...) call
        # (a verbatim literal like @"Deck\d" is a regex, not a path, and is left alone)
        if REL.match(v) and not v.startswith('..') and (PATHISH.search(v) or 'Path.Combine' in line or re.search(r'\bR\(', line)):
            return '"' + fwd(v) + '"'
        return m.group(0)
    # verbatim literals only: @"..."; a regular "..." literal with a path would need \\ and is reported below
    return re.sub(r'(@)"((?:[^"]|"")*)"', lit, text)


LEFT = re.compile(r'C:' + re.escape(BS) + r'|C:/|/c/|scratchpad|\.\.' + re.escape(BS) + r'|Path\.Combine\("\.\."|BaseDirectory|ProcessStartInfo|\.exe\b|cygpath')


def main():
    for folder in sys.argv[1:]:
        for root, dirs, files in os.walk(folder):
            dirs[:] = [d for d in dirs if d not in ('obj', 'out', 'bin')]
            for f in sorted(files):
                if not (f.endswith('.cs') or f.endswith('.csproj')):
                    continue
                p = os.path.join(root, f)
                with open(p, encoding='utf-8-sig', newline='') as h:
                    old = h.read()
                text = old.replace('\r\n', '\n')
                new = port_csproj(text) if f.endswith('.csproj') else port_cs(text)
                if new != old:
                    with open(p, 'w', encoding='utf-8', newline='\n') as h:
                        h.write(new)
                    print('rewrote', p)
                for i, line in enumerate(new.split('\n'), 1):
                    if LEFT.search(line) and not line.lstrip().startswith(('//', '<!--')):
                        print('  CHECK %s:%d: %s' % (p, i, line.strip()[:200]))


if __name__ == '__main__':
    main()
