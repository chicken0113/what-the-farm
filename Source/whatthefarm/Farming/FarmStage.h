#pragma once
#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "FarmStage.generated.h"

class AFarmCharacter;
class AFarmFirstStage;
class UStaticMeshComponent;

UCLASS(Blueprintable)
class WHATTHEFARM_API AFarmStageMonster : public ACharacter
{
    GENERATED_BODY()
public:
    AFarmStageMonster();
    virtual void BeginPlay() override;
    virtual void Tick(float Seconds) override;
    virtual float TakeDamage(float Damage, const FDamageEvent& Event, AController* Instigator, AActor* Causer) override;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly) TObjectPtr<UStaticMeshComponent> Body;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Combat", meta=(ClampMin="1")) float MaxHealth=12;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Combat") float Health=12;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Combat", meta=(ClampMin="1")) float MoveSpeed=250;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Combat", meta=(ClampMin="1")) float AttackDamage=20;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Combat", meta=(ClampMin="1")) float AttackRange=190;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Combat", meta=(ClampMin="0.1")) float AttackInterval=1.2;
    UPROPERTY() TObjectPtr<AFarmFirstStage> Stage;
    UFUNCTION(BlueprintCallable, Category="Combat") void Hit(float Damage);
    UFUNCTION(BlueprintCallable, Category="Combat") bool Attack(AFarmCharacter* Player);
private:
    float NextAttack=0;
    bool Defeated=false;
};

// Place this actor only in FirstFarm. Other stages do not inherit its boundary rule.
UCLASS(Blueprintable)
class WHATTHEFARM_API AFarmFirstStage : public AActor
{
    GENERATED_BODY()
public:
    AFarmFirstStage();
    virtual void Tick(float Seconds) override;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Encounter") FVector2D OriginalFarmHalfSize=FVector2D(1300,1300);
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Encounter") float SpawnDistance=650;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Encounter") TSubclassOf<AFarmStageMonster> MonsterClass;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Encounter") bool Spawned=false;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Encounter") bool Cleared=false;
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Encounter") TObjectPtr<AFarmStageMonster> Monster;
    UFUNCTION(BlueprintCallable, Category="Encounter") bool CheckBoundary(AFarmCharacter* Player);
    void MonsterDefeated(AFarmStageMonster* Enemy);
};

UCLASS(Blueprintable)
class WHATTHEFARM_API AFarmStageExit : public AActor
{
    GENERATED_BODY()
public:
    AFarmStageExit();
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly) TObjectPtr<UStaticMeshComponent> Door;
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Travel") FName NextLevel="StageTwo";
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Travel") bool RequireFirstStageClear=true;
    UPROPERTY(EditInstanceOnly, BlueprintReadWrite, Category="Travel") TObjectPtr<AFarmFirstStage> FirstStage;
    UFUNCTION(BlueprintPure, Category="Travel") bool CanTravel() const;
    UFUNCTION(BlueprintCallable, Category="Travel") bool Travel(AFarmCharacter* Player);
};
