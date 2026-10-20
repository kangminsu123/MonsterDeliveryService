from pathlib import Path
import json,re,hashlib,shutil
from pypdf import PdfReader
W=Path('C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx')
R=Path('C:/Users/xlfhf/Documents/Codex/2026-09-18/https-github-com-kangminsu123-monsterdeliveryservice-git/MonsterDeliveryService')
D=R/'Docs/ChangeRecords/20261019'
q={'case':'06','date':'2026-10-19','tests':{},'scope':'Two local Unity Editors; artificial60ms one-way and20% input/frame omissions; no WAN, four-player or release rebuild.'}
for name,count in [('broadcaster-offline.txt',17),('broadcaster-special-regression.txt',31),('broadcaster-row-regression.txt',45),('broadcaster-connected/host.txt',9),('broadcaster-connected/client.txt',9)]:
 s=(D/'evidence'/name).read_text(encoding='utf-8-sig');assert s.count('PASS ')==count and 'FAIL ' not in s,name;q['tests'][name]=count
hashes=json.loads((D/'evidence/broadcaster-source-hashes.json').read_text(encoding='utf-8'))
for rel,h in hashes.items():assert hashlib.sha256((R/rel).read_bytes()).hexdigest()==h,rel
q['source_hashes_match']=True
bad=[]
for rel in re.findall(r'!?\[[^\]]*\]\(([^)]+)\)',(D/'README.md').read_text(encoding='utf-8')):
 if '://' not in rel and not rel.startswith('#') and '*' not in rel and not (D/rel.split('#')[0]).exists():bad.append(rel)
assert not bad,bad;q['readme_relative_links_valid']=True
for rel in re.findall(r'(?:src|href)="([^"]+)"',(D/'index.html').read_text(encoding='utf-8')):
 assert '://' in rel or (D/rel).exists(),rel
q['html_relative_links_valid']=True
a=(D/'ChangeReport_20261019.docx').read_bytes();b=(W/'outputs/SpecialItemDesignReport.docx').read_bytes();assert a==b
q['report_output_identical']=True;q['report_sha256']=hashlib.sha256(a).hexdigest()
render=W/'work/broadcaster-word-render';pages=sorted(render.glob('page-*.png'));assert len(pages)==11
reader=PdfReader(render/'report.pdf');assert len(reader.pages)==11
assert '4' in reader.pages[9].extract_text() and '13' in reader.pages[9].extract_text()
q['report_pages']=11;q['visual_qa']='All11 final page PNGs opened and inspected: Korean, tables, images, captions and case06 page breaks fit without clipping.'
q['renderer']='Packaged render_docx.py attempted: LibreOffice unavailable, temp cleanup access error. Read-only hidden Word PDF export and bundled Poppler succeeded.'
q['main_console_errors_after_return']=0;q['main_state']='Fresh Play; Show Five Item Row';q['temporary_clone_17060']='Stopped after process path and dedicated log identity verified.'
q['focused_diff_check']='Relevant gameplay and Editor scripts passed. Full pre-existing scene diff has Unity serialized trailing spaces; unrelated content preserved.'
(D/'evidence/broadcaster-final-qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(render/'report.pdf',D/'evidence/broadcaster-report.pdf')
shutil.copy2(W/'work/render_broadcaster.ps1',D/'source/render_broadcaster.ps1')
shutil.copy2(__file__,D/'source/verify_broadcaster.py')
p=D/'README.md';s=p.read_text(encoding='utf-8');s+='\n문서 최종 검수: 누적 Word 11쪽을 읽기 전용 Word PDF와 Poppler PNG로 출력해 전 페이지를 열어 확인했다. 한글·표·이미지·캡션 잘림 없음. 기본 render_docx.py는 LibreOffice 부재와 임시 폴더 권한 오류로 실패했으며 대체 출력은 성공했다. [문서 QA](evidence/broadcaster-final-qa.json) · [렌더러 로그](evidence/renderer-broadcaster.log) · [검수 PDF](evidence/broadcaster-report.pdf). 메인 Unity는 새 Play 공원5종 앞으로 복귀했고 Console 오류0, 검사용 숨김 참가자17060은 종료했다. 커밋·푸시는 하지 않았다.\n';p.write_text(s,encoding='utf-8')
p=R/'NEXT_SESSION.txt';s=p.read_text(encoding='utf-8');p.write_text('인계 2026-10-19 방송기4초·연출 강화 완료. 메인 새 Play 공원5종 앞, 2번 슬롯 방송기·좌클릭 사용. 검사 clone17060 종료. 보고서11쪽 전 페이지 검수, 소스해시·링크·출력동일 확인. 커밋·푸시 없음.\n'+s,encoding='utf-8')
print(json.dumps(q,ensure_ascii=False,indent=2))
