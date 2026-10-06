#include "FarmTravel.h"
#include "Components/StaticMeshComponent.h"

void UFarmGameInstance::Capture(AFarmCharacter* Player)
{
    if(!Player) return;
    Items.Reset(); Gold=Player->Gold; Selected=Player->SelectedSlot;
    for(const auto& Item : Player->Inventory)
    {
        FFarmCarriedItem Data;
        if(IsValid(Item))
        {
            Data.Class=Item->GetClass(); Data.Model=Item->Mesh->GetStaticMesh(); Data.Profile=Item->GrowthProfile;
            Data.Kind=Item->Kind; Data.Value=Item->BaseValue; Data.Generation=Item->Generation;
            Data.WasPlanted=Item->HasBeenPlanted; Data.Scale=Item->GetActorScale3D(); Data.OriginalScale=Item->OriginalScale;
            for(int32 I=0; I<Item->Mesh->GetNumMaterials(); ++I) Data.Materials.Add(Item->Mesh->GetMaterial(I));
        }
        Items.Add(Data);
    }
    Pending=true;
}
void UFarmGameInstance::Restore(AFarmCharacter* Player)
{
    if(!Pending || !Player) return;
    Pending=false; Player->Gold=Gold;
    Player->Inventory.SetNum(FMath::Max(Player->Inventory.Num(),Items.Num()));
    for(int32 I=0; I<Items.Num(); ++I)
    {
        const auto& Data=Items[I]; if(!Data.Class) continue;
        FActorSpawnParameters Params; Params.SpawnCollisionHandlingOverride=ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
        Params.TransformScaleMethod=ESpawnActorScaleMethod::OverrideRootScale;
        auto* Item=Player->GetWorld()->SpawnActor<AFarmItem>(Data.Class,FTransform(FQuat::Identity,Player->GetActorLocation(),Data.Scale),Params);
        if(!Item) continue;
        Item->RestockOnPickup=false; Item->Kind=Data.Kind; Item->BaseValue=Data.Value; Item->Generation=Data.Generation;
        Item->GrowthProfile=Data.Profile; Item->HasBeenPlanted=Data.WasPlanted; Item->OriginalScale=Data.OriginalScale;
        Item->Mesh->SetStaticMesh(Data.Model);
        for(int32 M=0; M<Data.Materials.Num(); ++M) Item->Mesh->SetMaterial(M,Data.Materials[M]);
        Item->Pickup(Player->Hand); Player->Inventory[I]=Item;
    }
    Player->Select(FMath::Clamp(Selected,0,Player->Inventory.Num()-1));
    Items.Reset();
}
