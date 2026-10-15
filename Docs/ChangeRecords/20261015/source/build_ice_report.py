from pathlib import Path
import json,csv,html,shutil,hashlib
from docx import Document
from docx.shared import Inches,Pt,RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.text import WD_ALIGN_PARAGRAPH
P=Path(r'C:\Users\xlfhf\Documents\Codex\2026-09-18\https-github-com-kangminsu123-monsterdeliveryservice-git\MonsterDeliveryService')
D=P/'Docs/ChangeRecords/20261015'; (D/'source').mkdir(parents=True,exist_ok=True)
O=Path(r'C:\Users\xlfhf\Documents\Codex\2026-10-15\agents-md\outputs');O.mkdir(exist_ok=True)
for src in ['toy-hammer-check-20261014.txt','parcel-random-loot-check-20261008.txt','surface-ink-check-20261013.txt']:
 shutil.copy2(P/'art-review'/src,D/'evidence'/src)
logs=[('아이스크림과 표면 흔적','Tools/Gameplay/IceCream/Verify Effects',50,'ice-cream-check.txt'),('만년필 표면 회귀 검사','Tools/Gameplay/Fountain Pen/Verify Surface Ink',26,'surface-ink-check-20261013.txt')]
for _,_,n,f in logs:
 s=(D/'evidence'/f).read_text(encoding='utf-8-sig'); assert sum(x.startswith('PASS ') for x in s.splitlines())==n,(n,f)
images=[('opponent','초기 사각 얼음 2026년 10월 15일 첫 구현'),('spiky_opponent','후속 수정 뾰족한 다면체와 12개 결정'),('spiky_local','후속 수정 본인 화면의 뾰족한 서리'),('surface_wall','실제 맵 울타리의 아이스크림 흔적'),('surface_floor','실제 바닥의 아이스크림 흔적'),('surface_wall_fade','3초 유지 뒤 서서히 사라지는 흔적')]
intro='후속 피드백을 반영해 사각 얼음을 끝이 뾰족한 결정으로 바꾸고 벽과 바닥 충돌에 딸기색 흔적을 추가했다. 무선 선풍기를 아이스크림으로 교체했다. 좌클릭으로 던져 직접 맞히면 상대가 1초 동안 얼어 이동과 행동 및 점령을 할 수 없다. 현재 탐색 테스트는 2번 아이스크림을 선택해 시작한다.'
problem='초기 외형은 Cube 기반이라 얼음이 네모나게 보였다. 비플레이어 표면 충돌은 시각 흔적 없이 종료되었다. 사용자는 뾰족한 얼음과 벽·바닥 흔적을 추가 요청했다. 초기 요청에서는 사용자는 기존 선풍기 자리에 투척형 아이스크림을 요청했다. 이전 선풍기는 목록과 모델 및 기본 밀기 로직이 있었지만 사용 효과는 보류되어 있었다. 이번 변경은 결함 수정이 아니라 새 아이템으로의 요구 변경이다. 수정 전 화면은 별도로 촬영하지 않았으며 이전 상태는 코드와 기존 문서로 확인했다.'
method='중심 다면체와 주변 12개 결정은 양 끝이 뾰족한 육각 메시 하나를 공유한다. 표면 흔적은 기존 SurfaceInkMark에 색상과 유지·페이드 시간만 선택 인자로 추가해 재사용했다. 기존 만년필의 기본 검정색과 1초 유지 및 1초 페이드는 유지한다. 아이템 ID5를 유지한 채 IceCream으로 교체하고 택배 획득과 HUD 및 재생성 빌더를 연결했다. 기존 Rigidbody와 BrawlShot의 고속 이동 스윕 및 첫 충돌 1회 처리를 재사용했다. 딸기와 바닐라 콘 모델은 Unity 네이티브 메시와 재질로 만들고 같은 모델을 슬롯 아이콘으로 렌더했다.'
rules='적중은 DeliveryItemHit(stunSeconds:1) → ApplyItemHit → ReceiveHit 공통 경로를 사용한다. Applied 결과에서만 빙결 표시 시간을 기록하므로 에어캡은 한 번 막고 터지며 면역 중에는 보호막을 소비하지 않는다. 기절 종료 뒤 2초 면역을 공유한다. 빙결 중 점령 진행을 초기화하고 아이템 사용과 이동을 막으며 로봇 Animator를 잠시 멈춘다.'
visual='상대의 떠 있는 몸통을 투명한 푸른 육각 다면체와 뾰족한 결정이 감싼다. 본인은 화면 가장자리의 서리와 빙결 남은 시간을 본다. 빙결 동안 뿅망치의 노란 기절 고리는 숨긴다. 효과는 1초 후 사라지고 애니메이션과 행동이 복구된다. 표시용 얼음에는 충돌체를 추가하지 않았다.'
retry='이번 후속 아이스크림 50개와 만년필 표면 회귀 26개는 통과했다. 화면 서리가 사각형으로 보이던 보간 계산도 보정해 뾰족한 끝이 나타나도록 한 뒤 재검사·재촬영했다. 아래는 첫 구현 때의 검수 이력이다. 초기 아이스크림 32개 검사는 통과했다. 첫 캡처는 검수 중 비활성화한 HUD와 남아 있던 보호막 표시 때문에 부적절하여 HUD를 켜고 보호막 상태를 갱신한 뒤 재촬영했다. 뿅망치 회귀 검사는 처음에 애니메이션 포즈 확인 시점 지연으로 실패했다. 타격 판정은 실제 시간 흐름으로 확인하고 시각 포즈만 타격 후 0.19초로 고정해 재검사한 결과 36개가 통과했다.'
limits='검사는 Unity 6000.5.10f1의 SampleScene 로컬 Play Mode에서 2026-10-15 실행했다. 검증 도구는 격리 위치에서 실제 물리 비행을 실행하며, 아래 화면은 실제 맵에 검수 상태를 설정한 재현 캡처다. 정지 화면으로 1초 정확도를 주장하지 않으며 시간 해제는 별도 검사 로그로 확인했다. 온라인 동기화와 봇의 아이스크림 자동 사용은 이번 범위에 포함하지 않는다.'
warnings='재컴파일 오류는 없었다. 기존 Unity 구형 검색 API의 경고는 기능 오류와 구분해 기록했다. 앞선 뿅망치 36개와 택배 48개 통과는 첫 구현 때의 기록이며 이번 후속 검사는 아이스크림 50개와 만년필 표면 26개다. 검사군 수는 고유 사례 수로 합산하지 않는다.'
params=[('표면 흔적','딸기색 / 3초 유지 + 1초 페이드 / 효과 없음'),('카탈로그','9종 유지 / ID5 선풍기 → 아이스크림'),('투척','18m/s 전방 + 3m/s 상승 / 중력 적용'),('적중과 제한','반경 0.18m / 첫 충돌 1회 / 3초 안전 삭제'),('상태','직접 1초 빙결 / 종료 뒤 2초 기절 면역'),('사용','재사용 0.6초 / 테스트 무제한 / G 영구 폐기'),('시작 장비','1 뚫어뻥 / 2 아이스크림 선택 / 3 에어캡')]
data=dict(followup='R04 뾰족한 결정 / R05 벽과 바닥 흔적',history_checks={'initial_icecream':32,'toyhammer':36,'parcel':48},date='2026-10-15',intro=intro,problem=problem,method=method,rules=rules,visual=visual,retry=retry,limits=limits,warnings=warnings,params=params,checks=logs,images=images)
(D/'source/report_data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(__file__,D/'source/build_ice_report.py')
with (D/'requests.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['요청 ID','대상','요청 요약','관련 사례','처리 상태']);w.writerows([['R01','아이템 목록','선풍기 제거 및 아이스크림 교체','사례 01','완료'],['R02','투척','아이스크림을 던져 직접 적중','사례 01','완료'],['R03','빙결','기절과 비슷하게 1초 동안 얼기','사례 01','완료'],['R04','얼음 외형','사각 얼음을 뾰족한 느낌으로 변경','사례 01 후속','완료'],['R05','충돌 흔적','벽과 바닥에 아이스크림 흔적 남기기','사례 01 후속','완료']])
manifest=[]
for key,caption in images:
 f=D/f'images/20261015_icecream_{key}.png';manifest.append(dict(path=str(f.relative_to(D)),caption=caption,capture_date='2026-10-15',reproduction=True,sha256=hashlib.sha256(f.read_bytes()).hexdigest()))
(D/'evidence/image_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
changed=['Assets/Gameplay/SurfaceInkMark.cs','Assets/Gameplay/MonsterDeliveryPrototype.cs','Assets/Gameplay/DeliveryItemState.cs','Assets/Gameplay/IceCreamCombat.cs','Assets/Gameplay/IceCreamFreezeEffect.cs','Assets/Gameplay/ToyHammerCombat.cs','Assets/Gameplay/MonsterDeliveryHUD.cs','Assets/Gameplay/ExplorationRival.cs','Assets/Editor/IceCreamSetup.cs','Assets/Editor/IceCreamEffectsCheck.cs','Assets/Editor/DeliveryHeldItemSetup.cs','Assets/Editor/InGameUISetup.cs']
(D/'evidence/code_sha256.json').write_text(json.dumps({f:hashlib.sha256((P/f).read_bytes()).hexdigest() for f in changed},indent=2),encoding='utf-8')
(D/'evidence/retries.txt').write_text(retry+'\n\n'+warnings,encoding='utf-8')
doc=Document();sec=doc.sections[0];sec.page_width=Inches(8.27);sec.page_height=Inches(11.69);sec.top_margin=sec.bottom_margin=Inches(.65);sec.left_margin=sec.right_margin=Inches(.7)
for name in ['Normal','Title','Heading 1','Heading 2','Caption']:
 st=doc.styles[name];st.font.name='Malgun Gothic';st.font.color.rgb=RGBColor(0,0,0);st.element.rPr.rFonts.set(qn('w:eastAsia'),'Malgun Gothic')
doc.styles['Normal'].font.size=Pt(10);doc.styles['Normal'].paragraph_format.space_after=Pt(6);doc.styles['Normal'].paragraph_format.line_spacing=1.05
for st in doc.styles:
 if st.type == 1:
  pr=st.element.get_or_add_pPr()
  for node in list(pr):
   if node.tag in [qn('w:pBdr'),qn('w:snapToGrid')]:pr.remove(node)
  node=OxmlElement('w:snapToGrid');node.set(qn('w:val'),'0');pr.append(node)
doc.styles['Caption'].font.bold=False;doc.styles['Caption'].font.size=Pt(9)
doc.styles['Title'].font.size=Pt(23);doc.styles['Heading 1'].font.size=Pt(16);doc.styles['Heading 2'].font.size=Pt(12)
def p(t):doc.add_paragraph(t)
def table(headers,rows,widths):
 t=doc.add_table(rows=1,cols=len(headers));t.autofit=False
 for c,w in zip(t.columns,widths):c.width=Inches(w)
 for i,x in enumerate(headers):t.rows[0].cells[i].text=x
 for row in rows:
  for c,x in zip(t.add_row().cells,row):c.text=str(x)
 for ri,row in enumerate(t.rows):
  for ci,c in enumerate(row.cells):
   c.width=Inches(widths[ci])
   pr=c._tc.get_or_add_tcPr(); borders=OxmlElement('w:tcBorders')
   for edge in ['top','left','bottom','right']:
    e=OxmlElement('w:'+edge);e.set(qn('w:val'),'single');e.set(qn('w:sz'),'4');e.set(qn('w:color'),'D9D9D9');borders.append(e)
   pr.append(borders)
   if ri==0:
    shade=OxmlElement('w:shd');shade.set(qn('w:fill'),'DDEBF7');pr.append(shade)
   for para in c.paragraphs:
    para.paragraph_format.space_before=Pt(4);para.paragraph_format.space_after=Pt(4)
    for r in para.runs:r.font.size=Pt(9.5);r.bold=ri==0
def picture(key,caption):
 para=doc.add_paragraph();para.add_run().add_picture(str(D/f'images/20261015_icecream_{key}.png'),width=Inches(6.6));para.paragraph_format.space_after=Pt(3)
 doc.add_paragraph('2026-10-15 실제 Game View 검수용 재현 · '+caption,'Caption')
doc.add_paragraph('아이스크림 아이템 구현 보고서','Title');p('2026년 10월 15일  |  Monster Delivery Service  |  사례 01')
p(intro);doc.add_heading('요청과 변경 배경',1);p(problem)
table(['항목','최종 구현'],params,[1.2,5.4]);doc.add_heading('구현 방법과 방어 규칙',1);p(method);p(rules)
doc.add_page_break();doc.add_heading('얼음 외형 수정 전후',1);p(visual);picture(*images[0]);picture(*images[1])
doc.add_page_break();doc.add_heading('실행 검증과 본인 빙결',1)
table(['검사군','결과'],[(a,str(n)+'개 통과') for a,_,n,_ in logs],[4.6,2.0])
p('위 검사군은 서로 겹치는 공통 경로가 있으므로 고유 사례 수로 합산하지 않는다. 실행 메뉴와 전체 PASS 로그는 evidence 폴더에 보존했다.')
picture(*images[2]);doc.add_heading('검증 범위와 이력',2);p(retry);p(limits);p(warnings)
doc.add_page_break();doc.add_heading('벽과 바닥에 남는 흔적',1);p('실제 투척체가 비플레이어 표면에 부딪힌 위치에 딸기색 흔적을 남긴다. 표면 방향과 굴곡에 맞춰 투영하고, 3초 유지 뒤 1초 동안 사라진다. 새 판 시작과 세션 종료 시 남은 흔적을 정리한다.');picture(*images[3]);picture(*images[4])
foot=sec.footer.paragraphs[0];foot.alignment=WD_ALIGN_PARAGRAPH.RIGHT;run=foot.add_run('Monster Delivery Service  ·  ');fld=OxmlElement('w:fldSimple');fld.set(qn('w:instr'),'PAGE');foot._p.append(fld)
out=D/'ChangeReport_20261015.docx';doc.save(out)
md=['# 아이스크림 아이템 변경 기록','작성 날짜: 2026-10-15','관련 요청: R01, R02, R03, R04, R05','분류: 요구 변경 / 새 아이템','상태: 구현 및 실행 검증 완료','## 문제와 기대 동작',problem,'## 후속 피드백 R04와 R05', '초기 Cube 기반 외형을 6면 다면체와 12개 뾰족한 결정으로 교체했다. 표면 무연출은 딸기색 흔적으로 바꿨고, 3초 유지 후 1초 페이드를 적용했다. 이전 만년필 기본값은 그대로다.', '## 원인', '기존 ID5는 미활성 선풍기였으며, 요청한 투척과 빙결은 없었다. 기존 공통 기절 처리를 재사용해 동작을 추가했다.','## 사용한 방법',method,rules,'## 해결 결과',intro,visual,'| 항목 | 최종 구현 |','|---|---|']+[f'| {a} | {b} |' for a,b in params]+['## 실제 화면']
for key,cap in images:md.extend([f'![{cap}](images/20261015_icecream_{key}.png)','2026-10-15 실제 Game View 검수용 상태 재현. '+cap+'를 확인했다.'])
md+=['## 확인 근거',limits]+[f'- {a}: `{menu}` — {n}개 통과. [실행 로그](evidence/{f}).' for a,menu,n,f in logs]+['## 재시도와 환경',retry,warnings,'## 수정 파일']+[f'- `{f}`' for f in changed]+['## 산출물','[Word 보고서](ChangeReport_20261015.docx) · [화면 열람본](index.html) · [요청 대조표](requests.csv)','편집 원문과 생성기는 source 폴더, 코드 SHA256과 캡처 날짜 및 로그는 evidence 폴더에 보존했다. 기존 ItemTroubleshooting_20261015 보고서는 그대로 유지했다.']
(D/'README.md').write_text('\n\n'.join(md),encoding='utf-8')
body=f'<h1>아이스크림 아이템 구현 보고서</h1><p>2026-10-15 · 사례 01 · R01 R02 R03 R04 R05</p>'
for h,paras in [('요청과 해결',[intro,problem,method]),('빙결과 방어',[rules,visual]),('실행 검증',[limits,retry,warnings])]:
 body+=f'<h2>{h}</h2>'+''.join('<p>'+html.escape(t)+'</p>' for t in paras)
body+='<ul>'+''.join(f'<li>{a}: {n}개 통과 · <a href="evidence/{f}">로그</a></li>' for a,_,n,f in logs)+'</ul>'
for key,cap in images:body+=f'<figure><img src="images/20261015_icecream_{key}.png"><figcaption>2026-10-15 검수용 재현 · {cap}</figcaption></figure>'
(D/'index.html').write_text('<!doctype html><meta charset="utf-8"><title>아이스크림 구현 보고서</title><style>body{max-width:1050px;margin:40px auto;padding:0 24px;font:17px/1.7 "Malgun Gothic",sans-serif;color:#17212b}h1,h2{color:#000}img{width:100%}figure{margin:28px 0}figcaption{font-size:14px;color:#435262}</style>'+body,encoding='utf-8')
shutil.copy2(out,O/out.name)
print(out)
