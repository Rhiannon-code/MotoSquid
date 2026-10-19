#!/usr/bin/env bash
# Builds and runs the road harness: the proof-of-concept layout, surface builder and road checker, outside Unity.
# Usage: bash tools/road-harness/run.sh               the proof-of-concept course
#        bash tools/road-harness/run.sh designed      the designed West Gate / CityLink-style interchange
#        bash tools/road-harness/run.sh corridor      the West Gate corridor: sunken + raised diamonds, system interchanges, earthworks
#        bash tools/road-harness/run.sh cloverstack   the cloverstack: two freeways crossing, loops, S-ramps and flyovers
#        bash tools/road-harness/run.sh mergesplit   a freeway ending on another with a merge / split (two branches, one flyover)
#        bash tools/road-harness/run.sh plan         the World Plan (RoadData/world_plan.json, or PLAN=<file>) with its interchanges
#                                                    PLANONLY=1: the plan check alone, no surface or road checker
#        bash tools/road-harness/run.sh free         free straight stretches left on each freeway (where a diamond fits)
#        bash tools/road-harness/run.sh world         the open world from RoadData/world_roads.txt (tools/roads/build_world.py)
#        bash tools/road-harness/run.sh interchange   the real-data interchange (RoadData/spline_roads.txt)
# Writes out/surface.obj (+ -barriers.obj, -ground.obj, -centrelines.csv) and prints the checker report.
# Diagnostics: DENSITY=1 (adaptive-mesh triangle estimate), PROFILE="<road>" (height/bank/curvature every 4 m),
#              PROBE="<road>:<lateral>:<from>:<to>" (built surface vs design height along a line).
set -euo pipefail
T=$(cd "$(dirname "$0")" && pwd); P=$(cd "$T/../.." && pwd); O="$T/out"
U=~/Unity/Hub/Editor/6000.3.7f1/Editor/Data; R=$U/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21
CSC=("$U/NetCoreRuntime/dotnet" "$U/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -langversion:9 -unsafe -nowarn:CS1701,CS1702,CS8632)
mkdir -p "$O"
refs=(); for f in "$R"/*.dll; do refs+=("-r:$f"); done
for f in "$U/Managed/UnityEngine/UnityEngine.CoreModule.dll" "$U/Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll" "$U/Managed/UnityEngine/UnityEngine.PhysicsModule.dll" "$P/Library/ScriptAssemblies/Unity.Mathematics.dll"; do
  refs+=("-r:$f"); cp -f "$f" "$O/"
done
# Unity's editor build of Splines calls into the editor, so compile its runtime source without UNITY_EDITOR.
S=$(ls -d "$P"/Library/PackageCache/com.unity.splines@*/Runtime)
mapfile -d '' splines < <(find "$S" -name '*.cs' -print0)
"${CSC[@]}" -target:library -out:"$O/Unity.Splines.dll" "${refs[@]}" -r:"$P/Library/ScriptAssemblies/Unity.Burst.dll" "${splines[@]}" | grep -E "error" || true
mapfile -d '' roads < <(find "$P/Assets/Scripts/Roads" -name '*.cs' -not -path '*/Editor/*' -print0)
"${CSC[@]}" -out:"$O/Harness.dll" "${refs[@]}" -r:"$O/Unity.Splines.dll" "$T/Program.cs" "${roads[@]}" | grep -E "error" && exit 1 || true
echo '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"}}}' > "$O/Harness.runtimeconfig.json"
cd "$O" && exec "$U/NetCoreRuntime/dotnet" Harness.dll "$O/surface.obj" "${1:-poc}" "$P" "${@:2}"
