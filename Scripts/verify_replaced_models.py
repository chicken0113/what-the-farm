"""Check replacements and capture the merchant and supply models in PIE."""
from pathlib import Path
import time
import traceback
import unreal as ue

levels = ue.get_editor_subsystem(ue.LevelEditorSubsystem)
actors = ue.get_editor_subsystem(ue.EditorActorSubsystem)
levels.load_level('/Game/Farm/Maps/FirstFarm')
placed = actors.get_all_level_actors()
hoe = next(a for a in placed if isinstance(a, ue.FarmItem) and a.kind == ue.FarmKind.HOE)
assert str(hoe.mesh.get_editor_property('static_mesh').get_path_name()).startswith('/Game/3D_LOW_POLY_FarmerPack/')
assert abs(hoe.get_actor_bounds(False)[1].z * 2 - 27) < .1
assert not any(a.get_actor_label() == 'Buyer Visual' for a in placed)
visual = next(a for a in placed if a.get_actor_label() == 'Buyer Visual - Unreal Farmer')
component = visual.get_component_by_class(ue.SkeletalMeshComponent)
assert component.get_editor_property('animation_data').get_editor_property('anim_to_play') is not None
assert isinstance(visual.get_attach_parent_actor(), ue.FarmMerchant)
position = ue.Vector(1700, 100, 450)
capture_actor = actors.spawn_actor_from_class(ue.SceneCapture2D, position, ue.MathLibrary.find_look_at_rotation(position, ue.Vector(900, 750, 95)))
ue.EditorPythonScripting.set_keep_python_script_alive(True)
levels.editor_request_begin_play()
state = {'step': 0, 'next': time.monotonic() + 3, 'start': time.monotonic()}

def close():
    ue.unregister_slate_post_tick_callback(handle)
    levels.editor_request_end_play()
    actors.destroy_actor(capture_actor)
    ue.EditorPythonScripting.set_keep_python_script_alive(False)

def tick(delta):
    try:
        if time.monotonic() - state['start'] > 90:
            raise RuntimeError('Model verification timed out')
        if time.monotonic() < state['next']:
            return
        worlds = ue.EditorLevelLibrary.get_pie_worlds(False)
        if not worlds:
            return
        world = worlds[0]
        if state['step'] == 0:
            visual = next(a for a in ue.GameplayStatics.get_all_actors_of_class(world, ue.SkeletalMeshActor) if a.get_actor_label() == 'Buyer Visual - Unreal Farmer')
            component = visual.get_component_by_class(ue.SkeletalMeshComponent)
            assert component.is_playing(), 'Merchant idle animation must play in PIE'
            capture = ue.GameplayStatics.get_all_actors_of_class(world, ue.SceneCapture2D)[0].get_component_by_class(ue.SceneCaptureComponent2D)
            texture = ue.RenderingLibrary.create_render_target2d(world, 1000, 800, ue.TextureRenderTargetFormat.RTF_RGBA8)
            capture.set_editor_property('texture_target', texture)
            capture.set_editor_property('capture_source', ue.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
            capture.set_editor_property('fov_angle', 50)
            state.update(capture=capture, texture=texture, step=1, next=time.monotonic()+1)
        elif state['step'] == 1:
            output = Path(ue.Paths.project_saved_dir()) / 'Screenshots'
            output.mkdir(parents=True, exist_ok=True)
            state['capture'].capture_scene()
            ue.RenderingLibrary.export_render_target(world, state['texture'], str(output), 'UnrealFarmerReplacement.png')
            state['capture'].get_owner().set_actor_location(ue.Vector(-150, -820, 90), False, False)
            state['capture'].get_owner().set_actor_rotation(ue.MathLibrary.find_look_at_rotation(ue.Vector(-150, -820, 90), ue.Vector(-250, -700, 17)), False)
            state.update(step=2, next=time.monotonic()+1)
        else:
            state['capture'].capture_scene()
            ue.RenderingLibrary.export_render_target(world, state['texture'], str(Path(ue.Paths.project_saved_dir()) / 'Screenshots'), 'UnrealHoeReplacement.png')
            ue.log('REPLACED_MODELS_PIE_SUCCESS idle_animation_and_hoe_dimensions')
            close()
    except Exception:
        ue.log_error(traceback.format_exc())
        close()

handle = ue.register_slate_post_tick_callback(tick)
