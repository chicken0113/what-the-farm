"""Replace matching Unity models with the added Unreal farmer pack.

Run with a rendering editor after saving and closing the interactive editor.
Retains unrelated placements and uses a marker to make repeat runs harmless.
"""
import unreal as ue

assets = ue.EditorAssetLibrary
actors = ue.get_editor_subsystem(ue.EditorActorSubsystem)
levels = ue.get_editor_subsystem(ue.LevelEditorSubsystem)
hoe = assets.load_asset('/Game/3D_LOW_POLY_FarmerPack/Props/SM_Hoe')
farmer = assets.load_asset('/Game/3D_LOW_POLY_FarmerPack/Characters/Mesh/SKM_Farmer_male')
idle = assets.load_asset('/Game/3D_LOW_POLY_FarmerPack/Animations/Farmer/anim_Farmer_idle_basic')
assert hoe and farmer and idle, 'Farmer pack is required'
bp = assets.load_asset('/Game/Farm/Blueprints/BP_Hoe')
marker = 'WTFUnrealFarmerModelsV3'
if assets.get_metadata_tag(bp, marker) == '1':
    ue.log('MODEL_REPLACEMENT_ALREADY_APPLIED')
else:
    levels.load_level('/Game/Farm/Maps/FirstFarm')
    placed = actors.get_all_level_actors()
    for actor in placed:
        if actor.get_actor_label() == 'Original Farm Area - Visual Only':
            actor.static_mesh_component.set_collision_profile_name('NoCollision')
            actor.static_mesh_component.set_collision_enabled(ue.CollisionEnabled.NO_COLLISION)
    supplies = [a for a in placed if isinstance(a, ue.FarmItem) and a.kind == ue.FarmKind.HOE]
    target_scale = 27.0 / (hoe.get_bounds().box_extent.z * 2)

    def replace_hoe(component):
        component.set_static_mesh(hoe)
        component.set_editor_property('override_materials', [])
        # The old mesh used metres; the new mesh uses Unreal centimetres.
        component.set_relative_scale3d(ue.Vector(target_scale, target_scale, target_scale))

    for item in supplies:
        position = item.get_actor_location()
        center, extent = item.get_actor_bounds(False)
        bottom = 3.5  # Supply path top (3 cm) plus the original 0.5 cm clearance.
        replace_hoe(item.mesh)
        item.set_actor_rotation(ue.Rotator(pitch=0, yaw=90, roll=0), False)
        center, extent = item.get_actor_bounds(False)
        item.set_actor_location(position + ue.Vector(0, 0, bottom - (center.z - extent.z)), False, False)
        ue.log('REPLACED_HOE ' + str(item.get_actor_transform()))
    replace_hoe(ue.get_default_object(bp.generated_class()).get_editor_property('mesh'))

    buyer = next(a for a in placed if isinstance(a, ue.FarmMerchant))
    old_visual = next((a for a in placed if a.get_actor_label() in ('Buyer Visual', 'Buyer Visual - Unreal Farmer')), None)
    assert old_visual, 'Expected the original merchant visual'
    center, extent = old_visual.get_actor_bounds(False)
    old_position = old_visual.get_actor_location()
    floor = 0
    desired_height = 180
    visual = actors.spawn_actor_from_class(ue.SkeletalMeshActor, ue.Vector(old_position.x, old_position.y, floor), ue.Rotator(pitch=0, yaw=-90, roll=0))
    visual.set_actor_label('Buyer Visual - Unreal Farmer')
    component = visual.get_component_by_class(ue.SkeletalMeshComponent)
    component.set_skeletal_mesh_asset(farmer)
    component.set_mobility(ue.ComponentMobility.MOVABLE)
    component.set_collision_profile_name('NoCollision')
    component.set_collision_enabled(ue.CollisionEnabled.NO_COLLISION)
    scale = desired_height / (farmer.get_bounds().box_extent.z * 2)
    visual.set_actor_scale3d(ue.Vector(scale, scale, scale))
    component.set_animation_mode(ue.AnimationMode.ANIMATION_SINGLE_NODE)
    component.override_animation_data(idle, True, True, 0.0, 1.0)
    visual.attach_to_actor(buyer, '', ue.AttachmentRule.KEEP_WORLD, ue.AttachmentRule.KEEP_WORLD, ue.AttachmentRule.KEEP_WORLD, False)
    actors.destroy_actor(old_visual)
    assets.set_metadata_tag(bp, marker, '1')
    assets.save_loaded_asset(bp, False)
    assert levels.save_current_level()
    ue.log('MODEL_REPLACEMENT_SUCCESS hoe_and_merchant')
