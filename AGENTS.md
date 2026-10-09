# Project instructions

## Active project

- The user switched back to Unity. Work in `C:/Users/MSI/what-the-farm`.
- Use Unity 6000.3.25f1 with URP. Open `Assets/Scenes/FirstFarm.unity`.
- Gameplay lives in `Assets/Scripts/Prototype`. Configure content with Unity prefabs and ScriptableObjects.
- The Unreal project (`whatthefarm.uproject`, `Source`, `Content`, `Config`, `Scripts`) is retained reference work. Do not implement new gameplay there unless the user asks.
- Preserve user edits to scenes, materials and render pipeline settings. Do not regenerate edited maps with the initial builders.
- Keep `.meta` files with Unity assets. Do not track Library, Temp, Logs, UserSettings or generated IDE files.

## Gameplay and migration

- Preserve aim-point planting, one plant per tilled area, watering to begin growth, environment bonuses to final size, grown harvest size, tool range scaling, inventory, restocking and NPC sale.
- Intended design from Unreal work: each item may be planted only once; small loose items do not block the player; FirstFarm alone has a boundary monster that unlocks stage travel. These are not yet present in the restored Unity baseline; see README migration status.
- Existing Unreal animations and replacement models require a supported export/import workflow before Unity can use them. Do not imply that `.uasset` files work directly in Unity.
- Current gameplay is a local single-player prototype. Multiplayer, paid shops and disk saves are not implemented.
- Use the installed Unity editor to compile and validate changes. Existing editor validation entry point: `ToolSizeValidation.Validate` (matches the restored baseline, including repeat planting).
