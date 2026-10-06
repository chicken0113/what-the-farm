#include "FarmStage.h"
#include "FarmGameplay.h"
#include "FarmTravel.h"
#if WITH_DEV_AUTOMATION_TESTS
#include "Misc/AutomationTest.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/World.h"
#include "Engine/Engine.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FFarmEncounterTest,"WhatTheFarm.Stage.FirstEncounter",EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FFarmEncounterTest::RunTest(const FString&)
{
    UWorld* World=UWorld::CreateWorld(EWorldType::Game,false);
    GEngine->CreateNewWorldContext(EWorldType::Game).SetCurrentWorld(World);
    World->InitializeActorsForPlay(FURL()); World->BeginPlay(); World->SetBegunPlay(true);
    auto* Soil=World->SpawnActor<AFarmSoil>(); Soil->SetActorScale3D(FVector(100,100,.25)); Soil->SetActorLocation(FVector(0,0,-12.5)); Soil->Ground->UpdateBounds();
    auto* Player=World->SpawnActor<AFarmCharacter>(FVector(0,-1000,100),FRotator::ZeroRotator);
    auto* Stage=World->SpawnActor<AFarmFirstStage>(); auto* Exit=World->SpawnActor<AFarmStageExit>(); Exit->FirstStage=Stage;
    if(!TestNotNull(TEXT("Player exists"),Player)) { GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return false; }
    TestFalse(TEXT("Inside original farm does not spawn"),Stage->CheckBoundary(Player));
    TestFalse(TEXT("Exit locked before encounter"),Exit->CanTravel());
    Player->SetActorLocation(FVector(1300,0,100)); TestFalse(TEXT("Exact edge is still inside"),Stage->CheckBoundary(Player));
    Player->SetActorLocation(FVector(1350,0,100)); TestTrue(TEXT("Crossing old fence spawns guardian"),Stage->CheckBoundary(Player));
    auto* Enemy=Stage->Monster.Get();
    if(TestNotNull(TEXT("Guardian spawned"),Enemy))
    {
        TestFalse(TEXT("Repeated crossing cannot spawn a second guardian"),Stage->CheckBoundary(Player));
        TestFalse(TEXT("Exit locked while guardian alive"),Exit->CanTravel());
        Player->SetActorLocation(Enemy->GetActorLocation()+FVector(120,0,0));
        TestTrue(TEXT("Guardian attacks nearby player"),Enemy->Attack(Player));
        TestEqual(TEXT("Attack deducts health"),Player->Health,80.f);
        TestFalse(TEXT("Attack cooldown prevents duplicate hit"),Enemy->Attack(Player));
        Enemy->Hit(2); TestEqual(TEXT("Player hit hurts guardian"),Enemy->Health,10.f);
        Enemy->Hit(100); TestTrue(TEXT("Guardian defeat clears first stage"),Stage->Cleared);
        TestTrue(TEXT("Exit opens only after defeat"),Exit->CanTravel());
        TestFalse(TEXT("Guardian never respawns after defeat"),Stage->CheckBoundary(Player));
        Player->ReceiveMonsterDamage(1000); TestEqual(TEXT("Knockout restores health"),Player->Health,Player->MaxHealth);
    }
    Exit->FirstStage=nullptr; Exit->RequireFirstStageClear=false;
    TestTrue(TEXT("Other stages can travel without first-stage encounter"),Exit->CanTravel());
    GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FFarmTravelTest,"WhatTheFarm.Stage.CarriedItems",EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FFarmTravelTest::RunTest(const FString&)
{
    UWorld* World=UWorld::CreateWorld(EWorldType::Game,false);
    GEngine->CreateNewWorldContext(EWorldType::Game).SetCurrentWorld(World);
    World->InitializeActorsForPlay(FURL()); World->BeginPlay(); World->SetBegunPlay(true);
    auto* From=World->SpawnActor<AFarmCharacter>(FVector(0,-1000,150),FRotator::ZeroRotator);
    auto* To=World->SpawnActor<AFarmCharacter>(FVector(0,1000,150),FRotator::ZeroRotator);
    if(!From || !To) { AddError(TEXT("Travel test pawn spawn failed")); GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return false; }
    auto* Item=World->SpawnActor<AFarmItem>(); Item->Configure(EFarmKind::Hoe); Item->SetActorScale3D(Item->GetActorScale3D()*3); Item->HasBeenPlanted=true;
    From->Select(2); From->PickupItem(Item); From->Gold=123;
    auto* Travel=NewObject<UFarmGameInstance>(); Travel->Capture(From); Travel->Restore(To);
    TestEqual(TEXT("Gold survives travel"),To->Gold,int64(123)); TestEqual(TEXT("Selected slot survives travel"),To->SelectedSlot,2);
    auto* Restored=To->Inventory[2].Get();
    if(TestNotNull(TEXT("Held tool survives travel"),Restored))
    {
        TestTrue(TEXT("Model and size survive travel"),Restored->Mesh->GetStaticMesh()==Item->Mesh->GetStaticMesh() && Restored->GetActorScale3D().Equals(Item->GetActorScale3D()));
        TestEqual(TEXT("Tool size multiplier survives travel"),Restored->SizeMultiplier(),3.f);
        TestTrue(TEXT("Replant restriction survives travel"),Restored->HasBeenPlanted);
        TestFalse(TEXT("Carried tool is not a free supply"),Restored->RestockOnPickup);
        Travel->Restore(To); TestTrue(TEXT("Travel state is consumed once"),To->Inventory[2]==Restored);
    }
    GEngine->DestroyWorldContext(World); World->DestroyWorld(false); return true;
}
#endif
