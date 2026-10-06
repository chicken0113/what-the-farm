"""Connect existing farmer animations to the first-person farming pawn."""
import unreal as ue

library = ue.EditorAssetLibrary
bp = library.load_asset('/Game/Farm/Blueprints/BP_Farmer')
defaults = ue.get_default_object(bp.generated_class())
root = '/Game/3D_LOW_POLY_FarmerPack/'
mesh = library.load_asset(root + 'Characters/Mesh/SKM_Farmer_male')
idle = library.load_asset(root + 'Animations/Farmer/anim_Farmer_idle_combat')
swing = library.load_asset(root + 'Animations/Farmer/anim_Farmer_attack_A')
pickup = library.load_asset(root + 'Animations/Farmer/anim_Farmer_grab_medium')
assert mesh and idle and swing and pickup
defaults.first_person_arms.set_skeletal_mesh_asset(mesh)
defaults.first_person_arms.set_editor_property('relative_location', ue.Vector(60, -15, -135))
# Keep the existing farmer textures and use bind-pose positions to show only
# the arms. The mask follows skinned vertices even when the hands cross the chest.
material_path = '/Game/Farm/Materials/M_FirstPersonArms'
material = library.load_asset(material_path)
if not material:
    material = library.duplicate_asset(root + 'Material/M_farm', material_path)
if library.get_metadata_tag(material, 'WTFArmMask') != '3':
    material.set_editor_property('blend_mode', ue.BlendMode.BLEND_MASKED)
    material.set_editor_property('two_sided', True)
    position = ue.MaterialEditingLibrary.create_material_expression(material, ue.MaterialExpressionPreSkinnedPosition, -600, 600)
    interpolator = ue.MaterialEditingLibrary.create_material_expression(material, ue.MaterialExpressionVertexInterpolator, -500, 600)
    mask = ue.MaterialEditingLibrary.create_material_expression(material, ue.MaterialExpressionComponentMask, -400, 600)
    mask.set_editor_property('r', True)
    absolute = ue.MaterialEditingLibrary.create_material_expression(material, ue.MaterialExpressionAbs, -200, 600)
    edge = ue.MaterialEditingLibrary.create_material_expression(material, ue.MaterialExpressionSubtract, 0, 600)
    edge.set_editor_property('const_b', 22)
    assert ue.MaterialEditingLibrary.connect_material_expressions(position, '', interpolator, '')
    assert ue.MaterialEditingLibrary.connect_material_expressions(interpolator, '', mask, '')
    assert ue.MaterialEditingLibrary.connect_material_expressions(mask, '', absolute, '')
    assert ue.MaterialEditingLibrary.connect_material_expressions(absolute, '', edge, 'A')
    assert ue.MaterialEditingLibrary.connect_material_property(edge, '', ue.MaterialProperty.MP_OPACITY_MASK)
    ue.MaterialEditingLibrary.recompile_material(material)
    library.set_metadata_tag(material, 'WTFArmMask', '3')
    library.save_loaded_asset(material, False)
defaults.first_person_arms.set_material(0, material)
defaults.set_editor_property('idle_animation', idle)
defaults.set_editor_property('swing_animation', swing)
defaults.set_editor_property('pickup_animation', pickup)
library.save_loaded_asset(bp, False)
ue.log('PLAYER_ANIMATION_SETUP_SUCCESS')
