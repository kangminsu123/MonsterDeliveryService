# 택배 무작위 배치와 재생성 변경 기록

2026-10-16 · 사례 01 · R01~R04 · 요구 변경 · 구현 및 개발 환경 검증 완료

이전 기록: [2026-10-15 멀티플레이 사례02](../20261015/README.md)

## 결과

택배 후보 위치를 50곳으로 지정하고 경기 시작 때 중복 없는 30곳을 무작위로 선택한다. 한 개를 먹으면 잠시 29개가 된 뒤 같은 프레임의 LateUpdate에서 다른 빈 위치에 보충해 30개를 유지한다. 바깥 풀밭을 포함한 GrassBase 표면은 후보에서 제외했다. 새 Windows 실행 파일의 1대1과 2대2에서 재생성 위치 동기화까지 확인했다.

## 문제와 원인

기존에는 SampleScene에 택배 10개가 저장되어 있었고, FloatingParcel.TryCollect는 획득한 오브젝트를 비활성화하는 것으로 끝났다. 경기 재시작 시 10개를 다시 켰지만, 경기 중 다른 위치에 보충하는 처리는 없었다. 기존 배치 도구도 풀밭을 허용했다. 이번 요청은 후보 약 50곳·활성 30개·획득 후 빈 위치 보충·바깥 풀밭 제외다.

## 구현

ParcelSpawnPool에 저장된 Transform 후보 50개와 기존 프리팹으로 만든 택배 30개를 연결했다. 시작 배치는 부분 Fisher–Yates 추첨으로 중복을 막는다. 획득 즉시 슬롯을 비우고 오브젝트·상호작용을 끈다. LateUpdate에서 사용 중인 슬롯과 방금 먹은 슬롯을 제외한 후보를 뽑아 같은 오브젝트를 재사용한다. 별도 리스폰 지연은 추가하지 않았다. 여러 플레이어의 같은 프레임 획득도 모아서 보충한다.

후보는 도로·보도 40곳과 기존 계단으로 접근 가능한 평평한 옥상 10곳이다. 풀밭·나무·장애물·기울어진 지붕·계단 없는 지붕을 거르고, 택배 발판의 아홉 지점 지지와 상자/플레이어 공간 및 후보 간 최소 4m를 검사했다. 좌표는 장면의 Layer_12_TestParcelDrops/SpawnPoints에 저장되어 직접 조정할 수 있다.

위치 추첨과 아이템 지급은 호스트가 처리한다. 스냅샷의 기존 택배 활성 여부 배열을 30개 모델 각각의 후보 번호 배열로 바꿔, 클라이언트가 같은 위치·활성 상태를 적용한다. 중복되거나 범위를 벗어난 배열은 전체 적용 전에 거부한다. 초기화와 재경기에는 30곳을 다시 추첨한다. 메시지 형식이 바뀌어 NGO ProtocolVersion을 2로 올렸으므로 모든 참가자가 이번 새 실행 파일을 사용해야 한다.

| 항목 | 최종값 |
|---|---|
| 저장 후보 | 50곳 / 도로·보도 40곳 + 접근 가능한 평평한 옥상 10곳 |
| 경기 시작 | 중복 없는 무작위 30곳에 활성 택배 30개 |
| 획득 후 | 29개 → 같은 프레임 LateUpdate에서 다른 빈 위치에 보충 → 30개 |
| 금지 위치 | GrassBase 풀밭 전체 / 장애물·지원 없는 발판·접근 불가 지붕 |
| 배치 안전 | 후보 간 최소 4m / 발판 아홉 점과 여유 공간 검사 |
| 멀티플레이 | 호스트가 위치 결정 / 모든 클라이언트에 같은 후보 번호 전달 |

## 실제 화면

상공 화면은 실제 Unity Game View에 후보 번호와 점유 상태를 표시한 검수용 재현이다. 번호·상태 색은 검수용이며 일반 게임에는 나오지 않는다. 활성은 청록, 빈 후보는 노랑, 획득 위치는 빨강, 새 위치는 초록이다. 2026-10-16 촬영했다. 도로와 옥상 근접 화면도 같은 날 네이티브 카메라로 촬영했다.

![50개 후보와 활성 30개의 실제 상공 검수 화면](images/20261016_parcels_50_points_30_live.png)

50개 후보와 활성 30개의 실제 상공 검수 화면

![획득 위치는 빨강 새 보충 위치는 초록으로 표시한 실제 재생성](images/20261016_parcels_after_refill.png)

획득 위치는 빨강 새 보충 위치는 초록으로 표시한 실제 재생성

![도로의 실제 부유 택배와 보호막](images/20261016_parcel_ground.png)

도로의 실제 부유 택배와 보호막

![접근 가능한 평평한 옥상의 실제 부유 택배](images/20261016_parcel_roof.png)

접근 가능한 평평한 옥상의 실제 부유 택배

## 실행 검증

2026-10-16 Unity 6000.5.10f1 SampleScene에서 배치 51개, 풀 동작 19개, 기존 랜덤 획득 48개, 기존 상호작용 43개 검사를 실행했다. 풀 검사 안에는 200회 새 판과 1,000회 실제 TryCollect/Refill 반복, 5개/30개 동시 획득, 중복 지급 방지, 모델 재사용 및 실제 LateUpdate 자동 보충이 포함된다. 반복 횟수를 독립 검사 개수로 합산하지 않는다. 최종 EXE 실제 2프로세스는 호스트 17개/클라이언트 5개, 4프로세스는 호스트 18개/클라이언트 각각 5개가 통과했다.

- 후보 배치 검사: 51개 통과. [로그](evidence/parcel-layout-check.txt)

- 리스폰 풀 검사: 19개 통과. [로그](evidence/parcel-respawn-check.txt)

- 랜덤 아이템 획득 회귀: 48개 통과. [로그](evidence/parcel-loot-check.txt)

- 부유·E 상호작용 회귀: 43개 통과. [로그](evidence/floating-interaction-check.txt)

- 최종 EXE 1대1: 호스트 17 / 클라이언트 5 통과. [로그](evidence/network/1v1/host-checks.txt)

- 최종 EXE 2대2: 호스트 18 / 클라이언트 각 5 통과. [로그](evidence/network/2v2/host-checks.txt)

- 실행 메뉴: Tools/Gameplay/Test Parcels/Build 50 Spawn Points, Verify Spawn Layout, Verify Respawn Pool, Verify Random Loot, Verify Floating Interaction.

- 네트워크: 최종 EXE에 -delivery-host [-delivery-mode 1v1] / -delivery-join 127.0.0.1 / -delivery-proof 인자를 전달해 실제 프로세스를 연결했다. 입력·위치·점령 및 종료 시간 일부를 검수용으로 설정한 자동 시나리오다.

## 재시도

새 스크립트 추가 직후에는 에셋 강제 새로 고침 전 타입을 찾지 못하는 컴파일 메시지가 있었고 전체 Refresh 후 해소됐다. 기존 획득 검사는 공개 NetworkScreens 필드를 찾지 못하던 검사 플래그와 고정 접근 방향/예전 카메라 높이 때문에 실패했다. 검사 도구를 현재 필드와 실제 눈높이·장애물 없는 접근 방향에 맞춰 수정한 뒤 48개/43개가 통과했다. 첫 상공 캡처는 장착 아이스크림 카메라가 일부 후보를 가려, 검수 중 장착 카메라를 숨기고 다시 촬영했다.

## 빌드와 제한

Windows x64 Mono Development LZ4 빌드 성공, 약 263.32MB·23.40초. 기존 com.unity.pipeline의 CodeAnalysis Unsafe DLL 경로 예외 1개와 기존 Minimap 검색 API 경고 2개는 빌드 진단에 남아 있다. 게임 코드 컴파일과 실제 배포 EXE 실행 통과와 구분해 기록한다. 동일 PC의 다중 프로세스 검증이며 서로 다른 물리 PC·Tailscale/WAN 연결은 이번 검증에 포함하지 않았다.

## 수정 파일

- Assets/Gameplay/ParcelSpawnPool.cs

- Assets/Gameplay/FloatingParcel.cs

- Assets/Gameplay/MonsterDeliveryPrototype.cs

- Assets/Gameplay/DeliveryNetwork.cs

- Assets/Gameplay/DeliveryNetworkProof.cs

- Assets/Editor/ParcelSpawnSetup.cs

- Assets/Editor/ParcelSpawnCheck.cs

- Assets/Editor/TestParcelDrops.cs

- Assets/Editor/ParcelLootCheck.cs

- Assets/Editor/FloatingParcelCheck.cs

- Assets/Scenes/SampleScene.unity

## 전달본

새 실행 패키지는 MonsterDeliveryService_20261016.zip이다. 전체 압축 해제 후 실행하며, 참가자 전원 같은 새 버전을 사용한다. Tailscale 접속 방법은 기존과 같다.

[Word 보고서](ChangeReport_20261016.docx) · [열람본](index.html) · [요청 대조표](requests.csv)

## 통합 인수인계와 커밋 정리

2026-10-16 후속 요청에 따라 [NEXT_SESSION.txt](../../../NEXT_SESSION.txt) 맨 위에 현재 9종 아이템, 1대1/2대2, 택배50곳/활성30개, 실행 검수 근거, 최신 전달물의 실제 경로, 직접 IPv4/Tailscale 테스트 계획, 미구현 범위를 통합했다. TEAM_STATUS에도 이 진입점을 연결했다. 지금까지 누적된 구현·에셋·검사·캡처·보고서를 로컬 커밋 대상으로 정리했으며 원격 푸시는 요청받지 않았다. 이 후속 변경은 문서만 수정하므로 가이드에 따라 Word 재출력과 게임 재검사는 생략하고 링크·파일 경로·Git 변경 범위를 확인했다.
