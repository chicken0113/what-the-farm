# Project instructions

## Active project

- The user switched back to Unity. Work in `C:/Users/MSI/what-the-farm`.
- Use Unity 6000.3.25f1 with URP. Open `Assets/Scenes/FirstFarm.unity`.
- Gameplay lives in `Assets/Scripts/Prototype`. Configure content with Unity prefabs and ScriptableObjects.
- The Unreal project (`whatthefarm.uproject`, `Source`, `Content`, `Config`, `Scripts`) is retained reference work. Do not implement new gameplay there unless the user asks.
- Preserve user edits to scenes, materials and render pipeline settings. Do not regenerate edited maps with the initial builders.
- Keep `.meta` files with Unity assets. Do not track Library, Temp, Logs, UserSettings or generated IDE files.

## Gameplay and migration

- Planting requires the entire visible item footprint (horizontal X/Z bounds in its planted orientation and current size, including off-centre pivots) to fit inside the selected tilled area with a 5mm margin. Apply to ordinary items and bodies, reject without consuming the item, and keep the mouse position. Use the stored gameplay radius for irregular visual outlines.
- The Unity Shovel_A tool (Hoe.prefab) is 1.5 times its earlier size. New stock is a blade-only GrowableTool and cannot dig or attack until planted, watered, fully grown and harvested. Planting buries the original complete mesh with only the blade exposed. Shovel growth raises the whole unchanged-size model until the handle is out of the soil; no size growth or maturity recolouring. Harvest retains its complete mesh, unchanged size and linear tool range. Bare hands can prepare soil for the blade. Keep restocked tools in blade/unusable state.
- Maturity must preserve original colours/material appearance for all plants and items; no orange maturity tint.
- Preserve aim-point planting, one plant per tilled area, watering to begin growth, environment bonuses to final size, grown harvest size, tool range scaling, inventory, restocking and NPC sale.
- Intended design from Unreal work: each item may be planted only once; small loose items do not block the player; FirstFarm alone has an encounter using the existing merchant: hitting the merchant or crossing the boundary makes them hostile; defeat unlocks stage travel. Do not spawn a separate boundary monster. These are implemented in Unity; preserve the single-planting rule and restrict the encounter to FirstFarm.
- The user explicitly forbids importing assets used in Unreal. Use Unity assets only. The new Unity packs contain no character action animations; optional Animator hooks do not constitute working animations.
- The merchant uses the existing Unity WWII_SMG_B via MerchantSMG.prefab while hostile. Keep finite-speed bullets with a fixed direction at firing time, swept collision along each frame of travel, solid cover blocking shots, procedural aiming/flash/tracer/recoil, and hide the gun while peaceful/dead. No imported Unreal assets or asset animation claims.
- Current gameplay is a local single-player prototype. Multiplayer, paid shops and disk saves are not implemented.
- Dead NPC bodies are inventory items: plant the original body to gradually recover its actual health and revive the same NPC and trade functions at full HP. Bodies do not grow or require water. Body pickup must cover the complete posed mesh; plant bodies upright with the lower half buried, keep their size fixed, and let revived NPCs walk back to their original home around scene obstacles. Corpses are unsellable; each new death creates a fresh plantable body. Keep stage clearance after revival. PlantableCorpse retains owner identity; Player Revival Requires Planting is opt-in for local cooperative revival groundwork, not implemented network multiplayer.
- Use the installed Unity editor to compile and validate changes. Unity may remain open; use the live editor migration bridge and `UnityMigrationPlayCheck.Run` for integration checks. Do not require closing the editor for ordinary C# changes. `ToolSizeValidation.Validate` also verifies growth size and linear tool ranges with single planting.
