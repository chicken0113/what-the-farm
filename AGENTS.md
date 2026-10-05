# Project instructions

## Active project

- Work in the user-created Unreal project at the repository root: `whatthefarm.uproject`.
- Engine version: Unreal Engine 5.7.4. Module name: `whatthefarm`.
- Keep the existing First Person template and user-created assets unless a requested feature requires changing them.
- Unity `Assets`, `Packages`, and `ProjectSettings` are a retained prototype. They are intentionally excluded from this checkout using sparse checkout. Do not remove them from Git or implement new features there.

## Development

- Implement gameplay in C++ with properties and functions exposed to the editor / Blueprints where the user needs control.
- Preserve the game's existing design: any obtainable item can be planted; one plant per tilled area; plant at the aim point; environment affects final size; harvest retains grown size; tool range scales linearly with grown size.
- New Unreal project currently contains the template only. Do not describe Unity prototype features as working in Unreal until implemented and checked.
- Keep generated `Binaries`, `Intermediate`, `Saved`, and `DerivedDataCache` out of Git. Track `Config`, `Content`, `Source` and the project descriptor.
- Build with UnrealBuildTool for `whatthefarmEditor Win64 Development` when changing C++.
- Do not directly edit binary `.uasset` / `.umap` files as text. Use the Unreal editor or supported editor automation.
