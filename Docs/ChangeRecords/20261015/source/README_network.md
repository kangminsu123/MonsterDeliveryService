# 보고서 현재 재생성 방법

report_data.json의 network 필드가 사례02의 편집 원문입니다. render_network_report.py를 실행하면 보존된 사례01 DOCX에 사례02를 다시 생성합니다. images 폴더가 함께 필요합니다. build_ice_report.py는 이전 사례01 생성기로 보존한 이력입니다. 현재 보고서 재생성에는 render_network_report.py를 사용하세요.

기본 render_docx.py는 LibreOffice 부재로 실패해 Word 읽기 전용 PDF 출력과 Poppler 이미지 렌더로 대체했습니다. evidence/network_renderer.log 및 report QA를 참조하세요.
