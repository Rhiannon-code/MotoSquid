#!/usr/bin/env bash
# Compiles Assembly-CSharp and Assembly-CSharp-Editor outside Unity, with Unity's own compiler flags
# (from the last Bee build) and a source list regenerated from Assets/ (Bee's list goes stale).
# Works while the editor is open. Usage: tools/compile-check.sh [--self-test]
# SRC=<dir> compiles the .cs files under <dir>/Assets instead (a dry-run copy of the project).
set -u
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT" || exit 2
UNITY=~/Unity/Hub/Editor/6000.3.7f1/Editor/Data
DAG=$(ls -d Library/Bee/artifacts/*E.dag 2>/dev/null | head -1)
[ -n "$DAG" ] || { echo "FAIL: no Library/Bee/artifacts/*E.dag - open the project in Unity once"; exit 2; }
SRC=$(cd "${SRC:-$ROOT}" && pwd)
OUT=$(mktemp -d)
[ -n "${KEEP:-}" ] && echo "keeping $OUT" || trap 'rm -rf "$OUT"' EXIT

# Directories owned by an asmdef belong to that assembly, not the default ones.
find "$SRC/Assets" -name '*.asmdef' -printf '%h\n' | sed "s|^$SRC/||" | sort -u > "$OUT/asmdef-dirs"
python3 - "$OUT" "$SRC" <<'EOF'
import os, sys
out, src = sys.argv[1], sys.argv[2]
os.chdir(src)
owned = [l.strip() + "/" for l in open(os.path.join(out, "asmdef-dirs")) if l.strip()]
game, editor = [], []
for dp, dn, fn in os.walk("Assets"):
    for f in fn:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(dp, f)
        if any(p.startswith(o) for o in owned):
            continue
        (editor if "Editor" in p.split("/")[:-1] else game).append(p)
for name, files in (("game", game), ("editor", editor)):
    with open(os.path.join(out, name + ".src"), "w") as fh:
        fh.writelines(f'"{os.path.join(src, p)}"\n' for p in sorted(files))
print(f"sources: {len(game)} game, {len(editor)} editor")
EOF

if [ "${1:-}" = "--self-test" ]; then
  echo 'class CompileCheckSelfTest : UndefinedSelfTestBase { void M() { undefined_symbol(); } }' > "$OUT/SelfTest.cs"
  echo "\"$OUT/SelfTest.cs\"" >> "$OUT/game.src"
fi

compile() { # name source-rsp source-list [extra args...]
  local name=$1 rsp=$2 list=$3; shift 3
  { grep -vE '\.cs"?$|^-out:|^-refout:|^/additionalfile:' "$rsp" | grep -v 'Assembly-CSharp.ref.dll'
    for a in "$@"; do echo "$a"; done
    echo "-out:\"$OUT/$name.dll\""
    echo "-refout:\"$OUT/$name.ref.dll\""
    cat "$list"
  } > "$OUT/$name.rsp"
  "$UNITY/NetCoreRuntime/dotnet" "$UNITY/DotNetSdkRoslyn/csc.dll" "@$OUT/$name.rsp" > "$OUT/$name.log" 2>&1
  local code=$? errors
  errors=$(grep -c 'error CS' "$OUT/$name.log")
  if [ $code -ne 0 ] || [ "$errors" -ne 0 ]; then
    echo "FAIL $name (exit $code, $errors errors)"
    grep 'error CS' "$OUT/$name.log" | sed "s|$SRC/||; s|$ROOT/||" | sort -u | head -${SHOW:-60}
    return 1
  fi
  echo "ok   $name"
}

# Player flavour: the game assembly with UNITY_EDITOR undefined, which catches code, braces and usings
# on the wrong side of an #if. Package DLLs here were built for the editor and expose editor types,
# so UnityEditor.CoreModule stays referenced for metadata: a bare `using UnityEditor` slips through.
python3 - "$DAG/Assembly-CSharp.rsp" > "$OUT/player-base.rsp" <<'PY'
import os, re, sys
for line in open(sys.argv[1]):
    l = line.strip()
    if l.startswith("-define:UNITY_EDITOR"):
        continue
    if l.startswith("-r:"):
        path = l[3:].strip('"')
        name = os.path.basename(path)
        in_assets_editor = "/Assets/" in "/" + path and "/Editor/" in path.split("Assets/", 1)[-1]
        if (name.startswith("UnityEditor") and name != "UnityEditor.CoreModule.dll") or re.search(r"Editor(\.ref)?\.dll$", name) or in_assets_editor:
            continue
    sys.stdout.write(line)
PY

status=0
compile Assembly-CSharp "$DAG/Assembly-CSharp.rsp" "$OUT/game.src" || status=1
[ $status -eq 0 ] && { compile Assembly-CSharp-Player "$OUT/player-base.rsp" "$OUT/game.src" || status=1; }
if [ $status -eq 0 ]; then
  compile Assembly-CSharp-Editor "$DAG/Assembly-CSharp-Editor.rsp" "$OUT/editor.src" "-r:\"$OUT/Assembly-CSharp.ref.dll\"" || status=1
else
  echo "skip Assembly-CSharp-Editor (game assembly failed)"
fi

if [ "${1:-}" = "--self-test" ]; then
  [ $status -ne 0 ] && grep -q UndefinedSelfTestBase "$OUT/Assembly-CSharp.log" && { echo "SELF-TEST OK: the broken file was caught"; exit 0; }
  echo "SELF-TEST FAILED: the broken file was not caught, so this checker cannot be trusted"; exit 3
fi
exit $status
