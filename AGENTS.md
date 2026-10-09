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
- Intended design from Unreal work: each item may be planted only once; small loose items do not block the player; FirstFarm alone has an encounter using the existing merchant: hitting the merchant or crossing the boundary makes them hostile; defeat unlocks stage travel. Do not spawn a separate boundary monster. These are implemented in Unity; preserve the single-planting rule and restrict the encounter to FirstFarm.
- The user explicitly forbids importing assets used in Unreal. Use Unity assets only. The new Unity packs contain no character action animations; optional Animator hooks do not constitute working animations.
- Current gameplay is a local single-player prototype. Multiplayer, paid shops and disk saves are not implemented.
- Dead NPC bodies are inventory items: plant and water the original body to restore the same NPC and its trade functions. Body pickup must cover the complete posed mesh; plant bodies upright and let revived NPCs walk back to their original home around scene obstacles. Corpses are unsellable; each new death creates a fresh plantable body. Keep stage clearance after revival. PlantableCorpse retains owner identity; Player Revival Requires Planting is opt-in for local cooperative revival groundwork, not implemented network multiplayer.
- Use the installed Unity editor to compile and validate changes. Unity may remain open; use the live editor migration bridge and `UnityMigrationPlayCheck.Run` for integration checks. Do not require closing the editor for ordinary C# changes. `ToolSizeValidation.Validate` also verifies growth size and linear tool ranges with single planting.
