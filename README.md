# PotatoWright

Generic building pieces for Valheim: Ask and Embla, wooden mannequins in every stance and emote, and ropes and chains that hang between any two points. Player docs: [package/README.md](package/README.md).

## Layout
- `src/Plugin.cs` – plugin setup, translations.
- `src/MannequinPieces.cs` – the 80 mannequin pieces (copies of the game's ArmorStand_Male/_Female, one per stance or emote), their effects and cape colliders cleaned; `MannequinColor.cs` the wood colour and paint layers; `MannequinPanel.cs` the colour and base panel beside the build menu; `MannequinMenu.cs` the fold-open Mannequins entry; `MannequinIcons.cs` draws each stance's icon in game (rendered, then shaded in the emote icons' orange).
- `src/Swag.cs` – ropes and chains: the pieces, building the rope (a twisted tube, PotatoFarmer's hitching post rope) or the links, the two-click placing with the wheel for sag, and the end check that drops one whose end goes. `SwagIcons.cs` draws their hammer icons in game. `SwagWear.cs` keeps support and weather damage off them (needed on ships with Build On Ship).
- `tools/shade.py` – the offline version of the stance icon shading, for tuning on saved renders.
- `package/` – Thunderstore files.

## Build
`bash build.sh` with `REFS` pointing at the game's Managed DLLs, BepInEx, 0Harmony and Jötunn, with publicized `assembly_valheim`, `assembly_utils` and `assembly_guiutils` in `$REFS/pub` (the mannequin code reads private fields; compiled with `-unsafe+` so Mono skips the access checks). The build runs `check_config_names.py`, `check_patches.py` and `check_text.py` first.

Never rename a piece's prefab (`PotatoWright_...`) or a ZDO key after release: worlds save them.
