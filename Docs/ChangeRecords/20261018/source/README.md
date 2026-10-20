# 편집과 재생성

report_data.json이 문제 원인 방법 결과 및 요청 대조표의 원문이다.
번들 Python으로 render_report.py를 실행해 README CSV HTML Word를 재생성한다.
출력 디렉터리를 선택 인자로 주면 BoxingPunchFix.docx와 이미지 내장 HTML을 복사한다.
기존10월16일 보고서의 report_template.docx 스타일을 재사용했다.
render_docx.py는 LibreOffice 부재와 임시폴더 권한오류로 실패했다(renderer.log).
승인된 render_word.ps1의 숨김 읽기 전용 Word 출력과 번들 Poppler로 최종4쪽을 렌더했다.
page-1~4.png 전부 열어 검수했으며 PDF는 검수 중간 산출물이다.
게임 소스의 before는 이번 요청 시작 시점이고 after는 최종본이다. 기존 미커밋 변경을 포함하므로 evidence/punch-session.diff가 이번 차이만 보여준다.
before 검사 메뉴는 수정 전 소스에서 실행한 기록이다. 수정 후 코드로는 과거 현상이 재현되지 않는다.
검사 메뉴는 장비·위치·상태를 설정하므로 새 Play 세션에서 실행하고 Stop으로 종료한다.
실제 네트워크 검사도 물리적으로 다른PC 검증이 아니라 동일PC의 MPPM 별도 프로세스 localhost 연결이다.

사례02는 report_data.json의 case02 및 append_throw_report.py로 재생성한다. 출력 복사본은 ThrowMotionFix.docx와 ThrowMotionFix.html이다. case02/에 기존 사례01 보고서와 투척 작업 시작 전후 소스, evidence/throw-session.diff에 이번 차이만 보존한다. evidence/boxing-regression-after-throw.txt는 투척 구현 후 복싱25항목 재실행이며 이전 사례01 캡처는 저장된 원본 DOCX에서 복원했다. render_word_throws.ps1은 전체 추가 보고서를 evidence/render-throws에 렌더한다.
punch-after.txt도 회귀 검사 실행 당시 시각을 가진 최신25항목 로그다. 이전 수치와 설명은 보존된 ChangeReport_case01.docx 및 최초 실제 연결 로그에서 확인한다. 사례01의 기존 캡처는 원본 보고서에서 동일 바이트로 복원했다.

사례03 원문은 report_data.json의 case03, 출력은 append_projectile_report.py와 render_report.py를 재실행한다. 최신 복사본은 MultiplayerProjectileFix.docx 및 이미지 내장 HTML이다. source/case03에 이전 사례02 보고서와 소스 변경 전후, evidence/projectile-session.diff에 이번 차이를 보존했다. 기존 투척98항목 로그는 원본을 복구하고 이번 재실행만 throw-regression-after-projectile.txt로 분리했다. render_word_projectiles.ps1은 evidence/render-projectiles에 검수 페이지를 만든다. 그래프는30Hz 검수용 샘플 입력의 측정 CSV를 사용하며 실제 게임 화면을 대신하지 않는다.

사례04는 report_data.json의 case04와 append_sync_report.py로 재생성한다. 현재 복사본은 MultiplayerSyncAudit.docx 및 이미지 내장 HTML이다. source/case04는 직전12쪽 보고서와 게임 코드 변경 전후를 보존한다. regression-originals에 이전 검사 원본을 두고 이번 재실행은 *-regression-after-sync.txt로 분리했다. sync-comparison.json은 원본CSV와 JSON에서 계산했다. analyze_sync.py의 R 경로는 현재 프로젝트 루트이며 다른 위치에서 재생성할 때 맞춰야 한다. 마이크로벤치 변동과 할당 카운터 미지원은 지표 해석을 참고한다. renderer-sync.log의 기본 렌더러 실패 후 숨김 Word 읽기 전용 출력과 Poppler로 전체 검수했다.

The before/after connected CSV ping fields predate final server-ID RTT lookup correction; do not use their ping values as an improvement comparison. Final movement logs contain corrected RTT lookup.

사례05는 report_data.json case05와 append_empty_start_report.py를 render_report.py에서 호출해 재생성한다. 현재 사용자 사본은 EmptyStartUpdate.docx/html이다. source/case05에 직전16쪽 보고서 및 변경 전후 코드, 첫 HUD 없는 촬영 원본을 보존했다. empty-start-session.diff는 이번 요청만의 게임 코드 차이이며 검사13항목은 네 상태와 참가자 수신 재현으로 실제 네 연결을 뜻하지 않는다. render_word_empty_start.ps1로17쪽을 확인했고 기존16쪽은 직전 렌더와 바이트 동일하다.

사례06 현행 사용량은 item_usage_data.json을 render_item_usage.py로 재생성한다. CurrentItemUsage.docx/html/md가 사용자 사본이며 현재9종만 표에 담는다. 누적 사례06은 report_data.json/append_usage_report.py/render_report.py를 사용하고 ItemUsageChangeRecord.docx/html로 복사한다. render_usage_word.ps1로 두 문서를 검수했다. 원래17쪽은 source/case06/ChangeReport_case05.docx에 보존했다. 빈손 회귀 검사 후 그 문서의 마지막 두 그림을 원래 이미지 파일로 복구해 이전 캡처를 보존했고 새 로그만 empty-start-regression-after-usage.txt로 구분했다. empty-start-check.txt는 최신 재실행13항목이며 첫 검사 원본은 별도 보존되어 있다. 기존 무제한 전용 검사군은 유한 사용량196항목의 실행 결과로 대체해 읽는다.

사례07: 현행 item_usage_data.json/render_item_usage.py는2/2/1과100/50·네 코너 규칙을 포함한다. 누적 report_data.json/append_round_layout_report.py/render_report.py는 사례07을20~22쪽에 추가한다. render_round_word.ps1로 렌더 후 현행2쪽과 새3쪽을 육안 확인했고 누적 앞19쪽은 기존 render-usage-changes와 바이트 동일했다. case07에 변경 전 코드·장면과 최종 소스·장면 해시를 보존했다. 삭제한 이전 전용 문서5개의 목록과 해시는 evidence/retired-item-documents.json에 있다. 이전 사례의 사용량·배치 수치는 당시 기록이며 현행은 사례07이 우선한다.

사례08 재점령 보호: report_data.json/append_capture_protection_report.py/render_report.py가 누적23~24쪽과 HTML/Markdown을 재생성한다. 현행 item_usage_data.json에는 재점령 제한과 프로토콜6을 반영하되 사용량9종은 유지한다. render_capture_word.ps1로 렌더했고 LibreOffice 기본 렌더 실패 로그를 evidence/renderer-capture-*에 보존했다. 원래22쪽은 case08/ChangeReport_case07.docx 및 render-round-changes에서 확인 가능하다. 기절·빙결 면역5초는 제안이며 실제2초다. source/case08 요청과 실제 변경 전후가 우선하며 work/finalize_capture_lock.py는 최초 추가용으로 반복 실행하면 안 된다.
