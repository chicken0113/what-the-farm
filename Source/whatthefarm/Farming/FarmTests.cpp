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
    TestTrue(TEXT("Replant keeps harvested size"),Seed->PlantAt(Soil,Aim) && Seed->GetActorScale3D().Equals(Grown));
    Soil->WaterArea(Aim,80,50); Seed->Grow(20); Seed->Hit(100);
    TestTrue(TEXT("Replant generation increases"),Seed->Generation==1);
    auto* Player=World->SpawnActor<AFarmCharacter>(FVector(0,-1000,150),FRotator::ZeroRotator);
    if(!TestNotNull(TEXT("Player spawned"),Player)) { GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return false; }
    Player->Inventory.SetNum(2);
    auto* Supply=World->SpawnActor<AFarmItem>(); Supply->Configure(EFarmKind::WateringCan); Supply->RestockOnPickup=true;
    int32 Before=0; for(TActorIterator<AFarmItem> It(World);It;++It) ++Before;
    TestTrue(TEXT("Pickup fills inventory"),Player->PickupItem(Supply));
    int32 After=0; for(TActorIterator<AFarmItem> It(World);It;++It) ++After;
    TestEqual(TEXT("Original supply immediately refills"),After,Before+1);
    Player->Select(1); TestTrue(TEXT("Second slot works"),Player->PickupItem(Seed));
    int32 FullBefore=0; for(TActorIterator<AFarmItem> It(World);It;++It) ++FullBefore;
    TestFalse(TEXT("Full inventory does not consume supply"),Player->PickupItem(Other));
    int32 FullAfter=0; for(TActorIterator<AFarmItem> It(World);It;++It) ++FullAfter;
    TestEqual(TEXT("Full inventory does not refill"),FullAfter,FullBefore);
    auto* Merchant=World->SpawnActor<AFarmMerchant>();
    TestFalse(TEXT("Held items cannot be sold"),Merchant->TrySell(Seed,Player));
    Seed->Throw(FVector(500,0,100),FVector(800,0,0));
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
#endif
