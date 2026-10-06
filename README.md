# What The Farm

현재 개발 대상은 **Unreal Engine 5.7** 프로젝트 `whatthefarm.uproject`입니다. 기존 Unity 시제품은 같은 저장소의 `Assets`, `Packages`, `ProjectSettings`에 보관합니다.

## 언리얼 프로젝트 열기

1. `whatthefarm.uproject`를 Unreal Engine **5.7.4**로 엽니다.
2. C++ 모듈을 다시 빌드할지 묻는 창이 나오면 빌드합니다. Visual Studio의 C++ 게임 개발 도구와 Windows SDK가 필요합니다.
3. 기본 맵 `Content/Farm/Maps/FirstFarm`에서 Play를 누릅니다. 기존 First Person 템플릿도 그대로 보관합니다.

Unity 시제품의 농사, 인벤토리, 성장, 수확, 재고 보충, NPC 판매를 C++와 편집 가능한 Blueprint/Data Asset으로 이식했습니다. 현재는 **로컬 1인 시제품**입니다. 멀티플레이, 유료 상점, 디스크 저장/불러오기는 아직 구현하지 않았습니다. 첫 스테이지 클리어 후 임시 다음 맵으로 이동할 수 있습니다.

## 언리얼 시제품 조작과 설정

| 키 | 동작 |
| --- | --- |
| WASD / 마우스 / Shift / Space | 이동 / 시점 / 달리기 / 점프 |
| E | 아이템 줍기 / 선택 아이템 심기 / NPC 대화 |
| 마우스 왼쪽 | 괭이로 갈기 / 물 주기 / 다 자란 대상 공격 |
| Q | 선택 아이템 던지기. 판매 NPC에게 던지면 골드 지급 |
| 1~9 / 휠 | 핫바 선택 |
| Tab | 인벤토리 열기/닫기. 두 칸을 차례로 클릭해 이동/교환 |

시작 위치 앞에 괭이, 물뿌리개, 씨앗, 돌이 있습니다. 가져가면 공급품이 즉시 보충됩니다. 괭이로 **길 옆 지면**을 갈고 씨앗을 선택한 뒤 갈린 부분을 보고 E로 심으세요. 갈린 영역 하나에 하나만 심으며 조준한 위치에 놓입니다. 물을 주면 성장하고 다 자란 대상은 도망갑니다. 때려 수확한 뒤에도 모델과 커진 크기는 유지됩니다. 각 아이템은 한 번만 심을 수 있습니다. 수확한 대상은 재식재할 수 없고 사용하거나 판매합니다. 빈 밭에는 새로운 아이템을 심을 수 있습니다. 성장으로 겹친 식물은 삭제하지 않습니다.

- **칸 수/도구 범위:** `Content/Farm/Blueprints/BP_Farmer`의 Class Defaults에서 `Inventory Slots`, `Hoe Radius`, `Water Radius`, `Water Per Use`를 설정합니다. 거리 단위는 cm입니다. 처음 9칸까지 핫바로 사용합니다. 커진 도구의 범위는 원래 크기 대비 배율에 정비례합니다.
- **개별 성장 조건:** `Content/Farm/Growth`의 Data Asset에서 기본 성장률, 빛/물 범위, 선호 땅, 각 보너스, 성장 시간과 크기 배율을 설정합니다. 환경 보너스는 **최종 크기**에 적용됩니다. `100 + 빛 보너스 + 물 보너스 + 땅 보너스`가 150이면 기준 완성 크기의 1.5배입니다.
- **새 아이템:** `FarmItem` 기반 Blueprint를 만들고 Mesh의 Static Mesh, Kind, Base Value, Growth Profile을 지정합니다. 모델과 크기를 유지한 채 심기·성장·수확·판매 시스템을 사용합니다. `Restock On Pickup`을 켜면 공급품으로 사용합니다.
- **환경:** 맵의 `FarmSoil`에서 `Soil Type`, `Light Amount`, `Initial Water`, `Use Sunlight`, `Shade Light`를 설정합니다. Soil Type은 임의 이름이며 성장 설정의 Preferred Soils와 일치시키면 됩니다. Sunlight를 사용하면 Directional Light 방향으로 장애물 그늘을 확인합니다. 수동 수치는 Sunlight를 끄고 사용합니다.
- **NPC:** 맵의 Buyer NPC에서 `Dialogue`, `Merchant Name`, `Price Multiplier`와 Receiver 반경을 변경합니다. 던진 느슨한 아이템만 판매합니다. 캐릭터 모델은 임시 정적 모델이며 애니메이션은 아직 연결하지 않았습니다.
- 첫 맵의 나무·바위·괭이·NPC에 기존 에셋 일부를 가져왔습니다. 물가·상점 건물·출구는 배치 표시입니다. Play를 끈 상태에서 맵 배치를 편집해 저장하세요.

아이템 기본 길이는 초기 시제품의 30%로 줄였습니다. 바닥의 아이템은 플레이어와 다른 아이템을 밀지 않으며, 바닥 물리와 E로 줍는 조준 판정은 유지합니다. 공급품은 처음 배치한 위치·회전·크기로 재입고됩니다.

## 첫 스테이지 진행

- 첫 맵은 **100m × 100m**입니다. 기존 펜스는 제거했습니다. 밝은 흙으로 표시한 원래 농장 범위는 **26m × 26m**입니다.
- 원래 농장 경계(X/Y ±13m)를 처음 넘어가면 붉은 임시 모델의 **Farm Guardian 한 마리**가 등장합니다. 플레이어를 추적하며 가까이에서 공격합니다. 다시 경계를 넘어도 추가로 나오지 않습니다.
- 왼쪽 클릭으로 공격합니다. 괭이는 기본 피해 2, 맨손/다른 아이템은 1이며, 커진 괭이는 크기 배율만큼 피해도 증가합니다. 기본 몬스터 체력은 12입니다.
- 플레이어 체력은 100입니다. 체력이 소진되면 아이템을 유지한 채 농장 시작 위치로 돌아옵니다.
- 몬스터를 잡으면 북쪽 보라색 출구가 열립니다. 가까이서 **E**를 누르면 `Content/Farm/Maps/StageTwo`로 이동합니다. 인벤토리, 골드, 아이템 크기와 재식재 제한은 이어집니다.
- **StageTwo는 이동 확인용 빈 맵**입니다. 다음 스테이지의 콘텐츠와 진행 규칙은 아직 만들지 않았습니다. 이 맵에는 첫 스테이지 경계/몬스터 규칙이 없습니다.
- 맵의 **First Stage - Old Fence Boundary**에서 경계 크기와 몬스터 종류/등장 거리를 조절합니다. `BP_Guardian` Class Defaults에서 체력, 이동 속도, 공격 거리·피해·간격을 조절합니다. 거리 단위는 cm입니다.
- **First Stage Exit - Defeat Guardian**의 Next Level은 이동할 맵 이름입니다. 이후 스테이지에는 FarmFirstStage를 배치하지 않고, 필요하면 출구의 Require First Stage Clear를 끄면 됩니다.
- `Scripts/verify_first_stage.py`는 실제 PIE에서 추적/공격/처치/이동과 다음 맵에서 몬스터 미등장을 확인합니다. `WhatTheFarm.Stage` 자동 검사는 경계 조건, 전투, 이동 시 아이템 보존을 확인합니다.

## 언리얼 빌드와 검증

Editor 타깃: `whatthefarmEditor Win64 Development`. 에디터를 닫고 `Build.bat`에 프로젝트 절대 경로와 `-WaitMutex -NoHotReloadFromIDE -NoUBA -MaxParallelActions=1`을 전달하면 메모리 사용을 제한해 빌드할 수 있습니다.

Unreal Automation의 `WhatTheFarm.Farming`에는 성장/수확/재식재 거부/인벤토리/재고/판매와 크기별 도구 범위 검사가 있습니다. `Scripts/verify_farm.py`는 실제 첫 맵에서 PIE 조작과 물리 던지기 판매를 확인합니다. `UnrealEditor.exe <프로젝트> -ExecutePythonScript=<스크립트 절대 경로> -unattended -d3d11 -nosound`로 실행합니다.

생성된 맵과 에셋은 Git에 포함되어 있어 생성 스크립트 실행이 필요하지 않습니다. 초기 생성이 필요할 때만 `Scripts/setup_farm.py`를 같은 방식으로 실행하세요. 선택 사항인 `WTF_LEGACY_ASSETS` 환경변수에 기존 Unity Assets 경로를 지정하면 모델을 가져옵니다. 기존 맵과 설정은 재생성으로 덮어쓰지 않습니다.

- 작업 폴더: `C:\Users\MSI\Documents\Unreal Projects\whatthefarm`
- 원격 저장소: https://github.com/chicken0113/what-the-farm
- `Config`, `Content`, `Source`, `.uproject`는 Git에 저장합니다. `Binaries`, `Intermediate`, `Saved`, 캐시와 Visual Studio 생성 파일은 저장하지 않습니다.
- 이 작업 폴더는 Git의 선택적 체크아웃으로 Unity 폴더를 생략합니다. 원격 저장소와 Git 이력에는 Unity 파일이 그대로 남습니다. 일반적으로 저장소를 새로 복제하면 양쪽 프로젝트가 모두 포함됩니다.

## 기존 Unity 시제품

아래 내용은 이전 Unity 프로토타입의 사용 안내입니다.

## 시작하기

1. Unity Hub에서 이 폴더를 **Add project**로 등록합니다.
2. Unity **6000.3.25f1**로 엽니다. 첫 실행 시 패키지 가져오기에 시간이 걸릴 수 있습니다.
3. `Assets/Scenes/FirstFarm.unity`를 열고 **Play**를 누르면 편집 가능한 첫 농장 맵을 플레이할 수 있습니다. 기존 `FarmPrototype.unity`는 기능 확인용으로 남아 있습니다.

## 첫 맵 배치하기

`Assets/Prefabs/Blockout`의 프리팹을 Project 창에서 Scene 창으로 드래그해 배치합니다. 첫 맵에는 기본 지면, 넓은 경작 가능한 지면, 물가, 다리, 상점과 판매소, 지역 출구를 배치했습니다. 도형의 색과 모양은 배치 용도를 구분하기 위한 임시 표현입니다.

| 프리팹 | 구분 / 용도 |
| --- | --- |
| Ground_Block | 회녹색 지면. 기본 크기 4m × 4m |
| Path_Block | 베이지색 길. 기본 크기 2m × 2m |
| Water_Block | 파란색 물가 표시. 이동을 막지 않음 |
| Bridge_Block | 갈색 다리 바닥 |
| Fence_Block | 갈색 울타리. 기본 길이 2m |
| Rock_Block / Tree_Block | 회색 바위 / 짙은 초록색 나무 장애물 |
| Shop_Block / Sell_Block | 노란 상점 / 주황 판매소 위치 표시 |
| RegionExit_Block | 보라색 다음 지역 출구 위치 표시. 현재 닫혀 있음 |
| SpawnMarker_Block | 청록색 시작 위치 표시 |
| BuyerNPC | 판매 NPC. E 대화, Q로 던진 아이템을 골드로 환전 |

- Play를 끈 상태에서 이동·회전·크기를 조절하고 씬을 저장합니다. 블록의 원점은 바닥 기준입니다. Ground_Block의 X/Z 크기를 늘려 넓은 바닥으로 사용하세요. 별도 밭 블록은 필요하지 않습니다.
- `FirstFarm`에 배치된 시작 위치 표시를 옮기면 플레이어 시작 위치도 바뀝니다. 다른 표시를 사용하려면 **Farm Prototype > Player Spawn Point**에 해당 Transform을 연결합니다.
- **Farm Prototype > Tilling Radius**에서 괭이 한 번에 갈리는 반경(m)을 조절합니다. 기본값은 0.8m입니다. 괭이로 갈아 만든 영역 하나당 대상 하나를 심습니다. 영역 안에서 조준한 위치에 심으며 중앙으로 고정하지 않습니다.
- **Farm Prototype > Inventory Slot Count**에서 인벤토리 칸 수를 조절합니다.
- 상점·판매소 건물·물가·지역 출구는 배치용 표시입니다. 판매소 앞 BuyerNPC는 판매가 가능합니다. 상점 구매, 물 보충, 지역 이동 기능은 아직 연결되지 않았습니다.
- 현재 작물 이동 범위는 중심 기준 X/Z ±12.2m입니다. 첫 맵 크기는 이 범위에 맞춰 26m × 26m로 잡았습니다.
- `What The Farm > Create First Farm Blockout` 메뉴로 초기 배치를 다시 생성할 수 있습니다. 재생성은 첫 맵과 프리팹을 덮어쓰므로 편집한 맵은 다른 이름으로 저장해 보관하세요.

## 시제품 조작

| 키 | 동작 |
| --- | --- |
| WASD / 마우스 | 이동 / 시점 변경 |
| Shift | 달리기 |
| E | 물건을 인벤토리에 집기 / 선택한 아이템 심기 / NPC 대화 |
| Q | 조준 방향으로 선택한 아이템 던지기 / NPC에게 던져 판매 |
| 1~9 / 마우스 휠 | 핫바 칸 선택 |
| Tab | 인벤토리 열기 / 닫기 |
| 마우스 왼쪽 | 괭이로 밭 갈기 / 물뿌리개로 물 주기 / 작물 때리기 |
| Esc | 인벤토리 닫기 / 마우스 잠금 해제 |

바닥은 처음에 전부 갈리지 않은 상태입니다. 괭이를 들고 지면을 보고 왼쪽 클릭하면 가리킨 지점 주변만 원형의 진한 갈색 흙으로 바뀝니다. 바닥 블록 전체는 바뀌지 않습니다. 갈린 범위 밖에는 심을 수 없으며, 갈린 영역 하나에는 대상 하나만 심을 수 있고, 심을 때 가리킨 지점에 작물이 놓입니다. 이미 심어진 영역에 다시 심으려 하면 아이템은 소비되지 않습니다. 심을 때 기존 작물은 조준을 가리지 않으며, 길과 장애물은 조준을 막습니다. 물을 준 영역은 푸른 흙으로 바뀌고 수확 후에는 갈색으로 돌아옵니다. 길·다리·물가·장애물 표면은 갈 수 없습니다. `E`로 물건을 여러 개 집어 인벤토리에 넣고, 숫자키나 마우스 휠로 핫바 아이템을 선택합니다. `Tab`으로 인벤토리를 열어 아이템 칸을 차례로 클릭하면 이동하거나 서로 교환할 수 있습니다. 갈린 빈 밭에 선택한 물건을 `E`로 심으세요. 기본 설정에서는 심은 뒤 물뿌리개로 밭이나 작물을 보고 왼쪽 클릭해야 성장이 시작됩니다. 물이 이미 있는 땅에서는 바로 시작할 수 있습니다. 오브젝트별 설정에서 물 주기 시작 조건을 끌 수 있습니다. 심은 물건은 원래 모델·색·크기를 유지하며, 바닥에 닿는 높이에 놓입니다. 성장은 심을 때의 크기와 비율을 유지하며, 빛·물·땅 조건에 맞는 보너스가 최종 크기에 반영됩니다. 성장 중 몸집이 커져 주변 식물과 겹쳐도 주변 식물이나 다른 오브젝트는 제거되지 않습니다. 자란 작물은 플레이어를 피해 도망갑니다. 작물을 때려 수확하고 다시 심으면 가치와 체력이 올라갑니다. 인벤토리 칸 수는 Unity에서 `FarmPrototype.unity` 씬의 **Farm Prototype** 오브젝트를 선택한 뒤 Inspector의 **Inventory Slot Count**에서 변경할 수 있습니다. 처음 9칸까지 핫바로 사용합니다. 이 장면은 **로컬 1인 시제품**이며 멀티플레이와 스테이지 진행은 아직 연결되지 않았습니다.

시작 지점의 씨앗·괭이·물뿌리개·돌을 `E`로 가져가면 원래 위치에 같은 아이템이 즉시 다시 채워집니다. 이미 가져간 아이템을 다시 줍거나 수확물을 줍는 동작은 재고를 추가하지 않습니다. 현재는 무료 공급이며, 이후 상점 구매가 완료되는 시점에 같은 보충 기능을 연결할 수 있습니다.

## 오브젝트별 성장 조건

성장률은 **최종 크기**에 적용됩니다. 성장 속도는 환경 보너스의 영향을 받지 않습니다.

`최종 크기 성장률 = 기본 100% + 맞는 빛 조건의 보너스 + 맞는 물 조건의 보너스 + 맞는 땅 조건의 보너스`

예를 들어 빛 +20%, 물 +30%가 맞으면 150%이며, 기본 100% 완성 크기의 1.5배가 됩니다. 조건이 맞지 않으면 해당 보너스만 빠집니다. 성장 도중 환경을 다시 확인하고 완성 시점의 조건으로 최종 크기가 결정됩니다.

### 오브젝트 설정

- `Assets/Data/Growth`에 씨앗·수확물·괭이·물뿌리개·돌의 예제 설정과 기본 설정이 있습니다. 파일을 선택하고 Inspector에서 수치를 변경하세요.
- 새 설정은 Project 창의 **Create > What The Farm > Plant Growth Profile**로 만듭니다. 오브젝트 프리팹의 **Farm Item > Growth Profile**에 연결하면 오브젝트마다 다른 조건을 사용할 수 있습니다.
- 얻을 수 있는 임의 모델에도 FarmItem과 Collider/Rigidbody를 연결하고 Growth Profile을 지정해 같은 성장 시스템을 사용할 수 있습니다. 종류별로 별도 성장 코드를 작성할 필요는 없습니다.
- 현재 자동 생성하는 아이템은 씬의 **Farm Prototype > Growth Profiles**에 연결한 설정을 사용합니다. 목록에 없는 종류는 **Default Growth Profile**을 사용합니다.
- 수확 후 아이템과 재고 보충에도 해당 설정이 유지됩니다. 수확물로 종류가 바뀌어도 원래 오브젝트의 성장 조건을 이어받습니다.

| Inspector 항목 | 의미 |
| --- | --- |
| Base Rate Percent | 기준 최종 크기 성장률. 기본 100 |
| Growth Seconds / Seconds Per Generation | 성장 시간 / 세대당 추가 시간 |
| Mature Size Multiplier / Size Per Generation | 100% 조건의 완성 크기 배율 / 세대당 추가 배율 |
| Require Water To Start | 물을 받은 뒤 성장 시작. 해제하면 마른 땅에서도 시작 |
| Use Light Condition / Min Light / Max Light / Light Bonus Percent | 빛 조건 사용 여부, 선호 범위, 맞으면 더할 % |
| Use Water Condition / Min Water / Max Water / Water Bonus Percent | 물 조건 사용 여부, 선호 범위, 맞으면 더할 % |
| Use Soil Condition / Preferred Soils / Soil Bonus Percent | 땅 조건 사용 여부, 선호 땅 종류, 맞으면 더할 % |

### 땅과 환경 설정

- Ground_Block의 자식 Ground에 붙은 **Soil Surface**에서 Soil Type, Light Amount, Initial Water Amount를 지정합니다. 빛과 물의 기준 범위는 0~100입니다.
- `Assets/Data/Soils`에는 Loam(일반 흙), Clay(점토), Sand(모래), Paddy(논), Jungle(정글)의 예제 종류가 있습니다. **Create > What The Farm > Soil Type**으로 종류를 더 만들 수 있습니다.
- **Sunlight**에 Directional Light를 연결하면 세기와 실제 장애물의 그늘을 계산합니다. Full Sun Intensity는 기준 태양 세기, Shade Light Amount는 그늘의 수치입니다. 비워 두면 Light Amount를 그대로 사용합니다. 첫 맵은 태양에 연결되어 있습니다.
- 물뿌리개는 **Farm Prototype > Water Per Use**만큼 물의 양을 늘립니다. 기본값 25, 최대 100이며 여러 번 물을 줄 수 있습니다. 선호 범위를 넘으면 물 보너스를 받지 못합니다.
- Initial Water Amount는 새로 갈아 만든 영역의 초기 물 양입니다. 플레이 중 갈린 영역의 Farm Plot > Water Amount에서 현재 양을 확인할 수 있습니다.
- 갈린 영역 하나당 대상 하나, 조준 위치에 심기, 원래 외형·크기 유지 규칙은 계속 적용됩니다.

## 판매 NPC

첫 맵 판매소 앞 `(9, 0, 7.5)`에 BuyerNPC를 배치했습니다. 가까이서 NPC를 보고 `E`를 누르면 대사가 순서대로 출력됩니다. 수확한 작물 또는 다른 아이템을 핫바에서 선택하고 NPC를 향해 `Q`로 던지면 받는 범위에 들어간 아이템이 사라지고 골드가 지급됩니다. 성장 중인 대상이나 도망다니는 작물은 먼저 수확해서 아이템으로 만든 뒤 판매합니다. 씨앗·수확물·도구·물뿌리개·돌 모두 판매할 수 있습니다.

- 기본 판매가는 아이템에 표시된 골드 값입니다. 화면 오른쪽 위에 보유 골드가 표시됩니다.
- BuyerNPC의 **Npc Merchant > Display Name / Dialogue / Dialogue Seconds / Sale Multiplier**에서 이름, 대사, 표시 시간, 가격 배율을 변경합니다.
- **Sphere Collider > Radius**는 아이템을 받는 범위입니다. Capsule Collider는 몸의 충돌과 대화 조준에 사용됩니다.
- 다른 곳에 NPC를 추가하려면 BuyerNPC 프리팹을 배치합니다. World 참조가 비어 있으면 현재 씬의 Farm Prototype을 찾습니다.
- 보유 골드는 현재 플레이에만 유지되며, **Farm Prototype > Starting Gold**에서 초기 금액을 설정합니다.

## 저장소 규칙

- `Assets/`, `Packages/`, `ProjectSettings/`와 각 에셋의 `.meta` 파일을 함께 커밋합니다.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` 등 Unity가 다시 만드는 파일은 커밋하지 않습니다.
- 씬과 프리팹은 텍스트 직렬화, 메타 파일은 표시 모드로 설정되어 있습니다.
- 큰 바이너리 에셋이 많아지면 Git LFS 도입을 검토합니다.

원격 저장소: [what-the-farm](https://github.com/chicken0113/what-the-farm)
