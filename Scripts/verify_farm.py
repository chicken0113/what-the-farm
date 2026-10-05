"""Check the actual first map and player actions in PIE, then capture the map."""
from pathlib import Path
import time
import traceback
import unreal as ue

levels = ue.get_editor_subsystem(ue.LevelEditorSubsystem)
editor_actors = ue.get_editor_subsystem(ue.EditorActorSubsystem)
levels.load_level("/Game/Farm/Maps/FirstFarm")
placed = editor_actors.get_all_level_actors()
assert sum(isinstance(a,ue.FarmSoil) for a in placed) == 1
assert sum(isinstance(a,ue.FarmMerchant) for a in placed) == 1
assert sum(isinstance(a,ue.FarmItem) for a in placed) == 6
for item in (a for a in placed if isinstance(a,ue.FarmItem)):
    assert item.mesh.get_editor_property("static_mesh") is not None and item.growth_profile is not None
    ue.log("Supply %s scale=%s" % (item.display_name(), item.get_actor_scale3d()))
ue.EditorPythonScripting.set_keep_python_script_alive(True)
levels.editor_request_begin_play()
state = {"step":0,"next":time.monotonic()+2,"started":time.monotonic(),"preview":None}

def aim(actor, point):
    pc = ue.GameplayStatics.get_player_controller(actor,0)
    camera = actor.get_editor_property("camera")
    pc.set_control_rotation(ue.MathLibrary.find_look_at_rotation(camera.get_world_location(),point))

def close():
    ue.unregister_slate_post_tick_callback(handle)
    levels.editor_request_end_play()
    ue.EditorPythonScripting.set_keep_python_script_alive(False)

def tick(delta):
    try:
        if time.monotonic()-state["started"]>120:
            raise RuntimeError("PIE verification timed out")
        if state["step"] in (6,7,8):
            worlds = ue.EditorLevelLibrary.get_pie_worlds(False)
            if worlds:
                player = ue.GameplayStatics.get_player_pawn(worlds[0],0)
                if player:
                    aim(player,state["seed"].get_actor_location())
        if time.monotonic()<state["next"]:
            return
        step = state["step"]
        if step<20:
            worlds = ue.EditorLevelLibrary.get_pie_worlds(False)
            if not worlds:
                return
            world = worlds[0]
            player = ue.GameplayStatics.get_player_pawn(world,0)
            assert isinstance(player,ue.FarmCharacter), "Farm pawn was not spawned"
            items = ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmItem)
            soil = ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmSoil)[0]
            if step==0:
                assert len(player.inventory)==12
                player.set_actor_location(ue.Vector(0,-950,100),False,False)
                state["hoe"] = next(a for a in items if a.kind==ue.FarmKind.HOE and a.restock_on_pickup)
                aim(player,state["hoe"].get_actor_location())
            elif step==1:
                player.interact()
                assert player.inventory[0]==state["hoe"], "E did not pick up hoe"
                aim(player,ue.Vector(-100,-850,0))
            elif step==2:
                player.use()
                assert len(soil.plots)==1, "Left click did not till"
                state["plot_point"] = soil.plots[0].center+ue.Vector(25,0,0)
                player.select(1)
                seed = next(a for a in items if a.kind==ue.FarmKind.SEED and a.restock_on_pickup)
                state["seed"] = seed
                aim(player,seed.get_actor_location())
            elif step==3:
                player.interact()
                assert player.inventory[1]==state["seed"], "E did not pick up seed"
                aim(player,state["plot_point"])
            elif step==4:
                player.interact()
                crop=state["seed"]
                assert crop.planted and player.inventory[1] is None
                assert abs(crop.get_actor_location().x-state["plot_point"].x)<1
                player.select(2)
                can = next(a for a in items if a.kind==ue.FarmKind.WATERING_CAN and a.restock_on_pickup)
                state["can"] = can
                aim(player,can.get_actor_location())
            elif step==5:
                player.interact()
                assert player.inventory[2]==state["can"]
                aim(player,state["seed"].get_actor_location())
            elif step==6:
                player.use()
                assert soil.plots[0].water==25, "Watering action failed"
                aim(player,state["seed"].get_actor_location())
            elif step==7:
                player.use()
                assert soil.plots[0].water==50
                state["seed"].grow(10)
                assert state["seed"].mature
                state["grown"] = state["seed"].get_actor_scale3d()
                player.select(0)
                aim(player,state["seed"].get_actor_location())
            elif step==8:
                player.use()
                assert state["seed"].health<3, "Harvest attack action failed"
                state["seed"].hit(999)
                assert not state["seed"].mature and state["seed"].get_actor_scale3d()==state["grown"]
                player.select(1)
                assert player.pickup_item(state["seed"])
                player.toggle_inventory()
                player.toggle_inventory()
                assert len(ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmItem))==9
                merchant=ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmMerchant)[0]
                player.set_actor_location(merchant.get_actor_location()+ue.Vector(0,-250,5),False,False)
                aim(player,merchant.get_actor_location())
            elif step==9:
                player.drop()
                assert player.inventory[1] is None and state["seed"].thrown
                merchant=ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmMerchant)[0]
                receiver=merchant.get_editor_property("receiver")
                ue.log("Throw start=%s velocity=%s receiver=%s radius=%s" % (state["seed"].get_actor_location(),state["seed"].mesh.get_physics_linear_velocity(),receiver.get_world_location(),receiver.get_scaled_sphere_radius()))
            elif step==10:
                if player.gold==0:
                    receiver=ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmMerchant)[0].get_editor_property("receiver")
                    ue.log("Throw final=%s physics=%s overlap=%s" % (state["seed"].get_actor_location(),state["seed"].mesh.is_simulating_physics(),receiver.is_overlapping_actor(state["seed"])))
                assert player.gold>0, "Thrown harvest did not sell through physics overlap"
                ue.log("FARM_PIE_ACTIONS_SUCCESS gold=%d" % player.gold)
                levels.editor_request_end_play()
                state["step"]=19
            state["step"]+=1
            state["next"]=time.monotonic()+.7
            return
        if step==20:
            # Editor placement can pump Slate; guard against callback re-entry.
            state["step"]=21
            state["next"]=time.monotonic()+5
            pos=ue.Vector(2500,-3000,3000)
            camera=editor_actors.spawn_actor_from_class(ue.CameraActor,pos,ue.MathLibrary.find_look_at_rotation(pos,ue.Vector(0,0,0)))
            component=camera.get_component_by_class(ue.CameraComponent)
            component.set_editor_property("field_of_view",55)
            editor_actors.clear_actor_selection_set()
            levels.editor_set_game_view(True)
            ue.SystemLibrary.execute_console_command(camera,"viewmode lit")
            state["preview_camera"]=camera
            output=Path(ue.Paths.project_saved_dir())/"Screenshots"/"FarmPreview.png"
            output.parent.mkdir(parents=True,exist_ok=True)
            output.unlink(missing_ok=True)
            state["preview"]=ue.AutomationLibrary.take_high_res_screenshot(1200,900,str(output),camera=camera,delay=2,force_game_view=True)
            state["step"]=21
            state["next"]=time.monotonic()+5
        elif step==21:
            output=Path(ue.Paths.project_saved_dir())/"Screenshots"/"FarmPreview.png"
            if not output.exists():
                return
            editor_actors.destroy_actor(state["preview_camera"])
            ue.log("FARM_VISUAL_VERIFICATION_SUCCESS")
            close()
    except Exception:
        ue.log_error(traceback.format_exc())
        close()

handle=ue.register_slate_post_tick_callback(tick)
