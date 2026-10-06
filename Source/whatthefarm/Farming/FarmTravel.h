#pragma once
#include "CoreMinimal.h"
#include "Engine/GameInstance.h"
#include "FarmGameplay.h"
#include "FarmTravel.generated.h"
class UStaticMesh;
class UMaterialInterface;

USTRUCT()
struct FFarmCarriedItem
{
    GENERATED_BODY()
    UPROPERTY() TSubclassOf<AFarmItem> Class;
    UPROPERTY() TObjectPtr<UStaticMesh> Model;
    UPROPERTY() TArray<TObjectPtr<UMaterialInterface>> Materials;
    UPROPERTY() TObjectPtr<UFarmGrowthProfile> Profile;
    UPROPERTY() EFarmKind Kind=EFarmKind::Seed;
    UPROPERTY() int32 Value=10;
    UPROPERTY() int32 Generation=0;
    UPROPERTY() bool WasPlanted=false;
    UPROPERTY() FVector Scale=FVector::OneVector;
    UPROPERTY() FVector OriginalScale=FVector::OneVector;
};

// Travel state exists only for this running game; this is not a disk save system.
UCLASS()
class WHATTHEFARM_API UFarmGameInstance : public UGameInstance
{
    GENERATED_BODY()
public:
    UFUNCTION(BlueprintCallable, Category="Travel") void Capture(AFarmCharacter* Player);
    UFUNCTION(BlueprintCallable, Category="Travel") void Restore(AFarmCharacter* Player);
private:
    UPROPERTY() TArray<FFarmCarriedItem> Items;
    int64 Gold=0;
    int32 Selected=0;
    bool Pending=false;
};
