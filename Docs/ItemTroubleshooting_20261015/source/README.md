# 보고서 편집 방법

Word 파일을 직접 편집하거나 report_data.json의 문장을 고친 뒤 build_item_report.py를 실행한다. 생성기는 원래 Unity 프로젝트의 art-review와 Assets 근거 파일을 읽으므로 프로젝트 안의 Docs 폴더에서 사용한다. Python 3와 python-docx가 필요하다. 이미지는 images에 포함되어 있으며, 판정 검증 로그는 evidence에서 읽는다. 코드를 바꾸고 검사를 다시 실행한 경우 검사 개수와 촬영 날짜도 함께 갱신한다.
