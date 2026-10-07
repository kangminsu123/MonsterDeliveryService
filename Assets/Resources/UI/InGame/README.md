# 인게임 UI 이미지

최신 1인칭 UI 시안을 참고해 게임에 사용할 투명 PNG를 부분별로 재제작했다. 글자와 점수는 이미지에 합성하지 않고 실제 상태로 표시한다.

- score_panel: 왼쪽 위 점령 현황
- timer_panel: 상단 시간
- badge_panel / badge_active: 플레이어 점령 수
- minimap_panel: 전체 맵 프레임
- capture_panel: 상호작용·점령 진행
- portrait_panel / robot_portrait: 내 캐릭터
- control_panel: 대쉬·상호작용 안내
- keycap_light / keycap_dark: 키 표시
- bar_track / bar_fill: 스태미나와 점령 진행바
- crosshair: 중앙 조준점
- mailbox_icon / parcel_icon: 실제 게임 모델로 렌더한 대상 아이콘

패널은 2배 해상도와 투명 모서리, Sprite 9-slice border로 저장했다. bar_fill과 배지는 플레이어 색으로 틴트한다. Tools > Gameplay > InGame UI > Build Image Assets에서 재생성할 수 있다.

- map_mailbox / player_arrow: 점령자 색으로 표시하는 미니맵 우체통과 내 방향 표시

- slot_panel: 중앙 하단 1·2·3 아이템 슬롯 배경
- slot_outline: 선택 슬롯에 플레이어 색으로 틴트하는 투명 테두리

- item_*: 기존 DeliveryItemConcepts의 생활용품 이미지10종을 원본 PNG 그대로 복사한 실제 슬롯 아이콘
