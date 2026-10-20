from pathlib import Path
import csv,json,re,math,html,zipfile,hashlib,shutil
W=Path('C:/Users/xlfhf/Documents/Codex/2026-10-20/dbsl');O=W/'outputs'
R=Path('C:/Users/xlfhf/Documents/Codex/2026-09-18/https-github-com-kangminsu123-monsterdeliveryservice-git/MonsterDeliveryService');D=R/'Docs/ChangeRecords/20261020';E=D/'evidence'
stats={};logs={}
for role in ['host','guest']:
 logs[role]=(E/f'minimap-connected/{role}.txt').read_text(encoding='utf8');assert 'FAIL ' not in logs[role]
 rows=list(csv.DictReader((E/f'minimap-connected/{role}.csv').open(encoding='utf-8-sig')))
 a=[{k:v if k=='phase' else float(v) for k,v in row.items()} for row in rows]
 stationary=[r for r in a if 1<=r['elapsed_s']<6.5];moving=[r for r in a if r['phase']=='move']
 stats[role]={'pass_count':logs[role].count('PASS '),'samples':len(a),'first_elapsed_s':a[0]['elapsed_s'],'last_elapsed_s':a[-1]['elapsed_s'],'stationary_x_min':min(r['x'] for r in stationary),'stationary_x_max':max(r['x'] for r in stationary),'stationary_z_min':min(r['z'] for r in stationary),'stationary_z_max':max(r['z'] for r in stationary),'walk_phase_xz_displacement_m':math.hypot(moving[-1]['x']-moving[0]['x'],moving[-1]['z']-moving[0]['z']),'max_marker_matrix_error_px':max(r['max_rotation_error_px'] for r in a),'yaw_min':min(r['yaw'] for r in a),'yaw_max':max(r['yaw'] for r in a),'own':int(a[0]['own'])}
rotation=(E/'minimap-rotation-after.txt').read_text(encoding='utf8')
before=re.findall(r'BASELINE scale=(\S+) mapPoint=(\([^\n]+?\)) stationary marker displacement=(\S+) pixels',rotation)
after=re.findall(r'PASS scale=(\S+) mapPoint=(\([^\n]+?\)) fixed anchor max displacement=(\S+)',rotation)
assert len(before)==len(after)==15
md='''# 미니맵 방향 버그 원인과 수정 및 실제 게스트 검증

2026년 10월 20일 최신화 · Unity 6000.5.10f1 · SampleScene · 프로젝트 MonsterDeliveryService

## 결론

방장이 제자리에서 방향을 돌릴 때 미니맵 화살표가 이동하던 표시 버그를 수정했다. 화면 배율과 화살표 회전 중심이 어긋난 것이 원인이며, 가장자리 제한 전후의 좌표 불일치도 함께 수정했다. 게스트는 같은 표시 코드를 사용하므로 공통 수정이 적용된다. 실제 두 Unity Editor를 연결한 검사에서 방장과 게스트 모두 제자리 회전의 XZ 변화 0m를 확인했다. 실제 걷기에서는 위치가 정상적으로 이동했다. 게스트 전용 게임 코드의 추가 수정은 필요하지 않았다.

## 신고 내용과 확인한 원인

사용자는 멀티에서 내 위치 좌표가 계속 바뀐다고 신고했고, 확인 질문에 방향을 돌릴 때 화살표 위치도 움직이며 방장에서 발생한다고 답했다. 이후 게스트에서도 같은 문제가 있는지 확인을 요청했다.

미니맵은 실제 로컬 Player Transform을 계속 참조했다. 발견한 원인은 다른 플레이어의 좌표가 섞이는 문제가 아니라 표시 변환이었다. 기존 코드는 1920×1080 논리 좌표를 화면 배율로 줄이거나 키운 뒤, 같은 논리 좌표를 GUIUtility.RotateAroundPivot의 회전 중심으로 넘겼다. Unity 공식 구현의 행렬 곱 순서에서는 회전이 기존 배율 앞에 적용되어 배율 1이 아닐 때 화살표가 자신의 화면상 중심을 기준으로 돌지 않았다.

또한 가장자리에서는 아이콘 중심을 지도 안쪽으로 제한했지만 회전 중심은 제한 전 좌표였다. 이 때문에 배율 1에서도 가장자리 회전 시 중심이 이동할 수 있었다.

## 수정한 처리

- DeliveryMinimap.OnGUI에서 GUIUtility.RotateAroundPivot 호출을 생산 코드 PlayerMarkerMatrix로 교체했다.
- 행렬 순서를 배율 × 중심 이동 × 회전 × 중심 복귀로 바꿔 논리 좌표에서 회전한 뒤 화면 배율을 적용한다.
- MarkerCenter를 공통으로 사용하여 실제 그리는 중심과 회전 중심에 같은 가장자리 제한을 적용한다.
- 화살표 크기는 기존 22 논리 픽셀을 유지한다. 가장자리 중심 여백은 아이콘 절반인 11 논리 픽셀이다.
- 월드 좌표, 이동 속도, 카메라, 서버 보정, 스냅샷 형식과 프로토콜15는 이번 수정으로 바꾸지 않았다.

공식 구현 확인 출처: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUIUtility.cs

## 회전 중심 검사 조건과 전체 수치

MinimapRotationCheck에서 실제 생산 코드 PlayerMarkerMatrix를 호출했다. 배율은 0.5, 2/3, 1, 4/3, 2의 5종이다. 위치는 정규화 좌표 중앙 (0.5,0.5), 좌상단 (0,0), 우하단 (1,1)의 3곳이다. 각 조합에서 0°,45°,90°,180°,270°,359°의 6개 방향을 검사했다. 5×3=15개 조합 판정, 총 90회 회전이 통과했다.

아래 수정 전 값은 원래 행렬 곱 순서와 제한 전 회전 중심을 재현한 계산값이다. 실제 사용자 화면에서 픽셀을 검출한 값이 아니다. 검사 사각형은 (1600,70,254,254) 논리 좌표이며, 배율별 실제 창 크기를 바꿔 촬영한 실험으로 해석하면 안 된다. 수정 후 값 역시 화면 픽셀 단위로 환산한 표시 행렬의 중심 오차다.

| UI 배율 | 지도 정규화 위치 | 수정 전 최대 중심 이동 계산값(px) | 수정 후 최대 중심 오차(px) |
|---|---|---|---|
'''
for b,a in zip(before,after):
 assert b[:2]==a[:2]
 md+=f'| {b[0]} | {b[1]} | {b[2]} | {a[2]} |\n'
md+='''
수정 후 15개 조합의 최대 중심 오차는 0.0002516544px다. 소수 계산 오차 수준이며 가장자리에서도 화살표 중심은 유지됐다. 이 검사의 Console 오류는 0이다.

## 실제 방장과 게스트 연결 검사

두 개의 독립 Unity Editor 프로세스를 NGO/UTP localhost로 연결했다. 메인 Editor가 방장이고 기존 가상 프로젝트 mppmaddd9a33이 게스트다. 검사용 입력을 실제 TickNetwork에 주입했으며, 일반 사용자가 직접 조작한 WAN 실전 경기를 기록한 것은 아니다.

- 인위적 지연: 편도 60ms. 설정상 왕복 지연 추가량은 약120ms이며 실제 RTT 실측값이 아니다.
- 메시지 누락: 송신 경로에서 5번째 입력 또는 상태 메시지마다 하나를 누락하는 결정적 20% 조건. 전송 계층이 알아서 손실시킨 패킷 비율의 실측은 아니다.
- 실제로 누락이 발생했는지 양쪽 카운터가 0보다 큰지 검사해 통과했다. 정확한 누락 건수는 이번 로그에 저장하지 않아 제시하지 않는다.
- 각 피어의 표본 수집 구간은 약16초다. 0~7초와 9~16초는 회전, 7~9초는 W 전진 입력을 주입했다. 정지 판정은 초기 안정화 시간을 제외한 1~6.5초 구간이다.
- 각 표본에서 5개 화면 배율로 생산 표시 행렬 중심을 검사했다. 실제 화면의 화살표 픽셀을 영상 추적한 검사는 아니다.

| 측정 항목 | 방장 | 게스트 |
|---|---|---|
'''
H=stats['host'];G=stats['guest']
def row(label,key,fmt=str):
 global md
 md+=f'| {label} | {fmt(H[key])} | {fmt(G[key])} |\n'
row('통과 항목','pass_count');row('글로벌 본인 슬롯','own');md+='| 미니맵 참조 | 로컬 Player Transform | 로컬 Player Transform |\n'
row('실제 수집 표본 수','samples');row('첫 표본 경과 시간(s)','first_elapsed_s');row('마지막 표본 경과 시간(s)','last_elapsed_s')
for label,key in [('정지 구간 X 최솟값(m)','stationary_x_min'),('정지 구간 X 최댓값(m)','stationary_x_max'),('정지 구간 Z 최솟값(m)','stationary_z_min'),('정지 구간 Z 최댓값(m)','stationary_z_max')]:row(label,key)
md+='| 제자리 회전 XZ 최대 이동(m) | 0 | 0 |\n'
row('걷기 입력 구간 XZ 변위(m)','walk_phase_xz_displacement_m',lambda n:f'{n:.9f}')
row('수집 yaw 최솟값(도)','yaw_min');row('수집 yaw 최댓값(도)','yaw_max');row('최대 표시 행렬 중심 오차(px)','max_marker_matrix_error_px')
md+='''| 패킷 누락 실제 발생 | 확인 | 확인 |
| 게임 런타임 및 연결 종료 오류 | 0 | 0 |
| 별도 기록한 UnityEditor.Search 초기화 예외 | 0 | 1 |

표본 수 합계는 6,428개다. 이는 검사 코루틴에서 수집한 상태 표본 수이며 화면 렌더링 FPS나 네트워크 메시지 수가 아니다. 걷기 변위 11.817020841m와 6.486025441m는 서로 다른 스폰 위치·지형·네트워크 보정 조건의 이동 확인 값이며, 속도 비교나 성능 벤치마크가 아니다. XZ는 수평 좌표이고 높이 Y는 이 정지 판정에 포함하지 않았다.

## 양쪽에서 통과한 10개 항목

- 실제 방장과 게스트가 연결되고 경기에 진입함.
- 본인 글로벌 슬롯이 방장0·게스트1로 배정됨.
- 미니맵이 본인의 로컬 Player를 참조하며 게스트의 글로벌→로컬 매핑이 맞음.
- 60개를 넘는 실제 연결 상태 표본을 수집함.
- 회전 입력으로 120도를 넘는 방향 변화를 확인함.
- 제자리 회전 중 실제 XZ 위치 변화가 0.1m 미만임. 실제 결과는 양쪽0m.
- 5개 배율에서 화살표 중심 오차가 0.002px 미만임. 실제 최대0.0002729575px.
- 걷기 입력으로 실제 캐릭터와 미니맵 참조가 1m를 넘게 이동함.
- 테스트 메시지 누락 카운터가 0보다 커 실제 누락이 발생함.
- 게임 런타임·연결 종료 오류0을 확인함.

방장10항목과 게스트10항목은 같은 검사를 각 피어에서 수행한 결과다. 서로 다른 고유 기능20개를 검증했다는 뜻은 아니다.

## 재시도와 에디터 환경 문제

기존 가상 프로젝트에서 ProBuilder와 Settings Manager 로컬 패키지 연결 두 개가 빠져 복구했다. 에셋 재임포트 과정의 패키지 경로 오류와 검색 초기화 예외가 게스트 실행을 지연시켰다. UnityEditor.Search의 ArgumentOutOfRangeException이 게스트의 자동 일시정지를 유발하여, 검사용 명령줄 플래그가 있는 실행에서만 재개하도록 검사 도구를 보완했다. 최종 게스트에도 이 에디터 초기화 예외 1건은 별도로 남아 있으며 게임 오류0과 혼동하지 않는다.

첫 연결 검사에서는 인위적 지연은 켜졌지만 loss 라벨이 없어 누락이 발생하지 않았다. 해당 결과는 normal-attempt 폴더에 보존했다. 설정 순서를 정리하고 라벨을 minimap-loss로 바꿔 재실행한 최종 로그에서는 양쪽 누락 검사가 통과했다. 최종 결과만 위 표에 사용했다.

## 확인 범위와 미실행 항목

확인 완료: 공통 미니맵 표시 수정, Editor의 생산 표시 행렬, 실제 localhost 두 프로세스의 본인 슬롯·회전·걷기·지연·누락·종료 검증.

미실행: WAN, 서로 다른 실제 PC, 사용자 화면 해상도를 그대로 재현한 영상 픽셀 추적, macOS 실기, 4인 연결, 배포 실행 파일 재빌드. 이미 배포한 Windows/macOS 패키지에는 이번 미니맵 수정이 포함되지 않는다. 프로토콜15라는 번호만 같다고 이 수정이 들어 있는 것은 아니다.

## 변경 파일과 검사 원본

- 게임 수정: Assets/Gameplay/DeliveryMinimap.cs — MarkerCenter와 PlayerMarkerMatrix, OnGUI 회전 처리.
- 단독 검사: Assets/Editor/MinimapRotationCheck.cs.
- 실제 연결 검사: Assets/Editor/MinimapGuestCheck.cs.
- 행렬 전체 로그: minimap-rotation-after.txt.
- 최종 피어 로그: host.txt, guest.txt.
- 전체6,428표본 좌표·방향·지도좌표·오차: host.csv, guest.csv.
- 앞선 실패/재시도 원본: normal-attempt 폴더.

자료실의 별도 ‘미니맵 검증 원본 로그와 소스’ 항목에서 위 원본과 수정 당시 소스를 ZIP으로 내려받을 수 있다. 실제 미니맵 전후 스크린샷은 이번 검사에서 촬영하지 않았다. 설명용 이미지를 실제 게임 화면인 것처럼 추가하지 않았다.
'''
(O/'MinimapBugReport_20261020.md').write_text(md,encoding='utf8')
# The library's own content renderer supports headings, lists and tables.
parts=[];lines=md.splitlines();i=0
while i<len(lines):
 line=lines[i];i+=1
 if not line.strip():continue
 if line.startswith('|'):
  tab=[line]
  while i<len(lines) and lines[i].startswith('|'):tab.append(lines[i]);i+=1
  cells=[x.strip().strip('|').split('|') for x in tab if not re.match(r'^\|[\s:|\-]+\|$',x)]
  parts.append('<div class="table"><table>'+''.join('<tr>'+''.join(f'<{"th" if n==0 else "td"}>{html.escape(c.strip())}</{"th" if n==0 else "td"}>' for c in row)+'</tr>' for n,row in enumerate(cells))+'</table></div>');continue
 if line.startswith('#'):
  level=min(3,len(line)-len(line.lstrip('#')));parts.append(f'<h{level}>{html.escape(line.lstrip("# "))}</h{level}>')
 else:parts.append('<p>'+html.escape(line.removeprefix('- '))+'</p>')
standalone='<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>미니맵 방향 버그 수정과 검증</title><style>body{font:16px/1.8 "Malgun Gothic",sans-serif;color:#172337;background:#f4f6fa;margin:0}main{max-width:1120px;margin:auto;background:white;padding:48px}h1{font-size:32px;line-height:1.4}h2{margin-top:40px;border-bottom:1px solid #ccd5e2;padding-bottom:8px}table{border-collapse:collapse;width:100%;font-size:14px}th,td{border:1px solid #ccd5e2;padding:10px;text-align:left}th{background:#e8eef6}tbody tr:nth-child(even){background:#fafbfd}.table{overflow:auto}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:13px;background:#f1f4f8;padding:20px}details{margin:20px 0}summary{cursor:pointer;font-weight:bold}@media(max-width:700px){main{padding:20px}h1{font-size:25px}}@media print{main{padding:0}body{background:white}h2{break-after:avoid}tr{break-inside:avoid}}</style><main>'+''.join(parts)
for title,body in [('전체 회전 검사 원문',rotation),('방장 검사 원문',logs['host']),('게스트 검사 원문',logs['guest'])]:standalone+='<details><summary>'+title+'</summary><pre>'+html.escape(body)+'</pre></details>'
standalone+='</main></html>'
(O/'MinimapBugReport_20261020.html').write_text(standalone,encoding='utf8')
(W/'work/minimap_report_stats.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding='utf8')
with zipfile.ZipFile(O/'MinimapEvidence_20261020.zip','w',zipfile.ZIP_DEFLATED) as z:
 for file in [E/'minimap-rotation-after.txt',*(E/'minimap-connected').glob('*.txt'),*(E/'minimap-connected').glob('*.csv'),*(E/'minimap-connected/normal-attempt').glob('*')]:z.write(file,'evidence/'+file.relative_to(E).as_posix())
 for file in [R/'Assets/Gameplay/DeliveryMinimap.cs',R/'Assets/Editor/MinimapRotationCheck.cs',R/'Assets/Editor/MinimapGuestCheck.cs']:z.write(file,'source/'+file.name)
 z.write(O/'MinimapBugReport_20261020.md','MinimapBugReport_20261020.md');z.write(W/'work/minimap_report_stats.json','derived-stats.json')
for name in ['MinimapBugReport_20261020.md','MinimapBugReport_20261020.html','MinimapEvidence_20261020.zip']:shutil.copy2(O/name,D/name)
print(json.dumps({'report_chars':len(md),'stats':stats},ensure_ascii=False))
