"""Verify actual clip playback, hand movement and first-person action visuals."""
from pathlib import Path
import time
import traceback
import unreal as ue

levels = ue.get_editor_subsystem(ue.LevelEditorSubsystem)
actors = ue.get_editor_subsystem(ue.EditorActorSubsystem)
levels.load_level('/Game/Farm/Maps/FirstFarm')
capture_actor = actors.spawn_actor_from_class(ue.SceneCapture2D, ue.Vector())
ue.EditorPythonScripting.set_keep_python_script_alive(True)
levels.editor_request_begin_play()
state = {'step': 0, 'next': time.monotonic()+3, 'start': time.monotonic()}

def close():
    ue.unregister_slate_post_tick_callback(handle)
    levels.editor_request_end_play()
    actors.destroy_actor(capture_actor)
    ue.EditorPythonScripting.set_keep_python_script_alive(False)

def shot(world, player, name):
    ue.log('VIEW_HAND '+name+' '+str(ue.MathLibrary.inverse_transform_location(player.camera.get_world_transform(), player.get_editor_property('hand').get_world_location())))
    actor = state['capture'].get_owner()
    actor.set_actor_location(player.camera.get_world_location(), False, False)
    actor.set_actor_rotation(player.camera.get_world_rotation(), False)
    state['capture'].capture_scene()
    output = Path(ue.Paths.project_saved_dir()) / 'Screenshots'
    output.mkdir(parents=True, exist_ok=True)
    ue.RenderingLibrary.export_render_target(world, state['texture'], str(output), name+'.png')

def tick(delta):
    try:
        if time.monotonic()-state['start'] > 90:
            raise RuntimeError('Action animation verification timed out')
        if time.monotonic() < state['next']:
            return
        worlds = ue.EditorLevelLibrary.get_pie_worlds(False)
        if not worlds:
            return
        world = worlds[0]
        player = ue.GameplayStatics.get_player_pawn(world, 0)
        if not player:
            return
        step = state['step']
        if step == 0:
            ue.GameplayStatics.get_player_controller(player, 0).set_control_rotation(ue.Rotator(pitch=-10, yaw=90, roll=0))
            assert player.first_person_arms.get_editor_property('skeletal_mesh_asset') is not None
            assert player.get_editor_property("hand").get_attach_parent() == player.first_person_arms
            assert str(player.get_editor_property("hand").get_attach_socket_name()) == 'hand_r'
            player.first_person_arms.set_only_owner_see(False)
            capture = ue.GameplayStatics.get_all_actors_of_class(world, ue.SceneCapture2D)[0].get_component_by_class(ue.SceneCaptureComponent2D)
            texture = ue.RenderingLibrary.create_render_target2d(world, 1000, 800, ue.TextureRenderTargetFormat.RTF_RGBA8)
            capture.set_editor_property('texture_target', texture)
            capture.set_editor_property('capture_source', ue.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
            capture.set_editor_property('fov_angle', 72)
            state.update(capture=capture, texture=texture)
            hoe = next(a for a in ue.GameplayStatics.get_all_actors_of_class(world, ue.FarmItem) if a.kind == ue.FarmKind.HOE)
            assert player.pickup_item(hoe)
            assert str(player.current_action) == 'Pickup'
            state['hand'] = player.get_editor_property("hand").get_world_location()
            state['next'] = time.monotonic()+.2
        elif step == 1:
            assert (player.get_editor_property("hand").get_world_location()-state['hand']).length() > 1, 'Pickup clip must move the hand'
            shot(world, player, 'PlayerPickup')
            state['next'] = time.monotonic()+.6
        elif step == 2:
            assert str(player.current_action) == 'None', 'Action must return to idle'
            player.use()
            assert str(player.current_action) == 'Use'
            state['hand'] = player.get_editor_property("hand").get_world_location()
            state['next'] = time.monotonic()+.2
        elif step == 3:
            assert (player.get_editor_property("hand").get_world_location()-state['hand']).length() > 1, 'Swing clip must move the held item'
            shot(world, player, 'PlayerSwing')
            state['next'] = time.monotonic()+.6
        else:
            assert str(player.current_action) == 'None'
            shot(world, player, 'PlayerIdle')
            ue.log('PLAYER_ACTION_ANIMATION_SUCCESS pickup_swing_hand_socket_idle_return')
            close()
            return
        state['step'] += 1
    except Exception:
        ue.log_error(traceback.format_exc())
        close()

handle = ue.register_slate_post_tick_callback(tick)
