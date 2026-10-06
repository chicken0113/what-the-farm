#include "FarmGameplay.h"
#if WITH_DEV_AUTOMATION_TESTS
#include "Misc/AutomationTest.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/World.h"
#include "Engine/Engine.h"
#include "EngineUtils.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FFarmCoreTest,"WhatTheFarm.Farming.CoreLoop",EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FFarmCoreTest::RunTest(const FString&)
{
    UWorld* World=UWorld::CreateWorld(EWorldType::Game,false);
    GEngine->CreateNewWorldContext(EWorldType::Game).SetCurrentWorld(World);
    World->InitializeActorsForPlay(FURL()); World->BeginPlay();
    auto* Soil=World->SpawnActor<AFarmSoil>(); Soil->SetActorScale3D(FVector(26,26,.25)); Soil->SetActorLocation(FVector(0,0,-12.5)); Soil->Ground->UpdateBounds(); Soil->UseSunlight=false;
    auto* Profile=NewObject<UFarmGrowthProfile>();
    TestEqual(TEXT("Additive environment bonuses"),Profile->Evaluate(80,50,"Loam"),170.f);
    TestEqual(TEXT("Unmatched conditions keep base size"),Profile->Evaluate(0,0,"Sand"),100.f);
    auto* Hoe=World->SpawnActor<AFarmItem>(); Hoe->Configure(EFarmKind::Hoe); Hoe->SetActorScale3D(Hoe->GetActorScale3D()*2);
    TestEqual(TEXT("Tool size ratio"),Hoe->SizeMultiplier(),2.f);
    const int32 Plot=Soil->Till(FVector::ZeroVector,80*Hoe->SizeMultiplier());
    TestTrue(TEXT("Bigger hoe makes a bigger plot"),Plot!=INDEX_NONE && FMath::IsNearlyEqual(Soil->Plots[Plot].Radius,160.f));
    auto* Seed=World->SpawnActor<AFarmItem>(); Seed->Configure(EFarmKind::Seed); Seed->GrowthProfile=Profile;
    const FVector Aim(130,0,0);
    TestTrue(TEXT("Plant at off-centre aim point"),Seed->PlantAt(Soil,Aim));
    TestTrue(TEXT("Aim position retained"),FMath::IsNearlyEqual(Seed->GetActorLocation().X,130.));
    auto* Other=World->SpawnActor<AFarmItem>(); Other->Configure(EFarmKind::Stone);
    TestFalse(TEXT("One plant per plot"),Other->PlantAt(Soil,FVector::ZeroVector));
    TestFalse(TEXT("Untilled ground rejects planting"),Other->PlantAt(Soil,FVector(500,0,0)));
    FVector Small=Seed->GetActorScale3D(); Seed->Grow(20);
    TestTrue(TEXT("Dry crop stays small"),Seed->GetActorScale3D().Equals(Small));
    TestEqual(TEXT("Water aimed at plant location"),Soil->WaterArea(Aim,80,50),1);
    Seed->Grow(2); TestFalse(TEXT("Size bonus does not shorten duration"),Seed->Mature);
    Seed->Grow(2); TestTrue(TEXT("Watered seed matures"),Seed->Mature);
    TestTrue(TEXT("Final size includes environment"),Seed->GetActorScale3D().Equals(Small*3.4));
    const FVector Grown=Seed->GetActorScale3D(); auto Mesh=Seed->Mesh->GetStaticMesh();
    Seed->Hit(100); TestFalse(TEXT("Harvest returns a loose item"),Seed->Mature);
    TestTrue(TEXT("Harvest preserves scale and mesh"),Seed->GetActorScale3D().Equals(Grown) && Seed->Mesh->GetStaticMesh()==Mesh);
    TestTrue(TEXT("Seed becomes produce"),Seed->Kind==EFarmKind::Produce);
    TestTrue(TEXT("Profile preserved"),Seed->GrowthProfile==Profile);
    const int32 HarvestValue=Seed->Value();
    TestFalse(TEXT("Harvest cannot be planted again"),Seed->PlantAt(Soil,Aim));
    Soil->Till(FVector(500,0,0),80);
    TestFalse(TEXT("New soil cannot reset planting history"),Seed->PlantAt(Soil,FVector(500,0,0)));
    TestTrue(TEXT("Rejected replant preserves size and value"),Seed->GetActorScale3D().Equals(Grown) && Seed->Value()==HarvestValue);
    TestTrue(TEXT("An unplanted object can use the freed plot"),Other->PlantAt(Soil,Aim));
    TestEqual(TEXT("First planting does not increment generation"),Other->Generation,0);
    Soil->WaterArea(Aim,80,50); Other->Grow(20); Other->Hit(100);
    TestFalse(TEXT("Harvested tool or item cannot be planted again"),Other->PlantAt(Soil,Aim));
    auto* Player=World->SpawnActor<AFarmCharacter>(FVector(0,-1000,150),FRotator::ZeroRotator);
    if(!TestNotNull(TEXT("Player spawned"),Player)) { GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return false; }
    Player->Inventory.SetNum(2);
    auto* Supply=World->SpawnActor<AFarmItem>(); Supply->Configure(EFarmKind::WateringCan); Supply->RestockOnPickup=true;
    int32 Before=0; for(TActorIterator<AFarmItem> It(World);It;++It) ++Before;
    TestTrue(TEXT("Pickup fills inventory"),Player->PickupItem(Supply));
    int32 After=0; for(TActorIterator<AFarmItem> It(World);It;++It) ++After;
    TestEqual(TEXT("Original supply immediately refills"),After,Before+1);
    Player->Select(1); TestTrue(TEXT("Second slot works"),Player->PickupItem(Seed));
    TestFalse(TEXT("Pickup does not reset planting history"),Seed->PlantAt(Soil,Aim));
    int32 FullBefore=0; for(TActorIterator<AFarmItem> It(World);It;++It) ++FullBefore;
    TestFalse(TEXT("Full inventory does not consume supply"),Player->PickupItem(Other));
    int32 FullAfter=0; for(TActorIterator<AFarmItem> It(World);It;++It) ++FullAfter;
    TestEqual(TEXT("Full inventory does not refill"),FullAfter,FullBefore);
    auto* Merchant=World->SpawnActor<AFarmMerchant>();
    TestFalse(TEXT("Held items cannot be sold"),Merchant->TrySell(Seed,Player));
    Seed->Throw(FVector(500,0,100),FVector(800,0,0));
    TestFalse(TEXT("Throwing does not reset planting history"),Seed->PlantAt(Soil,Aim));
    const int32 Price=Seed->Value(); TestTrue(TEXT("Thrown item sells"),Merchant->TrySell(Seed,Player));
    TestEqual(TEXT("Sale pays correct gold"),Player->Gold,int64(Price));
    TestFalse(TEXT("Cannot pay twice"),Merchant->TrySell(Seed,Player));
    GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FFarmWaterTest,"WhatTheFarm.Farming.ToolRanges",EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FFarmWaterTest::RunTest(const FString&)
{
    UWorld* World=UWorld::CreateWorld(EWorldType::Game,false);
    GEngine->CreateNewWorldContext(EWorldType::Game).SetCurrentWorld(World);
    World->InitializeActorsForPlay(FURL()); World->BeginPlay();
    auto* Soil=World->SpawnActor<AFarmSoil>(); Soil->SetActorScale3D(FVector(26,26,.25)); Soil->SetActorLocation(FVector(0,0,-12.5)); Soil->Ground->UpdateBounds();
    AFarmItem* Crops[4]; const float Positions[]={0,60,130,210};
    for(int32 I=0;I<4;++I)
    {
        FVector Point(Positions[I],0,0); Soil->Till(Point,20); Crops[I]=World->SpawnActor<AFarmItem>(); Crops[I]->Configure(EFarmKind::Seed);
        TestTrue(TEXT("Neighbour planted"),Crops[I]->PlantAt(Soil,Point+FVector(15,0,0)));
    }
    auto* Can=World->SpawnActor<AFarmItem>(); Can->Configure(EFarmKind::WateringCan);
    TestEqual(TEXT("Normal water range"),Soil->WaterArea(FVector::ZeroVector,80*Can->SizeMultiplier(),25),2);
    Can->SetActorScale3D(Can->GetActorScale3D()*2);
    TestEqual(TEXT("Double-size water range reaches more plants"),Soil->WaterArea(FVector::ZeroVector,80*Can->SizeMultiplier(),25),3);
    TestEqual(TEXT("Water amount per plant is unchanged"),Soil->Plots[2].Water,25.f);
    TestEqual(TEXT("Outside radius is dry"),Soil->Plots[3].Water,0.f);
    Can->SetActorScale3D(Can->GetActorScale3D()*.25);
    TestEqual(TEXT("Half-size radius"),Soil->WaterArea(FVector::ZeroVector,80*Can->SizeMultiplier(),25),1);
    TestTrue(TEXT("Overlapping growth never destroys neighbours"),IsValid(Crops[1]) && IsValid(Crops[2]));
    GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return true;
}
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FFarmSupplyTest,"WhatTheFarm.Farming.SupplyAndCollision",EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FFarmSupplyTest::RunTest(const FString&)
{
    UWorld* World=UWorld::CreateWorld(EWorldType::Game,false);
    GEngine->CreateNewWorldContext(EWorldType::Game).SetCurrentWorld(World);
    World->InitializeActorsForPlay(FURL()); World->BeginPlay();
    // This isolated world has no game mode to dispatch StartPlay for spawned actors.
    World->SetBegunPlay(true);
    auto* Player=World->SpawnActor<AFarmCharacter>(FVector(0,-1000,150),FRotator::ZeroRotator);
    if(!TestNotNull(TEXT("Player spawned"),Player)) { GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return false; }
    Player->Inventory.SetNum(1);
    FActorSpawnParameters Params; Params.SpawnCollisionHandlingOverride=ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
    Params.TransformScaleMethod=ESpawnActorScaleMethod::OverrideRootScale;
    const FTransform Display(FRotator(90,30,0),FVector(320,-700,25),FVector(.12,.08,.24));
    auto* Supply=World->SpawnActor<AFarmItem>(AFarmItem::StaticClass(),Display,Params);
    Supply->RestockOnPickup=true; Supply->Mesh->SetSimulatePhysics(false);
    for(int32 Cycle=0; Cycle<4; ++Cycle)
    {
        AddInfo(FString::Printf(TEXT("Refill cycle %d actual=%s expected=%s"),Cycle,*Supply->GetActorTransform().ToString(),*Display.ToString()));
        TestTrue(TEXT("Refill stays at its display location/rotation/size"),Supply->GetActorTransform().Equals(Display,.001));
        TestEqual(TEXT("Loose item does not block player"),Supply->Mesh->GetCollisionResponseToChannel(ECC_Pawn),ECR_Ignore);
        TestEqual(TEXT("Loose items do not push each other"),Supply->Mesh->GetCollisionResponseToChannel(ECC_PhysicsBody),ECR_Ignore);
        TestEqual(TEXT("Pickup trace remains available"),Supply->Mesh->GetCollisionResponseToChannel(ECC_Visibility),ECR_Block);
        TestEqual(TEXT("Floor still supports item"),Supply->Mesh->GetCollisionResponseToChannel(ECC_WorldStatic),ECR_Block);
        TestTrue(TEXT("Repeated pickup succeeds"),Player->PickupItem(Supply));
        Supply->Throw(FVector(0,0,150),FVector::ZeroVector);
        TestEqual(TEXT("Thrown item still ignores player"),Supply->Mesh->GetCollisionResponseToChannel(ECC_Pawn),ECR_Ignore);
        Player->Inventory[0]=nullptr;
        Supply->Destroy(); Supply=nullptr;
        for(TActorIterator<AFarmItem> It(World); It; ++It) if(It->RestockOnPickup) { Supply=*It; break; }
        if(!TestNotNull(TEXT("Replacement exists"),Supply)) break;
        TestTrue(TEXT("Refilled baseline keeps tool ratio at one"),FMath::IsNearlyEqual(Supply->SizeMultiplier(),1.f));
    }
    GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return true;
}
#endif
