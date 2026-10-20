from pathlib import Path
import json, shutil, html, hashlib
W=Path('C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx')
R=Path('C:/Users/xlfhf/Documents/Codex/2026-09-18/https-github-com-kangminsu123-monsterdeliveryservice-git/MonsterDeliveryService')
D=R/'Docs/ChangeRecords/20261019'
for name,count in [('broadcaster-offline.txt',17),('broadcaster-special-regression.txt',31),('broadcaster-row-regression.txt',45),('broadcaster-connected/host.txt',9),('broadcaster-connected/client.txt',9)]:
 s=(D/'evidence'/name).read_text(encoding='utf-8-sig');assert s.count('PASS ')==count and 'FAIL ' not in s,name
c={
 'title':'사례06 긴급 배송 방송기 4초 기절과 음파 연출',
 'date':'2026년 10월 19일 · R15, R16 · 수치 조정 및 연출 개선 · 해결 완료',
 'problem':'기절 시간을 4초로 바꾸고 지나치게 간결한 사용 이펙트를 더 화려하게 해 달라는 요청이다. 이전 방송기는 3초 기절과 얇은 수평 원 하나를 사용했다. 수정 전 화면은 없으며 이전 코드와 9쪽 보고서를 source/case06/before에 보존했다.',
 'cause':'공통 사용 판정의 방송기 기절값이 3초였고, SpecialDeliveryPulse가 방송기와 포장기를 함께 처리하면서 방송기에는 원 하나만 그렸다. 겹쳐 퍼지는 음파·입체 파동·경고등·불꽃 표현이 없었다.',
 'method':'공통 BroadcasterStunSeconds를 4로 정의해 로컬과 서버 판정이 함께 사용한다. 전용 BroadcasterFX에는 시간차로 퍼지는 수평 음파 3겹, 수직 반원 파동 2개, 회전하는 중앙 경고 원, 방사형 음파 8줄, 주황색 점멸 광원과 금빛 불꽃 40개를 넣었다. native LineRenderer·ParticleSystem·Light를 사용하며 연출은 1.3초 뒤 객체와 전용 재질을 정리한다.',
 'policy':'12m 안의 보이는 상대에게 적용하며 본인·아군·벽 뒤는 제외한다. 에어캡 방어와 소모, 구명조끼 면역, 기절 후 2초 면역을 유지한다. 사용 횟수는 1회이며 마지막 사용 후 0.6초 반동이 끝나면 슬롯과 모델이 정리된다. 이펙트에는 Collider·Rigidbody가 없어 추가 피격을 만들지 않는다.',
 'network':'기존 서버 권한과 actionAt 전송을 재사용한다. 참가자는 수신한 동작의 경과 시간부터 파동과 불꽃을 재생하고 반복 스냅샷은 중복 생성하지 않는다. 연출이 기절이나 힘을 별도로 적용하지 않는다. 기절 규칙과 연출 버전이 다른 참가자가 섞이지 않게 프로토콜을 12에서 13으로 올렸다.',
 'verification':'Unity 6000.5.10f1 SampleScene 실제 Play에서 방송기 17항목, 기존 특수 3종·택배 31항목, 공원 5종 행 45항목 통과. 별도 두 Editor localhost에 편도 60ms 인위 지연과 20% frame/input 누락을 넣어 호스트 9·참가자 9항목 통과. 양방향 최대 기절값은 모두 4초였으며 해제·소모·면역·연출·중복 방지·정리를 확인했다. 검사 중 런타임 오류 0. 검사군은 중복을 포함하므로 합산하지 않는다.',
 'retry':'이번 실제 게임 검사는 재시도 없이 모두 통과했다. 소스 편집 스크립트에서 기본 문자 인코딩 읽기가 한 번 실패해 UTF-8로 읽도록 고친 뒤 남은 편집을 적용했다. 이는 실행 중 게임 오류가 아니다. 이전 보고서와 코드 사본을 보존했다.',
 'limits':'실제 외부망·4인전·배포본 재빌드는 미실행이다. 참가자 시작 때의 기존 Pipeline DLL·MPPM websocket 환경 오류 2개는 전투 검사 전 오류로 구별했다. 정지 화면은 연출 확인용이고 판정과 시간은 실행 로그로 확인했다. 공기 대포는 기존 직선 60m/s·반경 6m·본인 발밑 상승을 유지한다.'
}
p=D/'source/report_data.json';data=json.loads(p.read_text(encoding='utf-8'));assert 'case06' not in data
data['case06']=c
data['intro']='최신 사례06에서는 긴급 배송 방송기를 4초 기절과 다층 음파·경고등·불꽃 연출로 변경하고 실제 두 Editor에서 확인했다. 현재 프로토콜은 13이다. 공기 대포는 직선 60m/s·폭발 반경 6m와 물체 충돌 폭발 연출을 유지한다. 아래 사례01~05의 수치와 미적용 설명은 각 작성 당시 이력이다.'
p.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
append='''from docx.shared import Inches

def append_case06(doc,D,c):
 doc.add_page_break();doc.add_heading(c['title'],1);doc.add_paragraph(c['date'])
 for label,key in [('요청','problem'),('원인','cause'),('수정 방법','method'),('판정 정책','policy'),('멀티 동기화','network'),('검증 결과','verification'),('재시도','retry'),('확인 범위','limits')]:
  p=doc.add_paragraph();p.add_run(label+'  ').bold=True;p.add_run(c[key])
 doc.add_paragraph('검증 원문 evidence/broadcaster-offline.txt · broadcaster-special-regression.txt · broadcaster-row-regression.txt · broadcaster-connected/host.txt 및 client.txt')
 doc.add_page_break();doc.add_heading('실제 방송기 사용과 참가자 화면',1)
 doc.add_paragraph('2026년 10월 19일 실제 연결된 두 Unity Editor에서 촬영했다. 검수용 위치와 카메라를 사용했다. 원격 참가자 화면에는 동일한 방송 동작의 경과 시간을 적용한다.')
 for filename,caption in [('20261019_broadcaster_host_connected.png','사용자 시점. 여러 겹의 따뜻한 음파와 금빛 불꽃이 퍼지고, 맞은 상대 머리 위에 기절 표시가 나타난다. 연출은 1.3초, 기절은 4초로 서로 구분한다.'),('20261019_broadcaster_client_connected.png','별도 참가자의 검수용 외부 카메라. 수평 음파 3겹·수직 반원 2개·중앙 경고 원·방사형 음파가 보인다. 파동은 최대 12m까지 확장한다. 광원과 불꽃의 작동·소멸은 실행 로그로도 확인했다.')]:
  doc.add_paragraph().add_run().add_picture(str(D/'images'/filename),width=Inches(6.35))
  doc.add_paragraph(caption,'Caption')
'''
(D/'source/append_broadcaster_report.py').write_text(append,encoding='utf-8')
p=D/'source/render_report.py';s=p.read_text(encoding='utf-8');needle="append_case05(doc,D,data['case05'])";assert needle in s
s=s.replace(needle,needle+"\nfrom append_broadcaster_report import append_case06\nappend_case06(doc,D,data['case06'])")
p.write_text(s,encoding='utf-8')
sections=[('문제와 기대 동작','problem'),('원인','cause'),('수정 방법','method'),('판정 정책','policy'),('멀티 동기화','network'),('검증 결과','verification'),('재시도','retry'),('확인 범위','limits')]
md='\n\n## '+c['title']+'\n\n'+c['date']+'\n'+''.join('\n### '+label+'\n\n'+c[key]+'\n' for label,key in sections)
for filename,caption in [('20261019_broadcaster_host_connected.png','실제 사용자의 카메라'),('20261019_broadcaster_client_connected.png','실제 참가자의 검수용 외부 카메라')]:md+='\n!['+caption+'](images/'+filename+')\n\n'+caption+'.\n'
md+='\n[방송기 17항목](evidence/broadcaster-offline.txt) · [기존 31항목](evidence/broadcaster-special-regression.txt) · [공원 행 45항목](evidence/broadcaster-row-regression.txt) · [호스트](evidence/broadcaster-connected/host.txt) · [참가자](evidence/broadcaster-connected/client.txt). 메뉴 Tools/Gameplay/Special Delivery/Verify Broadcaster 및 Verify Broadcaster Host와 Client.\n'
p=D/'README.md';s=p.read_text(encoding='utf-8');p.write_text('최신 사례06: 방송기 4초 기절·다층 음파·경고등·불꽃. 프로토콜13. 이전 3초와 프로토콜12 이하는 당시 이력이다.\n\n'+s+md,encoding='utf-8')
p=D/'index.html';s=p.read_text(encoding='utf-8');part='<section><h1>'+html.escape(c['title'])+'</h1><p>'+html.escape(c['date'])+'</p>'
part+=''.join('<h2>'+label+'</h2><p>'+html.escape(c[key])+'</p>' for label,key in sections)
for filename in ['20261019_broadcaster_host_connected.png','20261019_broadcaster_client_connected.png']:part+='<img src="images/'+filename+'" alt="실제 Unity 방송기 연출">'
part+='<p>검수용 위치·카메라로 촬영한 실제 두 Editor 화면. 시간과 판정은 실행 로그 참조.</p><a href="evidence/broadcaster-connected/host.txt">호스트 검사</a> · <a href="evidence/broadcaster-connected/client.txt">참가자 검사</a></section>'
p.write_text('<meta charset="utf-8"><p><b>최신 사례06: 방송기 기절4초·다층 음파·경고등·불꽃. 프로토콜13. 아래 사례01~05는 당시 이력이다.</b></p>'+s+part,encoding='utf-8')
p=D/'requests.csv';s=p.read_text(encoding='utf-8-sig');assert 'R15,' not in s;p.write_text(s.rstrip()+'\nR15,긴급 배송 방송기 기절,기절 시간을4초로 변경,사례06,공통 판정 및 실제 양방향 멀티4초 확인\nR16,긴급 배송 방송기 이펙트,간결한 기존 연출을 더 화려하게 변경,사례06,다층 음파 경고등 불꽃 및 실제 양쪽 화면 확인\n',encoding='utf-8-sig')
rootnote='## 방송기 4초와 음파 연출 강화 2026년 10월 19일 사례06\n\n'+c['method']+'\n\n'+c['policy']+'\n\n'+c['verification']+'\n\n현재 프로토콜13. 공기 대포는 기존 6m 직선 버전을 유지한다. 아래 이전 수치와 프로토콜은 당시 이력이다. [변경 기록](Docs/ChangeRecords/20261019/README.md).\n\n'
for name in ['GAME_DESIGN.md','TEAM_STATUS.md','ART_REVIEW.md']:
 p=R/name;s=p.read_text(encoding='utf-8-sig');first,rest=s.split('\n',1);p.write_text(first+'\n\n'+rootnote+rest.lstrip(),encoding='utf-8')
p=R/'NEXT_SESSION.txt';s=p.read_text(encoding='utf-8-sig');p.write_text('최신 2026-10-19 사례06: 방송기 기절4초와 다층 음파·경고등·불꽃 완료. 프로토콜13. Play17·기존31/45·실제 호스트9/참가자9 통과, 검사 중 오류0. 외부망·4인전·배포본 재빌드는 미실행. 기록 Docs/ChangeRecords/20261019/. 이전3초는 당시 이력.\n\n'+s,encoding='utf-8')
after=D/'source/case06/after';after.mkdir(parents=True,exist_ok=True)
files=['Assets/Gameplay/BroadcasterFX.cs','Assets/Gameplay/SpecialDeliveryItems.cs','Assets/Gameplay/DeliveryNetwork.cs','Assets/Editor/BroadcasterCheck.cs','Assets/Editor/BroadcasterNetworkCheck.cs','Assets/Editor/SpecialDeliveryCheck.cs','Assets/Editor/SpecialDeliveryNetworkCheck.cs']
hashes={}
for rel in files:
 shutil.copy2(R/rel,after/Path(rel).name);hashes[rel]=hashlib.sha256((R/rel).read_bytes()).hexdigest()
(D/'evidence/broadcaster-source-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
shutil.copy2(__file__,D/'source/record_broadcaster.py')
print('Recorded case06 and source snapshots; render canonical report next.')
