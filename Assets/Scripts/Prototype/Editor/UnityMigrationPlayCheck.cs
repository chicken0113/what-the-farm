using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WhatTheFarm.Prototype;

// Runs the actual scene in the already-open editor. SessionState survives entering Play.
[InitializeOnLoad]
public static class UnityMigrationPlayCheck
{
    const string Flag="WhatTheFarm.PlayCheck";
    const string Result="Library/WhatTheFarmMigration.result";
    static int phase;
    static double next, started;
    static FarmPrototype world;
    static LocalFarmer player;
    static FarmFirstStage stage;
    static FarmStageExit exit;
    static FarmItem grownItem;
    static FleeingCrop crop;
    static SoilSurface surface;
    static FarmPlot home;
    static Vector3 plantedPoint;
    static Vector3 scale, enemyStart;
    static long gold;
    static float health;
    static int hits;
    static UnityMigrationPlayCheck() { EditorApplication.update+=Tick; EditorApplication.playModeStateChanged+=Changed; }
    [MenuItem("What The Farm/Validate Unity Migration (Play)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before validation.");
        if(!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Scene save failed.");
        EditorSceneManager.OpenScene("Assets/Scenes/FirstFarm.unity");
        SessionState.SetBool(Flag,true); EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Flag,false))
        {
            SessionState.SetBool(Flag,false); EditorSceneManager.OpenScene("Assets/Scenes/FirstFarm.unity");
            if(Application.isBatchMode) EditorApplication.Exit(File.Exists(Result) && File.ReadAllText(Result).StartsWith("PLAYCHECK_SUCCESS") ? 0 : 1);
        }
    }
    static void Check(bool condition,string reason) { if(!condition) throw new InvalidOperationException(reason); }
    static void Move(Vector3 point)
    {
        var body=player.GetComponent<CharacterController>(); body.enabled=false; player.transform.position=point; body.enabled=true; Physics.SyncTransforms();
    }
    static void Aim(Vector3 point) { player.View.transform.rotation=Quaternion.LookRotation(point-player.View.transform.position); Physics.SyncTransforms(); }
    static void Hold(FarmItem item,int slot)
    {
        foreach(var collider in item.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
        item.GetComponent<Rigidbody>().isKinematic=true;
        var inventory=player.CaptureInventory(); inventory[slot]=item; player.RestoreInventory(inventory,slot);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if(started==0) { started=EditorApplication.timeSinceStartup; next=Time.time+.5; }
            if(EditorApplication.timeSinceStartup-started>60) throw new TimeoutException("Play validation timed out at phase "+phase);
            // A hidden batch editor advances only requested player loops. Use a fixed simulation step there.
            if (Application.isBatchMode) Time.captureDeltaTime = .02f;
            EditorApplication.QueuePlayerLoopUpdate();
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if(Time.time<next) return;
            next=Time.time+.5;
            switch(phase)
            {
                case 0:
                    world=UnityEngine.Object.FindFirstObjectByType<FarmPrototype>(); player=world.Player;
                    stage=world.GetComponent<FarmFirstStage>(); exit=UnityEngine.Object.FindFirstObjectByType<FarmStageExit>();
                    foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        foreach(var material in renderer.sharedMaterials)
                            Check(material != null && material.shader != null && material.shader.name.StartsWith("Universal Render Pipeline/"),"Non-URP scene material: "+renderer.name);
                    Check(stage!=null && !stage.Spawned && !exit.CanTravel,"First-stage setup/locked exit");
                    var soil=UnityEngine.Object.FindFirstObjectByType<SoilSurface>();
                    Check(Mathf.Abs(soil.GetComponent<Collider>().bounds.size.x-100)<.1f,"100m ground missing");
                    Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name.StartsWith("Fence_Block")),"Old fences remain");
                    var stock=world.GetComponentsInChildren<FarmItem>().First(i=>i.Kind==ItemKind.Tool);
                    var stockPosition=stock.transform.position;
                    Aim(stock.GetComponent<Collider>().bounds.center); player.Interact(); Check(player.HeldItem==stock,"Ray pickup failed");
                    player.SelectSlot(1); Aim(stockPosition+Vector3.up*.1f); player.Interact(); Check(player.HeldItem!=null && player.HeldItem.Kind==ItemKind.Tool,"Immediate restock pickup failed");
                    var refill=world.GetComponentsInChildren<FarmItem>().First(i=>i.Kind==ItemKind.Tool && i.transform.parent==world.transform);
                    Check(Vector3.Distance(refill.transform.position,stockPosition)<.001f,"Restock position drift");
                    Check(Physics.GetIgnoreLayerCollision(8,9) && Physics.GetIgnoreLayerCollision(8,8),"Loose item collision");
                    Check(stock.GetComponentsInChildren<Renderer>(true).Max(r=>r.bounds.size.magnitude)<1,"Tool not reduced");
                    player.SelectSlot(0); Aim(new Vector3(-1,0,-8.5f)); player.Swing();
                    var plot=soil.FindPlot(new Vector3(-1,0,-8.5f)); Check(plot!=null,"Hoe till action failed");
                    var source=world.CreateItem(ItemKind.Tool,0,16,new Vector3(0,5,0));
                    var point=plot.transform.position+Vector3.right*.2f;
                    Check(world.TryPlant(source,plot,point),"Fresh tool planting failed");
                    crop=world.GetComponentsInChildren<FleeingCrop>().Single();
                    Check(Mathf.Abs(crop.transform.position.x-point.x)<.001f,"Planting not at aim point");
                    Check(crop.transform.lossyScale==source.transform.lossyScale,"Planting changed size");
                    var second=world.CreateItem(ItemKind.Seed,0,10,new Vector3(0,5,0));
                    Check(!world.TryPlant(second,plot,point),"Occupied plot allowed second plant"); UnityEngine.Object.Destroy(second.gameObject);
                    crop.Grow(20); Check(!crop.IsMature,"Dry plant grew");
                    surface=soil; home=plot; plantedPoint=point;
                    UnityEngine.Object.Destroy(source.gameObject);
                    break;
                case 1:
                    var can=world.CreateItem(ItemKind.WateringCan,0,14,new Vector3(0,5,0));
                    Check(world.TryWater(surface,plantedPoint,can)==1,"Watering failed"); world.TryWater(surface,plantedPoint,can); crop.Grow(20);
                    Check(crop.IsMature && !home.IsOccupied,"Growth did not free plot");
                    scale=crop.transform.lossyScale; var model=crop.gameObject; crop.TakeHit(1000); grownItem=model.GetComponent<FarmItem>();
                    Check(grownItem.HasBeenPlanted && grownItem.transform.lossyScale==scale,"Harvest size/history changed: planted="+grownItem.HasBeenPlanted+" before="+scale.ToString("F6")+" after="+grownItem.transform.lossyScale.ToString("F6"));
                    Check(!world.TryPlant(grownItem,home,plantedPoint),"Harvested item can be replanted");
                    Check(Mathf.Abs(world.TillingRadiusFor(grownItem)-.8f*grownItem.SizeMultiplier)<.001f,"Grown tool radius not linear");
                    UnityEngine.Object.Destroy(can.gameObject); Hold(grownItem,2);
                    // A thrown object must be converted by the existing NPC physics trigger.
                    var npc=UnityEngine.Object.FindFirstObjectByType<NpcMerchant>();
                    var trigger=npc.GetComponents<Collider>().First(c=>c.isTrigger);
                    var sale=world.CreateItem(ItemKind.Curio,0,6,trigger.bounds.center);
                    sale.MarkThrown(); gold=world.Gold;
                    break;
                case 2:
                    Check(world.Gold>gold,"NPC trigger sale failed"); gold=world.Gold;
                    Move(new Vector3(13,.1f,0)); Check(!stage.CheckBoundary(player),"Boundary equality spawned monster");
                    Move(new Vector3(14,.1f,0)); Check(stage.CheckBoundary(player),"Boundary did not spawn monster");
                    Check(!stage.CheckBoundary(player),"Duplicate monster spawn"); enemyStart=stage.Monster.transform.position;
                    break;
                case 3:
                    Check(Vector3.Distance(enemyStart,stage.Monster.transform.position)>.1f,"Monster did not chase: start="+enemyStart+" now="+stage.Monster.transform.position+" time="+Time.time);
                    Move(stage.Monster.transform.position+Vector3.back*1.5f); health=player.Health;
                    break;
                case 4:
                    Check(player.Health<health,"Monster did not damage player");
                    player.ReceiveDamage(999); Check(player.Health==player.MaxHealth && Vector3.Distance(player.transform.position,world.SpawnPosition)<.01f,"Knockout failed");
                    Check(player.CaptureInventory()[2]==grownItem,"Knockout lost inventory");
                    Move(stage.Monster.transform.position+Vector3.back*2.3f); player.SelectSlot(0);
                    Aim(stage.Monster.transform.position+Vector3.up*.9f); health=stage.Monster.Health; player.Swing();
                    Check(stage.Monster.Health<health,"Swing did not hit guardian"); hits=1;
                    break;
                case 5:
                    if(stage.Monster!=null)
                    {
                        Move(stage.Monster.transform.position+Vector3.back*2.3f); Aim(stage.Monster.transform.position+Vector3.up*.9f); player.Swing();
                        Check(++hits<12,"Guardian cannot be defeated by swings"); return;
                    }
                    Check(stage.Cleared && exit.CanTravel && !stage.CheckBoundary(player),"Clear/exit unlock failed");
                    player.SelectSlot(2); scale=grownItem.transform.lossyScale;
                    Move(exit.transform.position+Vector3.back*2.5f+Vector3.up*.1f);
                    Aim(exit.transform.position+Vector3.up); player.Interact();
                    break;
                case 6:
                    Check(SceneManager.GetActiveScene().name=="StageTwo","StageTwo not loaded");
                    var nextWorld=UnityEngine.Object.FindFirstObjectByType<FarmPrototype>();
                    Check(nextWorld.Gold==gold && nextWorld.Player.HeldItem==grownItem,"Travel lost gold/inventory/selection");
                    Check(grownItem.HasBeenPlanted && grownItem.transform.lossyScale==scale,"Travel lost size/history");
                    Check(UnityEngine.Object.FindFirstObjectByType<FarmFirstStage>()==null && UnityEngine.Object.FindFirstObjectByType<FarmGuardian>()==null,"First-stage encounter leaked");
                    File.WriteAllText(Result,"PLAYCHECK_SUCCESS: stock pickup/refill, till/aim planting, occupied soil, watering/growth, harvest size, single planting, tool radius, NPC trigger sale, boundary/chase/damage/swing/knockout/clear, travel inventory/gold/history.");
                    Debug.Log("UNITY_MIGRATION_PLAYCHECK_SUCCESS"); EditorApplication.isPlaying=false; return;
            }
            phase++;
        }
        catch(Exception error) { Debug.LogException(error); File.WriteAllText(Result,error.ToString()); EditorApplication.isPlaying=false; }
    }
}
