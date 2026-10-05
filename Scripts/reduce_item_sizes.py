"""One-time migration: reduce existing farm item defaults and displays to 30%.
Preserves map placement in XY and rests the supplies on their existing surface.
"""
import unreal as ue

assets = ue.EditorAssetLibrary
actors = ue.get_editor_subsystem(ue.EditorActorSubsystem)
levels = ue.get_editor_subsystem(ue.LevelEditorSubsystem)
marker = assets.load_asset("/Game/Farm/Blueprints/BP_Seed")
if assets.get_metadata_tag(marker,"WTFSmallItems30") == "1":
    ue.log("ITEM_SIZE_MIGRATION_ALREADY_APPLIED")
else:
    levels.load_level("/Game/Farm/Maps/FirstFarm")
    items = [(a,a.get_actor_scale3d(),a.get_actor_location())
             for a in actors.get_all_level_actors() if isinstance(a,ue.FarmItem)]
    floors = [a.get_actor_bounds(True) for a in actors.get_all_level_actors()
              if isinstance(a,(ue.StaticMeshActor,ue.FarmSoil))]
    for actor,scale,position in items:
        actor.set_actor_scale3d(scale*.3)
        # The first map has flat block surfaces. Find the highest supporting
        # block under this display, excluding other items from the calculation.
        surfaces = [center.z+extent.z for center,extent in floors
                    if abs(position.x-center.x)<=extent.x and abs(position.y-center.y)<=extent.y
                    and center.z+extent.z<=position.z]
        ground_z = max(surfaces) if surfaces else position.z
        origin,extent = actor.get_actor_bounds(False)
        actor.set_actor_location(position+ue.Vector(0,0,ground_z+.5-(origin.z-extent.z)),False,False)
        ue.log("Small display %s: position=%s scale=%s" % (actor.get_actor_label(),actor.get_actor_location(),actor.get_actor_scale3d()))
    # Complete the placement calculations before saving changes to any asset.
    for name in ("Seed","Produce","Hoe","WateringCan","Stone"):
        bp = assets.load_asset("/Game/Farm/Blueprints/BP_"+name)
        mesh = ue.get_default_object(bp.generated_class()).get_editor_property("mesh")
        mesh.set_relative_scale3d(mesh.get_editor_property("relative_scale3d")*.3)
        assets.save_loaded_asset(bp,False)
    levels.save_current_level()
    assets.set_metadata_tag(marker,"WTFSmallItems30","1")
    assets.save_loaded_asset(marker,False)
    ue.log("ITEM_SIZE_MIGRATION_SUCCESS")
