#include "FarmStage.h"
#include "FarmGameplay.h"
#include "FarmTravel.h"
#include "AIController.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Kismet/GameplayStatics.h"
#include "EngineUtils.h"
#include "UObject/ConstructorHelpers.h"

AFarmStageMonster::AFarmStageMonster()
{
    PrimaryActorTick.bCanEverTick=true;
    GetCapsuleComponent()->InitCapsuleSize(55,90);
    GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_Visibility,ECR_Block);
    AIControllerClass=AAIController::StaticClass();
    AutoPossessAI=EAutoPossessAI::PlacedInWorldOrSpawned;
    Body=CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Monster Body"));
    Body->SetupAttachment(GetCapsuleComponent());
    static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
    Body->SetStaticMesh(Sphere.Object); Body->SetRelativeScale3D(FVector(1.1,1.1,1.8));
    Body->SetCollisionEnabled(ECollisionEnabled::NoCollision);
    GetCharacterMovement()->MaxWalkSpeed=MoveSpeed;
    GetCharacterMovement()->bOrientRotationToMovement=true;
    bUseControllerRotationYaw=false;
}
void AFarmStageMonster::BeginPlay() { Super::BeginPlay(); Health=MaxHealth; }
void AFarmStageMonster::Tick(float Seconds)
{
    Super::Tick(Seconds); if(Defeated) return;
    auto* Player=Cast<AFarmCharacter>(UGameplayStatics::GetPlayerPawn(this,0)); if(!Player) return;
    FVector Direction=Player->GetActorLocation()-GetActorLocation(); Direction.Z=0;
    GetCharacterMovement()->MaxWalkSpeed=MoveSpeed;
    if(Direction.Size()>AttackRange*.75) AddMovementInput(Direction.GetSafeNormal());
    Attack(Player);
}
bool AFarmStageMonster::Attack(AFarmCharacter* Player)
{
    if(Defeated || !Player || FVector::Dist(GetActorLocation(),Player->GetActorLocation())>AttackRange ||
        GetWorld()->GetTimeSeconds()<NextAttack) return false;
    FCollisionQueryParams Params; Params.AddIgnoredActor(this); Params.AddIgnoredActor(Player);
    FHitResult Obstacle;
    if(GetWorld()->LineTraceSingleByChannel(Obstacle,GetActorLocation(),Player->GetActorLocation(),ECC_Visibility,Params)) return false;
    NextAttack=GetWorld()->GetTimeSeconds()+FMath::Max(.1f,AttackInterval);
    Player->ReceiveMonsterDamage(AttackDamage); return true;
}
float AFarmStageMonster::TakeDamage(float Damage,const FDamageEvent&,AController*,AActor*) { Hit(Damage); return FMath::Max(0.f,Damage); }
void AFarmStageMonster::Hit(float Damage)
{
    if(Defeated || Damage<=0) return;
    Health=FMath::Max(0.f,Health-Damage);
    if(Health>0) return;
    Defeated=true;
    if(IsValid(Stage)) Stage->MonsterDefeated(this);
    Destroy();
}

AFarmFirstStage::AFarmFirstStage()
{
    PrimaryActorTick.bCanEverTick=true;
    SetRootComponent(CreateDefaultSubobject<USceneComponent>(TEXT("Original Farm Centre")));
    MonsterClass=AFarmStageMonster::StaticClass();
}
void AFarmFirstStage::Tick(float Seconds)
{
    Super::Tick(Seconds);
    CheckBoundary(Cast<AFarmCharacter>(UGameplayStatics::GetPlayerPawn(this,0)));
}
bool AFarmFirstStage::CheckBoundary(AFarmCharacter* Player)
{
    if(!Player || Spawned || Cleared || !MonsterClass) return false;
    FVector Offset=Player->GetActorLocation()-GetActorLocation();
    if(FMath::Abs(Offset.X)<=OriginalFarmHalfSize.X && FMath::Abs(Offset.Y)<=OriginalFarmHalfSize.Y) return false;
    Offset.Z=0;
    FVector Position=Player->GetActorLocation()+Offset.GetSafeNormal()*SpawnDistance;
    for(TActorIterator<AFarmSoil> It(GetWorld()); It; ++It)
    {
        const FBox Bounds=It->Ground->Bounds.GetBox();
        Position.X=FMath::Clamp(Position.X,Bounds.Min.X+100,Bounds.Max.X-100);
        Position.Y=FMath::Clamp(Position.Y,Bounds.Min.Y+100,Bounds.Max.Y-100); break;
    }
    FHitResult Floor; FCollisionQueryParams Query; Query.AddIgnoredActor(Player);
    if(GetWorld()->LineTraceSingleByChannel(Floor,Position+FVector(0,0,1000),Position-FVector(0,0,10000),ECC_Visibility,Query))
        Position.Z=Floor.ImpactPoint.Z+90;
    FActorSpawnParameters Params; Params.SpawnCollisionHandlingOverride=ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
    Monster=GetWorld()->SpawnActor<AFarmStageMonster>(MonsterClass,Position,FRotator::ZeroRotator,Params);
    if(!Monster) return false;
    Monster->Stage=this; Spawned=true;
    Player->Notify(TEXT("Farm Guardian appeared! Defeat it to unlock the next stage.")); return true;
}
void AFarmFirstStage::MonsterDefeated(AFarmStageMonster* Enemy)
{
    if(Cleared || Enemy!=Monster) return;
    Cleared=true; Monster=nullptr;
    if(auto* Player=Cast<AFarmCharacter>(UGameplayStatics::GetPlayerPawn(this,0)))
        Player->Notify(TEXT("Guardian defeated! Go to the purple exit and press E."));
}

AFarmStageExit::AFarmStageExit()
{
    Door=CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Stage Exit")); SetRootComponent(Door);
    static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(TEXT("/Engine/BasicShapes/Cube.Cube"));
    Door->SetStaticMesh(Cube.Object); Door->SetCollisionProfileName(TEXT("BlockAll"));
}
bool AFarmStageExit::CanTravel() const { return !NextLevel.IsNone() && (!RequireFirstStageClear || (IsValid(FirstStage) && FirstStage->Cleared)); }
bool AFarmStageExit::Travel(AFarmCharacter* Player)
{
    if(!Player) return false;
    if(!CanTravel()) { Player->Notify(TEXT("Defeat the guardian before entering the next stage.")); return false; }
    if(auto* Travel=Cast<UFarmGameInstance>(Player->GetGameInstance())) Travel->Capture(Player);
    UGameplayStatics::OpenLevel(this,NextLevel); return true;
}
