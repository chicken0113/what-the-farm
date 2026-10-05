#include "FarmGameplay.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/SphereComponent.h"
#include "Engine/Canvas.h"
#include "Engine/DirectionalLight.h"
#include "Engine/StaticMesh.h"
#include "EngineUtils.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Kismet/GameplayStatics.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "ProceduralMeshComponent.h"
#include "TimerManager.h"
#include "UObject/ConstructorHelpers.h"

namespace Farm
{
    void Tint(UMeshComponent* Mesh, FLinearColor Color)
    {
        auto* Base = LoadObject<UMaterialInterface>(nullptr, TEXT("/Game/Farm/Materials/M_Color.M_Color"));
        if (!Base) Base = LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial"));
        auto* Material = UMaterialInstanceDynamic::Create(Base, Mesh);
        Material->SetVectorParameterValue(TEXT("Tint"), Color);
        Mesh->SetMaterial(0, Material);
    }
    bool InRange(float Value, FVector2D Range)
    {
        return Value >= FMath::Min(Range.X, Range.Y) && Value <= FMath::Max(Range.X, Range.Y);
    }
    double Distance2D(FVector A, FVector B) { return FVector2D::Distance(FVector2D(A.X,A.Y), FVector2D(B.X,B.Y)); }
}

float UFarmGrowthProfile::Evaluate(float Light, float Water, FName Soil) const
{
    return FMath::Max(0.f, BasePercent) + (UseLight && Farm::InRange(Light, LightRange) ? FMath::Max(0.f, LightBonus) : 0)
        + (UseWater && Farm::InRange(Water, WaterRange) ? FMath::Max(0.f, WaterBonus) : 0)
        + (UseSoil && PreferredSoils.Contains(Soil) ? FMath::Max(0.f, SoilBonus) : 0);
}

AFarmSoil::AFarmSoil()
{
    Ground = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Ground"));
    SetRootComponent(Ground);
    static ConstructorHelpers::FObjectFinder<UStaticMesh> Shape(TEXT("/Engine/BasicShapes/Cube.Cube"));
    Ground->SetStaticMesh(Shape.Object);
    Ground->SetCollisionProfileName(TEXT("BlockAll"));
}

int32 AFarmSoil::FindPlot(FVector Point) const
{
    int32 Result = INDEX_NONE;
    double Closest = TNumericLimits<double>::Max();
    for (int32 I=0; I<Plots.Num(); ++I)
    {
        double Distance = Farm::Distance2D(Point, Plots[I].Center);
        if (Distance <= Plots[I].Radius && Distance < Closest && FMath::Abs(Point.Z-Plots[I].Center.Z) < 6)
        { Result=I; Closest=Distance; }
    }
    return Result;
}

int32 AFarmSoil::Till(FVector Point, float Radius)
{
    if (FindPlot(Point) != INDEX_NONE || Radius <= 0) return INDEX_NONE;
    const FBox Box = Ground->Bounds.GetBox();
    if (Point.X<Box.Min.X || Point.X>Box.Max.X || Point.Y<Box.Min.Y || Point.Y>Box.Max.Y) return INDEX_NONE;
    FFarmPlot Plot; Plot.Center=Point; Plot.Radius=Radius; Plot.Water=InitialWater;
    auto* Patch = NewObject<UProceduralMeshComponent>(this);
    Patch->RegisterComponent();
    Patch->AttachToComponent(Ground, FAttachmentTransformRules::KeepWorldTransform);
    Patch->SetWorldLocation(Point+FVector(0,0,1.2f+Plots.Num()*.01f));
    Patch->SetWorldScale3D(FVector::OneVector);
    Patch->SetCollisionEnabled(ECollisionEnabled::NoCollision);
    TArray<FVector> Vertices; TArray<int32> Triangles; TArray<FVector> Normals;
    Vertices.Add(FVector::ZeroVector); Normals.Add(FVector::UpVector);
    const int32 Segments=64;
    for (int32 I=0; I<=Segments; ++I)
    {
        const double Angle=2*PI*I/Segments;
        FVector Edge=Point+FVector(FMath::Cos(Angle)*Radius, FMath::Sin(Angle)*Radius, 0);
        Edge.X=FMath::Clamp(Edge.X,Box.Min.X,Box.Max.X); Edge.Y=FMath::Clamp(Edge.Y,Box.Min.Y,Box.Max.Y);
        Vertices.Add(Edge-Point); Normals.Add(FVector::UpVector);
        // Unreal treats clockwise triangles as front-facing. The old winding
        // faced down, so a single-sided soil material disappeared from above.
        if(I<Segments) { Triangles.Add(0); Triangles.Add(I+2); Triangles.Add(I+1); }
    }
    Patch->CreateMeshSection(0,Vertices,Triangles,Normals,{}, {}, {},false);
    Plot.Visual=Patch; int32 Index=Plots.Add(Plot); RefreshPlot(Index); return Index;
}

void AFarmSoil::RefreshPlot(int32 Index)
{
    if (Plots.IsValidIndex(Index) && Plots[Index].Visual)
        Farm::Tint(Plots[Index].Visual, Plots[Index].Water>0 ? FLinearColor(.10,.17,.23) : FLinearColor(.14,.065,.025));
}

void AFarmSoil::ReleasePlant(AFarmItem* Item)
{
    for (int32 I=0; I<Plots.Num(); ++I) if (Plots[I].Plant==Item)
    { Plots[I].Plant=nullptr; Plots[I].Water=InitialWater; RefreshPlot(I); }
}

int32 AFarmSoil::WaterArea(FVector Point, float Radius, float Amount)
{
    int32 Count=0;
    for (int32 I=0; I<Plots.Num(); ++I)
    {
        auto& Plot=Plots[I];
        if (!IsValid(Plot.Plant) || Plot.Water>=100 || Farm::Distance2D(Point,Plot.Plant->GetActorLocation())>Radius) continue;
        Plot.Water=FMath::Clamp(Plot.Water+FMath::Max(0.f,Amount),0.f,100.f); RefreshPlot(I); ++Count;
    }
    return Count;
}

float AFarmSoil::LightAt(FVector Point, AFarmItem* Ignore) const
{
    if (!UseSunlight) return LightAmount;
    ADirectionalLight* Sun=nullptr;
    for(TActorIterator<ADirectionalLight> It(GetWorld()); It; ++It) { Sun=*It; break; }
    if (!Sun) return LightAmount;
    FCollisionQueryParams Params; Params.AddIgnoredActor(this); if(Ignore) Params.AddIgnoredActor(Ignore);
    for(TActorIterator<AFarmItem> It(GetWorld()); It; ++It) Params.AddIgnoredActor(*It);
    FHitResult Hit;
    const FVector Start=Point+FVector(0,0,5);
    return GetWorld()->LineTraceSingleByChannel(Hit,Start,Start-Sun->GetActorForwardVector()*10000,ECC_Visibility,Params)
        ? ShadeLight : LightAmount;
}

AFarmItem::AFarmItem()
{
    PrimaryActorTick.bCanEverTick=true;
    Mesh=CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh")); SetRootComponent(Mesh);
    static ConstructorHelpers::FObjectFinder<UStaticMesh> Shape(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
    Mesh->SetStaticMesh(Shape.Object); Mesh->SetCollisionProfileName(TEXT("PhysicsActor"));
    Mesh->SetMobility(EComponentMobility::Movable); Mesh->SetGenerateOverlapEvents(true);
    Mesh->SetUseCCD(true);
}

void AFarmItem::Configure(EFarmKind NewKind)
{
    Kind=NewKind;
    const TCHAR* Shape=Kind==EFarmKind::Seed ? TEXT("/Engine/BasicShapes/Sphere.Sphere") :
        Kind==EFarmKind::Hoe || Kind==EFarmKind::WateringCan ? TEXT("/Engine/BasicShapes/Cylinder.Cylinder") : TEXT("/Engine/BasicShapes/Cube.Cube");
    Mesh->SetStaticMesh(LoadObject<UStaticMesh>(nullptr,Shape));
    SetActorScale3D(Kind==EFarmKind::Hoe ? FVector(.17,.17,1) : Kind==EFarmKind::WateringCan ? FVector(.36,.36,.56) : FVector(.5));
    OriginalScale=GetActorScale3D();
    Farm::Tint(Mesh,Kind==EFarmKind::Seed ? FLinearColor(.9,.7,.1) : Kind==EFarmKind::Hoe ? FLinearColor(.4,.65,.8) :
        Kind==EFarmKind::WateringCan ? FLinearColor(.1,.6,.9) : FLinearColor(.45,.4,.5));
}

void AFarmItem::BeginPlay()
{
    Super::BeginPlay(); SupplyPosition=GetActorLocation(); OriginalScale=GetActorScale3D();
    Mesh->SetSimulatePhysics(!RestockOnPickup);
}

void AFarmItem::EndPlay(const EEndPlayReason::Type Reason)
{
    if(IsValid(HomeSoil)) HomeSoil->ReleasePlant(this);
    Super::EndPlay(Reason);
}

float AFarmItem::SizeMultiplier() const
{
    const FVector Scale=GetActorScale3D().GetAbs(); const FVector Base=OriginalScale.GetAbs();
    return FMath::Max3(Scale.X/FMath::Max(.0001,Base.X),Scale.Y/FMath::Max(.0001,Base.Y),Scale.Z/FMath::Max(.0001,Base.Z));
}
int32 AFarmItem::Value() const { return FMath::RoundToInt(BaseValue*FMath::Pow(1.8f,Generation)); }
FString AFarmItem::DisplayName() const
{
    static const TCHAR* Names[]={TEXT("Seed"),TEXT("Crop"),TEXT("Hoe"),TEXT("Watering Can"),TEXT("Stone")};
    return Generation>0 ? FString::Printf(TEXT("%s +%d"),Names[int32(Kind)],Generation) : FString(Names[int32(Kind)]);
}

void AFarmItem::AlignOnSoil()
{
    Mesh->UpdateBounds(); const float Bottom=Mesh->Bounds.Origin.Z-Mesh->Bounds.BoxExtent.Z;
    AddActorWorldOffset(FVector(0,0,SoilHeight-Bottom),false);
}

bool AFarmItem::PlantAt(AFarmSoil* Soil, FVector Point)
{
    if (!Soil || Planted || Mature) return false;
    int32 Index=Soil->FindPlot(Point);
    if(Index==INDEX_NONE || IsValid(Soil->Plots[Index].Plant)) return false;
    const FBox Bounds=Soil->Ground->Bounds.GetBox();
    if(Point.X<Bounds.Min.X || Point.X>Bounds.Max.X || Point.Y<Bounds.Min.Y || Point.Y>Bounds.Max.Y) return false;
    DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
    Mesh->SetSimulatePhysics(false); SetActorLocation(Point);
    Mesh->SetCollisionEnabled(ECollisionEnabled::QueryOnly); SetActorHiddenInGame(false);
    HomeSoil=Soil; PlotIndex=Index; SoilHeight=Point.Z; PlantScale=GetActorScale3D();
    Planted=true; Mature=false; Held=false; Thrown=false; RestockOnPickup=false; GrowthProgress=0;
    if(Kind!=EFarmKind::Seed) ++Generation;
    Health=3+Generation*2;
    Duration=GrowthProfile ? FMath::Max(.1f,GrowthProfile->GrowthSeconds+Generation*GrowthProfile->SecondsPerGeneration) : 4+Generation;
    Soil->Plots[Index].Plant=this; AlignOnSoil(); return true;
}

void AFarmItem::Grow(float Seconds)
{
    if(!Planted || Mature || Seconds<=0 || !IsValid(HomeSoil) || !HomeSoil->Plots.IsValidIndex(PlotIndex)) return;
    auto& Plot=HomeSoil->Plots[PlotIndex];
    GrowthPercent=GrowthProfile ? GrowthProfile->Evaluate(HomeSoil->LightAt(GetActorLocation(),this),Plot.Water,HomeSoil->SoilType) : 100;
    if((!GrowthProfile || GrowthProfile->RequireWater) && Plot.Water<=0) return;
    GrowthProgress=FMath::Min(Duration,GrowthProgress+Seconds);
    const float BaseSize=GrowthProfile ? GrowthProfile->MatureMultiplier+Generation*GrowthProfile->SizePerGeneration : 2+Generation*.32f;
    SetActorScale3D(PlantScale*FMath::Lerp(1.f,FMath::Max(1.f,BaseSize*GrowthPercent/100),GrowthProgress/Duration)); AlignOnSoil();
    if(GrowthProgress>=Duration)
    { Mature=true; Planted=false; HomeSoil->ReleasePlant(this); PlotIndex=INDEX_NONE; }
}

void AFarmItem::Hit(float Damage)
{
    if(!Mature || Damage<=0) return;
    Health-=Damage; if(Health>0) return;
    Mature=false; Planted=false; if(Kind==EFarmKind::Seed) Kind=EFarmKind::Produce;
    AddActorWorldOffset(FVector(0,0,50)); Mesh->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
    Mesh->SetSimulatePhysics(true); Mesh->SetEnableGravity(true);
    // The grown actor is the drop; mesh, dimensions and original tool reference stay intact.
}

void AFarmItem::Pickup(USceneComponent* Hand)
{
    if(Planted || Mature || Held || !Hand) return;
    Mesh->SetSimulatePhysics(false); Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
    if(RestockOnPickup)
    {
        FActorSpawnParameters Params; Params.Template=this; Params.SpawnCollisionHandlingOverride=ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
        auto* Replacement=GetWorld()->SpawnActor<AFarmItem>(GetClass(),SupplyPosition,GetActorRotation(),Params);
        if(Replacement)
        {
            Replacement->SetActorScale3D(OriginalScale); Replacement->OriginalScale=OriginalScale;
            Replacement->Mesh->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
            Replacement->Mesh->SetSimulatePhysics(false); Replacement->RestockOnPickup=true;
        }
        RestockOnPickup=false;
    }
    Thrown=false; Held=true;
    AttachToComponent(Hand,FAttachmentTransformRules::KeepWorldTransform);
    SetActorRelativeLocation(FVector::ZeroVector); SetActorRelativeRotation(FRotator(0,15,-15));
}

void AFarmItem::Throw(FVector Position, FVector Velocity)
{
    DetachFromActor(FDetachmentTransformRules::KeepWorldTransform); SetActorLocation(Position);
    SetActorHiddenInGame(false); Held=false; Thrown=true;
    Mesh->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics); Mesh->SetSimulatePhysics(true);
    Mesh->SetEnableGravity(true); Mesh->SetPhysicsLinearVelocity(Velocity);
}

void AFarmItem::Tick(float Seconds)
{
    Super::Tick(Seconds); if(Planted) Grow(Seconds);
    if(!Mature) return;
    auto* Player=UGameplayStatics::GetPlayerPawn(this,0); if(!Player) return;
    FVector Away=GetActorLocation()-Player->GetActorLocation(); Away.Z=0;
    if(Away.SizeSquared()>360000 || Away.IsNearlyZero()) return;
    FVector Next=GetActorLocation()+Away.GetSafeNormal()*(200+Generation*35)*Seconds;
    if(IsValid(HomeSoil))
    {
        const FBox Bounds=HomeSoil->Ground->Bounds.GetBox();
        Next.X=FMath::Clamp(Next.X,Bounds.Min.X+80,Bounds.Max.X-80); Next.Y=FMath::Clamp(Next.Y,Bounds.Min.Y+80,Bounds.Max.Y-80);
    }
    SetActorLocation(Next); AddActorWorldRotation(FRotator(0,95*Seconds,0)); AlignOnSoil();
}

AFarmMerchant::AFarmMerchant()
{
    Body=CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Body")); SetRootComponent(Body);
    static ConstructorHelpers::FObjectFinder<UStaticMesh> Shape(TEXT("/Engine/BasicShapes/Cube.Cube")); Body->SetStaticMesh(Shape.Object);
    Body->SetCollisionProfileName(TEXT("BlockAll"));
    Receiver=CreateDefaultSubobject<USphereComponent>(TEXT("Sale Receiver")); Receiver->SetupAttachment(Body);
    Receiver->SetSphereRadius(110); Receiver->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
    Receiver->OnComponentBeginOverlap.AddDynamic(this,&AFarmMerchant::Receive);
}

void AFarmMerchant::Talk(AFarmCharacter* Player)
{
    if(Player && Dialogue.Num()>0) Player->Notify(MerchantName+TEXT(": ")+Dialogue[DialogueIndex++ % Dialogue.Num()]);
}
bool AFarmMerchant::TrySell(AFarmItem* Item, AFarmCharacter* Player)
{
    if(!IsValid(Item) || !Player || !Item->Thrown || Item->Planted || Item->Mature || !Item->Mesh->IsSimulatingPhysics()) return false;
    int32 Price=FMath::RoundToInt(Item->Value()*PriceMultiplier); if(Price<=0) return false;
    Item->Thrown=false; Player->Gold+=Price;
    Player->Notify(FString::Printf(TEXT("%s: Sold %s for %d gold."),*MerchantName,*Item->DisplayName(),Price));
    Item->Destroy(); return true;
}
void AFarmMerchant::Receive(UPrimitiveComponent*, AActor* Other, UPrimitiveComponent*, int32, bool, const FHitResult&)
{
    // Enabling a thrown item's collision can report an overlap before physics is enabled.
    // Complete the throw first, then validate and sell on the next game tick.
    TWeakObjectPtr<AFarmMerchant> Merchant(this);
    TWeakObjectPtr<AFarmItem> Item(Cast<AFarmItem>(Other));
    if(!Item.IsValid()) return;
    GetWorld()->GetTimerManager().SetTimerForNextTick([Merchant,Item]()
    {
        if(Merchant.IsValid() && Item.IsValid())
            Merchant->TrySell(Item.Get(),Cast<AFarmCharacter>(UGameplayStatics::GetPlayerPawn(Merchant.Get(),0)));
    });
}

AFarmCharacter::AFarmCharacter()
{
    PrimaryActorTick.bCanEverTick=true; GetCapsuleComponent()->InitCapsuleSize(34,90);
    Camera=CreateDefaultSubobject<UCameraComponent>(TEXT("Farm Camera")); Camera->SetupAttachment(GetCapsuleComponent());
    Camera->SetRelativeLocation(FVector(0,0,65)); Camera->bUsePawnControlRotation=true; Camera->FieldOfView=72;
    Hand=CreateDefaultSubobject<USceneComponent>(TEXT("Held Item")); Hand->SetupAttachment(Camera);
    Hand->SetRelativeLocation(FVector(80,42,-36)); GetCharacterMovement()->MaxWalkSpeed=500;
}
void AFarmCharacter::BeginPlay()
{
    Super::BeginPlay(); Inventory.SetNum(FMath::Max(1,InventorySlots)); Notify(TEXT("Pick up the hoe, till the ground, plant and water."));
    if(auto* PC=Cast<APlayerController>(Controller)) PC->SetInputMode(FInputModeGameOnly());
}
void AFarmCharacter::SetupPlayerInputComponent(UInputComponent* Input)
{
    Super::SetupPlayerInputComponent(Input);
    Input->BindKey(EKeys::E,IE_Pressed,this,&AFarmCharacter::Interact);
    Input->BindKey(EKeys::Q,IE_Pressed,this,&AFarmCharacter::Drop);
    Input->BindKey(EKeys::LeftMouseButton,IE_Pressed,this,&AFarmCharacter::Use);
    Input->BindKey(EKeys::Tab,IE_Pressed,this,&AFarmCharacter::ToggleInventory);
    Input->BindKey(EKeys::SpaceBar,IE_Pressed,this,&AFarmCharacter::Jump);
}
void AFarmCharacter::Notify(const FString& Text) { Message=Text; MessageUntil=GetWorld()->GetTimeSeconds()+5; }
AFarmItem* AFarmCharacter::HeldItem() const { return Inventory.IsValidIndex(SelectedSlot) && IsValid(Inventory[SelectedSlot]) ? Inventory[SelectedSlot].Get() : nullptr; }
void AFarmCharacter::RefreshInventory()
{
    for(int32 I=0; I<Inventory.Num(); ++I) if(IsValid(Inventory[I])) Inventory[I]->SetActorHiddenInGame(I!=SelectedSlot);
}
void AFarmCharacter::Select(int32 Index)
{
    if(Inventory.IsValidIndex(Index)) { SelectedSlot=Index; RefreshInventory(); }
}
bool AFarmCharacter::Aim(FHitResult& Hit, bool ThroughPlants) const
{
    FCollisionQueryParams Params; Params.AddIgnoredActor(this);
    for(const auto& Item : Inventory) if(IsValid(Item)) Params.AddIgnoredActor(Item.Get());
    if(ThroughPlants) for(TActorIterator<AFarmItem> It(GetWorld()); It; ++It) if(It->Planted || It->Mature) Params.AddIgnoredActor(*It);
    const FVector Start=Camera->GetComponentLocation();
    return GetWorld()->LineTraceSingleByChannel(Hit,Start,Start+Camera->GetForwardVector()*400,ECC_Visibility,Params);
}
bool AFarmCharacter::PickupItem(AFarmItem* Item)
{
    if(!IsValid(Item) || Item->Planted || Item->Mature || Inventory.Contains(Item)) return false;
    int32 Slot=Inventory.IsValidIndex(SelectedSlot) && !IsValid(Inventory[SelectedSlot]) ? SelectedSlot : INDEX_NONE;
    if(Slot==INDEX_NONE) for(int32 I=0; I<Inventory.Num(); ++I) if(!IsValid(Inventory[I])) { Slot=I; break; }
    if(Slot==INDEX_NONE) { Notify(TEXT("Inventory full. Throw an item with Q.")); return false; }
    Item->Pickup(Hand); Inventory[Slot]=Item; RefreshInventory(); Notify(TEXT("Picked up ")+Item->DisplayName()); return true;
}
void AFarmCharacter::Interact()
{
    if(InventoryOpen) return;
    FHitResult Hit; if(!Aim(Hit)) return;
    if(auto* Merchant=Cast<AFarmMerchant>(Hit.GetActor())) { Merchant->Talk(this); return; }
    if(auto* Item=Cast<AFarmItem>(Hit.GetActor())) if(!Item->Planted && !Item->Mature) { PickupItem(Item); return; }
    if(!HeldItem()) { Notify(TEXT("Select an item to plant.")); return; }
    FHitResult SoilHit;
    if(Aim(SoilHit,true) && SoilHit.ImpactNormal.Z>=.9)
    {
        if(HeldItem()->PlantAt(Cast<AFarmSoil>(SoilHit.GetActor()),SoilHit.ImpactPoint))
        { Inventory[SelectedSlot]=nullptr; RefreshInventory(); Notify(TEXT("Planted. Water to start growth.")); }
        else Notify(TEXT("Use an empty tilled area. One plant per area."));
    }
}
void AFarmCharacter::Drop()
{
    if(InventoryOpen || !HeldItem()) return;
    AFarmItem* Item=HeldItem(); Inventory[SelectedSlot]=nullptr;
    Item->Throw(Camera->GetComponentLocation()+Camera->GetForwardVector()*100,Camera->GetForwardVector()*ThrowSpeed); RefreshInventory();
}
void AFarmCharacter::Use()
{
    if(InventoryOpen)
    {
        auto* PC=Cast<APlayerController>(Controller); auto* HUD=PC ? Cast<AFarmHUD>(PC->GetHUD()) : nullptr;
        float X=0,Y=0; if(!HUD || !PC->GetMousePosition(X,Y)) return;
        for(int32 I=0; I<HUD->InventoryRects.Num(); ++I) if(HUD->InventoryRects[I].IsInside(FVector2D(X,Y)))
        {
            if(SwapSlot==INDEX_NONE) { SwapSlot=I; SelectedSlot=I; }
            else { Inventory.Swap(SwapSlot,I); SwapSlot=INDEX_NONE; }
            RefreshInventory(); break;
        }
        return;
    }
    if(GetWorld()->GetTimeSeconds()<NextUse) return; NextUse=GetWorld()->GetTimeSeconds()+.42;
    FHitResult Hit; if(!Aim(Hit)) return; AFarmItem* Tool=HeldItem(); auto* Plant=Cast<AFarmItem>(Hit.GetActor());
    if(Plant && Tool && Tool->Kind==EFarmKind::WateringCan && Plant->Planted)
    {
        int32 Count=Plant->HomeSoil->WaterArea(Plant->GetActorLocation(),WaterRadius*Tool->SizeMultiplier(),WaterPerUse);
        Notify(FString::Printf(TEXT("Watered %d plant(s)."),Count)); return;
    }
    if(Plant && Plant->Mature) { Plant->Hit(Tool && Tool->Kind==EFarmKind::Hoe ? 2*Tool->SizeMultiplier() : 1); return; }
    auto* Soil=Cast<AFarmSoil>(Hit.GetActor()); if(!Soil || !Tool || Hit.ImpactNormal.Z<.9) return;
    if(Tool->Kind==EFarmKind::Hoe)
    {
        Notify(Soil->Till(Hit.ImpactPoint,HoeRadius*Tool->SizeMultiplier())!=INDEX_NONE ? TEXT("Tilled. Press E to plant.") : TEXT("This ground is already tilled."));
    }
    else if(Tool->Kind==EFarmKind::WateringCan)
    {
        int32 Count=Soil->WaterArea(Hit.ImpactPoint,WaterRadius*Tool->SizeMultiplier(),WaterPerUse);
        Notify(FString::Printf(TEXT("Watered %d plant(s)."),Count));
    }
}
void AFarmCharacter::ToggleInventory()
{
    InventoryOpen=!InventoryOpen; SwapSlot=INDEX_NONE;
    if(auto* PC=Cast<APlayerController>(Controller))
    {
        PC->bShowMouseCursor=InventoryOpen;
        if(InventoryOpen) { FInputModeGameAndUI Mode; Mode.SetHideCursorDuringCapture(false); PC->SetInputMode(Mode); }
        else PC->SetInputMode(FInputModeGameOnly());
    }
}
void AFarmCharacter::Tick(float Seconds)
{
    Super::Tick(Seconds); auto* PC=Cast<APlayerController>(Controller); if(!PC) return;
    const FKey Keys[]={EKeys::One,EKeys::Two,EKeys::Three,EKeys::Four,EKeys::Five,EKeys::Six,EKeys::Seven,EKeys::Eight,EKeys::Nine};
    for(int32 I=0; I<9; ++I) if(PC->WasInputKeyJustPressed(Keys[I])) Select(I);
    if(PC->WasInputKeyJustPressed(EKeys::MouseScrollUp)) Select((SelectedSlot+Inventory.Num()-1)%Inventory.Num());
    if(PC->WasInputKeyJustPressed(EKeys::MouseScrollDown)) Select((SelectedSlot+1)%Inventory.Num());
    if(InventoryOpen) return;
    AddMovementInput(GetActorForwardVector(),(PC->IsInputKeyDown(EKeys::W)?1:0)-(PC->IsInputKeyDown(EKeys::S)?1:0));
    AddMovementInput(GetActorRightVector(),(PC->IsInputKeyDown(EKeys::D)?1:0)-(PC->IsInputKeyDown(EKeys::A)?1:0));
    float X,Y; PC->GetInputMouseDelta(X,Y); AddControllerYawInput(X); AddControllerPitchInput(-Y);
    GetCharacterMovement()->MaxWalkSpeed=PC->IsInputKeyDown(EKeys::LeftShift)?800:500;
}

void AFarmHUD::DrawHUD()
{
    Super::DrawHUD(); auto* Player=Cast<AFarmCharacter>(GetOwningPawn()); if(!Player || !Canvas) return;
    const float W=Canvas->SizeX,H=Canvas->SizeY;
    DrawText(TEXT("WHAT THE FARM - Unreal prototype"),FLinearColor::White,20,20,nullptr,1.2f);
    DrawText(TEXT("WASD move | Shift sprint | E pickup / plant / talk | Q throw"),FLinearColor::White,20,48);
    DrawText(TEXT("Left click: till / water / harvest | 1-9 / wheel select | Tab inventory"),FLinearColor::White,20,68);
    DrawText(FString::Printf(TEXT("Gold: %lld"),Player->Gold),FLinearColor::Yellow,W-160,20);
    if(GetWorld()->GetTimeSeconds()<Player->MessageUntil) DrawText(Player->Message,FLinearColor::Yellow,20,100);
    if(auto* Item=Player->HeldItem())
        DrawText(FString::Printf(TEXT("%s | size x%.2f | value %d"),*Item->DisplayName(),Item->SizeMultiplier(),Item->Value()),FLinearColor::White,20,125);
    DrawText(TEXT("+"),FLinearColor::White,W/2-5,H/2-8);
    FHitResult Hit;
    if(!Player->InventoryOpen && Player->Aim(Hit)) if(auto* Item=Cast<AFarmItem>(Hit.GetActor()))
    {
        FString Info=Item->Mature ? FString::Printf(TEXT("%s | HP %.0f | value %d"),*Item->DisplayName(),Item->Health,Item->Value()) :
            Item->Planted ? FString::Printf(TEXT("Growing | size rate %.0f%% | water %.0f"),Item->GrowthPercent,Item->HomeSoil->Plots[Item->PlotIndex].Water) : Item->DisplayName();
        DrawText(Info,FLinearColor::White,W/2-140,H/2+24);
    }
    InventoryRects.Reset();
    int32 Count=Player->InventoryOpen ? Player->Inventory.Num() : FMath::Min(9,Player->Inventory.Num());
    const int32 Columns=Player->InventoryOpen ? FMath::Max(1,FMath::Min(6,int32((W-40)/100))) : Count;
    const int32 Rows=FMath::DivideAndRoundUp(Count,Columns); const float StartX=(W-Columns*100)/2;
    const float StartY=Player->InventoryOpen ? FMath::Max(150.f,(H-Rows*80)/2) : H-90;
    for(int32 I=0; I<Count; ++I)
    {
        const float X=StartX+(I%Columns)*100,Y=StartY+(I/Columns)*80;
        DrawRect(I==Player->SelectedSlot ? FLinearColor(.35,.3,.05,.95) : FLinearColor(.06,.06,.07,.9),X,Y,94,72);
        DrawText(FString::FromInt(I+1),FLinearColor::White,X+6,Y+4);
        if(IsValid(Player->Inventory[I])) DrawText(Player->Inventory[I]->DisplayName(),FLinearColor::White,X+6,Y+28,nullptr,.8f);
        if(Player->InventoryOpen) InventoryRects.Add(FBox2D(FVector2D(X,Y),FVector2D(X+94,Y+72)));
    }
    if(Player->InventoryOpen) DrawText(TEXT("Click two slots to move / swap. Tab to close."),FLinearColor::White,StartX,StartY-25);
}

AFarmGameMode::AFarmGameMode()
{
    DefaultPawnClass=AFarmCharacter::StaticClass(); HUDClass=AFarmHUD::StaticClass(); PlayerControllerClass=APlayerController::StaticClass();
}
