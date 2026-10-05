"""Capture actual PIE soil from above; verify dry and watered plot rendering.

Run with a rendering UnrealEditor and -ExecutePythonScript. Output is under
Saved/Screenshots; WTF_TILLING_CAPTURE selects a before/after image name.
"""
import os
from pathlib import Path
import time
import traceback
import unreal as ue

levels = ue.get_editor_subsystem(ue.LevelEditorSubsystem)
levels.load_level("/Game/Farm/Maps/FirstFarm")
actors = ue.get_editor_subsystem(ue.EditorActorSubsystem)
target = ue.Vector(-120,-300,0)
pos = ue.Vector(-120,-750,850)
editor_capture = actors.spawn_actor_from_class(ue.SceneCapture2D,pos,ue.MathLibrary.find_look_at_rotation(pos,target))
ue.EditorPythonScripting.set_keep_python_script_alive(True)
levels.editor_request_begin_play()
state = {"step": 0, "next": time.monotonic()+2, "start": time.monotonic()}

def close():
    ue.unregister_slate_post_tick_callback(handle)
    levels.editor_request_end_play()
    actors.destroy_actor(editor_capture)
    ue.EditorPythonScripting.set_keep_python_script_alive(False)

def tick(delta):
    try:
        if time.monotonic()-state["start"] > 90:
            raise RuntimeError("Soil visual capture timed out")
        if time.monotonic() < state["next"]:
            return
        worlds = ue.EditorLevelLibrary.get_pie_worlds(False)
        if not worlds:
            return
        world = worlds[0]
        if state["step"] == 0:
            state["step"] = 1
            state["next"] = time.monotonic()+2
            soil = ue.GameplayStatics.get_all_actors_of_class(world, ue.FarmSoil)[0]
            dry = ue.Vector(-260,-300,0)
            wet = ue.Vector(20,-300,0)
            assert soil.till(dry,100) >= 0 and soil.till(wet,100) >= 0
            seed = next(a for a in ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmItem) if a.kind==ue.FarmKind.SEED)
            assert seed.plant_at(soil,wet)
            assert soil.water_area(wet,100,25) == 1
            # Keep the test crop out of the image while retaining its plot link.
            seed.set_actor_hidden_in_game(True)
            capture = ue.GameplayStatics.get_all_actors_of_class(world,ue.SceneCapture2D)[0]
            component = capture.get_component_by_class(ue.SceneCaptureComponent2D)
            texture = ue.RenderingLibrary.create_render_target2d(world,1000,800,ue.TextureRenderTargetFormat.RTF_RGBA8)
            component.set_editor_property("texture_target", texture)
            component.set_editor_property("capture_source",ue.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
            component.set_editor_property("fov_angle",55)
            state.update(component=component, texture=texture)
        elif state["step"] == 1:
            state["step"] = 2
            state["next"] = time.monotonic()+1
            state["component"].capture_scene()
        else:
            output = Path(ue.Paths.project_saved_dir())/"Screenshots"
            output.mkdir(parents=True,exist_ok=True)
            name = os.environ.get("WTF_TILLING_CAPTURE","TillingAfter.png")
            ue.RenderingLibrary.export_render_target(world,state["texture"],str(output),name)
            assert (output/name).exists()
            ue.log("FARM_TILLING_CAPTURE_SUCCESS " + name)
            close()
    except Exception:
        ue.log_error(traceback.format_exc())
        close()

handle = ue.register_slate_post_tick_callback(tick)
