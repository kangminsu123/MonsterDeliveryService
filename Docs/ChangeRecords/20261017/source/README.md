# 보고서 재생성

report_data.json이 문제·원인·방법·결과·요청 대조표의 편집 원문이다.
번들 Python으로 render_report.py를 실행하면 Word, README, CSV, HTML을 갱신한다.
선택 인자로 출력 디렉터리를 주면 Word와 이미지 내장 HTML을 복사한다.
report_template.docx는 20261016 보고서의 기존 스타일이다.
render_word.ps1은 숨김 읽기 전용 Word PDF 출력 및 번들 Poppler 이미지 변환을 수행한다.
기본 render_docx.py는 LibreOffice 부재와 임시 폴더 권한 오류로 실패했다(renderer.log).
최초 Word COM 접근은 샌드박스에서 차단됐으며, 승인된 재실행으로 정상 출력했다.
최종 PDF4쪽과 page-1~4.png가 검수본이다. 모든 페이지를 실제 열어 검수했다.
DeliveryHeldItem_before.cs.txt는 수정 전 HEAD 내용, after는 이번 최종 소스다.
수정 전 메뉴는 당시 코드로 실행한 기록이다. 수정 후 코드에서 다시 실행하면 과거 현상을 재현하지 않는다.
요청 시작일10월17일 폴더를 유지하며, 수정 후 검증과 캡처 실제 날짜10월18일을 본문·로그에 표기했다.
