from pathlib import Path
import difflib,json,re,shutil,subprocess,sys
R=Path(r'C:/Users/xlfhf/Documents/Codex/2026-09-18/https-github-com-kangminsu123-monsterdeliveryservice-git/MonsterDeliveryService')
D=R/'Docs/ChangeRecords/20261018';S=D/'source';C=S/'case08';O=Path(r'C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/outputs')
text=(D/'evidence/capture-protection-check.txt').read_text(encoding='utf-8')
assert len(re.findall('^PASS ',text,re.M))==34 and not re.findall('^FAIL ',text,re.M)
files=['Assets/Gameplay/MonsterDeliveryPrototype.cs','Assets/Gameplay/MonsterDeliveryHUD.cs','Assets/Gameplay/DeliveryNetwork.cs','Assets/Gameplay/DeliveryNetworkGameplay.cs','Assets/Gameplay/ExplorationRival.cs','Assets/Editor/ItemUsageCheck.cs','Assets/Editor/CornerSpawnCheck.cs','Assets/Editor/CaptureProtectionCheck.cs']
diff=[]
for name in files:
 p=R/name;before=C/(p.name+'.before.txt');old=before.read_text(encoding='utf-8-sig') if before.exists() else ''
 after=p.read_text(encoding='utf-8-sig');(C/(p.name+'.after.txt')).write_text(after,encoding='utf-8')
 diff.extend(difflib.unified_diff(old.splitlines(True),after.splitlines(True),fromfile=name+' before',tofile=name+' after'))
(D/'evidence/capture-protection-session.diff').write_text(''.join(diff),encoding='utf-8')
(D/'evidence/capture-protection-verification.json').write_text(json.dumps({'date':'2026-10-18','pass':34,'fail':0,'duration':20,'protocol':6,'stun_freeze_immunity_seconds':2,'suggested_immunity_seconds_not_applied':5,'scope':'one Editor real local/shared/bot and host paths; controlled connected records and serialized participant frames; no separate peer/WAN rerun','captures':'authored mailbox and actual DrawGameplayHUD, controlled review camera/owner/timer state','retry':'initial menu call preceded asset import; force refresh resolved missing menu, no logic failure','main_console_errors':0,'build':'source only; standalone packages unchanged'},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

c=json.loads((S/'item_usage_data.json').read_text(encoding='utf-8'))
for rule in c['rules']:
 if rule[0]=='オンライン適用':raise AssertionError('unexpected source language')
 if rule[0]=='온라인 적용':rule[1]=rule[1].replace('프로토콜5','프로토콜6')
 if rule[0]=='강화와 장갑':rule[1]='러닝화·에어캡은 발동 시 소모되고 효과는 유지된다. 작업용 장갑은 점령 성공1회 뒤 소진된다. 점령 후20초는 재점령이 막혀 진행과 장갑 소모가 없으며, 바라보면 남은 초를 표시한다.'
c['verification']='기존 사례07의 사용량154·배치101·재생성22·코너4·시작28항목은 당시 기록이다. 이번 재점령20초와 표시·장갑·봇·서버/참가자 재현34항목을 통과했다. 기절·빙결 면역은 기존2초로 유지하고5초는 제안이다. 온라인은 한 Editor 서버 상태/직렬화 프레임 검수이며 별도 프로세스·WAN 재실행과 배포 재빌드는 미실행이다.'
(S/'item_usage_data.json').write_text(json.dumps(c,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
data=json.loads((S/'report_data.json').read_text(encoding='utf-8'))
assert 'case08' not in data
data['requests'] += [['R20','재점령','점령 후20초 동안 재점령 제한','사례08','완료 실제 경로34항목'],['R21','우체통 안내','바라보면 N초 후 점령 가능 표시','사례08','완료 실제 HUD 캡처'],['R22','기절과 빙결','쿨타임 증가 의견 검토','사례08','피격 후 면역2→5초 제안만 기록 기존2초 유지']]
data['case08']={
 'title':'사례 08 우체통 재점령 20초 제한과 남은 시간 표시',
 'date':'2026년 10월 18일  관련 요청 R20 R21 R22  요구 변경  Editor 검증 완료',
 'problem':'점령 직후 곧바로 빼앗기는 반복을 줄이기 위해 성공한 우체통을20초 동안 보호하고, 바라보면 N초 후 점령 가능을 표시한다. 기절·빙결 쿨타임 증가는 의견 요청으로 구분한다.',
 'cause':'기존 점령은 소유자와 진행 시간만 확인해 재점령 보호 시간이 없었다. 기절·빙결은 공통 피격 처리의 효과 종료 후2초 면역을 이미 사용하며 뿅망치·아이스크림은 각1회용이다.',
 'method':'우체통별 만료 시간을 두고 최초 점령과 탈환 성공 때20초를 시작한다. 공통 Capture에서 차단하고 로컬 E·봇·서버 진입 경로에서도 진행을 막는다. 봇은 잠긴 목표를 건너뛰며 장갑은 성공 때만 소모한다. 새 경기에서 전부 초기화한다. 프레임에 남은 시간을 보내 참가자 시계로 복원하고 프로토콜6으로 구분한다.',
 'result':'보호 중에는 소유권·점령 진행·장갑 사용량이 바뀌지 않는다. HUD는 양수 남은 시간을 올림해20초 후 점령 가능부터 표시하며0이 되면 기존 E 점령 안내로 돌아간다. 재점령 성공 시20초를 다시 시작하고 중립 우체통은 처음부터 점령 가능하다.',
 'verification':'Tools/Gameplay/Capture/Verify20 Second Protection에서34통과/0실패. 첫 점령·20초 경계·반복 잠금·미리 E 유지·장갑·봇·로컬 재시작·호스트0/1 상태·직렬화 참가자 소유자 매핑·잘못된/오래된 프레임을 확인했다. main Console 오류0, Play 중지. 실제 동작 경로와 제어된 상태 검사이며 별도 네트워크 프로세스 연결은 아니다.',
 'cooldown':'공격 재사용 간격과 피격 면역은 다르다. 현재 뿅망치 기절1.2초·공격 간격0.75초, 빙결1초·공격 간격0.6초, 종료 후 공통 면역2초다. 한 번 쓰는 장비의 공격 간격을 늘리기보다 공통 면역을5초로 늘리면 여러 상대의 연속 행동 제한을 줄일 수 있다고 제안했다. 이번에는 수치 변경 요청으로 확정하지 않아 모두 유지했다.',
 'limits':'멀티는 한 Editor의 실제 호스트 초기화, 검수용 연결 플레이어 기록과 직렬화 참가자 프레임 범위다. 별도 두 프로세스·WAN·배포 EXE/Mac 재빌드는 이번 미실행이다. 기존 프로토콜5 배포본과 새6 소스는 호환되지 않는다. 처음 검사 메뉴가 아직 임포트되지 않아 실행에 실패했고 강제 Asset Refresh 후 정상 실행했다. 기능 검사 실패는 없었다.',
 'capture_note':'저장된 실제 SampleScene 우체통과 실제 DrawGameplayHUD를 사용했다. 검수 카메라·소유자·타이머·상호작용 상태를 설정한 화면이며 두 캡처의 시간 차로20초 경과를 입증하는 자료는 아니다. 경계 동작은 별도34항목 로그로 확인한다.',
 'files':'MonsterDeliveryPrototype.cs · MonsterDeliveryHUD.cs · DeliveryNetwork.cs · DeliveryNetworkGameplay.cs · ExplorationRival.cs\nCaptureProtectionCheck.cs · ItemUsageCheck.cs · CornerSpawnCheck.cs\nevidence/capture-protection-check.txt · capture-protection-session.diff · source/case08',
 'images':[['20261018_capture_protected.png','제어된 실제 HUD 검수 상태 20초 후 점령 가능과 비활성 E'],['20261018_capture_available.png','만료 후 실제 HUD 검수 상태 E 유지 점령 안내 복귀']]
}
(S/'report_data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(S/'append_capture_protection_report.py').write_text('''from docx.shared import Inches

def append_case08(doc,D,c):
 doc.add_page_break();doc.add_heading(c['title'],1);doc.add_paragraph(c['date'])
 for title,key in [('문제와 기대 동작','problem'),('원인','cause'),('수정 방법','method'),('해결 결과','result'),('검증','verification'),('기절과 빙결 검토','cooldown'),('한계와 재시도','limits'),('변경 파일','files')]:
  p=doc.add_paragraph();p.add_run(title+'  ').bold=True;p.add_run(c[key])
 doc.add_page_break();doc.add_heading('실제 우체통 재점령 안내',1);doc.add_paragraph(c['capture_note'])
 for name,caption in c['images']:
  doc.add_paragraph().add_run().add_picture(str(D/'images'/name),width=Inches(6.0));doc.add_paragraph(caption,'Caption')
''',encoding='utf-8')
p=S/'render_report.py';s=p.read_text(encoding='utf-8')
s=s.replace("doc.save(D/'ChangeReport_20261018.docx')","from append_capture_protection_report import append_case08\nappend_case08(doc,D,data['case08'])\n\ndoc.save(D/'ChangeReport_20261018.docx')")
anchor="md.append('## 근거 링크"
idx=s.index(anchor)
append="""c=data['case08']
parts.append('<h1>'+html.escape(c['title'])+'</h1>');md.extend(['# '+c['title'],c['date']])
for title,key in [('문제와 기대 동작','problem'),('원인','cause'),('수정 방법','method'),('해결 결과','result'),('검증','verification'),('기절과 빙결 검토','cooldown'),('한계와 재시도','limits'),('화면 안내','capture_note'),('변경 파일','files')]:
 parts.append('<h2>'+title+'</h2><p>'+html.escape(c[key]).replace(chr(10),'<br>')+'</p>');md.extend(['## '+title,c[key]])
for name,caption in c['images']:
 parts.append(f'<figure><img src="images/{name}" alt="{html.escape(caption)}"><figcaption>{html.escape(caption)}</figcaption></figure>');md.append(f'![{caption}](images/{name})')
md.append('## 사례08 근거 링크\\n\\n[34항목 검사](evidence/capture-protection-check.txt) · [검증 범위](evidence/capture-protection-verification.json) · [이번 변경](evidence/capture-protection-session.diff) · [변경 전후](source/case08/)')
"""
s=s[:idx]+append+s[idx:];s=s.replace("+data['case07']['images']:","+data['case07']['images']+data['case08']['images']:")
p.write_text(s,encoding='utf-8')
for script,args in [('render_item_usage.py',[]),('render_report.py',[str(O)])]:subprocess.run([sys.executable,str(S/script)]+args,check=True)
print('generated updated current item guide and cumulative case08, request mapping R20-R22')
