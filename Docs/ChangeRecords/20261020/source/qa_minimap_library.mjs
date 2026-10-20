import {createRequire} from 'node:module';
import fs from 'node:fs';
const require=createRequire('C:/Users/xlfhf/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_qa.js');
const {chromium}=require('playwright');
const root='C:/Users/xlfhf/Documents/Codex/2026-10-20/dbsl';
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
try{
 const page=await browser.newPage({viewport:{width:1440,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+root+'/outputs/MinimapBugReport_20261020.html');
 await page.locator('table').nth(0).screenshot({path:root+'/work/minimap-report-matrix-table.png'});
 await page.locator('table').nth(1).screenshot({path:root+'/work/minimap-report-peer-table.png'});
 await page.goto('file:///'+root+'/outputs/MonsterDeliveryLibrary.html',{waitUntil:'load'});
 await page.getByText('최신 · 미니맵 방향 버그 원인과 수정 및 실제 게스트 검증',{exact:true}).first().click();
 await page.getByRole('dialog').waitFor();
 await page.getByRole('dialog').screenshot({path:root+'/work/minimap-library-detail.png'});
 const tables=await page.getByRole('dialog').locator('table').count();
 const body=await page.getByRole('dialog').innerText();
 if(tables!==2||!body.includes('0.0002729575')||!body.includes('6428')&&!body.includes('6,428'))throw new Error('Report detail lost numeric tables');
 const download=page.getByRole('dialog').getByRole('link',{name:'원본 다운로드'});
 const href=await download.getAttribute('href');if(!href?.startsWith('blob:'))throw new Error('Embedded report asset unresolved');
 if(errors.length)throw new Error(errors.join('\n'));
 fs.writeFileSync(root+'/work/library_browser_qa.json',JSON.stringify({pageErrors:errors,reportTables:tables,embeddedDownloadResolved:true,source:'local HTML',screenshots:['minimap-report-matrix-table.png','minimap-report-peer-table.png','minimap-library-detail.png']},null,2));
 console.log('Browser QA passed: report tables and embedded download work; no page errors');
}finally{await browser.close();}
