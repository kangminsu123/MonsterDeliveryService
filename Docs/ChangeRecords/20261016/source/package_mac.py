from pathlib import Path
import datetime,hashlib,json,os,plistlib,stat,struct,zipfile

W=Path('C:\\Users\\xlfhf\\Documents\\Codex\\2026-10-15\\agents-md')
P=Path(r'C:\Users\xlfhf\Documents\Codex\2026-09-18\https-github-com-kangminsu123-monsterdeliveryservice-git\MonsterDeliveryService')
O=W/'outputs';D=P/'Docs/ChangeRecords/20261016'
B=O/'MonsterDeliveryService_macOS_20261016';app=B/'MonsterDeliveryService.app'

def macho(path):
    with path.open('rb') as f:
        head=f.read(8)
        if head[:4] in [b'\xca\xfe\xba\xbe',b'\xca\xfe\xba\xbf']:
            count=struct.unpack('>I',head[4:])[0]
            records=f.read(count*(32 if head[:4]==b'\xca\xfe\xba\xbf' else 20))
            step=32 if head[:4]==b'\xca\xfe\xba\xbf' else 20
            cpus=[struct.unpack_from('>I',records,i*step)[0] for i in range(count)]
        elif head[:4] in [b'\xcf\xfa\xed\xfe',b'\xce\xfa\xed\xfe']:
            cpus=[struct.unpack('<I',head[4:])[0]]
        else:return []
    return [{0x1000007:'x86_64',0x100000c:'arm64'}.get(c,hex(c)) for c in cpus]

info=plistlib.loads((app/'Contents/Info.plist').read_bytes())
exe=app/'Contents/MacOS'/info['CFBundleExecutable']
assert set(macho(exe))=={'x86_64','arm64'}
native={p.relative_to(app).as_posix():macho(p) for p in app.rglob('*') if p.is_file() and not p.is_symlink()}
native={p:arch for p,arch in native.items() if arch}
assert native and all(set(arch)=={'x86_64','arm64'} for arch in native.values()),native
managed=list(app.rglob('Assembly-CSharp.dll'));assert len(managed)==1
assert list(app.rglob('globalgamemanagers')) or list(app.rglob('data.unity3d'))

guide='''# Monster Delivery Service 맥 개발 테스트 빌드

2026-10-16 · Unity 6000.5.10f1 · macOS Universal (Intel x86_64 + Apple Silicon arm64) · Mono 일반 빌드 · Protocol3

ZIP 전체를 맥에서 압축 해제한 뒤 MonsterDeliveryService.app을 실행하세요. 앱 안의 Contents 폴더는 그대로 유지하세요. macOS 11 이상으로 빌드되었습니다. Apple Developer ID 서명과 공증을 수행하지 않은 개발 배포본이라 처음 실행할 때 macOS의 확인이 필요할 수 있습니다. 실제 맥에서 실행과 프레임 검사는 아직 하지 않았습니다.

1. 1대1(2명) 또는 2대2(4명)를 선택하고 닉네임을 입력합니다.
2. 방장은 테스트방을 만들고, 다른 참가자는 방장 IPv4 또는 방 코드를 입력합니다.
3. 같은 PC는 127.0.0.1, 같은 LAN은 방장 로컬 IPv4, Tailscale 테스트는 같은 VPN에 있는 방장 Tailscale IPv4를 사용합니다. UDP7777 연결이 필요합니다.
4. 전원이 Protocol3 최신 빌드를 사용해야 합니다. 이번 Windows UI_NetworkFix 패키지와 같은 게임 소스로 만들었습니다. Windows와 Mac 사이 실제 접속 검사는 아직 하지 않았습니다.

한 맥에서 여러 창: Test_1v1.command 또는 Test_2v2.command를 실행합니다. 각각 2개/4개 앱을 열며 창을 전환해 조작합니다. 다른 테스트 전에 이전 게임 창을 모두 닫으세요. 수동으로는 Terminal에서 다음처럼 실행할 수 있습니다.

open -n "/앱이 있는 경로/MonsterDeliveryService.app" --args -delivery-host -delivery-mode 1v1 -delivery-name Host
open -n "/앱이 있는 경로/MonsterDeliveryService.app" --args -delivery-join 127.0.0.1 -delivery-name Player2

WASD 이동 / 마우스 시점·좌클릭 사용 / Space 점프 / C 웅크리기 / Shift 대쉬 / E 획득·3초 유지 점령 / 1~3 슬롯 / G 폐기 / Esc 메뉴 / F2 개발 아이템 도구

옷색은 중복 없이 자동 배정되며 초상화도 같은 색입니다. 1대1은 플레이어 이름과 개인 점령색, 2대2는 팀 기준을 표시합니다. 택배는 풀밭을 제외한 50곳 중30곳에 생성됩니다. 참가자는 로컬 이동 예측과 서버 확인 후 보정을 사용합니다.

검사 범위: Unity 맥 빌드 성공, 앱 구조·주요 네이티브 바이너리의 두 아키텍처·ZIP 실행 권한·CRC 검증. 앞선 UI·네트워크 실제 플레이 검사는 Windows에서 수행했습니다. Mac 실기 실행, Windows↔Mac 연결, 다른 물리PC/LAN·Tailscale·WAN 실측은 남아 있습니다. BUILD_INFO.json과 같은 날 보고서 사례03에 근거를 기록했습니다.
'''
# Use the generated minimum version rather than assume the installed module default.
guide=guide.replace('macOS 11 이상',f"macOS {info.get('LSMinimumSystemVersion','미확인')} 이상")
(B/'README_macOS.md').write_text(guide,encoding='utf-8')
for mode,count in [('1v1',2),('2v2',4)]:
    lines=['#!/bin/bash','set -e','scriptdir="$(cd -- "$(dirname -- "$0")" && pwd)"',
           f'open -n "$scriptdir/MonsterDeliveryService.app" --args -delivery-host -delivery-mode {mode} -delivery-name Host','sleep 2']
    lines += [f'open -n "$scriptdir/MonsterDeliveryService.app" --args -delivery-join 127.0.0.1 -delivery-name Player{i}' for i in range(2,count+1)]
    (B/f'Test_{mode}.command').write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')

data={'date':'2026-10-16','engine':'6000.5.10f1','platform':'macOS Universal','configuration':'Release Mono LZ4','network_protocol':3,
      'minimum_macos':info.get('LSMinimumSystemVersion'),'bundle_id':info.get('CFBundleIdentifier'),'bundle_executable':info['CFBundleExecutable'],
      'architectures':['x86_64','arm64'],'native_binaries':native,'assembly_sha256':hashlib.sha256(managed[0].read_bytes()).hexdigest(),
      'launcher_sha256':hashlib.sha256(exe.read_bytes()).hexdigest(),'base_commit':'7dbb9f1','current_changes_committed':False,
      'apple_developer_id_signing':'not performed','notarization':'not performed','mac_runtime_test':'not run; Windows build host',
      'cross_platform_network_test':'not run','note':'ZIP explicitly stores UNIX execute bits for Mach-O files and .command launchers.'}
(B/'BUILD_INFO.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
out=O/'MonsterDeliveryService_macOS_20261016.zip'
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in sorted(B.rglob('*')):
        if any('DoNotShip' in part for part in p.parts):continue
        rel=(Path(B.name)/p.relative_to(B)).as_posix()
        zi=zipfile.ZipInfo(rel+('/' if p.is_dir() and not p.is_symlink() else ''))
        zi.date_time=datetime.datetime.fromtimestamp(p.lstat().st_mtime).timetuple()[:6]
        zi.create_system=3;zi.compress_type=zipfile.ZIP_DEFLATED
        if p.is_symlink():
            zi.external_attr=(stat.S_IFLNK|0o777)<<16;content=os.readlink(p).encode()
        elif p.is_dir():
            zi.external_attr=((stat.S_IFDIR|0o755)<<16)|0x10;content=b''
        else:
            isexec=p.suffix=='.command' or bool(macho(p))
            zi.external_attr=(stat.S_IFREG|(0o755 if isexec else 0o644))<<16
            content=p.read_bytes()
        z.writestr(zi,content)
with zipfile.ZipFile(out) as z:
    assert z.testzip() is None
    for rel in list(native)+['../Test_1v1.command','../Test_2v2.command']:
        path=(Path(B.name)/app.name/rel).as_posix() if not rel.startswith('../') else B.name+'/'+rel[3:]
        assert z.getinfo(path).external_attr>>16 & 0o111,path
    entries=len(z.namelist())
verification={'file':out.name,'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'entries':entries,
              'crc_check':'PASS','universal_native_binaries':len(native),'all_native_binaries_universal':'PASS','unix_executable_modes':'PASS',
              'mac_runtime':'NOT RUN','windows_mac_network':'NOT RUN','bundle':data}
(D/'evidence/package-verification-macos.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(verification,ensure_ascii=False,indent=2))
