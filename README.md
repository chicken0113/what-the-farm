# What The Farm

현재 개발 대상은 다시 **Unity 6 (6000.3.25f1)** 프로젝트입니다. 작업 폴더는 `C:\Users\MSI\what-the-farm`입니다. Unity 6 기반 3D 게임 프로젝트입니다. 기본 3D 템플릿과 Universal Render Pipeline(URP)을 사용합니다.

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
## 유니티 복귀 상태

- 기존 유니티의 농사, 인벤토리, 환경에 따른 최종 크기, 수확 크기 유지, 도구 범위 배율, 공급품 재입고, NPC 판매를 기준으로 다시 개발합니다.
- 언리얼에서 추가한 작은 아이템 기본 크기, 느슨한 아이템의 플레이어 충돌 제거, 개별 아이템 1회 식재 제한, 넓어진 첫 맵과 경계 몬스터/다음 스테이지 이동은 아직 유니티에 이식하지 않았습니다.
- 언리얼에서 연결한 농부/괭이 모델과 줍기/휘두르기 애니메이션도 아직 유니티에 이식하지 않았습니다. `.uasset`은 유니티에서 직접 사용할 수 없으므로 FBX 등으로 내보낸 뒤 다시 연결해야 합니다.
- 저장소의 `whatthefarm.uproject`, `Source`, `Content`, `Config`, `Scripts`는 이전 언리얼 작업을 보관한 자료입니다. 유니티 개발은 `Assets`, `Packages`, `ProjectSettings`에서 진행합니다.
