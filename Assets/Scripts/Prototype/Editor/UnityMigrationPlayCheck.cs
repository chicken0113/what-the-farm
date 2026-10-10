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
    static float shotWaitStarted;
    static void CheckAction(FarmActionAnimation.Action expected,Transform moving)
    {
        var actions=player.Actions;
        Check(actions!=null && actions.CurrentAction==expected,"Player action was not triggered: "+expected);
        Vector3 start=moving.position; var cameraPosition=player.View.transform.position; var cameraRotation=player.View.transform.rotation;
        actions.AdvanceAnimation(.16f);
        Check(Vector3.Distance(moving.position,start)>.015f,"Action did not move the visible hand/item: "+expected);
        Check(player.View.transform.position==cameraPosition && player.View.transform.rotation==cameraRotation,"Animation moved aim camera");
        actions.AdvanceAnimation(1);
        Check(!actions.IsPlaying && Vector3.Distance(moving.position,start)<.001f,"Action did not return to idle pose");
    }
    static bool TillForRecovery(SoilSurface soil,Vector3 point)
    {
        var ready=world.CreateItem(ItemKind.Tool,0,10,Vector3.up*10); ready.MarkPlanted();
        bool result=world.TryTill(soil,point,ready); UnityEngine.Object.Destroy(ready.gameObject); return result;
    }
    static void CheckPlantFootprint(SoilSurface soil)
    {
        var center=new Vector3(30,0,20);
        var dry=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Tilled.mat");
        var wet=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Wet.mat");
        var plot=soil.Till(center,.6f,dry,wet);
        var model=new GameObject("Plant footprint check"); var item=model.AddComponent<FarmItem>(); item.Configure(ItemKind.Curio,0,10);
        var child=GameObject.CreatePrimitive(PrimitiveType.Cube); child.transform.SetParent(model.transform,false);
        child.transform.localPosition=new Vector3(.15f,1.5f,0); child.transform.localScale=new Vector3(.5f,3,.5f);
        model.transform.SetPositionAndRotation(Vector3.up*10,Quaternion.Euler(80,30,20));
        var bounds=item.PlantingBounds(Quaternion.identity);
        Check(Mathf.Abs(bounds.size.x-.5f)<.001f && Mathf.Abs(bounds.size.z-.5f)<.001f,"Held rotation distorted planted footprint");
        Check(!world.TryPlant(item,plot,center+Vector3.right*.3f) && !plot.IsOccupied && !item.HasBeenPlanted,"Edge-overhanging item planted or rejection consumed item");
        model.transform.localScale=Vector3.one*3;
        Check(!world.TryPlant(item,plot,center) && !plot.IsOccupied && !item.HasBeenPlanted,"Oversized item planted in small plot");
        model.transform.localScale=Vector3.one;
        var aimPoint=center-Vector3.right*.05f;
        Check(world.TryPlant(item,plot,aimPoint),"Tall item with fitting horizontal footprint rejected");
        var planted=world.GetComponentsInChildren<FleeingCrop>().Single(c=>c.Plot==plot);
        Check(Vector2.Distance(new Vector2(planted.transform.position.x,planted.transform.position.z),new Vector2(aimPoint.x,aimPoint.z))<.001f,"Footprint check snapped plant to plot centre");
        UnityEngine.Object.Destroy(planted.gameObject); UnityEngine.Object.Destroy(model);
    }
    static void CheckBuried(PlantableCorpse body, float groundHeight)
    {
        var renderers=body.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds; foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        Check(bounds.min.y<groundHeight-.1f && bounds.max.y>groundHeight+.1f && Mathf.Abs(bounds.center.y-groundHeight)<.05f,"Body is not buried to its waist");
    }
    static Color[] Appearance(FarmGuardian merchant) => merchant.transform.Find("Visual").GetComponentsInChildren<Renderer>().Select(renderer =>
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
                    CheckAction(FarmActionAnimation.Action.Swing,player.EmptyHand.transform);
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
                    CheckAction(FarmActionAnimation.Action.Pickup,stock.transform);
                    Check(!player.EmptyHand.IsVisible,"Hand visible while holding an item");
                    player.SelectSlot(11); Check(player.EmptyHand.IsVisible,"Empty slot did not show hand");
                    player.SelectSlot(0); Check(!player.EmptyHand.IsVisible,"Item slot did not hide hand");
                    var newStock=world.GetComponentsInChildren<FarmItem>().First(i=>i.Kind==ItemKind.Tool && i.transform.parent==world.transform);
                    player.SelectSlot(1); Physics.SyncTransforms(); Aim(newStock.GetComponent<Collider>().bounds.center);
                    bool stockRay=player.TryLook(out RaycastHit stockHit); player.Interact();
                    Check(player.HeldItem!=null && player.HeldItem.Kind==ItemKind.Tool,"Immediate restock pickup failed: centre="+newStock.GetComponent<Collider>().bounds.center+" player="+player.transform.position+" ray="+stockRay+" hit="+(stockRay?stockHit.collider.name:"none"));
                    var refill=world.GetComponentsInChildren<FarmItem>().First(i=>i.Kind==ItemKind.Tool && i.transform.parent==world.transform);
                    Check(Vector3.Distance(refill.transform.position,stockPosition)<.001f,"Restock position drift");
                    Check(!refill.CanUseTool && !refill.GetComponent<GrowableTool>().IsComplete,"Restock supplied a complete usable shovel");
                    Check(Physics.GetIgnoreLayerCollision(8,9) && Physics.GetIgnoreLayerCollision(8,8),"Loose item collision");
                    Check(stock.GetComponentsInChildren<Renderer>(true).Max(r=>r.bounds.size.magnitude)<1,"Tool not reduced");
                    Check(!player.HeldItem.CanUseTool && player.HeldItem.GetComponent<GrowableTool>()!=null,"Purchased shovel should be an unusable head");
                    Check(!world.TryTill(soil,new Vector3(2.8f,0,-8.5f),player.HeldItem) && soil.FindPlot(new Vector3(2.8f,0,-8.5f))==null,"Ungrown shovel tilled soil");
                    int headVertices=player.HeldItem.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount;
                    var heldScale=player.HeldItem.transform.localScale;
                    player.HeldItem.transform.localScale*=10;
                    Aim(new Vector3(.8f,0,-8.5f)); player.Interact();
                    Check(player.HeldItem!=null && !player.HeldItem.HasBeenPlanted,"Oversized hoe planted in fist-sized soil or rejection lost inventory");
                    player.HeldItem.transform.localScale=heldScale;
                    var largerPoint=new Vector3(1.3f,0,-8.5f);
                    Check(world.TryTill(soil,largerPoint),"Bare hand planting area till failed");
                    Check(world.TryTill(soil,largerPoint) && soil.FindPlot(largerPoint).Radius>.1f,"Repeated bare-hand till did not enlarge empty soil for bigger blade");
                    Aim(largerPoint); player.Interact();
                    Check(player.HeldItem==null && player.EmptyHand.IsVisible,"Planting did not restore empty hand");
                    CheckAction(FarmActionAnimation.Action.Plant,player.EmptyHand.transform);
                    var plantedExtra=world.GetComponentsInChildren<FleeingCrop>().Single();
                    Check(plantedExtra.GetComponent<GrowableTool>().IsComplete && plantedExtra.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount>headVertices,"Planting did not reveal complete shovel");
                    var shovelScale=plantedExtra.transform.lossyScale; float buriedY=plantedExtra.transform.position.y;
                    var shovelRenderer=plantedExtra.GetComponentInChildren<Renderer>();
                    var shovelColours=shovelRenderer.sharedMaterials.Select(m=>m.color).ToArray();
                    Check(shovelRenderer.bounds.min.y<largerPoint.y-.1f && shovelRenderer.bounds.max.y>largerPoint.y,"Shovel handle not buried below exposed blade");
                    plantedExtra.Grow(20); Check(!plantedExtra.IsMature,"Unwatered shovel grew");
                    Check(plantedExtra.transform.position.y==buriedY && plantedExtra.transform.lossyScale==shovelScale,"Dry shovel moved or changed size");
                    plantedExtra.Plot.Water(50); plantedExtra.Grow(plantedExtra.GrowthProfile.growthSeconds*.5f);
                    Check(!plantedExtra.IsMature && plantedExtra.transform.position.y>buriedY && shovelRenderer.bounds.min.y<largerPoint.y-.05f && plantedExtra.transform.lossyScale==shovelScale,"Shovel did not rise gradually at constant size");
                    plantedExtra.Grow(20);
                    Check(Mathf.Abs(shovelRenderer.bounds.min.y-largerPoint.y)<.001f && plantedExtra.transform.lossyScale==shovelScale,"Mature shovel not fully raised or changed size");
                    Check(shovelColours.SequenceEqual(shovelRenderer.sharedMaterials.Select(m=>m.color)),"Maturity recoloured shovel");
                    var combatStart=player.transform.position;
                    Move(plantedExtra.transform.position+Vector3.right*3+Vector3.up*.1f);
                    float beforeDistance=Vector3.Distance(plantedExtra.transform.position,player.transform.position);
                    plantedExtra.TickCombat(.2f);
                    Check(Vector3.Distance(plantedExtra.transform.position,player.transform.position)<beforeDistance,"Mature shovel fled instead of approaching player");
                    Move(plantedExtra.transform.position+Vector3.right*.8f+Vector3.up*.1f);
                    health=player.Health; plantedExtra.TickCombat(.01f);
                    Check(player.Health==health,"Shovel attack had no windup");
                    var attackFacing=plantedExtra.transform.rotation;
                    plantedExtra.TickCombat(.12f); Check(Quaternion.Angle(attackFacing,plantedExtra.transform.rotation)>5 && player.Health==health,"Shovel did not wind its body back before attack");
                    plantedExtra.TickCombat(.28f); Check(player.Health==health-10 && Quaternion.Angle(attackFacing,plantedExtra.transform.rotation)>30,"Mature shovel melee/sweeping body motion failed");
                    health=player.Health; plantedExtra.TickCombat(.1f); Check(player.Health==health,"Shovel ignored attack cooldown");
                    var meleeCover=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    meleeCover.transform.position=(plantedExtra.transform.position+player.transform.position)*.5f+Vector3.up*.6f;
                    meleeCover.transform.localScale=new Vector3(.1f,1,.4f); Physics.SyncTransforms();
                    plantedExtra.TickCombat(1.3f); plantedExtra.TickCombat(.4f); Check(player.Health==health,"Shovel hit through solid cover");
                    meleeCover.GetComponent<Collider>().enabled=false; UnityEngine.Object.Destroy(meleeCover); Physics.SyncTransforms();
                    plantedExtra.TickCombat(1.3f); Move(plantedExtra.transform.position+Vector3.right*3+Vector3.up*.1f);
                    plantedExtra.TickCombat(.4f); Check(player.Health==health,"Retreating player could not avoid shovel attack");
                    Move(combatStart);
                    plantedExtra.TakeHit(100);
                    plantedExtra.TickCombat(.4f); Check(player.Health==health,"Harvested shovel still attacked");
                    var readyShovel=plantedExtra.GetComponents<FarmItem>().Last(i=>i.HasBeenPlanted);
                    Check(readyShovel!=null && readyShovel.CanUseTool && readyShovel.HasBeenPlanted && readyShovel.GetComponent<GrowableTool>().IsComplete,"Harvested shovel not usable/full model");
                    Hold(readyShovel,0); UnityEngine.Object.Destroy(stock.gameObject);
                    CheckPlantFootprint(soil);
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
                    Hold(can,4); player.Actions.PlayUse(); CheckAction(FarmActionAnimation.Action.Use,can.transform);
                    player.Actions.PlaySwing(); CheckAction(FarmActionAnimation.Action.Swing,can.transform);
                    Check(world.TryWater(surface,plantedPoint,can)==1,"Watering failed"); world.TryWater(surface,plantedPoint,can); crop.Grow(20);
                    Check(crop.IsMature && !home.IsOccupied,"Growth did not free plot");
                    scale=crop.transform.lossyScale; var model=crop.gameObject; crop.TakeHit(1000); grownItem=model.GetComponent<FarmItem>();
                    Check(grownItem.HasBeenPlanted && grownItem.transform.lossyScale==scale,"Harvest size/history changed: planted="+grownItem.HasBeenPlanted+" before="+scale.ToString("F6")+" after="+grownItem.transform.lossyScale.ToString("F6"));
                    Check(!world.TryPlant(grownItem,home,plantedPoint),"Harvested item can be replanted");
                    Check(Mathf.Abs(world.TillingRadiusFor(grownItem)-.4f*grownItem.SizeMultiplier)<.001f,"Grown tool radius not linear");
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
                    Move(new Vector3(18,.1f,7.5f)); // Clear sight line beside the seller, beyond melee reach.
                    Check(stage.Monster.Firearm!=null && stage.Monster.Firearm.IsEquipped,"Hostile merchant did not equip existing gun model");
                    Check(stage.Monster.Firearm.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Held gun has physical collision"); health=player.Health; shotWaitStarted=Time.time;
                    break;
                case 4:
                    if(player.Health==health && Time.time-shotWaitStarted<2) return;
                    Check(stage.Monster.Firearm.ShotsFired>0 && player.Health<health && Vector3.Distance(stage.Monster.transform.position,player.transform.position)>4,"Merchant did not damage player at gun range: shots="+stage.Monster.Firearm.ShotsFired+" HP="+player.Health+" before="+health+" distance="+Vector3.Distance(stage.Monster.transform.position,player.transform.position));
                    Check(Vector3.Distance(enemyStart,stage.Monster.transform.position)>.1f,"Monster did not chase: start="+enemyStart+" now="+stage.Monster.transform.position+" time="+Time.time);
                    var modelForward=stage.Monster.transform.Find("Visual").forward;
                    var chaseDirection=player.transform.position-stage.Monster.transform.position; chaseDirection.y=0;
                    Check(Vector3.Dot(modelForward,chaseDirection.normalized)>.99f,"Merchant visual faces backwards while chasing");
                    var gun=stage.Monster.Firearm; var cover=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cover.transform.position=(stage.Monster.transform.position+player.transform.position)*.5f+Vector3.up;
                    cover.transform.localScale=new Vector3(2,3,2); Physics.SyncTransforms();
                    health=player.Health; int beforeShots=gun.ShotsFired;
                    Check(!gun.TryFire(player,12) && player.Health==health && gun.ShotsFired==beforeShots,"Merchant shot through solid cover");
                    cover.GetComponent<Collider>().enabled=false; UnityEngine.Object.Destroy(cover); Physics.SyncTransforms();
                    gun.AdvanceProjectiles(2); health=player.Health;
                    Check(gun.TryFire(player,1) && player.Health==health && gun.ActiveBulletCount==1,"Bullet caused instant damage");
                    gun.AdvanceProjectiles(.01f); Check(player.Health==health,"Bullet hit before reaching player");
                    gun.AdvanceProjectiles(2); Check(player.Health==health-1 && gun.ActiveBulletCount==0,"Stationary player was not hit by travelling bullet");
                    var dodgeStart=player.transform.position;
                    var sideways=Vector3.Cross(Vector3.up,(dodgeStart-stage.Monster.transform.position).normalized).normalized;
                    health=player.Health; Check(gun.TryFire(player,1),"Dodge test could not fire");
                    for(int step=0;step<180;step++)
                    {
                        Move(dodgeStart+sideways*(7f*.01f*(step+1)));
                        gun.AdvanceProjectiles(.01f);
                    }
                    Check(player.Health==health && gun.ActiveBulletCount==0,"Sprinting player could not dodge fixed-direction bullet");
                    Move(dodgeStart); Check(gun.TryFire(player,1),"Incoming cover test could not fire");
                    var incomingCover=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    incomingCover.transform.position=(gun.MuzzlePosition+dodgeStart+Vector3.up*1.05f)*.5f;
                    incomingCover.transform.localScale=new Vector3(2,3,2); Physics.SyncTransforms();
                    gun.AdvanceProjectiles(2);
                    Check(player.Health==health && gun.ActiveBulletCount==0,"Travelling bullet passed through newly inserted cover");
                    incomingCover.GetComponent<Collider>().enabled=false; UnityEngine.Object.Destroy(incomingCover);
                    Move(stage.Monster.transform.position+Vector3.right*1.5f); health=player.Health; shotWaitStarted=Time.time;
                    break;
                case 5:
                    if(player.Health==health && Time.time-shotWaitStarted<2) return;
                    Check(player.Health<health,"Monster did not damage player");
                    player.ReceiveDamage(999); Check(player.Health==player.MaxHealth && Vector3.Distance(player.transform.position,world.SpawnPosition)<.01f,"Knockout failed");
                    Check(player.CaptureInventory()[2]==grownItem,"Knockout lost inventory");
                    Check(!stage.Spawned && !stage.Cleared && !stage.Monster.IsHostile && stage.Monster.Health==stage.Monster.MaxHealth,"Knockout did not reset merchant encounter");
                    Check(!stage.Monster.Firearm.IsEquipped,"Merchant kept gun equipped after player death");
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
                    Check(!corpse.Firearm.IsEquipped,"Dead merchant retained equipped gun");
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
                    Check(TillForRecovery(surface,revivePoint),"Revival till failed"); revivalPlot=surface.FindPlot(revivePoint);
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
                    Check(!corpse.Firearm.IsEquipped,"Revived peaceful merchant equipped gun");
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
                    var playerPoint=new Vector3(23,0,-15); Check(TillForRecovery(surface,playerPoint),"Player revival till failed");
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
                    File.WriteAllText(Result,"PLAYCHECK_SUCCESS: first-person punch/item swing/pickup/use/plant poses move and return without moving aim camera; mature shovel windup and body sweep; doubled shovel size, repeated bare-hand patch expansion, mature shovel pursues/melee attacks with windup/cooldown, cover blocks and retreat dodges, harvest stops attacks; shovel handle buried below exposed blade, dry shovel stationary, gradual upward translation at fixed scale, full emergence, colours preserved; 3x growable shovel stock is head-only and unusable, restocks as head, planting reveals full mesh, watering required, harvested shovel usable with full mesh; footprint fit, oversized/edge rejection preserves item, held rotation ignored, tall item accepted at mouse position, NPC/player body fit; finite-speed bullets, no instant damage, stationary hit, 7m/s sideways sprint dodges, fixed firing direction, initial and newly inserted cover block shots, bullets expire; merchant gun hidden on player death/NPC death/peaceful revival; NPC/player burial, health recovery and owner revival; NPC walks home/trades; farming, inventory, combat and travel.");
                    Debug.Log("UNITY_MIGRATION_PLAYCHECK_SUCCESS"); EditorApplication.isPlaying=false; return;
            }
            phase++;
        }
        catch(Exception error) { Debug.LogException(error); File.WriteAllText(Result,error.ToString()); EditorApplication.isPlaying=false; }
    }
}
