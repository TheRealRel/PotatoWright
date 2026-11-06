#!/bin/bash
# Builds PotatoWright.dll with csc against the game's DLLs (publicized assembly_valheim/utils/guiutils in $REFS/pub:
# the mannequin code reads private fields), BepInEx, Harmony and Jotunn, all in REFS.
set -e
cd "$(dirname "$0")"
REFS="${REFS:-/home/claude/refs}"
CSC="${CSC:-$(ls -d /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)}"
OUT="${OUT:-build}"
python3 -I check_config_names.py src/*.cs || { echo "Build stopped: fix the setting names above."; exit 1; }
python3 -I check_patches.py src/*.cs || { echo "Build stopped: a patch class above is never switched on."; exit 1; }
python3 -I check_text.py src/*.cs || { echo "Build stopped: a text above is added to a switched-on object."; exit 1; }
mkdir -p "$OUT"
R=""
for f in mscorlib System System.Core netstandard UnityEngine UnityEngine.CoreModule UnityEngine.AudioModule UnityEngine.PhysicsModule UnityEngine.AssetBundleModule UnityEngine.ParticleSystemModule UnityEngine.InputLegacyModule UnityEngine.SharedInternalsModule UnityEngine.UI UnityEngine.UIModule UnityEngine.AnimationModule UnityEngine.ClothModule UnityEngine.TextRenderingModule UnityEngine.ImageConversionModule UnityEngine.IMGUIModule assembly_valheim assembly_guiutils assembly_utils Unity.TextMeshPro 0Harmony BepInEx Jotunn; do
  if [ -f "$REFS/pub/$f.dll" ]; then R="$R -r:$REFS/pub/$f.dll"; else R="$R -r:$REFS/$f.dll"; fi
done
dotnet "$CSC" -nologo -noconfig -nostdlib -target:library -langversion:9.0 -optimize+ -deterministic \
  -out:"$OUT/PotatoWright.dll" $R src/*.cs
sha256sum "$OUT/PotatoWright.dll"
