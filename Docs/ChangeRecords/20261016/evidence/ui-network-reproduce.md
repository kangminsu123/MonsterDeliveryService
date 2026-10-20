# 2026-10-16 UI와 참가자 지연 검수 재현

최종 게임은 MonsterDeliveryService_20261016_UI_NetworkFix. 모든 프로세스가 같은 새 빌드를 사용한다. 게임 창은 일반 창이며 비교는 -force-d3d11과960x540/목표60fps로 통제했다. 검수용 옵션은 개발 빌드에서만 동작한다.

## 지연 비교

방장: MonsterDeliveryService.exe -force-d3d11 -delivery-host -delivery-mode 1v1 -delivery-name Host -delivery-latency-proof after -delivery-latency-dir <절대결과폴더> -logFile <절대호스트로그>

참가자: MonsterDeliveryService.exe -force-d3d11 -delivery-join 127.0.0.1 -delivery-name Guest -delivery-latency-proof after -delivery-latency-dir <같은절대결과폴더> -logFile <절대클라이언트로그>

라운드 시작1초 뒤 약1.8초 전진하고,7초 측정 뒤 결과를 저장/자동 종료한다. 입력과 프레임 송신을각60ms 늦춘다. -delivery-latency-proof after-loss는 매5번째 송신 앱 메시지를 누락한다. 실제 UTP 패킷 손실이나 하드웨어 input-to-photon 측정이 아니다.

수정 전 결과 before-visible은 이전 서버 응답 대기 방식의 별도 빌드로 같은 시나리오를 실행했다. 원본 코드는 source/before_latency_fix, 결과는 before-visible/*.csv 및 JSON/PNG. 검은 캡처 before와 첫 순번 실패 after-visible은 최종 수치에서 제외하고 보존한다.

## 실제 두 모드 회귀

위 실행 명령에서 latency 옵션을 -delivery-proof -delivery-proof-dir <절대결과폴더>로 바꾼다.1대1 방장은 -delivery-mode 1v1,2대2는 -delivery-mode 2v2로 실행하고 참가자1개 또는3개를 연결한다. 자동 이동/대쉬·투척/점령·종료 시간은 검수용이다. 최종 network-ui-fix/1v1 및2v2의 checks는 모두PASS다.

## Editor

SampleScene Play에서 Tools/Gameplay/Network/Verify Prediction and HUD:18개.
Tools/Gameplay/Test Parcels/Verify Respawn Pool:19개. 검사 후 메인 메뉴 복원/Edit Mode 복귀.

## 전달본

Word7쪽 전체를 직접 검수했다. render_docx.py는 LibreOffice 부재/임시폴더 정리 권한으로 실패해 Word 읽기전용 PDF 내보내기와 번들 Poppler로 확인했다. 프레임별 CSV의 이동 그래프는 ReportLab LinePlot으로 생성했다. 실제 게임 캡처와 그래프를 구분한다. 게임 코드 변경 뒤 전체 빌드, 실제2개/4개 프로세스 재검수, 패키지ZIP CRC를 확인했다.
