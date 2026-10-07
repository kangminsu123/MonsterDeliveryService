# Monster Delivery Service 기술문서

개발 구조와 동작 규칙 및 검증 기준

작성일 2026년 10월 6일 · 문서 버전 1.0 · 기준 커밋 `0efe5a6`

## 1 개요

이 문서는 Monster Delivery Service의 현재 Unity 구현을 개발자가 이해하고 이어서 수정할 수 있도록 정리한 기술 기준서다. 섬 맵과 에셋 제작 과정, 플레이어 이동, 우체통 상호작용과 점령, 생활용품 아이템 로직, 검증 도구를 다룬다. 실제 코드와 저장된 검수 기록을 근거로 작성했다.

현재 기본 플레이는 빈손으로 섬을 탐색하고 집 앞 우체통을 점령하는 모드다. 생활용품을 사용하는 비살상 난투 로직은 준비되어 있지만 픽업과 사용 기능은 플레이에 노출하지 않았다. 우체통 본체 색은 유지하며 미점령 효과는 흰색, 점령 효과는 소유자의 색으로 표시한다. 로컬 테스트 플레이어의 색은 노란색이다.

| 구분 | 현재 상태 |
|---|---|
| 섬과 주택가 | 도로와 보도, 집 10채, 마당, 공원, 소품과 조명 배치 |
| 플레이어 | 빈손 시작, 지상 이동, 점프, 달리기와 스태미나 |
| 우체통 | 집 앞 10개 배치, 조준점과 안내, 흰 테두리, E 유지 점령 |
| 점령 표시 | 고리와 입자만 소유자 색 적용, 본체 재질 유지 |
| 생활용품 10종 | 종류와 소모 및 비살상 효과 구현, 플레이 노출 보류 |
| 색 선택 화면 | 연결 함수 준비, 입장 전 UI는 미구현 |
| 온라인 | 실제 참가자 연결과 상태 동기화는 미구현 |

구현 범위와 기획의 우선순위는 `GAME_DESIGN.md`의 2026년 10월 6일 내용과 `HANDOFF.md`의 현재 기준 항목을 따른다. 과거 기록에 남은 총기, 피해량, 사망과 리스폰 설명은 현재 규칙으로 사용하지 않는다.

대상 독자는 게임플레이 개발자와 레벨 및 테크니컬 아트 담당자다. 아래 검증 수치는 로컬 Editor 검사 결과이며 온라인 품질이나 프레임 성능을 의미하지 않는다.

## 2 실행 환경과 코드 구조

| 항목 | 확인된 구성 |
|---|---|
| Unity | 6000.5.10f1 Windows Editor |
| 언어와 입력 | C#, Unity Input System 1.20.0 |
| 렌더링 | Standard 재질과 프로젝트 Surface Shader 및 커스텀 Pass |
| 3D 제작 | Blender 5.1.0, Higgsfield Bridge를 통한 bpy 실행 |
| 개발 연결 | MCP for Unity 패키지, Editor 연결 자동 시작 코드 |
| 기본 씬 | `Assets/Scenes/SampleScene.unity` |

렌더링 구현은 현재 프로젝트의 Standard 및 Surface Shader 경로를 기준으로 한다. 패키지 목록에 Navigation, Multiplayer Center, Timeline 등이 있어도 현재 봇이 NavMesh를 사용하거나 온라인 연결이 구현됐다는 뜻은 아니다. 봇 이동은 코드의 CharacterController 직접 이동 방식이다.

### 런타임 책임

| 파일 또는 클래스 | 책임 |
|---|---|
| `MonsterDeliveryPrototype.cs` | 모드 분기, 입력과 이동, 시선 선택, 인벤토리, 점령 및 경기 상태 |
| `DeliveryItemState.cs` | 기절과 면역, 이동 배율, 방어, 강제 이동과 안전 위치 |
| `InteractionTarget.cs` | 대상 이름과 안내, 거리 및 활성 여부, 테두리 Renderer 제어 |
| `PickupKind` | 픽업이 지급하는 아이템 종류를 저장 |
| `BrawlShot` | 투척 물리 이동과 첫 충돌 처리, 수명 관리 |

`WeaponKind`, `UseWeapon`, `weaponAmmo`, `BrawlShot`은 이전 코드의 이름을 유지한다. 현재 의미는 생활용품 종류, 사용 동작, 사용 횟수, 비살상 투척체다. 이름만 보고 총기나 피해 처리로 해석하지 않는다.

### 초기화와 모드

`Install`은 `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`에서 실행된다. 씬에 게임 컴포넌트가 없으면 `MapExplorer`를 만들고, `CreateWorld`가 Main Camera와 플레이어를 준비한다. `Layer_11_Mailboxes`의 실제 배치 인스턴스를 점령 배열에 연결한다.

기본값은 `mapExploreMode=true`, `mapEditMode=false`다. 탐색 모드는 이동과 상호작용 및 점령을 실행하지만 봇과 임시 픽업을 생성하지 않고 경기 시간 종료도 실행하지 않는다. 일반 경기 분기에는 봇 3명과 180초 제한, 결과 순위 계산 코드가 남아 있다.

## 3 씬과 레벨 제작 구조

맵은 Editor에서 생성하거나 배치한 씬 데이터다. 실행 중 레벨 전체를 매번 만드는 구조가 아니다. `Assets/Editor`의 메뉴 도구가 메시와 머티리얼, 프리팹 배치를 준비하고 SampleScene에 저장한다. 루트 그룹으로 작업 영역을 구분한다.

| 씬 그룹 | 주요 내용 |
|---|---|
| `Layer_00_GrassBase` | 섬 상부 잔디 메시와 지면 MeshCollider |
| `Layer_01_Sidewalks` | 보도 표면과 이동용 BoxCollider |
| `Layer_02_Roads` | 도로 메시와 도로 재질 |
| `Layer_03_IslandTerrain` | 절벽, 수면과 해안, 보이지 않는 바다 경계 |
| `Layer_04_Houses`, `Layer_05_Yards` | 주택 10채와 마당, 울타리 및 진입로 |
| `Layer_06_LowPolyTownProps`, `Layer_07_ReferenceLandscape` | 차량, 바위와 나무, 공원 포장 및 식재 |
| `Layer_08_RooftopAccess`, `Layer_09_PitchedRoofRamps` | 옥상 접근과 경사 지붕 이동 표면 |
| `Layer_09_StreetLighting`, `Layer_10_EntranceAndParkLighting` | 가로등, 현관 및 공원 조명 |
| `Layer_11_Mailboxes` | 집별 우체통 프리팹 인스턴스 10개 |

`ReferenceIsland`는 96개 해안 샘플과 고정 난수 시드를 사용해 섬, 절벽과 해안 메시를 구성한다. 코드 기준 상부 지면은 Y=-0.25, 바다는 Y=-10.5다. 원래 도로와 보도 범위가 육지 위에 놓이는지 Collider Raycast로 확인한다.

`ReferenceHouses`와 `ReferenceYards`는 기준 평면 좌표와 실제 모델 Bounds를 사용해 집과 마당을 배치한다. `BakedCottageImport`는 주택 FBX 10개와 텍스처를 연결하고 프리팹을 만든다. `LowPolyTownPlacement`는 지면과 도로, 장애물을 검사해 나무·차량·바위 등을 놓으며 옥상 실외기는 허용한 평지붕의 지지 표면을 확인한다.

조경은 `ReferenceLandscape`가 공원 입구와 포장, 식재를 구성한다. 기존 조경 보고서에는 식재 575개가 육지에 있고 아스팔트를 침범하지 않는지 확인한 기록이 있다. 이 수치는 해당 배치 검수 기록의 결과이며 이번 문서 작성 중 다시 측정한 값은 아니다.

일부 기존 배치 메뉴는 이미 만들어진 그룹이 있으면 실행을 거부한다. 모든 레벨 메뉴를 일괄 실행하면 현재 씬 구성을 다시 만들 수 있으므로, 수정하려는 영역의 메뉴와 실행 조건을 먼저 확인한다.

## 4 맵 재질과 표면 안정성

### 절차적 표면 셰이더

`Assets/MapMaterials`의 셰이더는 텍스처 없이 또는 메시의 색상과 결합해 맵 표면을 표현한다. 잔디는 월드 XZ 좌표를 삼각 격자로 나누고 면별 색을 선택한다. 아스팔트는 월드 좌표 노이즈를 더해 큰 색 변화와 작은 입자감을 만든다.

| 셰이더 | 구현 방식 |
|---|---|
| `Map/Low Poly Grass` | 월드 공간 삼각 격자, 면별 색과 저주파 변화 |
| `Map/Reference Asphalt` | 서로 다른 크기의 노이즈로 도로 색 변화 |
| `Map/Sidewalk Concrete` | 로컬 좌표 기반 슬래브 줄눈, 월드 좌표 기반 얼룩과 입자 |
| `Map/Island Facets` | 섬 표면 메시의 면 색상 사용 |
| `Map/Island Ocean` | 시간과 월드 좌표의 사인 및 코사인으로 수면 정점 변형 |

보도 셰이더는 줄눈 계산에 오브젝트 로컬 좌표를 사용하므로 `DisableBatching=True`를 지정했다. 월드 공간 얼룩을 사용해 이어지는 보도 표면의 질감을 맞추고, 미세 입자는 화면 미분값으로 거리에 따른 변화량을 조절한다. 물의 정점 움직임은 시각 표현이며 수면을 걷는 물리 기능을 제공하지 않는다.

### 보도 모서리 중첩 수정

보도를 둥글게 연결하는 과정에서 같은 높이의 표면이 겹치면 깊이 경쟁으로 깜빡임이 생긴다. `SidewalkCornerRepair`는 각 보도의 XZ 투영을 다각형으로 처리해 이전 표면과 겹치는 부분을 빼고 남은 영역을 서로 겹치지 않는 조각으로 분할한다.

분할 결과는 `Assets/MapMeshes/Sidewalk_CornerSurfaces.asset`에 저장한다. 변경하는 것은 렌더링 메시이며 기존 Transform과 이동용 BoxCollider는 유지한다. `ReferenceSidewalks`의 재생성 메뉴도 이 수정을 호출한다. 생성 도구를 수정할 때 표면 처리와 이동 충돌을 함께 바꾸지 않도록 주의한다.

검수 화면은 `art-review/sidewalk-corners-fixed-20260930.png`에 있다. 옥상 경사로와 바다 경계, 조명 배치는 현재 씬에 저장된 작업이며 단일 재생성 메뉴만으로 모든 후속 수동 조정을 재현한다고 가정하지 않는다.

## 5 플레이어 입력과 이동

`Keyboard.current`와 `Mouse.current`로 입력을 읽는다. 마우스 델타는 플레이어의 수평 회전과 카메라의 수직 회전에 사용하며 수직 각도는 -80도에서 80도로 제한한다. 카메라 위치는 플레이어 위치와 자세 높이에 맞춰 갱신한다.

| 항목 | 기본 수치 또는 입력 |
|---|---|
| 이동 | WASD, 걷기 6.5, 달리기 10.5, 숙이기 3.6 |
| 점프 | Space 유지, 수직 속도 7.5, 중력 20 |
| 달리기 | Left Shift, 이동 중 스태미나 소모 |
| 스태미나 | 최대 100, 달리기 28/초 소모, 휴식 20/초 회복 |
| 숙이기 | C, Controller 높이 2에서 1.15로 변경 |
| 구르기 | Left Ctrl, 이동 입력 필요, 50 소모, 0.35초 동안 속도 17 |
| 시점 | 마우스, 회전 배율 0.12, 기본 목표 FOV 60 |
| 장비 | 시작 시 3개 슬롯 모두 비어 있음 |

`CharacterController.isGrounded`는 이전 `Move`의 접촉 결과를 사용한다. 먼저 지면 접촉과 점프 여부를 결정하고, 수평 이동·아이템 강제 이동·수직 이동을 합쳐 한 번의 `Move`로 적용한다. Controller 크기는 실제 숙이기 상태가 바뀔 때만 갱신해 접촉 상태가 불필요하게 초기화되는 문제를 줄였다.

스태미나가 0이 되면 `sprintExhausted`를 설정해 걷기로 전환한다. Shift를 계속 누른 상태로 회복돼도 자동 달리기를 시작하지 않으며, Shift를 놓았다 다시 눌러야 한다. 러닝화가 활성화된 동안에는 달리기 소모 대신 회복 경로를 사용한다.

### 안전한 지면 복귀

`RecoverActor`는 지면에 접촉하고 Y가 -5보다 높을 때 안전 위치를 기록한다. Y가 -8 아래로 떨어지면 Controller를 잠시 비활성화하고 안전 위치로 옮긴 뒤 다시 활성화한다. 수직 속도와 강제 이동을 초기화하고 점령 진행을 취소한다. 체력, 사망 또는 사망 리스폰 기능은 사용하지 않는다.

근거는 `MonsterDeliveryPrototype.UpdatePlayer`, `UpdateCamera`, `RecoverActor`다. 지면 이동 검사는 실제 씬의 도로와 차량, 현관 계단을 대상으로 수행한다.

## 6 조준점과 공통 상호작용

`UpdateInteraction`은 화면 중앙에서 나가는 Raycast로 가장 먼저 맞은 고체 표면을 선택한다. 가까운 우체통을 별도로 검색해 고르는 방식은 사용하지 않는다. 트리거는 시선을 막지 않으며 고체 울타리와 벽은 대상 선택을 가린다.

```csharp
var ray = cam.ViewportPointToRay(new Vector3(.5f, .5f, 0));
Physics.Raycast(ray, out var hit, 12, ~0,
    QueryTriggerInteraction.Ignore);
var next = hit.collider.GetComponentInParent<InteractionTarget>();
var distance = hit.distance + cam.nearClipPlane;
```

위 코드는 선택 흐름을 보여주는 발췌다. 실제 함수에는 카메라와 경기 상태, 맵 편집 모드 및 Raycast 성공 여부의 검사도 포함된다. 표면까지의 거리에 nearClipPlane을 더해 카메라 기준 거리로 판정한다.

| 조건 | 적용 방식 |
|---|---|
| 감지 범위 | 최대 12m |
| 기본 상호작용 거리 | `InteractionTarget.interactionDistance=3m` |
| 대상 구조 | Collider의 부모에서 InteractionTarget 검색 |
| 행동 상태 | 기절과 강제 이동 및 사용 중 상태에서 준비 해제 |
| 우체통 소유권 | 로컬 플레이어가 이미 소유하면 준비 해제 |
| 픽업 | 아이템 노출 플래그와 빈 슬롯이 있어야 준비됨 |

`InteractionTarget`에는 `displayName`, `actionHint`, `interactionDistance`, `available`을 저장한다. 다른 물건에도 같은 대상 판정 기준을 붙일 수 있지만 이름표를 붙이는 것만으로 새 행동이 실행되는 것은 아니다. 실제 동작은 게임의 해당 대상 처리 함수에 연결해야 한다.

`DrawInteractionHUD`는 IMGUI로 중앙의 흰 조준점과 검은 외곽선을 그린다. 대상이 감지되면 이름과 안내를 표시하며 준비된 우체통에는 E 유지 안내를 보여준다. 거리 밖 대상에는 접근 또는 상태 대기 안내, 자신의 우체통에는 점령 완료 안내를 표시한다. 조준점은 대상 유무와 관계없이 기본 플레이 중 유지한다.

## 7 흰 테두리와 점령 소유권

### 테두리 표시

`InteractionTarget.Awake`는 시각 메시 아래에 테두리용 MeshRenderer를 만들고 기본으로 끈다. `SetFocus`는 `focused && ready`일 때만 Renderer를 켜고 색을 흰색으로 지정한다. 대상이 멀거나 가려지거나 자신의 우체통이면 테두리가 없다. 테두리의 색은 점령자 색과 독립적이다.

`Delivery/Interaction Outline`은 두 Pass를 사용한다. 첫 Pass가 대상 내부를 스텐실 비트 64에 기록하고, 두 번째 Pass가 확장된 메시의 앞면을 제거해 내부 마스크 밖만 그린다. 확장 폭은 클립 좌표와 화면 크기로 계산하며 기본값은 4.5px이다. `ZTest LEqual`을 사용해 고체 표면 뒤의 윤곽 표시를 제한한다.

분리된 면 사이의 틈을 줄이기 위해 같은 위치의 정점 노멀을 합친 `mailbox_Outline.asset`을 사용한다. 테두리는 별도 시각 객체이며 Collider와 그림자를 추가하지 않는다.

### 점령 진행과 취소

`UpdateCapture`는 선택된 실제 우체통에 E를 유지한 시간을 계산한다. 기본 3초, 활성화한 작업용 장갑이 있으면 2초다. E 해제, 다른 대상을 바라봄, 장애물 가림, 거리 이탈, 기절과 강제 이동, 아이템 사용은 진행을 취소한다. 슬로우와 잉크 시야 차단만으로는 진행을 취소하지 않는다.

`owner[house]`는 -1을 중립, 0을 로컬 플레이어, 1~3을 로컬 봇으로 사용한다. 성공 시 `Capture`가 소유권과 해당 우체통의 효과 색을 갱신한다. 작업용 장갑은 성공 시 소모하고, 중단하면 활성 상태를 유지한다. 같은 플레이어가 소유한 우체통은 다시 점령할 수 없다.

### 색상 적용 API

`SetPlayerColor(int playerId, Color color)`는 참가자 팔레트에 색을 저장하고 해당 참가자가 소유한 우체통 효과를 갱신한다. 지원 범위는 현재 로컬 ID 0~3이며 잘못된 ID는 예외를 발생시킨다. 알파는 1로 정규화한다. 입장 전 색 선택 화면은 아직 없다.

`SetMailboxEffectColor`는 `CaptureBeacon` 하위 Renderer에만 `MaterialPropertyBlock`의 `_Color`를 적용한다. 본체 재질과 공유 효과 머티리얼을 바꾸지 않아 다른 중립 우체통에 색이 전파되지 않는다. 초기화와 경기 재시작은 중립 효과를 흰색으로 복원한다.

## 8 인벤토리와 생활용품 아이템

기본 장착은 `WeaponKind.None`이다. `inventory[3]`은 종류, `weaponAmmo[3]`은 남은 사용 횟수, `weaponSeconds[3]`는 선풍기 배터리를 보관한다. `AddWeapon`은 첫 빈 슬롯에 넣고 선택 슬롯을 바꾼다. 슬롯이 가득 차면 획득하지 않으며, 1~3으로 선택하고 G로 영구 폐기한다. 사용 완료나 폐기 후 자동 지급은 없다.

| 아이템 | 현재 효과 로직 | 소모 |
|---|---|---|
| 복싱 글러브 | 전방 3m 상대 밀기와 위로 띄우기 | 3회 |
| 압축 뚫어뻥 | SphereCast 9m 이내 상대 당기기, 가림 검사 | 3회 |
| 휴대용 확성기 | 0.25초 준비 후 전방 4m 상대 0.8초 기절 | 2회 |
| 포장용 테이프 | 반경 3m 영역 5초, 이동 속도 40% 감소 | 2회 |
| 무선 선풍기 | 전방 6m 유지형 밀기, 프레임 시간만큼 배터리 차감 | 4초 |
| 러닝화 | 5초 이동 속도 40% 증가, 달리기 소모 면제 | 1회 |
| 에어캡 | 8초 내 기절 또는 강제 이동 한 번 방어 | 1회 |
| 작업용 장갑 | 다음 점령 시간 3초에서 2초로 단축 | 점령 성공 1회 |
| 프린터 잉크 | 반경 3m 영역 4초, 공격과 봇 이동의 시야 검사 | 2회 |
| 미니 트램펄린 | 8m 이내 바닥 설치, 8초 동안 누구나 이용 | 1회 |

`UseWeapon`은 기절, 빈손, 경기 상태와 사용 간격을 확인한다. 선풍기를 제외한 기본 사용 간격은 0.6초다. 대부분의 사용은 0.5초 동안 점령을 제한하고, 선풍기는 0.15초, 확성기는 0.8초를 사용한다. 트램펄린은 바닥 법선 Y가 0.5 이상일 때만 설치한다.

현재 아이템 종류는 고정 enum과 switch로 관리한다. 별도 데이터베이스나 ScriptableObject 아이템 정의는 없다. 밸런스 수정 시 게임 코드의 효과 수치와 `GAME_DESIGN.md`의 표, 관련 검사 기대값을 함께 확인한다.

### 플레이 노출 차단

`itemPlaytestEnabled`는 비직렬화 private bool이며 기본 false다. 픽업 생성, 지급, 사용과 장비 HUD를 차단한다. `CreatePickups`의 테스트 픽업은 단순 큐브이며 각 아이템 종류를 정해 넣는 로직이다. 실제 택배 프리팹의 드랍과 상자 열기 및 무작위 파밍 연출은 아직 연결하지 않았다. 생활용품 10종의 PNG는 컨셉 자료이며 게임용 3D 모델은 이후 작업이다.

## 9 비살상 상태와 영역 효과

`DeliveryItemState`는 MonoBehaviour가 아닌 공유 상태 객체다. 상태는 `Time.time` 기준 종료 시각으로 보관하며 플레이어와 봇에 같은 판정 함수를 적용한다. 체력과 피해량, 사망 판정은 없다.

| 상태 필드 또는 함수 | 규칙 |
|---|---|
| `stunUntil`, `Stunned` | 현재 시간이 종료 시각보다 앞이면 기절 |
| `stunImmuneUntil` | 기절 종료 후 2초 동안 추가 기절 방지 |
| `forceUntil` | 제어가 적용된 뒤 0.35초 동안 점령 제한 |
| `forceResistUntil` | 0.8초 내 추가 밀기나 당기기의 힘을 35%로 줄임 |
| `shieldUntil` | 유효한 기절이나 강제 이동 한 번을 막고 종료 |
| `slowUntil`, `speedUntil` | 이동 배율 0.6과 1.4를 곱함 |
| `busyUntil`, `CanCapture` | 기절, 강제 이동, 사용 중에는 점령 불가 |
| `force`, `pendingLift` | 수평 힘과 수직 상승 속도를 분리해 이동에 적용 |

`Control`은 실제 적용할 기절 또는 힘이 있는지 먼저 확인하고 방어를 검사한다. 따라서 기절 면역 중 무효 기절은 에어캡을 소비하지 않는다. `TakeForce`는 수평 힘을 초당 30의 크기로 0에 접근시키고, `TakeLift`는 대기 중 상승 값을 반환한 뒤 비운다.

`ItemArea`는 종류, 위치와 방향, 종료 시각 및 참가자별 발판 재사용 시각을 저장한다. `UpdateItemAreas`가 종료된 영역을 제거하고 활성 참가자의 위치를 검사한다. 테이프는 영역 안의 대상에 짧은 종료 시각을 계속 갱신해 슬로우를 유지한다.

트램펄린은 수평 거리 제곱 1.5 이내와 높이 차 2.2 이내, 지면 접촉 상태를 검사한다. 발사 방향 힘 12와 상승 값 11을 적용하고 참가자별 0.8초 재사용 간격을 둔다. 누구나 이용할 수 있으며 에어캡을 소비하지 않는다.

잉크의 `VisionBlocked`는 공격 또는 봇 목표까지의 선분에서 구름 중심과 가장 가까운 점을 구해 반경 3m 내부를 통과하는지 검사한다. 현재 구름은 영역 데이터와 시야 판정만 있으며 렌더링 연출은 없다. 봇은 목표 우체통을 향해 직접 이동하고 점령하는 기존 로컬 구현으로, 새 아이템 파밍 및 사용 AI는 추가하지 않았다.

## 10 투척과 고속 충돌 처리

테이프와 잉크는 `MakeShot`에서 투척체를 만든다. 카메라 앞 0.6m까지 Raycast와 반지름 0.12의 SphereCast를 먼저 수행해 가까운 벽이나 울타리 앞에서 충돌을 처리한다. 그 공간이 비어 있으면 카메라 앞 0.6m에 지름 0.24m 구체와 Rigidbody를 만든다.

현재 기본 투척 속도는 전방 18이며 위쪽 속도 3을 더한다. `ContinuousDynamic` 충돌 모드를 설정하고 플레이어의 Collider는 충돌 대상에서 제외한다. Renderer는 꺼 두어 미완성 투척 모델이 플레이에 노출되지 않게 한다.

### 이동 경로 검사

`BrawlShot.FixedUpdate`는 `linearVelocity × fixedDeltaTime`으로 다음 물리 이동량을 계산한다. Rigidbody의 `SweepTest`로 이 구간을 검사해 첫 고체 표면을 발견하면 `Impact`를 실행한다. `OnCollisionEnter`도 같은 함수를 사용한다.

`impacted` 플래그는 이중 처리를 막는다. 첫 충돌 때 투척체를 비활성화하고 삭제한 뒤 `ItemImpact`를 한 번 호출한다. 충돌이 없어도 Start에서 3초 수명을 예약한다. 트리거는 이동 경로 검사와 생성 위치 검사에서 제외한다.

테이프는 충돌점 근처에서 아래쪽 표면을 찾아 바닥 영역을 놓고, 잉크는 충돌 위치에 시야 차단 영역을 만든다. 현재 투척은 비살상 영역 효과이며 기존 총알의 피해 로직을 사용하지 않는다.

### 충돌 검증 범위

`ProjectileCollisionCheck`는 실제 집 10채와 울타리 50구간을 두 가지 거리에서 검사한다. 가까운 거리 0.3m와 5m, 테스트 고속 투척 조건 및 장애물 없는 경로와 트리거 통과를 포함해 총 121개 결과를 기록한다. 2026년 10월 6일 기록은 121/121 통과다.

이 검사는 특정 씬과 테스트 조건에서 관통을 확인한 결과다. 임의의 이동 속도와 모든 향후 모델 또는 네트워크 지연 조건을 보장하는 검사는 아니다. 새 Collider나 투척 모델을 넣으면 생성 위치 검사와 이동 경로 검사를 같은 도구로 다시 확인한다.

근거는 `MonsterDeliveryPrototype.MakeShot`, `ItemImpact`, `BrawlShot`과 `art-review/item-projectile-collisions-20261006.txt`다.

## 11 3D 에셋과 Unity 임포트

우체통과 택배는 사용자 참고 이미지 2개를 바탕으로 Blender의 기본 메시와 재질을 직접 구성한 모델이다. Higgsfield Bridge로 bpy를 실행해 제작하고 텍스처를 베이크했다. Blender 소스와 FBX, GLB를 출력했으며 실제 Unity 플레이에서는 FBX와 프리팹을 사용한다.

| 모델 | 원본 크기 X Y Z | 삼각형 | Unity 처리 |
|---|---|---|---|
| 택배 상자 | 0.500 × 0.433 × 0.384m | 1816 | 정적 프리팹과 MeshCollider |
| 우체통 | 0.417 × 0.854 × 1.732m | 1613 | 원본 보존, 파생 메시 높이 2.832m |

크기 표는 Blender 모델 감사 파일의 좌표축 기준이며, Unity에서는 축 변환 후 배치한다. 우체통 높이는 단순 전체 확대가 아니라 `mailbox_RaisedPost.asset`에서 아래 목재 기둥을 연장했다. 함체와 깃발 및 받침 형상을 유지하며 원본 FBX는 바꾸지 않는다.

### 텍스처 변환

`Assets/Art/DeliveryProps`에는 모델별 2048 해상도 albedo와 ORM을 보관한다. ORM은 R에 AO, G에 roughness, B에 metallic을 저장한다. `ImportProp`은 Unity Standard용 MetallicSmoothness 맵을 만들 때 R에 ORM.B, A에 `1 - ORM.G`를 넣는다.

컬러 텍스처는 sRGB, 물성 데이터는 Linear로 설정한다. 현재 Standard 재질에는 albedo와 MetallicGlossMap을 연결한다. AO 데이터는 원본 ORM에 보존되지만 별도 OcclusionMap 연결은 하지 않았다. AO까지 사용하려면 Standard의 입력 채널에 맞춘 추가 처리를 검토한다.

### 임포터와 프리팹

ModelImporter는 `globalScale=1`, `useFileScale`, `bakeAxisConversion=true`를 사용하고 FBX의 카메라와 광원 및 애니메이션은 가져오지 않는다. 원본 노멀과 계산한 접선을 사용하며 `MAT_EXPORT_parcel` 또는 `MAT_EXPORT_mailbox`를 Unity 재질로 Remap한다.

우체통 프리팹은 Model, InteractionTarget, CaptureBeacon을 묶고 택배 프리팹도 재사용 가능한 에셋으로 저장한다. 택배 드랍은 플레이에 아직 배치하지 않았다. Blender 원본에는 분리된 제작 요소가 있어도 현재 게임용 FBX는 정적 출력이므로 문 열기나 깃발 회전 애니메이션은 별도 작업이다.

## 12 우체통 배치와 목표 이펙트

`DeliveryPropsPlacement`는 Import and Place Mailboxes 메뉴에서 에셋을 준비하고 10개 프리팹을 집별로 배치한다. 대상 그룹은 `Layer_11_Mailboxes`, 인스턴스 이름은 `Plot_01_Mailbox`부터 `Plot_10_Mailbox`까지다. 기존 배치를 갱신해 반복 실행 시 중복을 피한다.

배치는 각 집의 대문 옆 울타리 바깥 잔디를 기준으로 후보를 찾는다. 지면 Collider Raycast로 Y를 맞추고 OverlapBox로 울타리, 계단과 조명 등 장애물 겹침을 검사한다. 문은 도로를 향하며 9번과 10번 집은 반대 방향을 처리한다. 현재 높이는 2.832m, 지면은 -0.25m이며 저장 보고서의 통로 여유는 약 0.476~0.507m다.

### CaptureBeacon 구성

| 컴포넌트 | 주요 설정 |
|---|---|
| LineRenderer | 로컬 공간 64점 고리, 반지름 0.48m, 굵기 0.035m |
| Legacy Animation | 2.2초 반복, 고리 XZ 스케일 0.94에서 1.08 |
| ParticleSystem | 초당 4개 방출, 최대 16개, 로컬 공간과 prewarm |
| 입자 수명과 움직임 | 수명 2.2~3.2초, 속도 0.18~0.3, 크기 0.045~0.075m |
| ParticleSystemRenderer | 구체 Mesh, 크기 변화로 생성 및 소멸 표현 |
| 효과 재질 | Unlit/Color, 기본 흰색, 그림자와 추가 광원 없음 |

네이티브 Animation과 ParticleSystem이 움직임을 담당하므로 별도의 매 프레임 맥동 스크립트는 없다. 효과에 Collider를 추가하지 않아 시선과 이동을 방해하지 않는다. 색은 앞에서 설명한 MaterialPropertyBlock으로 소유권에 따라 바뀐다.

`Verify Mailboxes`는 10개 인스턴스, 높이와 지면 접촉, 도로 방향, 울타리 바깥 위치, 장애물 겹침, 재질과 효과 구성을 검사한다. `Verify Beacon Motion`은 Play Mode에서 Animation과 입자 재생 상태 및 입자 존재를 확인하고 Clip 샘플로 스케일 범위를 검사한다. 이 검사는 활성 상태와 곡선을 확인하며 프레임 성능 측정은 아니다.

씬의 배치 전 사본은 `art-review/before-mailbox-placement.unity`다. 현재 씬과 다른 시점의 복구 자료이므로 그대로 불러오면 이후 우체통 수정이 빠질 수 있다.

## 13 검증 도구와 실행 절차

검증은 Unity Editor의 메뉴 함수로 구현했다. 게임 함수와 실제 Collider를 호출하고, 필요한 검사에는 Input System의 임시 입력 장치를 사용한다. 검사 후 임시 객체와 카메라, 소유권 및 팔레트 등 수정 상태를 복원한다. 외부 CI나 독립적인 NUnit 전체 회귀 스위트는 아니다.

| 메뉴의 마지막 이름 | 모드 | 확인 결과 |
|---|---|---|
| Verify Crosshair Interaction | Play | 대상, 거리, 가림, 점령, 소유자 색 48개 |
| Verify Delivery Items | Play | 아이템 사용, 상태, 소모, 노출 차단 42개 |
| Verify Player Basics | Play | 빈손 시작, 입력과 스태미나 14개 |
| Verify Jump Surfaces | Play | 도로와 차량 및 계단 18개 |
| Verify Projectile Collisions | Play | 투척 관통과 트리거 121개 |
| Verify Mailboxes | Edit | 배치와 재질 및 효과 구성 10/10 |
| Verify Beacon Motion | Play | 고리 10개와 입자 시스템 10개 활성 |

위 결과는 2026년 10월 6일 개발 확인과 저장 보고서 기준이다. 문서 작성 중 게임 코드를 수정하거나 전체 검사를 다시 실행하지 않았다. 본 문서는 검증 범위와 방법을 기록하며 이후 코드가 바뀌면 결과를 갱신해야 한다.

### 실행 순서

1. Unity 6000.5.10f1에서 SampleScene을 열고 컴파일 오류를 확인한다.
2. Edit Mode에서 `Tools/Art/Delivery Props/Verify Mailboxes`를 실행한다.
3. Play Mode에 진입해 `Tools/Gameplay`의 기본 이동, 상호작용과 아이템 검사를 실행한다.
4. `Tools/Level/Verify Jump Surfaces`와 `Tools/Gameplay/Verify Projectile Collisions`를 실행한다. 투척 검사는 비동기이므로 완료 로그를 확인한 뒤 Play를 종료한다.
5. `Tools/Art/Delivery Props/Verify Beacon Motion`을 확인하고 일반 플레이에서 E 점령 화면을 확인한다.

관련 보고서는 `art-review/crosshair-interaction-20261006.txt`, `delivery-items-20261006.txt`, `item-projectile-collisions-20261006.txt`, `mailbox-placement-20261006.txt`다. 최신 UI 화면은 `mailbox-neutral-white-effects-20261006.png`와 `mailbox-owned-yellow-effects-20261006.png`를 사용한다. 과거 노란 테두리나 조준점 없는 검수 이미지는 현재 UI 기준이 아니다.

## 14 개발 연결과 운영 확인

### Unity와 Blender 연결

MCP for Unity는 Editor 상태를 읽고 메뉴를 실행하는 개발 도구다. 게임 참가자 사이의 온라인 통신은 담당하지 않는다. `NorthstarMcpAutoStart`는 Editor 로드 후 로컬 HTTP 서버 상태를 확인하고 필요하면 시작한 뒤 Bridge 연결을 시도한다. 서버 도달 여부는 최대 20회, 0.5초 간격으로 확인한다.

Blender 작업은 Higgsfield Bridge를 통해 bpy 코드를 실행한 제작 과정이다. 생성된 모델은 FBX와 텍스처로 Unity에 들어오므로 게임 실행 시 Blender 연결이 필요하지 않다.

### 빈 Game 화면 확인

이 프로젝트는 Play 진입 때 카메라를 만드는 경로가 있다. Edit Mode의 Game 창에 No cameras rendering이 보이면 먼저 Play 상태와 Main Camera 생성 여부를 확인한다. Play에서도 같은 화면이면 컴파일 오류, Install 실행, Camera 활성과 MainCamera 태그를 확인한다.

MCP의 WebSocket 경고와 카메라 렌더링 문제는 별도로 진단한다. Bridge 연결 실패는 Editor 제어 통신을, No cameras rendering은 렌더링 가능한 Camera 유무를 확인해야 한다. 콘솔의 정확한 발생 시각과 로그를 기준으로 판단한다.

### 수정 지점과 문서 갱신

| 변경 작업 | 우선 확인할 위치 |
|---|---|
| 상호작용 범위와 안내 | InteractionTarget, UpdateInteraction, DrawInteractionHUD |
| 점령 시간과 취소 | UpdateCapture, CaptureSeconds, ApplyControl |
| 소유자 효과 색 | SetPlayerColor, SetMailboxEffectColor |
| 아이템 수치와 소모 | Ammo, UseWeapon, DeliveryItemState, ItemArea |
| 우체통 에셋과 높이 | DeliveryPropsPlacement, 원본 FBX와 파생 메시 |
| 맵 표면과 배치 | 해당 Reference 계열 Editor 파일, SampleScene |

상태 규칙이나 아이템 수치를 수정하면 `GAME_DESIGN.md`와 관련 검증 기대값을 갱신한다. 기획 변경의 적용 범위는 `HANDOFF.md`와 `NEXT_SESSION.txt`에 기록하고, 화면 변경은 art-review에 증거를 남긴다. 이 문서는 구조 설명이며 원본 코드와 새 검수 결과를 대신하지 않는다.

## 15 후속 작업과 근거 파일

### 미구현 또는 노출 보류 항목

입장 전 플레이어 색 선택 UI는 `SetPlayerColor`에 연결할 예정이다. 현재 참가자 ID는 로컬 배열 인덱스이며 온라인 참가자 식별자와 연결하지 않았다. 온라인 단계에서는 소유권과 점령 진행의 권한, 상태 이상과 색상 동기화 방식을 별도로 정해야 한다.

생활용품 10종의 게임용 모델과 사용 애니메이션, 효과음, 상자 개봉 및 드랍·픽업 연출이 남아 있다. 잉크 구름의 시각화와 봇 아이템 파밍 및 사용 AI도 추가해야 한다. `itemPlaytestEnabled`를 켜는 것만으로 이러한 제작과 UI 작업이 완료되지는 않는다.

프레임 시간, Draw Call과 GPU 비용 또는 목표 기기별 성능은 아직 측정하지 않았다. 흰 테두리는 확장 메시를 다시 렌더링하며 우체통마다 고리와 입자 Renderer를 사용한다. 이후 성능 확인은 실제 카메라와 목표 기기를 정해 Profiler와 Frame Debugger로 수행한다.

### 문서의 구현 근거

| 영역 | 프로젝트 내 주요 근거 |
|---|---|
| 기획과 현재 범위 | GAME_DESIGN.md, HANDOFF.md의 2026년 10월 6일 항목 |
| 게임 동작 | Assets/Gameplay/MonsterDeliveryPrototype.cs |
| 공통 상태와 상호작용 | Assets/Gameplay/DeliveryItemState.cs, InteractionTarget.cs |
| 우체통 제작과 배치 | Assets/Editor/DeliveryPropsPlacement.cs, Assets/Art/DeliveryProps |
| 맵과 표면 | Assets/Editor/Reference 계열 파일, SidewalkCornerRepair.cs |
| 렌더링 코드 | Assets/MapMaterials의 shader, DeliveryProps/InteractionOutline.shader |
| 검증 구현 | Assets/Editor의 InteractionCheck, DeliveryItemsCheck 및 충돌과 이동 검사 |
| 저장된 증거 | art-review의 텍스트 보고서와 최신 우체통 화면 |
| 환경 | Packages/manifest.json, ProjectSettings/ProjectVersion.txt |

Blender 원본 크기와 삼각형 수는 제작 결과의 `model_audit.json`을 참고했다. 이전 기술 발표 자료는 `Docs/TechnologyOverview_20261006.pptx`에 있다. 이번 문서는 해당 발표 자료보다 코드 책임, 판정 조건과 이어서 수정할 위치를 상세히 설명한다.

기준 커밋 이후 변경이 있으면 해당 구현을 다시 확인해 문서 버전과 검증 날짜를 갱신한다. 계획된 기능은 구현 및 플레이 확인이 완료된 후 현재 상태로 옮긴다.
