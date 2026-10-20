from pathlib import Path
import json,html,shutil
from docx import Document
from docx.shared import Inches
R=Path('C:/Users/xlfhf/Documents/Codex/2026-09-18/https-github-com-kangminsu123-monsterdeliveryservice-git/MonsterDeliveryService')
D=R/'Docs/ChangeRecords/20261020'
data={
'title':'자동 획득 안내 제거와 취급주의 택배 반짝임',
'problem':'일반 택배의 닿으면 자동 획득 글을 없애고 취급주의 택배에 빛나는 효과를 넣어 달라는 후속 요청이다. 일반 택배에는 앞선 사례07에서 새 안내 패널을 표시했고, 기존 취급주의 효과는 바닥 링과 약한 광원뿐이었다.',
'method':'일반 택배는 빈 슬롯이 있을 때 상호작용 패널 전체를 숨기고 자동 획득 안내 문자열을 제거했다. 슬롯 가득 참과 G 버리기 안내는 유지한다. 취급주의 택배는 기존 ParkDeliveryGlow를 재사용해 금빛 링 두께와 밝기를 높이고, 여섯 개의 십자 반짝임이 서로 다른 높이에서 천천히 돌고 밝아졌다 어두워지도록 했다. 작은 점광원도 따뜻한 금빛으로 맥동한다. 탐색·로컬·서버·스냅샷 모두 기존 가용 상태에 연결되며 E 수집 후 즉시 숨기고 재등장 시 복구한다. 고정 아이템 5종에는 추가하지 않는다. 획득 및 출현 규칙과 프로토콜15는 그대로다.',
'verification':'2026-10-20 Unity6000.5.10f1 SampleScene 실제 Play에서 ParkDeliveryCheck 33항목 모두 통과했다. 링·반짝임·광원, 실제 시간에 따른 반짝임 이동, 이펙트의 추가 충돌체 없음, E 수집·숨김·다음 출현 복구, NGO 호스트 판정과 JSON 스냅샷 경로를 검사했다. ParcelTouchCheck도 안내 문자열 없음과 자동 획득·취급주의 E 유지 등 19항목을 통과했다. 최종 Console 오류0이며 검사 후 Play를 종료했다. 두 검사군은 별도이며 고유 사례 수로 합산하지 않는다.',
'limits':'별도 두 클라이언트 연결과 WAN·배포본 재빌드는 이번 미실행이다. 실제 Unity 캡처는 검사 도구로 시계와 카메라 위치를 설정한 재현 화면이다. 기존 LibreOffice 미설치로 Word 렌더와 페이지 화면 검수는 미완료이며 README·HTML·구조화 원문·로그를 우선 저장했다.',
'retry':'첫 실행 중 스크립트 재로드 시 readonly 배열에 있던 런타임 반짝임 참조가 복원되지 않아 NullReferenceException이 발생했다. 재로드 시 참조를 보존할 수 있는 일반 필드 배열로 바꾸고 재실행했다. 최종 관련33항목·자동획득19항목 통과와 Console오류0을 확인했다.'}
(D/'source/glow_report_data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')
md='\n\n## 사례08 '+data['title']+'\n\n작성 날짜 2026-10-20 · 요청 R22 R23 · Editor 확인 완료\n'
for label,key in [('문제와 원인','problem'),('수정 방법','method'),('검증 결과','verification'),('확인 범위','limits'),('재시도','retry')]:md+='\n### '+label+'\n\n'+data[key]+'\n'
md+='\n![취급주의 금빛 링과 반짝임](images/20261020_fragile_sparkle_notice.png)\n\n실제 Unity 검수용 재현 화면. [33항목 검사 로그](evidence/park-delivery-glow-play.txt) · [19항목 자동획득 검사](evidence/parcel-touch-glow-followup.txt)\n'
p=D/'README.md';p.write_text(p.read_text(encoding='utf8')+md,encoding='utf8')
p=D/'requests.csv';p.write_text(p.read_text(encoding='utf-8-sig')+'R22,일반 택배 안내,자동 획득 안내 글 제거,사례08,Editor 확인 완료\nR23,취급주의 택배,빛나는 이펙트 추가,사례08,Editor 33항목 확인 완료\n',encoding='utf-8-sig')
section='<section><h2>사례08 '+data['title']+'</h2>'+''.join('<p>'+html.escape(data[k])+'</p>' for k in ['problem','method','verification','limits','retry'])+'<img src="images/20261020_fragile_sparkle_notice.png" alt="실제 Unity 검수 화면"></section>'
p=D/'index.html';p.write_text(p.read_text(encoding='utf8')+section,encoding='utf8')
shutil.copy2(D/'evidence/parcel-touch-play.txt',D/'evidence/parcel-touch-glow-followup.txt')
shutil.copy2(D/'ChangeReport_20261020.docx',D/'source/glow_before_report.docx')
doc=Document(D/'ChangeReport_20261020.docx');doc.add_page_break();doc.add_heading(data['title'],1)
for label,key in [('요청과 변경','problem'),('수정 방법','method'),('검증 결과','verification'),('확인 범위','limits')]:doc.add_heading(label,2);doc.add_paragraph(data[key])
doc.add_paragraph(data['retry']);doc.add_picture(str(D/'images/20261020_fragile_sparkle_glow.png'),width=Inches(5.5));doc.add_paragraph('실제 Unity 카메라를 검수 위치에 배치한 취급주의 택배 화면')
doc.save(D/'ChangeReport_20261020.docx')
note='## 자동 획득 안내 제거와 취급주의 반짝임 2026년10월20일\n\n일반택배 자동획득 안내창 제거·가득참 안내 유지. 취급주의 금빛 링·회전하는 반짝임6개·맥동 광원 강화, E수집 유지. ParkDeliveryCheck33항목·ParcelTouchCheck19항목 통과·최종Console0. 실제 캡처 Docs/ChangeRecords/20261020/images/20261020_fragile_sparkle_notice.png. 사례08. 별도 실제 참가자·배포본 재빌드 미실행. Word 본문 갱신·LibreOffice 미설치로 화면검수 미완료.\n\n'
for f in ['NEXT_SESSION.txt','GAME_DESIGN.md','TEAM_STATUS.md','ART_REVIEW.md']:
 p=R/f;p.write_text(note+p.read_text(encoding='utf8'),encoding='utf8')
snapshot=D/'source/glow-after';snapshot.mkdir(exist_ok=True)
for f in ['Assets/Gameplay/ParkDeliveryGlow.cs','Assets/Gameplay/SpecialDeliveryParcel.cs','Assets/Gameplay/MonsterDeliveryHUD.cs','Assets/Editor/ParcelTouchCheck.cs','Assets/Editor/ParkDeliveryCheck.cs','Assets/Editor/ParkDeliveryNetworkCheck.cs']:shutil.copy2(R/f,snapshot/Path(f).name)
shutil.copy2(__file__,D/'source/glow-record.py')
print('Recorded case08; Word visual QA remains pending due to missing LibreOffice')
