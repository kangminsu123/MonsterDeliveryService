import fs from 'node:fs';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
const W='C:/Users/xlfhf/Documents/Codex/2026-10-20/dbsl';
const desktop='C:/Users/xlfhf/OneDrive/Desktop/MonsterDeliveryLibrary.html';
const site='C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/work/monster-delivery-hub';
const original=fs.readFileSync(desktop,'utf8');
fs.copyFileSync(desktop,W+'/work/MonsterDeliveryLibrary_before_minimap_20261020.html');
const seedPattern=/(<script type="application\/json" id="library-seed">)([\s\S]*?)(<\/script>)/;
assert(seedPattern.test(original));
const oldEntries=JSON.parse(original.match(seedPattern)[2]);
const report=fs.readFileSync(W+'/outputs/MinimapBugReport_20261020.md','utf8');
const now=new Date().toISOString();
const latest='2026년10월20일 후속 최신 기준: 일반 택배는 접촉 자동 획득하며 자동획득 안내창은 숨겼습니다. 취급주의 택배는 E 수집을 유지하고 금빛 링·12개 반짝임·상승나선·광원5m 밝기3~5를 적용했습니다. 원격 배송기 사거리는18m에서36m로 늘렸습니다. 미니맵은 배율과 가장자리 회전중심을 수정했고 실제 localhost 방장10·게스트10항목을 통과했습니다. 기존 실행 패키지는 이 후속 수정들을 포함하지 않으며 재빌드하지 않았습니다.';
function amend(entries){
 const plan=entries.find(e=>e.id==='current-plan');
 if(plan){plan.content='# 후속 최신 반영\n\n'+latest+'\n\n'+plan.content.replaceAll('18m 조준선','36m 조준선');plan.summary='미니맵 방향 오류 수정과 실제 게스트 검증 완료. 일반 접촉 획득·원격36m·취급주의 광채 강화.';plan.updatedAt=now;}
 const fx=entries.find(e=>e.id==='effects-current-20261020');if(fx){fx.content='# 후속 최신 반영\n\n'+latest+'\n\n아래는 각 작성 시점의 기록입니다. 위 수치와 다르면 위 최신 값을 적용합니다.\n\n'+fx.content;fx.updatedAt=now;}
 const remote=entries.find(e=>e.id==='item-PackingMachine');if(remote){remote.content='# 사거리 후속 최신 반영\n\n현재 사거리는36m입니다. 이전18m의2배이며 조준 검색·로컬/서버 판정·발사선이 같은 값을 씁니다. 이전 설명의18m는 당시 이력입니다.\n\n'+remote.content;remote.summary='36m 조준으로 상대·중립 우체통 즉시 점령. 기존 보호 무시·1회 소모.';remote.updatedAt=now;}
 for(const e of entries.filter(x=>x.id.startsWith('release-'))){e.content='# 후속 수정 포함 여부\n\n이 실행 패키지에는 이후 적용한 접촉 자동 획득·원격배송기36m·취급주의 광채 강화·미니맵 회전 수정이 포함되지 않습니다. 이번에는 실행파일을 다시 빌드하지 않았습니다. 아래는 기존 배포 시점의 기록입니다.\n\n'+e.content;e.summary='기존 배포본 · 후속 미니맵 수정 미포함 · 재빌드 필요';e.updatedAt=now;}
 const added=[{id:'minimap-direction-fix-20261020',title:'최신 · 미니맵 방향 버그 원인과 수정 및 실제 게스트 검증',category:'보고서',summary:'5배율·15조합·90회전 검사, 실제 방장10·게스트10항목 통과. 정지XZ 변화0m, 6,428표본과 전체 수치 공개.',content:report,url:'/library/MinimapBugReport_20261020.html',filename:'MinimapBugReport_20261020.html',image:'',status:'current',tags:'미니맵 방향 회전 위치 버그 방장 게스트 수치 검증 최신',updatedAt:now,deleted:0},{id:'minimap-direction-evidence-20261020',title:'미니맵 검증 원본 로그와 소스',category:'보고서',summary:'방장·게스트 전체6,428표본 CSV, 회전 검사 로그, 재시도 원본과 수정 소스 ZIP.',content:'# 미니맵 검증 원본\n\n최종 host.txt와 guest.txt는 각각10항목 통과입니다. host.csv 3,005개와 guest.csv 3,423개로 전체6,428개 표본을 제공합니다.\n\nZIP에는 생산 코드 DeliveryMinimap.cs, 검사 코드2개, 15조합90회전 로그와 재시도 normal-attempt 원본 및 상세 Markdown 보고서가 포함됩니다.\n\n최종 게임 런타임·연결종료 오류는 양쪽0이며 게스트 UnityEditor.Search 초기화 예외1건은 원본로그에 따로 남겨 두었습니다. 원본표본의 표시 행렬 오차는 화면픽셀 직접 측정값이 아닙니다.',url:'/library/MinimapEvidence_20261020.zip',filename:'MinimapEvidence_20261020.zip',image:'',status:'current',tags:'미니맵 원본 로그 CSV 소스 ZIP 검증',updatedAt:now,deleted:0}];
 for(const e of added){const index=entries.findIndex(x=>x.id===e.id);if(index<0)entries.push(e);else entries[index]=e;}
 return entries;
}
const entries=amend(structuredClone(oldEntries));
let updated=original.replace(seedPattern,(_,open,_old,close)=>open+JSON.stringify(entries).replaceAll('<','\\u003c')+close);
const files=[['MinimapBugReport_20261020.html','text/html'],['MinimapEvidence_20261020.zip','application/zip'],['MinimapBugReport_20261020.md','text/markdown']];
for(const [name,mime]of files){
 const block=`<script type="application/octet-stream" data-library-asset="${name}" data-mime="${mime}">${fs.readFileSync(W+'/outputs/'+name).toString('base64')}</script>`;
 assert(!updated.includes(`data-library-asset="${name}"`));updated=updated.replace('</body>',()=>block+'\n</body>');
}
const output=W+'/outputs/MonsterDeliveryLibrary.html';fs.writeFileSync(output,updated);
const newEntries=JSON.parse(updated.match(seedPattern)[2]);
for(const entry of oldEntries)assert(newEntries.some(e=>e.id===entry.id),'old record lost '+entry.id);
const oldAssets=[...original.matchAll(/<script type="application\/octet-stream" data-library-asset="([^"]+)"[^>]*>([\s\S]*?)<\/script>/g)];
for(const asset of oldAssets){assert(updated.includes(asset[0]),'old embedded asset changed '+asset[1]);}
assert.equal(newEntries.length,oldEntries.length+2);assert.equal(new Set(newEntries.map(e=>e.id)).size,newEntries.length);
assert.equal(newEntries.filter(e=>e.category==='아이템').length,14);
if(fs.existsSync(site+'/lib/seed.json')){
 const source=JSON.parse(fs.readFileSync(site+'/lib/seed.json','utf8'));fs.copyFileSync(site+'/lib/seed.json',W+'/work/library_seed_before_minimap.json');
 fs.writeFileSync(site+'/lib/seed.json',JSON.stringify(amend(source),null,2));
 for(const [name]of files)fs.copyFileSync(W+'/outputs/'+name,site+'/public/library/'+name);
}
const qa={originalRecords:oldEntries.length,updatedRecords:newEntries.length,preservedOriginalAssets:oldAssets.length,updatedAssets:(updated.match(/data-library-asset=/g)||[]).length,itemRecords:14,updatedBytes:Buffer.byteLength(updated),sha256:crypto.createHash('sha256').update(updated).digest('hex'),externalPublished:false};
fs.writeFileSync(W+'/work/library_minimap_qa.json',JSON.stringify(qa,null,2));console.log(JSON.stringify(qa));
