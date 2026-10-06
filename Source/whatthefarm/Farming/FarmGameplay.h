#pragma once
#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "GameFramework/Actor.h"
#include "GameFramework/Character.h"
#include "GameFramework/GameModeBase.h"
#include "GameFramework/HUD.h"
#include "FarmGameplay.generated.h"

class UStaticMeshComponent;
class UCameraComponent;
class USphereComponent;
class UMeshComponent;
class AFarmItem;
class AFarmSoil;
class AFarmCharacter;

UENUM(BlueprintType)
enum class EFarmKind : uint8 { Seed, Produce, Hoe, WateringCan, Stone };

UCLASS(BlueprintType)
class WHATTHEFARM_API UFarmGrowthProfile : public UDataAsset
{
    GENERATED_BODY()
public:
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Growth", meta=(ClampMin="0")) float BasePercent = 100;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Growth") bool RequireWater = true;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Growth", meta=(ClampMin="0.1")) float GrowthSeconds = 4;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Growth", meta=(ClampMin="0")) float SecondsPerGeneration = 1;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Growth", meta=(ClampMin="1")) float MatureMultiplier = 2;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Growth", meta=(ClampMin="0")) float SizePerGeneration = .32f;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Light") bool UseLight = true;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Light") FVector2D LightRange = FVector2D(60,100);
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Light", meta=(ClampMin="0")) float LightBonus = 25;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Water") bool UseWater = true;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Water") FVector2D WaterRange = FVector2D(40,80);
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Water", meta=(ClampMin="0")) float WaterBonus = 25;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Soil") bool UseSoil = true;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Soil") TArray<FName> PreferredSoils = { FName("Loam") };
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Soil", meta=(ClampMin="0")) float SoilBonus = 20;
    UFUNCTION(BlueprintPure, Category="Growth") float Evaluate(float Light, float Water, FName Soil) const;
};

USTRUCT(BlueprintType)
struct FFarmPlot
{
    GENERATED_BODY()
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly) FVector Center = FVector::ZeroVector;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly) float Radius = 80;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly) float Water = 0;
    UPROPERTY() TObjectPtr<AFarmItem> Plant;
    UPROPERTY() TObjectPtr<UMeshComponent> Visual;
};

UCLASS(Blueprintable)
class WHATTHEFARM_API AFarmSoil : public AActor
{
    GENERATED_BODY()
public:
    AFarmSoil();
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly) TObjectPtr<UStaticMeshComponent> Ground;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Environment") FName SoilType = "Loam";
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Environment", meta=(ClampMin="0",ClampMax="100")) float LightAmount = 80;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Environment", meta=(ClampMin="0",ClampMax="100")) float InitialWater = 0;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Environment") bool UseSunlight = true;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Environment", meta=(ClampMin="0",ClampMax="100")) float ShadeLight = 20;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Farming") TArray<FFarmPlot> Plots;
    UFUNCTION(BlueprintCallable, Category="Farming") int32 Till(FVector Point, float Radius);
    UFUNCTION(BlueprintPure, Category="Farming") int32 FindPlot(FVector Point) const;
    UFUNCTION(BlueprintCallable, Category="Farming") int32 WaterArea(FVector Point, float Radius, float Amount);
    UFUNCTION(BlueprintPure, Category="Environment") float LightAt(FVector Point, AFarmItem* Ignore) const;
    void ReleasePlant(AFarmItem* Item);
    void RefreshPlot(int32 Index);
};

UCLASS(Blueprintable)
class WHATTHEFARM_API AFarmItem : public AActor
{
    GENERATED_BODY()
public:
    AFarmItem();
    virtual void Tick(float DeltaSeconds) override;
    virtual void BeginPlay() override;
    virtual void EndPlay(const EEndPlayReason::Type Reason) override;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly) TObjectPtr<UStaticMeshComponent> Mesh;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Item") EFarmKind Kind = EFarmKind::Seed;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Item", meta=(ClampMin="0")) int32 Generation = 0;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Item", meta=(ClampMin="0")) int32 BaseValue = 10;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Growth") TObjectPtr<UFarmGrowthProfile> GrowthProfile;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Growth") float GrowthPercent = 100;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Growth") bool Planted = false;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Growth") bool HasBeenPlanted = false;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Growth") bool Mature = false;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Growth") float Health = 3;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Item") FVector OriginalScale = FVector::OneVector;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Supply") bool RestockOnPickup = false;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Item") bool Thrown = false;
    UPROPERTY() TObjectPtr<AFarmSoil> HomeSoil;
    int32 PlotIndex = INDEX_NONE;
    UFUNCTION(BlueprintCallable, Category="Item") void Configure(EFarmKind NewKind);
    UFUNCTION(BlueprintPure, Category="Item") float SizeMultiplier() const;
    UFUNCTION(BlueprintPure, Category="Item") int32 Value() const;
    UFUNCTION(BlueprintPure, Category="Item") FString DisplayName() const;
    UFUNCTION(BlueprintCallable, Category="Farming") bool PlantAt(AFarmSoil* Soil, FVector Point);
    UFUNCTION(BlueprintCallable, Category="Farming") void Grow(float Seconds);
    UFUNCTION(BlueprintCallable, Category="Farming") void Hit(float Damage);
    void Pickup(USceneComponent* Hand);
    void Throw(FVector Position, FVector Velocity);
private:
    FVector PlantScale = FVector::OneVector;
    FTransform SupplyTransform;
    float SoilHeight = 0;
    float GrowthProgress = 0;
    float Duration = 4;
    bool Held = false;
    void AlignOnSoil();
    void SetLooseCollision();
};

UCLASS(Blueprintable)
class WHATTHEFARM_API AFarmMerchant : public AActor
{
    GENERATED_BODY()
public:
    AFarmMerchant();
    UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> Body;
    UPROPERTY(VisibleAnywhere) TObjectPtr<USphereComponent> Receiver;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Merchant") FString MerchantName = "Farm Buyer";
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Merchant") TArray<FString> Dialogue = { "Throw your harvest to me to sell it.", "Harvested items can be used or sold, but cannot be planted again." };
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Merchant", meta=(ClampMin="0")) float PriceMultiplier = 1;
    void Talk(AFarmCharacter* Player);
    UFUNCTION(BlueprintCallable, Category="Merchant") bool TrySell(AFarmItem* Item, AFarmCharacter* Player);
    UFUNCTION() void Receive(UPrimitiveComponent* Component, AActor* Other, UPrimitiveComponent* OtherComponent, int32 BodyIndex, bool Sweep, const FHitResult& Hit);
private:
    int32 DialogueIndex = 0;
};

UCLASS(Blueprintable)
class WHATTHEFARM_API AFarmCharacter : public ACharacter
{
    GENERATED_BODY()
public:
    AFarmCharacter();
    virtual void BeginPlay() override;
    virtual void Tick(float Seconds) override;
    virtual void SetupPlayerInputComponent(UInputComponent* Input) override;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly) TObjectPtr<UCameraComponent> Camera;
    UPROPERTY(VisibleAnywhere) TObjectPtr<USceneComponent> Hand;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Inventory", meta=(ClampMin="1")) int32 InventorySlots = 12;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Inventory") TArray<TObjectPtr<AFarmItem>> Inventory;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Inventory") int32 SelectedSlot = 0;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Currency") int64 Gold = 0;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Combat", meta=(ClampMin="1")) float MaxHealth = 100;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Combat") float Health = 100;
    UFUNCTION(BlueprintCallable, Category="Combat") void ReceiveMonsterDamage(float Damage);
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Tools", meta=(ClampMin="1")) float HoeRadius = 80;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Tools", meta=(ClampMin="1")) float WaterRadius = 80;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Tools", meta=(ClampMin="1",ClampMax="100")) float WaterPerUse = 25;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Tools", meta=(ClampMin="1")) float ThrowSpeed = 800;
    bool InventoryOpen = false;
    FString Message;
    float MessageUntil = 0;
    void Notify(const FString& Text);
    AFarmItem* HeldItem() const;
    bool Aim(FHitResult& Hit, bool ThroughPlants = false) const;
    UFUNCTION(BlueprintCallable, Category="Inventory") bool PickupItem(AFarmItem* Item);
    UFUNCTION(BlueprintCallable, Category="Inventory") void Select(int32 Index);
    UFUNCTION(BlueprintCallable, Category="Actions") void Interact();
    UFUNCTION(BlueprintCallable, Category="Actions") void Use();
    UFUNCTION(BlueprintCallable, Category="Actions") void Drop();
    UFUNCTION(BlueprintCallable, Category="Inventory") void ToggleInventory();
    void RefreshInventory();
private:
    float NextUse = 0;
    int32 SwapSlot = INDEX_NONE;
};

UCLASS()
class WHATTHEFARM_API AFarmHUD : public AHUD
{
    GENERATED_BODY()
public:
    virtual void DrawHUD() override;
    TArray<FBox2D> InventoryRects;
};

UCLASS()
class WHATTHEFARM_API AFarmGameMode : public AGameModeBase
{
    GENERATED_BODY()
public:
    AFarmGameMode();
};
