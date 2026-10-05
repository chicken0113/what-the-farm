"""Run with UnrealEditor project.uproject -ExecutePythonScript=Scripts/setup_farm.py -d3d11.
Creates the initial farm only when it does not exist; keeps edited maps intact.
Optional WTF_LEGACY_ASSETS points to the archived Unity Assets directory.
"""
import os
from pathlib import Path
import unreal as ue

ROOT = "/Game/Farm"
MAP = ROOT + "/Maps/FirstFarm"
assets = ue.AssetToolsHelpers.get_asset_tools()
library = ue.EditorAssetLibrary
actors = ue.get_editor_subsystem(ue.EditorActorSubsystem)
levels = ue.get_editor_subsystem(ue.LevelEditorSubsystem)

def save(obj):
    library.save_loaded_asset(obj, only_if_is_dirty=False)
    return obj

def material(name, color=None, texture=None):
    path = ROOT + "/Materials/" + name
    existing = library.load_asset(path)
    if existing:
        return existing
    mat = assets.create_asset(name, ROOT + "/Materials", ue.Material, ue.MaterialFactoryNew())
    if texture:
        node = ue.MaterialEditingLibrary.create_material_expression(mat, ue.MaterialExpressionTextureSample, -400, 0)
        node.set_editor_property("texture", texture)
        ue.MaterialEditingLibrary.connect_material_property(node, "RGB", ue.MaterialProperty.MP_BASE_COLOR)
    else:
        node = ue.MaterialEditingLibrary.create_material_expression(mat, ue.MaterialExpressionVectorParameter, -400, 0)
        node.set_editor_property("parameter_name", "Tint")
        node.set_editor_property("default_value", ue.LinearColor(*color, 1))
        ue.MaterialEditingLibrary.connect_material_property(node, "", ue.MaterialProperty.MP_BASE_COLOR)
    rough = ue.MaterialEditingLibrary.create_material_expression(mat, ue.MaterialExpressionConstant, -400, 200)
    rough.set_editor_property("r", .8)
    ue.MaterialEditingLibrary.connect_material_property(rough, "", ue.MaterialProperty.MP_ROUGHNESS)
    ue.MaterialEditingLibrary.recompile_material(mat)
    return save(mat)

base = material("M_Color", (.5, .5, .5))
mats = {name: material("M_"+name, color) for name, color in {
    "Ground": (.22,.30,.12), "Path": (.58,.46,.24), "Wood": (.22,.10,.035),
    "Water": (.015,.25,.65), "Shop": (.85,.65,.035), "Sell": (.85,.19,.025),
    "Exit": (.36,.04,.65), "Seed": (.8,.57,.025), "Can": (.015,.35,.7),
    "Metal": (.32,.38,.42), "Rock": (.27,.29,.32), "Leaf": (.05,.3,.055)
}.items()}

def profile(name, light, water, soil, bonuses):
    path = ROOT + "/Growth/" + name
    obj = library.load_asset(path)
    if obj:
        return obj
    factory = ue.DataAssetFactory()
    factory.set_editor_property("data_asset_class", ue.FarmGrowthProfile)
    obj = assets.create_asset(name, ROOT + "/Growth", ue.FarmGrowthProfile, factory)
    obj.set_editor_property("light_range", ue.Vector2D(*light))
    obj.set_editor_property("water_range", ue.Vector2D(*water))
    obj.set_editor_property("preferred_soils", [soil])
    for prop, value in zip(("light_bonus", "water_bonus", "soil_bonus"), bonuses):
        obj.set_editor_property(prop, value)
    return save(obj)

profiles = {
    "Seed": profile("Seed", (60,100),(40,80),"Loam",(25,25,20)),
    "Produce": profile("Produce",(50,100),(40,90),"Loam",(20,20,20)),
    "Hoe": profile("Hoe",(0,40),(20,60),"Clay",(15,20,30)),
    "WateringCan": profile("WateringCan",(20,70),(70,100),"Paddy",(20,35,25)),
    "Stone": profile("Stone",(60,100),(0,30),"Sand",(10,15,35))
}

legacy = Path(os.environ.get("WTF_LEGACY_ASSETS", str(Path(ue.Paths.project_dir()) / "Assets")))
def import_file(relative, name, is_mesh=False):
    path = ROOT + "/Imported/" + name
    if library.does_asset_exist(path):
        return library.load_asset(path)
    source = legacy / relative
    if not source.exists():
        ue.log_warning("Optional legacy model missing: " + str(source))
        return None
    task = ue.AssetImportTask()
    task.set_editor_property("filename", str(source))
    task.set_editor_property("destination_path", ROOT + "/Imported")
    task.set_editor_property("destination_name", name)
    task.set_editor_property("automated", True)
    task.set_editor_property("save", True)
    if is_mesh:
        options = ue.FbxImportUI()
        options.set_editor_property("automated_import_should_detect_type", False)
        options.set_editor_property("mesh_type_to_import", ue.FBXImportType.FBXIT_STATIC_MESH)
        options.set_editor_property("import_as_skeletal", False)
        options.set_editor_property("import_materials", True)
        options.set_editor_property("import_textures", True)
        options.static_mesh_import_data.set_editor_property("combine_meshes", True)
        options.static_mesh_import_data.set_editor_property("auto_generate_collision", True)
        task.set_editor_property("options", options)
    assets.import_asset_tasks([task])
    paths = task.get_editor_property("imported_object_paths")
    for imported_path in paths:
        obj = library.load_asset(imported_path)
        if (is_mesh and isinstance(obj, ue.StaticMesh)) or (not is_mesh and isinstance(obj, ue.Texture2D)):
            return obj
    raise RuntimeError("Import did not produce expected asset: " + str(source))

nature_texture = import_file("SimpleNaturePack/Textures/NaturePackLite_Texture_01.png", "NatureAtlas")
weapon_texture = import_file("Low Poly Weapon Series V 4/Texture/Low Poly Weapon.png", "WeaponAtlas")
nature_mat = material("M_Nature", texture=nature_texture) if nature_texture else mats["Leaf"]
weapon_mat = material("M_Weapon", texture=weapon_texture) if weapon_texture else mats["Metal"]
models = {}
for name, source in {
    "Tree": "SimpleNaturePack/Models/Tree_01.fbx",
    "Rock": "SimpleNaturePack/Models/Rock_01.fbx",
    "Stone": "SimpleNaturePack/Models/Rock_02.fbx",
    "Hoe": "Low Poly Weapon Series V 4/Models/Low Poly Series V 4 New/Close Combat/Shovel_A.fbx",
    "Buyer": "Floreswa/Models/male01_1.fbx"
}.items():
    models[name] = import_file(source, "SM_"+name, True)
    if models[name] and name != "Buyer":
        for index in range(len(models[name].get_editor_property("static_materials"))):
            models[name].set_material(index, weapon_mat if name == "Hoe" else nature_mat)
        save(models[name])

def blueprint(name, parent):
    path = ROOT + "/Blueprints/" + name
    bp = library.load_asset(path)
    if bp:
        return bp, ue.get_default_object(bp.generated_class()), False
    factory = ue.BlueprintFactory()
    factory.set_editor_property("parent_class", parent)
    bp = assets.create_asset(name, ROOT + "/Blueprints", ue.Blueprint, factory)
    return bp, ue.get_default_object(bp.generated_class()), True

character, character_defaults, fresh = blueprint("BP_Farmer", ue.FarmCharacter)
if fresh:
    character_defaults.set_editor_property("inventory_slots", 12)
    save(character)
mode, mode_defaults, fresh = blueprint("BP_FarmGameMode", ue.FarmGameMode)
if fresh:
    mode_defaults.set_editor_property("default_pawn_class", character.generated_class())
    save(mode)

cube = library.load_asset("/Engine/BasicShapes/Cube")
sphere = library.load_asset("/Engine/BasicShapes/Sphere")
cylinder = library.load_asset("/Engine/BasicShapes/Cylinder")
item_bps = {}
for name, kind, scale, value, shape, mat in [
    ("Seed", ue.FarmKind.SEED, (.5,.5,.5), 10, sphere, mats["Seed"]),
    ("Produce", ue.FarmKind.PRODUCE, (.65,.65,.65), 10, cube, mats["Leaf"]),
    ("Hoe", ue.FarmKind.HOE, (.17,.17,1), 16, cylinder, mats["Metal"]),
    ("WateringCan", ue.FarmKind.WATERING_CAN, (.36,.36,.56), 14, cylinder, mats["Can"]),
    ("Stone", ue.FarmKind.STONE, (.5,.5,.5), 6, cube, mats["Rock"])
]:
    bp, defaults, fresh = blueprint("BP_"+name, ue.FarmItem)
    item_bps[name] = bp
    if fresh:
        defaults.set_editor_property("kind", kind)
        defaults.set_editor_property("base_value", value)
        defaults.set_editor_property("growth_profile", profiles[name])
        defaults.get_editor_property("mesh").set_static_mesh(shape)
        defaults.get_editor_property("mesh").set_material(0, mat)
        defaults.get_editor_property("mesh").set_relative_scale3d(ue.Vector(*scale))
        save(bp)

map_exists = library.does_asset_exist(MAP)
if map_exists:
    levels.load_level(MAP)
needs_map = not map_exists or not any(isinstance(a, ue.FarmSoil) for a in actors.get_all_level_actors())
if needs_map:
    if not map_exists and not levels.new_level(MAP):
        raise RuntimeError("Could not create FirstFarm")
    world = ue.get_editor_subsystem(ue.UnrealEditorSubsystem).get_editor_world()
    world.get_world_settings().set_editor_property("default_game_mode", mode.generated_class())
    def spawn(cls, name, position, scale=None, rotation=(0,0,0)):
        actor = actors.spawn_actor_from_class(cls, ue.Vector(*position), ue.Rotator(*rotation))
        actor.set_actor_label(name)
        if scale is not None:
            actor.set_actor_scale3d(ue.Vector(*scale))
        return actor
    def block(name, position, scale, mat):
        actor = spawn(ue.StaticMeshActor, name, position, scale)
        component = actor.static_mesh_component
        component.set_static_mesh(cube)
        component.set_material(0, mat)
        component.set_collision_profile_name("BlockAll")
        return actor
    def imported(name, mesh, position, height, fallback):
        if not mesh:
            return block(name, (position[0],position[1],height/2), (1,1,height/100), fallback)
        actor = spawn(ue.StaticMeshActor, name, position)
        component = actor.static_mesh_component
        component.set_static_mesh(mesh)
        _, extents = actor.get_actor_bounds(False)
        actor.set_actor_scale3d(ue.Vector(*(height / max(.01, extents.z*2),)*3))
        origin, extents = actor.get_actor_bounds(False)
        actor.set_actor_location(ue.Vector(position[0]-origin.x+actor.get_actor_location().x,
                                           position[1]-origin.y+actor.get_actor_location().y,
                                           position[2]-origin.z+extents.z+actor.get_actor_location().z), False, False)
        component.set_collision_profile_name("BlockAll")
        return actor
    soil = spawn(ue.FarmSoil,"Farm Ground - Loam",(0,0,-12.5),(26,26,.25))
    soil.ground.set_material(0,mats["Ground"])
    block("South Path",(0,-700,1.5),(22,2,.03),mats["Path"])
    block("North Path",(0,600,1.5),(22,2,.03),mats["Path"])
    block("Water",(-800,0,2.5),(3,6,.05),mats["Water"])
    block("Bridge",(-800,0,10),(4,2,.2),mats["Wood"])
    for x in range(-1200,1201,200):
        for y in (-1300,1300):
            if y==1300 and abs(x)<100:
                continue
            block("Fence",(x,y,60),(2,.15,1.2),mats["Wood"])
        for side in (-1300,1300):
            block("Fence",(side,x,60),(.15,2,1.2),mats["Wood"])
    for x,y in ((-1000,-1000),(1000,-1000),(-1100,400),(1100,400)):
        imported("Tree",models["Tree"],(x,y,0),400,mats["Leaf"])
    for x,y in ((800,0),(1000,-300)):
        imported("Rock",models["Rock"],(x,y,0),130,mats["Rock"])
    for x,mat,name in ((-900,mats["Shop"],"Shop"),(900,mats["Sell"],"Sell")):
        block(name+" Counter",(x,900,50),(2,1,1),mat)
        block(name+" Roof",(x,900,200),(2.4,1.5,.2),mat)
        for dx in (-90,90):
            block(name+" Support",(x+dx,940,100),(.15,.15,2),mats["Wood"])
    block("Region Exit",(0,1250,150),(3,.2,3),mats["Exit"])
    buyer = spawn(ue.FarmMerchant,"Farm Buyer",(900,750,95),(.65,.4,1.9))
    buyer.get_editor_property("body").set_material(0,mats["Sell"])
    buyer.get_editor_property("receiver").set_absolute(False,False,True)
    buyer.get_editor_property("receiver").set_world_scale3d(ue.Vector(1,1,1))
    if models["Buyer"]:
        model = imported("Buyer Visual",models["Buyer"],(900,750,0),180,mats["Sell"])
        buyer.get_editor_property("body").set_visibility(False)
        model.static_mesh_component.set_mobility(ue.ComponentMobility.MOVABLE)
        model.attach_to_actor(buyer,"",ue.AttachmentRule.KEEP_WORLD,ue.AttachmentRule.KEEP_WORLD,ue.AttachmentRule.KEEP_WORLD,False)
        model.static_mesh_component.set_collision_enabled(ue.CollisionEnabled.NO_COLLISION)
    for name,x in (("Hoe",-250),("WateringCan",-120),("Seed",0),("Seed",100),("Seed",200),("Stone",320)):
        item = spawn(item_bps[name].generated_class(),"Supply - "+name,(x,-700,60))
        item.set_editor_property("restock_on_pickup", True)
        if name in models and models[name]:
            mesh = item.mesh
            mesh.set_static_mesh(models[name])
            item.set_actor_scale3d(ue.Vector(1,1,1))
            origin, ext = item.get_actor_bounds(False)
            if name=="Hoe":
                if ext.x>ext.z and ext.x>=ext.y:
                    item.set_actor_rotation(ue.Rotator(90,0,0),False)
                elif ext.y>ext.z:
                    item.set_actor_rotation(ue.Rotator(0,0,90),False)
                origin, ext = item.get_actor_bounds(False)
            height = 90 if name=="Hoe" else 50
            item.set_actor_scale3d(ue.Vector(*(height/max(.01,ext.z*2),)*3))
            mesh.set_material(0,weapon_mat if name=="Hoe" else nature_mat)
    spawn(ue.PlayerStart,"Farmer Spawn",(0,-950,100),rotation=(0,90,0))
    sun=spawn(ue.DirectionalLight,"Sun",(0,0,800),rotation=(-55,-35,0))
    sun.light_component.set_editor_property("intensity",3)
    sun.light_component.set_editor_property("mobility",ue.ComponentMobility.MOVABLE)
    sky=spawn(ue.SkyLight,"Sky Light",(0,0,500))
    sky.light_component.set_editor_property("intensity",1)
    sky.light_component.set_editor_property("mobility",ue.ComponentMobility.MOVABLE)
    sky.light_component.set_editor_property("source_type",ue.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
    sky.light_component.set_editor_property("cubemap",library.load_asset("/Engine/MapTemplates/Sky/DaylightAmbientCubemap"))
    spawn(ue.SkyAtmosphere,"Sky Atmosphere",(0,0,0))
    levels.save_current_level()
    ue.log("FirstFarm created with editable soil, supplies and buyer.")
else:
    ue.log("Existing FirstFarm preserved.")
    placed = actors.get_all_level_actors()
    buyer = next((a for a in placed if isinstance(a, ue.FarmMerchant)), None)
    visual = next((a for a in placed if a.get_actor_label() == "Buyer Visual"), None)
    if buyer and visual:
        visual.static_mesh_component.set_mobility(ue.ComponentMobility.MOVABLE)
        visual.attach_to_actor(buyer,"",ue.AttachmentRule.KEEP_WORLD,ue.AttachmentRule.KEEP_WORLD,ue.AttachmentRule.KEEP_WORLD,False)
        levels.save_current_level()
library.save_directory(ROOT, only_if_is_dirty=False, recursive=True)
ue.log("FARM_SETUP_SUCCESS")
