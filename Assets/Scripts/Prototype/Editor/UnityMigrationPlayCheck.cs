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
    static FarmGuardian corpse;
    static Vector3 merchantHome;
    static Quaternion merchantHomeRotation;
    static Color[] merchantColors;
    static FarmPlot revivalPlot;
    static FleeingCrop revivalCrop;
    static string corpseOwnerId;
    static LocalFarmer teammate;
    static Vector3 returnStart;
    static float returnStarted;
    static Vector3 revivalScale;
    static void CheckBuried(PlantableCorpse body, float groundHeight)
    {
        var renderers=body.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds; foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        Check(bounds.min.y<groundHeight-.1f && bounds.max.y>groundHeight+.1f && Mathf.Abs(bounds.center.y-groundHeight)<.05f,"Body is not buried to its waist");
    }
    static Color[] Appearance(FarmGuardian merchant) => merchant.GetComponentsInChildren<Renderer>().Select(renderer =>
    {
        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); return block.GetColor("_BaseColor");
    }).ToArray();
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
    static void CaptureHandPreview(Camera camera)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var target=new RenderTexture(1280,720,24);
        var previousTarget=camera.targetTexture;
        var previousActive=RenderTexture.active;
        bool asyncShaders=ShaderUtil.allowAsyncCompilation;
        Texture2D pixels=null;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;
            camera.targetTexture=target;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=target });
            RenderTexture.active=target;
            pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            File.WriteAllBytes("Library/WhatTheFarm-empty-hand.png",pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previousTarget; RenderTexture.active=previousActive; ShaderUtil.allowAsyncCompilation=asyncShaders;
            if(pixels!=null) UnityEngine.Object.DestroyImmediate(pixels);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
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
                    Check(player.HeldItem==null && player.EmptyHand.IsVisible,"Starting empty hand missing");
                    Check(player.EmptyHand.GetComponentsInChildren<Collider>(true).Length==0,"Hand blocks aiming");
                    var fist=player.EmptyHand.transform.Find("Fist").GetComponent<Renderer>();
                    var viewport=player.View.WorldToViewportPoint(fist.bounds.center);
                    Check(viewport.x>0 && viewport.x<1 && viewport.y>0 && viewport.y<1 && viewport.z>player.View.nearClipPlane,"Hand outside camera view");
                    CaptureHandPreview(player.View);
                    Aim(new Vector3(.8f,0,-8.5f)); player.Swing();
                    var bareSoil=UnityEngine.Object.FindFirstObjectByType<SoilSurface>();
                    var barePlot=bareSoil.FindPlot(new Vector3(.8f,0,-8.5f));
                    Check(barePlot!=null && Mathf.Abs(barePlot.Radius-.08f)<.001f,"Bare hand did not create a fist-sized patch at aim point");
                    break;
                case 1:
                    world=UnityEngine.Object.FindFirstObjectByType<FarmPrototype>(); player=world.Player;
                    stage=world.GetComponent<FarmFirstStage>(); exit=UnityEngine.Object.FindFirstObjectByType<FarmStageExit>();
                    foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        foreach(var material in renderer.sharedMaterials)
                            Check(material != null && material.shader != null && material.shader.name.StartsWith("Universal Render Pipeline/"),"Non-URP scene material: "+renderer.name);
                    Check(stage!=null && !stage.Spawned && !exit.CanTravel,"First-stage setup/locked exit");
                    Check(stage.Monster!=null && !stage.Monster.IsHostile && stage.Monster.GetComponent<NpcMerchant>()!=null,"Merchant must start peaceful");
                    merchantHome=stage.Monster.transform.position; merchantHomeRotation=stage.Monster.transform.rotation;
                    var soil=UnityEngine.Object.FindFirstObjectByType<SoilSurface>();
                    Check(Mathf.Abs(soil.GetComponent<Collider>().bounds.size.x-100)<.1f,"100m ground missing");
                    Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name.StartsWith("Fence_Block")),"Old fences remain");
                    var stock=world.GetComponentsInChildren<FarmItem>().First(i=>i.Kind==ItemKind.Tool);
                    var stockPosition=stock.transform.position;
                    Aim(stock.GetComponent<Collider>().bounds.center); player.Interact(); Check(player.HeldItem==stock,"Ray pickup failed");
                    Check(!player.EmptyHand.IsVisible,"Hand visible while holding an item");
                    player.SelectSlot(11); Check(player.EmptyHand.IsVisible,"Empty slot did not show hand");
                    player.SelectSlot(0); Check(!player.EmptyHand.IsVisible,"Item slot did not hide hand");
                    player.SelectSlot(1); Aim(stockPosition+Vector3.up*.1f); player.Interact(); Check(player.HeldItem!=null && player.HeldItem.Kind==ItemKind.Tool,"Immediate restock pickup failed");
                    var refill=world.GetComponentsInChildren<FarmItem>().First(i=>i.Kind==ItemKind.Tool && i.transform.parent==world.transform);
                    Check(Vector3.Distance(refill.transform.position,stockPosition)<.001f,"Restock position drift");
                    Check(Physics.GetIgnoreLayerCollision(8,9) && Physics.GetIgnoreLayerCollision(8,8),"Loose item collision");
                    Check(stock.GetComponentsInChildren<Renderer>(true).Max(r=>r.bounds.size.magnitude)<1,"Tool not reduced");
                    Aim(new Vector3(.8f,0,-8.5f)); player.Interact();
                    Check(player.HeldItem==null && player.EmptyHand.IsVisible,"Planting did not restore empty hand");
                    var plantedExtra=world.GetComponentsInChildren<FleeingCrop>().Single(); UnityEngine.Object.Destroy(plantedExtra.gameObject);
                    player.SelectSlot(0); Aim(new Vector3(-1,0,-8.5f)); player.Swing();
                    var plot=soil.FindPlot(new Vector3(-1,0,-8.5f)); Check(plot!=null,"Hoe till action failed");
                    var source=world.CreateItem(ItemKind.Tool,0,16,new Vector3(0,5,0));
                    var point=plot.transform.position+Vector3.right*.2f;
                    Check(world.TryPlant(source,plot,point),"Fresh tool planting failed");
                    crop=world.GetComponentsInChildren<FleeingCrop>().Single(c=>c.Plot==plot);
                    Check(Mathf.Abs(crop.transform.position.x-point.x)<.001f,"Planting not at aim point");
                    Check(crop.transform.lossyScale==source.transform.lossyScale,"Planting changed size");
                    var second=world.CreateItem(ItemKind.Seed,0,10,new Vector3(0,5,0));
                    Check(!world.TryPlant(second,plot,point),"Occupied plot allowed second plant"); UnityEngine.Object.Destroy(second.gameObject);
                    crop.Grow(20); Check(!crop.IsMature,"Dry plant grew");
                    surface=soil; home=plot; plantedPoint=point;
                    UnityEngine.Object.Destroy(source.gameObject);
                    break;
                case 2:
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
                case 3:
                    Check(world.Gold>gold,"NPC trigger sale failed"); gold=world.Gold;
                    Move(new Vector3(13,.1f,0)); Check(!stage.CheckBoundary(player),"Boundary equality spawned monster");
                    var existingMerchant=stage.Monster;
                    merchantColors=Appearance(existingMerchant);
                    Move(new Vector3(14,.1f,0)); Check(stage.CheckBoundary(player),"Boundary did not activate merchant combat");
                    Check(merchantColors.SequenceEqual(Appearance(existingMerchant)),"Combat changed merchant colour");
                    Check(stage.Monster==existingMerchant && existingMerchant.IsHostile && UnityEngine.Object.FindObjectsByType<FarmGuardian>(FindObjectsSortMode.None).Length==1,"Boundary spawned a separate monster");
                    var rejected=world.CreateItem(ItemKind.Curio,0,6,Vector3.up*5); rejected.MarkThrown();
                    Check(!existingMerchant.GetComponent<NpcMerchant>().TrySell(rejected) && world.Gold==gold && !rejected.IsSold,"Hostile merchant accepted sale"); UnityEngine.Object.Destroy(rejected.gameObject);
                    Check(!stage.CheckBoundary(player),"Duplicate monster spawn"); enemyStart=stage.Monster.transform.position;
                    break;
                case 4:
                    Check(Vector3.Distance(enemyStart,stage.Monster.transform.position)>.1f,"Monster did not chase: start="+enemyStart+" now="+stage.Monster.transform.position+" time="+Time.time);
                    var modelForward=stage.Monster.transform.Find("Visual").forward;
                    var chaseDirection=player.transform.position-stage.Monster.transform.position; chaseDirection.y=0;
                    Check(Vector3.Dot(modelForward,chaseDirection.normalized)>.99f,"Merchant visual faces backwards while chasing");
                    Move(stage.Monster.transform.position+Vector3.right*1.5f); health=player.Health;
                    break;
                case 5:
                    Check(player.Health<health,"Monster did not damage player");
                    player.ReceiveDamage(999); Check(player.Health==player.MaxHealth && Vector3.Distance(player.transform.position,world.SpawnPosition)<.01f,"Knockout failed");
                    Check(player.CaptureInventory()[2]==grownItem,"Knockout lost inventory");
                    Check(!stage.Spawned && !stage.Cleared && !stage.Monster.IsHostile && stage.Monster.Health==stage.Monster.MaxHealth,"Knockout did not reset merchant encounter");
                    Check(Vector3.Distance(stage.Monster.transform.position,merchantHome)<.001f && Quaternion.Angle(stage.Monster.transform.rotation,merchantHomeRotation)<.01f,"Merchant did not return home");
                    Check(stage.Monster.GetComponent<CapsuleCollider>().enabled && !stage.Monster.GetComponent<CharacterController>().enabled,"Peaceful merchant collisions not restored");
                    var resumedSale=world.CreateItem(ItemKind.Curio,0,6,Vector3.up*5); resumedSale.MarkThrown();
                    Check(stage.Monster.GetComponent<NpcMerchant>().TrySell(resumedSale),"Merchant did not resume sales after player death"); gold=world.Gold;
                    Move(stage.Monster.transform.position+Vector3.right*2.3f); player.SelectSlot(0);
                    Aim(stage.Monster.transform.position+Vector3.up*.9f); health=stage.Monster.Health; player.Swing();
                    Check(stage.Monster.Health<health,"Swing did not hit guardian"); hits=1;
                    break;
                case 6:
                    if(stage.Monster!=null)
                    {
                        Move(stage.Monster.transform.position+Vector3.right*2.3f); Aim(stage.Monster.transform.position+Vector3.up*.9f); player.Swing();
                        Check(++hits<12,"Guardian cannot be defeated by swings"); return;
                    }
                    Check(stage.Cleared && exit.CanTravel && !stage.CheckBoundary(player),"Clear/exit unlock failed");
                    var throwable=world.CreateItem(ItemKind.Curio,0,6,Vector3.up*5);
                    Hold(throwable,3); Check(!player.EmptyHand.IsVisible,"Held item did not hide hand");
                    player.ThrowSelectedItem(); Check(player.HeldItem==null && player.EmptyHand.IsVisible,"Throw did not restore empty hand");
                    UnityEngine.Object.Destroy(throwable.gameObject);
                    player.SelectSlot(2); scale=grownItem.transform.lossyScale;
                    Move(exit.transform.position+Vector3.back*2.5f+Vector3.up*.1f);
                    Aim(exit.transform.position+Vector3.up); player.Interact();
                    break;
                case 7:
                    Check(SceneManager.GetActiveScene().name=="StageTwo","StageTwo not loaded");
                    var nextWorld=UnityEngine.Object.FindFirstObjectByType<FarmPrototype>();
                    Check(nextWorld.Gold==gold && nextWorld.Player.HeldItem==grownItem,"Travel lost gold/inventory/selection");
                    Check(grownItem.HasBeenPlanted && grownItem.transform.lossyScale==scale,"Travel lost size/history");
                    Check(!nextWorld.Player.EmptyHand.IsVisible,"Travel holding item showed empty hand");
                    nextWorld.Player.SelectSlot(11); Check(nextWorld.Player.EmptyHand.IsVisible,"Empty hand missing after travel");
                    Check(UnityEngine.Object.FindFirstObjectByType<FarmFirstStage>()==null && UnityEngine.Object.FindFirstObjectByType<FarmGuardian>()==null,"First-stage encounter leaked");
                    SceneManager.LoadScene("FirstFarm");
                    break;
                case 8:
                    world=UnityEngine.Object.FindFirstObjectByType<FarmPrototype>(); player=world.Player; stage=world.GetComponent<FarmFirstStage>();
                    Check(!stage.Spawned && !stage.Monster.IsHostile,"New round merchant not peaceful");
                    Move(stage.Monster.transform.position+Vector3.right*2+Vector3.up*.1f);
                    Aim(stage.Monster.transform.position+Vector3.up*.95f); player.Swing();
                    Check(stage.Spawned && stage.Monster.IsHostile && Mathf.Abs(stage.Monster.Health-11)<.001f,"Hitting peaceful merchant did not trigger combat/damage");
                    health=player.Health;
                    break;
                case 9:
                    Check(player.Health<health,"Hit-triggered merchant did not attack");
                    corpse=stage.Monster; corpse.TakeHit(1000);
                    Check(stage.Cleared && UnityEngine.Object.FindFirstObjectByType<FarmStageExit>().CanTravel,"Hit-triggered encounter did not unlock exit");
                    break;
                case 10:
                    Check(corpse!=null && corpse.IsDefeated && !corpse.IsHostile && Mathf.Abs(Vector3.Dot(corpse.transform.up,Vector3.up))<.01f,"Dead merchant disappeared or failed to fall over");
                    Check(corpse.GetComponentsInChildren<Renderer>().Any(r=>r.enabled) && !corpse.GetComponent<CharacterController>().enabled && !corpse.GetComponent<CapsuleCollider>().enabled,"Corpse invisible or character collision still enabled");
                    var corpseSale=world.CreateItem(ItemKind.Curio,0,6,Vector3.up*5); corpseSale.MarkThrown();
                    Check(!corpse.GetComponent<NpcMerchant>().TrySell(corpseSale),"Dead merchant accepted sale"); UnityEngine.Object.Destroy(corpseSale.gameObject);
                    corpse.ResetAfterPlayerDeath(); Check(corpse.IsDefeated && !corpse.BecomeHostile(),"Dead merchant revived after reset");
                    var bodyItem=corpse.GetComponent<FarmItem>(); corpseOwnerId=corpse.GetComponent<PlantableCorpse>().OwnerId;
                    Check(bodyItem!=null && bodyItem.Kind==ItemKind.Corpse && corpseOwnerId==corpse.ActorId,"Corpse pickup/identity missing");
                    var pickup=corpse.GetComponent<BoxCollider>(); var pickupBounds=new Bounds(pickup.center,pickup.size);
                    foreach(var skin in corpse.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var baked=new Mesh(); skin.BakeMesh(baked,false);
                        foreach(var vertex in baked.vertices)
                            Check(pickupBounds.Contains(corpse.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex))),"Pickup collider excludes part of the skinned body");
                        UnityEngine.Object.Destroy(baked);
                    }
                    var center=corpse.GetComponent<BoxCollider>().bounds.center;
                    Move(new Vector3(center.x+2,.1f,center.z)); player.SelectSlot(11); Aim(center); player.Interact();
                    Check(player.HeldItem==bodyItem,"E did not pick up NPC body");
                    revivalScale=bodyItem.transform.lossyScale;
                    surface=UnityEngine.Object.FindFirstObjectByType<SoilSurface>();
                    var revivePoint=new Vector3(20,0,-15);
                    Check(!world.TryPlant(bodyItem,null,revivePoint),"Corpse planted on untilled ground");
                    Check(world.TryTill(surface,revivePoint),"Revival till failed"); revivalPlot=surface.FindPlot(revivePoint);
                    Move(revivePoint+Vector3.back*2+Vector3.up*.1f); Aim(revivePoint); player.Interact();
                    Check(player.HeldItem==null && corpse!=null && revivalPlot.IsOccupied,"Planting consumed/destroyed original NPC body");
                    Check(Vector3.Dot(corpse.transform.up,Vector3.up)>.999f,"Planted NPC did not stand upright");
                    revivalCrop=corpse.GetComponent<FleeingCrop>(); revivalCrop.Grow(100);
                    Check(corpse.IsDefeated && corpse.Health==0 && corpse.transform.lossyScale==revivalScale,"NPC body still uses crop growth");
                    var npcRecovery=corpse.GetComponent<PlantableCorpse>(); CheckBuried(npcRecovery,revivePoint.y);
                    npcRecovery.Recover(.25f);
                    Check(corpse.Health>0 && corpse.Health<corpse.MaxHealth && corpse.IsDefeated && !revivalPlot.GrowthStarted,"NPC did not gradually recover health on dry soil");
                    break;
                case 11:
                    Check(corpse.Health>corpse.MaxHealth*.0625f && corpse.IsDefeated && corpse.transform.lossyScale==revivalScale,"Automatic health recovery failed or changed body size");
                    corpse.GetComponent<PlantableCorpse>().Recover(100);
                    Check(!corpse.IsDefeated && !corpse.IsHostile && corpse.Health==corpse.MaxHealth && !revivalPlot.IsOccupied && stage.Cleared,"NPC revival/reset or stage clear retention failed");
                    Check(corpse.ActorId==corpseOwnerId,"Revival duplicated/replaced NPC identity");
                    Check(corpse.transform.lossyScale==revivalScale,"NPC changed size on revival");
                    Check(corpse.IsReturningHome && Vector3.Distance(corpse.HomePosition,merchantHome)<.001f,"Revival changed original home or failed to start walking back");
                    returnStart=corpse.transform.position; returnStarted=Time.time;
                    var revivedSale=world.CreateItem(ItemKind.Curio,0,6,Vector3.up*5); revivedSale.MarkThrown();
                    Check(corpse.GetComponent<NpcMerchant>().TrySell(revivedSale),"Revived NPC cannot sell");
                    gold=world.Gold;
                    var walkingSale=world.CreateItem(ItemKind.Curio,0,6,corpse.GetComponents<Collider>().First(c=>c.isTrigger).bounds.center); walkingSale.MarkThrown();
                    break;
                case 12:
                    Check(world.Gold>gold,"Walking revived merchant did not receive a physical item sale");
                    if(corpse.IsReturningHome)
                    {
                        Check(Time.time-returnStarted<40,"NPC could not reach home around scene obstacles");
                        Check(Vector3.Distance(corpse.transform.position,returnStart)>.1f,"NPC not walking after revival"); return;
                    }
                    Check(Vector3.Distance(corpse.transform.position,merchantHome)<1.2f && Quaternion.Angle(corpse.transform.rotation,merchantHomeRotation)<.01f,"NPC did not return to original merchant spot and facing");
                    Check(corpse.GetComponent<FarmItem>()==null && corpse.GetComponent<FleeingCrop>()==null && corpse.GetComponent<PlantableCorpse>()==null,"Revival left item/crop components on NPC");
                    corpse.TakeHit(1000); // A revived actor may die and be planted again as a new body.
                    world.SetPlayerRevivalRequiresPlanting(true); player.ReceiveDamage(999);
                    Check(player.IsDead && player.Health==0 && player.DeathBody!=null && player.DeathBody.OwnerId==player.ActorId,"Player body/dead owner identity missing");
                    var helper=new GameObject("Local teammate validation");
                    var helperCamera=new GameObject("Teammate camera").AddComponent<Camera>(); helperCamera.transform.SetParent(helper.transform);
                    helperCamera.transform.localPosition=Vector3.up*1.55f;
                    teammate=helper.AddComponent<LocalFarmer>(); teammate.Configure(world,helperCamera,12);
                    var playerCenter=player.DeathBody.GetComponent<BoxCollider>().bounds.center;
                    helper.transform.position=new Vector3(playerCenter.x+2,.1f,playerCenter.z); Physics.SyncTransforms();
                    helperCamera.transform.rotation=Quaternion.LookRotation(playerCenter-helperCamera.transform.position); teammate.Interact();
                    Check(teammate.HeldItem!=null && teammate.HeldItem.GetComponent<PlantableCorpse>()==player.DeathBody,"Teammate could not pick up player body");
                    revivalScale=player.DeathBody.transform.lossyScale;
                    var playerPoint=new Vector3(23,0,-15); Check(world.TryTill(surface,playerPoint),"Player revival till failed");
                    revivalPlot=surface.FindPlot(playerPoint);
                    helper.transform.position=playerPoint+Vector3.back*2+Vector3.up*.1f;
                    helperCamera.transform.rotation=Quaternion.LookRotation(playerPoint-helperCamera.transform.position); Physics.SyncTransforms(); teammate.Interact();
                    Check(teammate.HeldItem==null && revivalPlot.IsOccupied && player.IsDead,"Teammate planting did not preserve dead owner");
                    Check(Vector3.Dot(player.DeathBody.transform.up,Vector3.up)>.999f,"Planted player body did not stand upright");
                    CheckBuried(player.DeathBody,playerPoint.y);
                    revivalCrop=player.DeathBody.GetComponent<FleeingCrop>(); revivalCrop.Grow(100);
                    Check(player.IsDead && player.Health==0 && player.DeathBody.transform.lossyScale==revivalScale,"Player body still uses growth");
                    player.DeathBody.Recover(.25f);
                    Check(player.IsDead && player.Health>0 && player.Health<player.MaxHealth && !revivalPlot.GrowthStarted && player.DeathBody.transform.lossyScale==revivalScale,"Player health recovery/burial or constant size failed");
                    player.DeathBody.Recover(100);
                    Check(!player.IsDead && player.Health==player.MaxHealth && player.DeathBody==null && Vector3.Distance(player.transform.position,playerPoint+Vector3.up*.1f)<.01f && !revivalPlot.IsOccupied,"Player owner failed revival at planted location");
                    Check(teammate.Health==teammate.MaxHealth,"Wrong player owner was revived");
                    break;
                case 13:
                    Check(corpse.GetComponent<FarmItem>()!=null && !corpse.GetComponent<FarmItem>().HasBeenPlanted,"Second death did not create fresh plantable body");
                    UnityEngine.Object.Destroy(teammate.gameObject); world.SetPlayerRevivalRequiresPlanting(false);
                    File.WriteAllText(Result,"PLAYCHECK_SUCCESS: NPC/player lower half buried, constant body size, immediate gradual real HP recovery on dry soil without growth/watering, full HP revives correct owner and releases soil; full body pickup; NPC walks home/trades, repeated death bodies, local cooperative revival; normal farming growth/harvest, inventory, combat and stage travel checks.");
                    Debug.Log("UNITY_MIGRATION_PLAYCHECK_SUCCESS"); EditorApplication.isPlaying=false; return;
            }
            phase++;
        }
        catch(Exception error) { Debug.LogException(error); File.WriteAllText(Result,error.ToString()); EditorApplication.isPlaying=false; }
    }
}
