# 보고서 재생성

report_data.json이 편집 원문입니다. render_report.py를 실행하면 report_template.docx의 서식으로 Word 보고서를 재생성합니다. images 폴더를 함께 보존하세요. 후보 좌표는 parcel-spawn-points.json과 실제 SampleScene의 SpawnPoints에 기록했습니다. 장면을 바꾸려면 Unity에서 후보 Transform을 조정하고 Verify Spawn Layout으로 확인하세요.


사례02는 report_data.json의 case02와 append_ui_network_report.py로 추가한다. render_report.py를 실행하면 사례01에 사례02를 이어 붙인 Word와 사례02 README/HTML이 재생성된다. 사례01 이전 Word는 ChangeReport_before_ui_network.docx로 보존했다. 현재 총7쪽이며 evidence/document-qa-ui-network.json을 참조한다. __pycache__는 전달 자료에서 제외한다.

사례03 맥 패키지는 report_data.json의 case03와 append_mac_report.py로 이어 붙인다. 최종 Word는 사례01·02·03 총8쪽이며 실제 Mac 실행은 미검증으로 표시한다. package_mac.py는 UNIX 실행 권한과 Universal Mach-O, ZIP CRC를 검사하며 evidence/package-verification-macos.json에 결과를 기록한다.
