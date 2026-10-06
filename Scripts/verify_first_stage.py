"""Verify first-stage boundary, real AI combat, exit travel, and carried inventory in PIE."""
from pathlib import Path
import time
import traceback
import unreal as ue

levels=ue.get_editor_subsystem(ue.LevelEditorSubsystem)
actors=ue.get_editor_subsystem(ue.EditorActorSubsystem)
levels.load_level("/Game/Farm/Maps/FirstFarm")
placed=actors.get_all_level_actors()
assert not any(a.get_actor_label().startswith("Fence") for a in placed)
soil=next(a for a in placed if isinstance(a,ue.FarmSoil))
assert abs(soil.get_actor_scale3d().x-100)<.01
position=ue.Vector(6500,-6500,7500)
editor_capture=actors.spawn_actor_from_class(ue.SceneCapture2D,position,ue.MathLibrary.find_look_at_rotation(position,ue.Vector()))
ue.EditorPythonScripting.set_keep_python_script_alive(True)
levels.editor_request_begin_play()
state={"step":0,"next":time.monotonic()+2,"start":time.monotonic(),"hits":0}

def aim(player,point):
    pc=ue.GameplayStatics.get_player_controller(player,0)
    pc.set_control_rotation(ue.MathLibrary.find_look_at_rotation(player.camera.get_world_location(),point))

def close():
    ue.unregister_slate_post_tick_callback(handle)
    levels.editor_request_end_play()
    actors.destroy_actor(editor_capture)
    ue.EditorPythonScripting.set_keep_python_script_alive(False)

def tick(delta):
    try:
        if time.monotonic()-state["start"]>120: raise RuntimeError("First-stage verification timed out")
        worlds=ue.EditorLevelLibrary.get_pie_worlds(False)
        if not worlds: return
        world=worlds[0]
        player=ue.GameplayStatics.get_player_pawn(world,0)
        if not isinstance(player,ue.FarmCharacter): return
        if state["step"] in (3,4) and state.get("enemy"):
            aim(player,state["enemy"].get_actor_location())
        if time.monotonic()<state["next"]: return
        step=state["step"]
        if step==0:
            stage=ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmFirstStage)[0]
            gate=ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmStageExit)[0]
            assert not stage.spawned and not gate.can_travel()
            assert not gate.travel(player),"Locked gate must reject travel"
            hoe=next(a for a in ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmItem) if a.kind==ue.FarmKind.HOE)
            assert player.pickup_item(hoe)
            state.update(stage=stage,gate=gate,hoe_scale=hoe.get_actor_scale3d())
            capture=ue.GameplayStatics.get_all_actors_of_class(world,ue.SceneCapture2D)[0].get_component_by_class(ue.SceneCaptureComponent2D)
            texture=ue.RenderingLibrary.create_render_target2d(world,1200,900,ue.TextureRenderTargetFormat.RTF_RGBA8)
            capture.set_editor_property("texture_target",texture)
            capture.set_editor_property("capture_source",ue.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
            capture.set_editor_property("fov_angle",65)
            state["texture"]=texture
            player.set_actor_location(ue.Vector(1400,0,100),False,False)
            state["step"]=1
        elif step==1:
            enemies=ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmStageMonster)
            assert len(enemies)==1 and state["stage"].spawned
            assert not state["stage"].check_boundary(player)
            enemy=enemies[0]; state.update(enemy=enemy,enemy_start=enemy.get_actor_location())
            player.set_actor_location(ue.Vector(2500,-500,100),False,False)
            state["step"]=2
        elif step==2:
            enemy=state["enemy"]
            motion=enemy.get_actor_location()-state["enemy_start"]
            assert abs(motion.x)+abs(motion.y)>20,"Guardian AI did not chase"
            state["health"]=player.health
            player.set_actor_location(enemy.get_actor_location()+ue.Vector(110,0,0),False,False)
            aim(player,enemy.get_actor_location())
            state["step"]=3
        elif step==3:
            assert player.health<state["health"],"Guardian did not damage player"
            output=Path(ue.Paths.project_saved_dir())/"Screenshots"
            output.mkdir(parents=True,exist_ok=True)
            ue.RenderingLibrary.export_render_target(world,state["texture"],str(output),"FirstStageExpanded.png")
            state["step"]=4
        elif step==4:
            enemy=state["enemy"]; before=enemy.health
            ue.log("Guardian hit attempt %d: enemyHP=%s playerHP=%s player=%s enemy=%s" % (state["hits"],enemy.health,player.health,player.get_actor_location(),enemy.get_actor_location()))
            player.use(); state["hits"]+=1
            if state["stage"].cleared:
                state["enemy"]=None
                assert state["gate"].can_travel()
                assert not state["stage"].check_boundary(player)
                assert not ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmStageMonster)
                ue.log("FIRST_STAGE_COMBAT_SUCCESS hits=%d" % state["hits"])
                sale_item=next(a for a in ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmItem) if a.kind==ue.FarmKind.STONE)
                player.select(1); assert player.pickup_item(sale_item); player.drop()
                merchant=ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmMerchant)[0]
                assert merchant.try_sell(sale_item,player)
                player.select(0); state["gold"]=player.gold
                assert state["gate"].travel(player)
                state["step"]=5
                state["next"]=time.monotonic()+2
                return
            assert enemy.health<before,"Left click missed guardian"
        elif step==5:
            if "StageTwo" not in world.get_name(): return
            assert not ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmFirstStage)
            assert not ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmStageMonster)
            assert player.gold==state["gold"] and player.inventory[0] is not None
            assert player.inventory[0].get_actor_scale3d()==state["hoe_scale"]
            player.set_actor_location(ue.Vector(1500,0,100),False,False)
            state["step"]=6
        elif step==6:
            assert not ue.GameplayStatics.get_all_actors_of_class(world,ue.FarmStageMonster)
            ue.log("FIRST_STAGE_TRAVEL_SUCCESS items_and_gold_preserved no_stage_two_encounter")
            close(); return
        state["next"]=time.monotonic()+(.45 if state["step"]==4 else .7)
    except Exception:
        ue.log_error(traceback.format_exc()); close()

handle=ue.register_slate_post_tick_callback(tick)
