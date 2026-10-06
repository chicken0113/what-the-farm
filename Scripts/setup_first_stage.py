"""Expand FirstFarm and create a travel-only StageTwo placeholder. Keeps existing props."""
import unreal as ue
assets=ue.EditorAssetLibrary
actors=ue.get_editor_subsystem(ue.EditorActorSubsystem)
levels=ue.get_editor_subsystem(ue.LevelEditorSubsystem)
tools=ue.AssetToolsHelpers.get_asset_tools()

def material(name,color):
    path="/Game/Farm/Materials/"+name
    existing=assets.load_asset(path)
    if existing: return existing
    mat=tools.create_asset(name,"/Game/Farm/Materials",ue.Material,ue.MaterialFactoryNew())
    node=ue.MaterialEditingLibrary.create_material_expression(mat,ue.MaterialExpressionVectorParameter,-300,0)
    node.set_editor_property("parameter_name","Tint")
    node.set_editor_property("default_value",ue.LinearColor(*color,1))
    ue.MaterialEditingLibrary.connect_material_property(node,"",ue.MaterialProperty.MP_BASE_COLOR)
    ue.MaterialEditingLibrary.recompile_material(mat); assets.save_loaded_asset(mat,False); return mat

def spawn(cls,name,pos,scale=None):
    a=actors.spawn_actor_from_class(cls,ue.Vector(*pos)); a.set_actor_label(name)
    if scale: a.set_actor_scale3d(ue.Vector(*scale))
    return a

red=material("M_Guardian",(.65,.035,.025))
outer=material("M_OuterGround",(.10,.17,.065))
bp=assets.load_asset("/Game/Farm/Blueprints/BP_Guardian")
if not bp:
    factory=ue.BlueprintFactory(); factory.set_editor_property("parent_class",ue.FarmStageMonster)
    bp=tools.create_asset("BP_Guardian","/Game/Farm/Blueprints",ue.Blueprint,factory)
    ue.get_default_object(bp.generated_class()).body.set_material(0,red)
    assets.save_loaded_asset(bp,False)
levels.load_level("/Game/Farm/Maps/FirstFarm")
placed=actors.get_all_level_actors()
if not any(isinstance(a,ue.FarmFirstStage) for a in placed):
    soil=next(a for a in placed if isinstance(a,ue.FarmSoil))
    soil.set_actor_scale3d(ue.Vector(100,100,.25)); soil.ground.set_material(0,outer)
    for a in placed:
        if a.get_actor_label().startswith("Fence") or a.get_actor_label()=="Region Exit": actors.destroy_actor(a)
    centre=spawn(ue.StaticMeshActor,"Original Farm Area - Visual Only",(0,0,.25),(26,26,.005))
    centre.static_mesh_component.set_static_mesh(assets.load_asset("/Engine/BasicShapes/Cube"))
    centre.static_mesh_component.set_material(0,assets.load_asset("/Game/Farm/Materials/M_Ground"))
    centre.static_mesh_component.set_collision_profile_name("NoCollision")
    centre.static_mesh_component.set_collision_enabled(ue.CollisionEnabled.NO_COLLISION)
    director=spawn(ue.FarmFirstStage,"First Stage - Old Fence Boundary",(0,0,0))
    director.set_editor_property("monster_class",bp.generated_class())
    gate=spawn(ue.FarmStageExit,"First Stage Exit - Defeat Guardian",(0,3000,150),(3,.2,3))
    gate.set_editor_property("first_stage",director)
    gate.door.set_material(0,assets.load_asset("/Game/Farm/Materials/M_Exit"))
    levels.save_current_level()

if not assets.does_asset_exist("/Game/Farm/Maps/StageTwo"):
    levels.new_level("/Game/Farm/Maps/StageTwo")
    world=ue.get_editor_subsystem(ue.UnrealEditorSubsystem).get_editor_world()
    world.get_world_settings().set_editor_property("default_game_mode",assets.load_asset("/Game/Farm/Blueprints/BP_FarmGameMode").generated_class())
    soil=spawn(ue.FarmSoil,"Stage Two Ground - Placeholder",(0,0,-12.5),(60,60,.25))
    soil.ground.set_material(0,assets.load_asset("/Game/Farm/Materials/M_Ground"))
    spawn(ue.PlayerStart,"Stage Two Spawn",(0,0,100))
    sun=spawn(ue.DirectionalLight,"Sun",(0,0,800))
    sun.set_actor_rotation(ue.Rotator(-55,-35,0),False)
    sun.light_component.set_editor_property("intensity",3)
    sun.light_component.set_editor_property("mobility",ue.ComponentMobility.MOVABLE)
    sky=spawn(ue.SkyLight,"Sky Light",(0,0,500))
    sky.light_component.set_editor_property("mobility",ue.ComponentMobility.MOVABLE)
    sky.light_component.set_editor_property("source_type",ue.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
    sky.light_component.set_editor_property("cubemap",assets.load_asset("/Engine/MapTemplates/Sky/DaylightAmbientCubemap"))
    # This level deliberately has no FarmFirstStage actor or boundary encounter.
    levels.save_current_level()
levels.load_level("/Game/Farm/Maps/FirstFarm")
ue.log("FIRST_STAGE_SETUP_SUCCESS")
