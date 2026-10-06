# Monster Delivery Service — 인수인계

## 현재 기준 (2026-10-06)

이 항목과 GAME_DESIGN.md가 아래 과거 기록보다 우선한다. 아래 총기·피해·사망·기본 장비 설명은 이전 단계의 기록이다.

- SampleScene은 빈손으로 시작하는 섬 맵 탐색 모드다. 기존 소총·피해·사망·리스폰 경로를 제거했으며 생활용품 10종의 비살상 로직을 준비했다. itemPlaytestEnabled=false로 픽업·아이템 사용·슬롯 HUD 노출은 보류했다.
- 택배와 우체통 FBX, 2K 텍스처, 프리팹을 Assets/Art/DeliveryProps에 추가했다. 높이 2.832m의 우체통 10개를 각 집 대문 옆 울타리 바깥에 배치했다.
- 기본 중앙 조준점과 안내 문구를 유지한다. 화면 중앙의 첫 고체 표면, 3m 거리, 행동 가능 상태를 만족하는 대상에만 흰 테두리를 표시한다. E 3초 유지로 우체통을 점령하며 시선·거리 이탈 등은 진행을 취소한다.
- 미점령 고리·입자는 흰색이다. 점령 후 소유자 색으로 효과만 바뀌며 본체 재질은 유지한다. 테스트 플레이어는 노란색, SetPlayerColor가 향후 색 선택 화면의 연결 지점이다.
- Input System과 CharacterController 지상 이동·점프·달리기 및 스태미나를 유지한다. 투척 Raycast/SphereCast/SweepTest 충돌 보완과 보도 모서리 중첩 표면 수정도 포함한다.
- 최신 Unity 검사: 상호작용 48, 아이템 42, 플레이어 기본 14, 점프 지면 18, 투척 충돌 121 통과. 배치 10/10, 네이티브 고리·입자 활성 확인. 상세 증거는 art-review에 있다.
- 기술 설명 자료: Docs/TechnologyOverview_20261006.pptx (17장). 현재 구현과 미노출/향후 항목을 구분한다.
- 다음 작업: 입장 전 색 선택 UI, 생활용품 10종 모델·사용 연출·파밍 노출, 봇 아이템 AI, 실제 온라인 동기화. FPS·성능 프로파일링은 아직 수행하지 않았다.

## 최신 변경 (2026-10-05, 아래 2026-09-19 기록보다 우선)

- 총알 충돌 보완: 카메라~탄 생성 지점 1m의 Raycast/SphereCast로 근거리 엄폐물 관통 방지. BrawlShot.FixedUpdate의 Rigidbody.SweepTest로 고속 이동 경로를 검사하고, 충돌 시 즉시 비활성화/삭제 및 피해 1회 처리. Tools/Gameplay/Verify Projectile Collisions: 건물 10채·울타리 50구간×거리 2종 및 빈 경로/트리거 검사 총 121/121 통과. 기존 PlayerBasicsCheck 17/17 통과, Console 오류·경고 0. 상세 기록: art-review/projectile-collisions-20261005.txt.
- 실제 프로젝트: C:/Users/xlfhf/Documents/Codex/2026-09-18/https-github-com-kangminsu123-monsterdeliveryservice-git/MonsterDeliveryService. Unity MCP 연결 정상, Unity 6000.5.10f1, SampleScene.
- 현재 기본은 섬 맵 탐색 모드. 상시 드론 지급/자동 비행/탐색 전용 드론 모델을 제거하고 지상에서 시작한다. 우체통·봇·임시 픽업을 생성하지 않는 탐색 범위와 기존 레벨/조명은 유지한다.
- 시작 장비: 전동 타카건(StapleRifle), 1번 슬롯 선택, 30발, 좌클릭 연사(0.1초), 우클릭 조준, 피해 26. 일반 경기 시작도 같은 장비를 지급한다. 소진/폐기 후 자동 재지급 없음.
- WASD/Space/Shift 기본 지상 이동·점프·달리기, 스태미나 최대 100/초당 소모 28/초당 회복 20 및 HUD 복구. 소진 뒤 Shift 재입력으로 달리기 재개. 기존 차량·계단 점프 수정 보존.
- Assets/Editor/PlayerBasicsCheck.cs, 메뉴 Tools/Gameplay/Verify Player Basics: 실제 Play Mode 입력과 게임 함수 기반 17개 검사 통과. Tools/Level/Verify Jump Surfaces: 도로·차량·현관 계단 18/18 통과.
- 검수 이미지: art-review/player-basics-20261005.png. 상세 후속 기록은 NEXT_SESSION.txt 최신 항목을 따른다. 이번 변경은 커밋/푸시하지 않았다.

갱신일: 2026-09-19

## 현재 구현

- Unity `6000.5.10f1`, 핵심 스크립트는 `Assets/Gameplay/MonsterDeliveryPrototype.cs` 하나다.
- 게임은 10개 우체통을 점령·탈환하는 Mailbox Brawl 규칙 검증판이다. `E`를 3초 유지해 중립 또는 상대 우체통만 점령한다.
- 맵은 중앙 사거리와 미국 교외 주택 10채, 인도·가로등·나무로 구성된다. 택배차/리스폰 지점은 동·서·남·북 끝 4곳이다.
- 플레이어와 봇 3명이 각 택배차에서 시작한다. 사망 뒤 3초 리스폰, 적 택배차 안전구역 침입 제한, 리스폰 5초 피해 보호(공격 시 해제)가 있다.
- 무기는 도끼 시작, 망치·검·소총 30발·저격총 5발 공중 드랍이다. 슬롯은 3칸이며 가득 차면 줍지 못한다. `G`는 현재 무기를 영구 폐기한다.
- `Left Shift` 달리기/스태미나, Space 길게 누르기 연속 점프, 경기 종료 점령 수 순위표를 구현했다.
- Unity AI Assistant와 URP 템플릿 의존성을 제거해 반복되던 `NoSubscription`·URP Console 오류가 재시작 후 사라졌다.

## 다음 작업

- 중앙 사거리와 주택가에 주차 차량, 덤스터, 울타리, 나무, 콘크리트 배리어 등 엄폐물을 추가한다.
- Unity Game View에서 점령·아이템 슬롯·총 소진·리스폰 보호·최종 순위표를 한 번씩 직접 확인한다.
- 실제 PvP/네트워크 동기화와 플레이어 간 넉백·기절은 아직 구현하지 않았다.
