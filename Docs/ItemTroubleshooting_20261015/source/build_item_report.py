from pathlib import Path
import json, shutil, hashlib, csv, html, zipfile
from datetime import datetime, timezone, timedelta
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT

ROOT = Path('C:/Users/xlfhf/Documents/Codex/2026-09-18/https-github-com-kangminsu123-monsterdeliveryservice-git/MonsterDeliveryService')
WORK = Path(__file__).parent
DEST = ROOT / 'Docs/ItemTroubleshooting_20261015'
DEST.mkdir(parents=True, exist_ok=True)
for folder in ['images', 'evidence', 'source']:
    (DEST / folder).mkdir(exist_ok=True)
data_path = WORK / 'item_report_data.json'
if not data_path.exists():
    data_path = WORK / 'report_data.json'
D = json.loads(data_path.read_text(encoding='utf-8'))
assets = {}
for key, (original, status, date) in D['assets'].items():
    src = ROOT / 'art-review' / original
    if not src.exists():
        raise FileNotFoundError(src)
    target = f'images/{key}_{date.replace("-", "")}.png'
    shutil.copy2(src, DEST / target)
    assets[key] = dict(file=target, source='art-review/' + original, status=status, capture_date=date,
                       sha256=hashlib.sha256(src.read_bytes()).hexdigest())
D['image_manifest'] = assets
logs = [
    ('러닝화','Running Shoes/Verify Effects',29,'running-shoes-effects-check-20261013.txt'),
    ('에어캡','Bubble Wrap/Verify Effects',32,'bubble-wrap-effects-check-20261013.txt'),
    ('만년필과 공통 피격','Fountain Pen/Verify Effects',54,'fountain-pen-check-20261013.txt'),
    ('표면 잉크','Fountain Pen/Verify Surface Ink',26,'surface-ink-check-20261013.txt'),
    ('뚫어뻥','Plunger/Verify Throw',92,'plunger-cup-volume-check-20261014.txt'),
    ('슬라임','Slime/Verify Effects',53,'slime-check-20261014.txt'),
    ('뿅망치','Toy Hammer/Verify Effects',36,'toy-hammer-check-20261014.txt'),
]
for _,_,count,name in logs:
    txt=(ROOT/'art-review'/name).read_text(encoding='utf-8-sig')
    observed=sum(line.startswith('PASS ') for line in txt.splitlines())
    if observed != count: raise ValueError((name,observed,count))
    shutil.copy2(ROOT/'art-review'/name, DEST/'evidence'/name)
D['checks']=[dict(item=a,menu='Tools/Gameplay/'+b,count=c,date='2026-10-15',log='evidence/'+d) for a,b,c,d in logs]
with (DEST/'requests.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f); w.writerow(['요청 ID','아이템','사용자가 요청한 내용 요약','상세 절']); w.writerows(D['requests'])

catalog=[
['복싱 글러브','교대 밀치기와 넉백','기존 프로젝트 기록'],
['압축 뚫어뻥','15m 투척과 당김 및 컵 폭 판정','상세 08'],
['뿅망치','3.5m 스윕과 1.2초 기절','상세 11부터 13'],
['슬라임','투척과 이동 속도 40% 감소','상세 09와 10'],
['무선 선풍기','전방 밀기 설계','효과 활성화 보류'],
['러닝화','5초 동안 이동 속도 50% 증가','상세 01과 02'],
['에어캡','8초 안의 유효 공격 한 번 방어','상세 03과 06'],
['작업용 장갑','선택 착용 중 점령 3초를 1.5초로 단축','기존 프로젝트 기록'],
['만년필','48m/s 직선 투척과 4초 시야 차단','상세 04부터 07'],
]
intro='이 보고서는 현재 대화에서 사용자가 요청한 아이템 관련 20개 항목을 프로젝트 코드, 수정 기록, 실제 Unity 캡처와 대조한 문서다. 13개 상세 절에서 어떤 문제가 있었고 어떤 방법으로 바꾸었는지, 그 결과를 무엇으로 확인했는지 설명한다.'
scope='화면 자료는 실제 Unity Game View다. 최종 모습은 2026년 10월 15일 다시 캡처했고, 초기 모습과 장갑 자료는 보존된 당시 화면을 날짜와 함께 표시했다. 수정 전 캡처가 없는 항목은 이미지를 재구성하지 않고 확인 가능한 이전 수치와 원인으로 설명했다.'
supplement='현재 대화보다 앞선 프로젝트 기록에는 복싱 글러브의 좌우 교대 동작과 밀침 거리 약 5.4m에서 16.2m로 강화, 작업용 장갑의 점령 시간 3초에서 1.5초로 단축, 반복 사용 뒤 아이템 유지가 있다. 이 내용은 대화의 20개 요청과 구분해 추가 프로젝트 기록으로 남긴다. 당시 장갑 검사 52개 통과 기록은 NEXT_SESSION과 보존된 로그에 있으며 오늘 다시 실행한 검사군에는 포함하지 않았다.'
limits='현재 테스트는 로컬 Unity 프로토타입이다. 온라인 동기화와 모든 봇의 아이템 자동 사용까지 완료했다는 뜻은 아니다. 검사는 서로 겹치는 사례를 포함하므로 검사군 개수를 합쳐 고유 기능 수나 오류 수로 해석하지 않는다.'

doc=Document(); sec=doc.sections[0]
sec.page_width=Inches(8.5); sec.page_height=Inches(11)
sec.top_margin=sec.bottom_margin=Inches(.64); sec.left_margin=sec.right_margin=Inches(.7)
sec.footer_distance=Inches(.25)
styles=doc.styles
for s in styles:
    for border in list(s._element.iter(qn('w:pBdr'))):
        border.getparent().remove(border)
for name in ['Normal','Title','Subtitle','Heading 1','Heading 2','Caption']:
    s=styles[name]; s.font.name='맑은 고딕'; s.font.color.rgb=RGBColor(0,0,0)
    s._element.get_or_add_rPr().get_or_add_rFonts().set(qn('w:eastAsia'),'맑은 고딕')
styles['Normal'].font.size=Pt(10.3); styles['Normal'].paragraph_format.line_spacing=1.18
styles['Normal'].paragraph_format.space_after=Pt(6)
styles['Title'].font.size=Pt(27); styles['Title'].font.bold=True
styles['Subtitle'].font.italic=False
styles['Title'].paragraph_format.space_after=Pt(10)
styles['Heading 1'].font.size=Pt(16); styles['Heading 1'].font.bold=True
styles['Heading 1'].paragraph_format.space_after=Pt(7)
styles['Heading 2'].font.size=Pt(11); styles['Heading 2'].font.bold=True
styles['Caption'].font.size=Pt(8.5); styles['Caption'].font.italic=False
styles['Caption'].paragraph_format.space_after=Pt(7)
footer=sec.footer.paragraphs[0]; footer.alignment=WD_ALIGN_PARAGRAPH.RIGHT
run=footer.add_run('아이템 트러블슈팅 기록   '); run.font.size=Pt(8)
field=OxmlElement('w:fldSimple'); field.set(qn('w:instr'),'PAGE'); footer._p.append(field)
doc.core_properties.title=D['title']; doc.core_properties.subject='사용자 요청별 문제와 구현 방법 및 해결 결과'
doc.core_properties.author='MonsterDeliveryService 프로젝트'; doc.core_properties.keywords='Unity, 아이템, 트러블슈팅, 2026-10-15'

def para(text,style=None,size=None):
    p=doc.add_paragraph(text,style); p.paragraph_format.keep_together=True
    if size:
        for r in p.runs:r.font.size=Pt(size)
    return p
def label(name,text):
    p=doc.add_paragraph(); p.paragraph_format.keep_together=True
    p.add_run(name+'  ').bold=True; p.add_run(text); return p
def page(title):
    doc.add_page_break(); doc.add_heading(title,level=1)
def table(headers,rows,widths,font=9.2):
    t=doc.add_table(rows=1,cols=len(headers)); t.alignment=WD_TABLE_ALIGNMENT.CENTER; t.autofit=False
    for col,width in zip(t.columns,widths):col.width=Inches(width)
    for i,h in enumerate(headers):t.rows[0].cells[i].text=h
    for row in rows:
        cells=t.add_row().cells
        for i,text in enumerate(row):cells[i].text=str(text)
    pr=t._tbl.tblPr
    borders=OxmlElement('w:tblBorders')
    for edge in ['top','left','bottom','right','insideH','insideV']:
        e=OxmlElement('w:'+edge); e.set(qn('w:val'),'single'); e.set(qn('w:sz'),'4'); e.set(qn('w:color'),'D9D9D9'); borders.append(e)
    pr.append(borders)
    for rid,row in enumerate(t.rows):
        for cid,c in enumerate(row.cells):
            c.width=Inches(widths[cid]); c.vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
            cp=c._tc.get_or_add_tcPr(); shade=OxmlElement('w:shd'); shade.set(qn('w:fill'),'243746' if rid==0 else ('F1F5F8' if rid%2==0 else 'FFFFFF')); cp.append(shade)
            margins=OxmlElement('w:tcMar')
            for side,val in [('top','55'),('bottom','55'),('left','75'),('right','75')]:
                m=OxmlElement('w:'+side); m.set(qn('w:w'),val); m.set(qn('w:type'),'dxa'); margins.append(m)
            cp.append(margins)
            for p in c.paragraphs:
                p.paragraph_format.space_after=Pt(0); p.paragraph_format.line_spacing=1.07
                for r in p.runs:
                    r.font.size=Pt(font); r.font.color.rgb=RGBColor.from_string('FFFFFF' if rid==0 else '000000'); r.bold=rid==0
        if rid==0:
            repeat=OxmlElement('w:tblHeader'); row._tr.get_or_add_trPr().append(repeat)
    return t
def figures(figs):
    p=doc.add_paragraph(); p.paragraph_format.keep_with_next=True; p.alignment=WD_ALIGN_PARAGRAPH.CENTER
    width=6.84 if len(figs)==1 else 3.35
    for n,(key,cap) in enumerate(figs):
        if n:p.add_run('  ')
        inline=p.add_run().add_picture(str(DEST/assets[key]['file']),width=Inches(width))
        inline._inline.docPr.set('descr',cap)
    cp=doc.add_paragraph(style='Caption'); cp.paragraph_format.keep_together=True
    cp.add_run('   '.join(('왼쪽 ' if n==0 and len(figs)>1 else '오른쪽 ' if len(figs)>1 else '')+cap for n,(_,cap) in enumerate(figs)))

para('몬스터 딜리버리 서비스\n아이템 트러블슈팅 기록','Title')
para('수정 요청과 구현 방법 및 해결 결과','Subtitle')
para('작성 기준  2026년 10월 15일    Unity 6000.5.10f1    SampleScene',size=9)
para(intro)
figures([['hammer_hud','현재 프로젝트의 망치와 HUD 최종 상태 2026년 10월 15일 재캡처']])
para(scope,size=9.4)
label('읽는 순서','다음 페이지의 요청 대조표에서 해당 ID를 찾고, 같은 번호의 상세 절에서 문제와 원인, 사용한 방식, 해결 결과를 확인하면 된다. 검증 로그와 원본 이미지 경로는 마지막 페이지와 프로젝트 폴더에 모았다.')

page('사용자 요청 20개와 상세 절 대조')
para('목록 조회 요청도 남겨 누락 여부를 확인할 수 있게 했다. 요청 문장은 읽기 쉽게 요약했으며, 상세 절 번호는 페이지 번호가 아니다.',size=9.2)
table(['ID','항목','요청 내용 요약','상세 절'],D['requests'],[.43,.95,4.65,.68],font=9)

for c in D['cases']:
    page(c['id']+' '+c['title'])
    para('요청 '+', '.join(c['req'])+'   '+c['kind'],size=9)
    figures(c['figures'])
    label('문제',c['problem']); label('원인과 배경',c['cause']); label('사용한 방법',c['method']); label('해결 결과',c['result'])
    label('확인 근거',c['verify'])
    para('구현 위치  '+' · '.join(Path(p).name for p in c['files']),size=8.6)

page('부록 현재 아이템 목록과 추가 프로젝트 기록')
para('R05의 아이템 목록 재확인 요청과 현재 코드의 WeaponKind를 대조했다. 교체 전 확성기와 테이프, 프린터 잉크는 현재 목록에서 각각 뿅망치와 슬라임, 만년필로 바뀌었다.')
table(['아이템','현재 역할','기록 범위'],catalog,[1.05,3.72,1.94],font=9.3)
doc.add_heading('앞선 프로젝트의 장갑 관련 개선',level=2)
para(supplement)
figures([['glove','복싱 글러브의 보존된 동작 자료 2026년 10월 9일'],['workglove','작업용 장갑 점령 시간의 보존된 자료 2026년 10월 9일']])
para('공통 테스트 설정은 UnlimitedItems true다. G는 선택 아이템을 영구 삭제하고, 강화 지속 시간과 재사용 시간, 슬롯 세 칸 제한은 유지한다.',size=9)

page('검증 결과와 프로젝트 파일 안내')
para('아래 7개 검사군은 보고서 캡처를 위해 2026년 10월 15일 Unity Editor에서 순서대로 다시 실행했다. 결과 로그 원문은 evidence 폴더에 보존했다.')
table(['검사군','통과','실행 메뉴'],[[a,str(c)+'개',b] for a,b,c,_ in logs],[1.55,.6,4.56],font=9.2)
para(limits,size=9.5)
doc.add_heading('이미지와 근거의 구분',level=2)
para('스크린샷은 결과를 보여 주는 시각 자료다. 일부 효과 장면은 검수 도구에서 상태를 재현한 화면이며, 실제 투사체 충돌과 방어 및 가장자리 판정은 별도의 검사 로그로 확인한다. 정지 화면 한 장만으로 시간 경과나 판정 정확도를 입증하지 않는다.',size=9.5)
doc.add_heading('폴더에서 바로 확인할 파일',level=2)
table(['파일','용도'],[
['ItemTroubleshooting_20261015.docx','편집 가능한 본 보고서'],
['index.html','이미지를 클릭해 원본을 여는 로컬 열람본'],
['README.md','프로젝트 안에서 읽는 동일 내용의 Markdown 원문'],
['requests.csv','20개 요청과 상세 절의 대응 목록'],
['images 폴더','보고서에 사용한 전체 원본 캡처'],
['evidence 폴더','재실행 검사 로그와 코드 위치 및 SHA256 목록'],
['source 폴더','보고서 생성 스크립트와 구조화된 원문'],
],[2.65,4.06],font=9.2)
para('근거 문서  GAME_DESIGN.md · TEAM_STATUS.md · NEXT_SESSION.txt · ART_REVIEW.md\n코드 기준  Assets/Gameplay와 Assets/Editor의 실제 파일. 당시 기록과 현재 최종 수치가 다르면 최신 코드 및 이 문서의 최종 값을 우선한다.',size=8.8)

outfile=DEST/'ItemTroubleshooting_20261015.docx'; doc.save(outfile)

def e(s):return html.escape(str(s))
def html_fig(figs):
    return '<div class="figures '+('pair' if len(figs)>1 else '')+'">'+''.join('<figure><a href="'+e(assets[k]['file'])+'"><img src="'+e(assets[k]['file'])+'" alt="'+e(cap)+'"></a><figcaption>'+e(cap)+'</figcaption></figure>' for k,cap in figs)+'</div>'
def html_table(headers,rows):
    return '<table><thead><tr>'+''.join('<th>'+e(x)+'</th>' for x in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+e(x)+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table>'
parts=['<section class="page"><h1>'+e(D['title'])+'</h1><p>수정 요청과 구현 방법 및 해결 결과 · 2026년 10월 15일</p><p>'+e(intro)+'</p>'+html_fig([['hammer_hud','현재 망치와 HUD 최종 상태']])+'<p class="small">'+e(scope)+'</p><p><a href="ItemTroubleshooting_20261015.docx">Word 보고서</a> · <a href="requests.csv">요청 대조표 CSV</a></p></section>']
parts.append('<section class="page"><h2>사용자 요청 20개와 상세 절 대조</h2>'+html_table(['ID','항목','요청 내용 요약','상세 절'],D['requests'])+'</section>')
for c in D['cases']:
    blocks=''.join('<h3>'+name+'</h3><p>'+e(c[key])+'</p>' for name,key in [('문제','problem'),('원인과 배경','cause'),('사용한 방법','method'),('해결 결과','result'),('확인 근거','verify')])
    refs=' · '.join('<a href="../../'+e(p)+'">'+e(p)+'</a>' for p in c['files'])
    parts.append('<section class="page" id="case-'+c['id']+'"><h2>'+e(c['id']+' '+c['title'])+'</h2><p class="small">요청 '+e(', '.join(c['req']))+' · '+e(c['kind'])+'</p>'+html_fig(c['figures'])+blocks+'<p class="small">구현 위치 '+refs+'</p></section>')
parts.append('<section class="page"><h2>부록 현재 아이템 목록과 추가 프로젝트 기록</h2>'+html_table(['아이템','현재 역할','기록 범위'],catalog)+'<h3>앞선 프로젝트의 장갑 관련 개선</h3><p>'+e(supplement)+'</p>'+html_fig([['glove','복싱 글러브 보존 자료'],['workglove','작업용 장갑 보존 자료']])+'</section>')
parts.append('<section class="page"><h2>검증 결과와 파일 안내</h2><p>2026년 10월 15일 Unity Editor 재실행 결과</p>'+html_table(['검사군','통과','실행 메뉴'],[[a,str(c)+'개',b] for a,b,c,_ in logs])+'<p>'+e(limits)+'</p><p>이미지는 결과 확인용이다. 시간 경과와 실제 충돌 판정의 근거는 evidence 폴더의 검사 로그를 함께 확인한다.</p><p>원본 이미지와 촬영 구분 및 SHA256은 image_manifest.json, 전체 요청은 requests.csv, 구조화된 보고서 원문은 source/report_data.json에 있다.</p></section>')
css='''*{box-sizing:border-box}body{font:15px/1.65 "Malgun Gothic",sans-serif;color:#17212b;background:#edf0f3;margin:0}.page{max-width:1000px;margin:24px auto;padding:38px 46px;background:white;box-shadow:0 2px 12px #0001}h1,h2,h3{color:black}h1{font-size:30px;line-height:1.4}h2{font-size:23px}h3{font-size:16px;margin:16px 0 4px}p{margin:8px 0}.small{font-size:12px;color:#475260}table{width:100%;border-collapse:collapse;margin:14px 0;font-size:13px}td,th{border:1px solid #d9d9d9;padding:8px;text-align:left}th{background:#243746;color:white}tr:nth-child(even){background:#f1f5f8}.figures{display:flex;gap:14px}.figures figure{margin:12px 0;flex:1;min-width:0}.figures img{width:100%;height:auto;display:block}.figures figcaption{font-size:12px;color:#475260;margin-top:6px}a{color:#075e96;overflow-wrap:anywhere}@media(max-width:700px){.page{padding:20px;margin:8px}.pair{display:block}}@media print{@page{size:A4;margin:16mm}body{background:white;font-size:10pt}.page{margin:0;padding:0;box-shadow:none;break-after:page;max-width:none}h2{font-size:15pt}h3{font-size:10.5pt}td,th{padding:4px}table{font-size:8pt}.small,figcaption{font-size:8pt}}'''
(DEST/'index.html').write_text('<!doctype html><html lang="ko"><meta charset="utf-8"><title>'+e(D['title'])+'</title><style>'+css+'</style><body>'+''.join(parts)+'</body></html>',encoding='utf-8')
md=['# '+D['title'],'','작성 기준 2026-10-15','',intro,'',scope,'','## 사용자 요청 대조','', '| ID | 항목 | 요청 요약 | 상세 절 |','|---|---|---|---|']
md += ['| '+' | '.join(row)+' |' for row in D['requests']]
for c in D['cases']:
    md+=['','## '+c['id']+' '+c['title'],'','요청 '+', '.join(c['req'])+' · '+c['kind'],'']
    for k,cap in c['figures']:md+=['!['+cap+']('+assets[k]['file']+')','']
    for name,key in [('문제','problem'),('원인과 배경','cause'),('사용한 방법','method'),('해결 결과','result'),('확인 근거','verify')]:md+=['### '+name,'',c[key],'']
    md+=['구현 위치 '+ ' · '.join('['+p+'](../../'+p+')' for p in c['files'])]
md+=['','## 현재 아이템 목록','', '| 아이템 | 역할 | 범위 |','|---|---|---|']+['| '+' | '.join(row)+' |' for row in catalog]
md+=['','## 추가 프로젝트 기록','',supplement,'','## 재실행 검사 결과','']
md += ['- '+a+' '+str(c)+'개 통과 2026-10-15 ['+d+'](evidence/'+d+')' for a,b,c,d in logs]
md+=['',limits,'','## 파일 안내','','index.html은 전체 이미지와 본문을 함께 보여 준다. Word 원본은 ItemTroubleshooting_20261015.docx다. requests.csv는 요청 대조표, image_manifest.json은 원본 경로와 촬영 구분 및 이미지 해시다. source 폴더의 생성기를 사용하면 문서와 열람본을 다시 만들 수 있다.']
(DEST/'README.md').write_text('\n'.join(md),encoding='utf-8')
(DEST/'image_manifest.json').write_text(json.dumps(assets,ensure_ascii=False,indent=2),encoding='utf-8')
(DEST/'source/report_data.json').write_text(json.dumps(D,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(__file__,DEST/'source/build_item_report.py')
locations=[]
for path in sorted(set(p for c in D['cases'] for p in c['files'])):
    raw=(ROOT/path).read_bytes(); lines=raw.decode('utf-8-sig').splitlines()
    locations.append(dict(path=path,line_count=len(lines),sha256=hashlib.sha256(raw).hexdigest()))
(DEST/'evidence/source_manifest.json').write_text(json.dumps(locations,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'docx':str(outfile),'folder':str(DEST),'requests':len(D['requests']),'cases':len(D['cases']),'images':len(assets),'logs':len(logs)},ensure_ascii=False))
