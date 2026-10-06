# Project instructions

## Active project

- Work in the user-created Unreal project at the repository root: `whatthefarm.uproject`.
- Engine version: Unreal Engine 5.7.4. Module name: `whatthefarm`.
- Keep the existing First Person template and user-created assets unless a requested feature requires changing them.
- Unity `Assets`, `Packages`, and `ProjectSettings` are a retained prototype. They are intentionally excluded from this checkout using sparse checkout. Do not remove them from Git or implement new features there.

## Development

- Implement gameplay in C++ with properties and functions exposed to the editor / Blueprints where the user needs control.
- Preserve the game's existing design: any obtainable item can be planted; one plant per tilled area; plant at the aim point; environment affects final size; harvest retains grown size; tool range scales linearly with grown size.
- Each individual item can be planted only once. Harvested objects retain their planting history across pickup and throw and cannot be replanted. Fresh supplies may still use a freed plot.
- The farm prototype lives in `Source/whatthefarm/Farming` and `Content/Farm/Maps/FirstFarm`. It is local single-player; multiplayer, paid shop, persistence and region travel are not implemented.
- Designer settings live in `Content/Farm/Blueprints` and `Content/Farm/Growth`. Preserve edited maps/assets; the setup script is for initial generation.
- Keep generated `Binaries`, `Intermediate`, `Saved`, and `DerivedDataCache` out of Git. Track `Config`, `Content`, `Source` and the project descriptor.
- Build with UnrealBuildTool for `whatthefarmEditor Win64 Development` when changing C++.
- Use `-NoUBA -MaxParallelActions=1` on this machine to avoid low-memory build failures. Close the editor before replacing its module DLL.
- Native automation tests are `WhatTheFarm.Farming`. `Scripts/verify_farm.py` checks PIE actions. Editor asset placement and screenshots require a rendering editor (`-d3d11`), not NullRHI.
- Do not directly edit binary `.uasset` / `.umap` files as text. Use the Unreal editor or supported editor automation.
