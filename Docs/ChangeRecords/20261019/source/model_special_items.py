from pathlib import Path
exec(Path(r'C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/work/blender_special_lib.py').read_text(encoding='utf-8'))
def build_primary(kind):
 r=bpy.data.objects['HERO_'+kind];g=bpy.data.materials['MDS_Blockout']
 if kind=='AirCannon':
  lathe('PRP_Cannon_PressureHousing',[(.52,.25),(.47,.34),(.2,.36),(-.30,.36),(-.34,.32)],(0,0,.96),material=g,parent=r,n=16,closed=False)
  lathe('PRP_Cannon_OpenMuzzle',[(-.32,.35),(-.78,.48),(-.90,.48),(-.90,.37),(-.73,.37),(-.27,.25)],(0,0,.96),material=g,parent=r,n=24)
  lathe('PRP_Cannon_AirTank',[(-.45,.17),(-.38,.23),(.32,.23),(.42,.17)],(0,0,.27),material=g,parent=r,n=16,closed=False)
 elif kind=='Broadcaster':
  lathe('PRP_Broadcaster_Horn',[(.35,.18),(.12,.22),(-.25,.38),(-.60,.53),(-.66,.53),(-.66,.46),(-.26,.31),(.25,.12)],(0,0,1.02),material=g,parent=r,n=32)
  lathe('PRP_Broadcaster_RearHousing',[(.1,.21),(.44,.23),(.54,.16)],(0,0,1.02),material=g,parent=r,n=24,closed=False)
  prism('PRP_Broadcaster_Grip',[(-.12,0),(.12,0),(.22,.15),(.15,.8),(-.10,.85),(-.20,.16)],.12,.42,g,r)
 elif kind=='PackingMachine':
  for side in [-1,1]:
   p=prism('PRP_Packer_Frame_'+str(side),[(-.58,0),(.58,0),(.65,.12),(.65,.74),(.35,.94),(-.25,.90),(-.58,.55)],-.12,.12,g,r);p.rotation_euler.z=math.pi/2;p.location.x=side*.68
  box('PRP_Packer_Conveyor',(0,0,.22),(1.2,1.1,.28),g,r)
  box('PRP_Packer_Parcel',(0,-.03,.64),(.64,.65,.55),g,r)
  for side in [-1,1]:lathe('PRP_Packer_TapeRoll_'+str(side),[(-.12,.28),(.20,.28),(.20,.12),(-.12,.12)],(side*.47,.22,1.22),material=g,parent=r,n=24)
 elif kind=='LifeJacket':
  left=[(-.50,0),(-.61,.10),(-.61,.83),(-.51,1.32),(-.42,1.58),(-.22,1.62),(-.17,1.45),(-.22,1.16),(-.10,.90),(-.06,.08),(-.19,0)]
  prism('PRP_Jacket_LeftFloat',left,-.20,.20,g,r)
  prism('PRP_Jacket_RightFloat',[(-x,z) for x,z in reversed(left)],-.20,.20,g,r)
  box('PRP_Jacket_BackBridge',(0,.17,.95),(.95,.12,.62),g,r)
 elif kind=='ExpressBattery':
  prism('PRP_Battery_Case',[(-.5,0),(.5,0),(.58,.09),(.58,.98),(.48,1.12),(-.48,1.12),(-.58,.98),(-.58,.09)],-.36,.36,g,r)
  for side in [-1,1]:lathe('PRP_Battery_Terminal_'+str(side),[(1.12,.14),(1.31,.14)],(side*.29,0,0),'Z',g,r,n=12,closed=False)
 bpy.data.objects['PRX_HERO_'+kind].hide_render=True;bpy.data.objects['PRX_HERO_'+kind].hide_viewport=True
 return {'kind':kind,'parts':[o.name for o in r.children if not o.name.startswith('PRX_')]}
def build_secondary(kind):
 r=bpy.data.objects['HERO_'+kind];g=bpy.data.materials['MDS_Blockout']
 if kind=='AirCannon':
  for x in [-.28,.28]:box('PRP_Cannon_HandlePost_'+str(x),(x,.16,1.38),(.13,.18,.50),g,r)
  box('PRP_Cannon_CarryHandle',(0,.16,1.59),(.65,.18,.15),g,r)
  box('PRP_Cannon_TankSaddle',(0,.1,.52),(.32,.4,.25),g,r)
 elif kind=='Broadcaster':
  lathe('PRP_Broadcaster_MouthRim',[(-.65,.54),(-.58,.54),(-.58,.46),(-.65,.46)],(0,0,1.02),material=g,parent=r,n=32)
  lathe('PRP_Broadcaster_Beacon',[(0,.14),(.22,.14),(.27,.10)],(0,.29,1.25),'Z',g,r,n=16,closed=False)
 elif kind=='PackingMachine':
  box('PRP_Packer_UpperBridge',(0,.39,1.0),(1.45,.3,.16),g,r)
  for x in [-.48,.48]:box('PRP_Packer_ReelBracket_'+str(x),(x,.23,.96),(.14,.22,.52),g,r)
 elif kind=='LifeJacket':
  for z in [.43,.78]:box('PRP_Jacket_Webbing_'+str(z),(0,-.22,z),(1.2,.08,.12),g,r)
 elif kind=='ExpressBattery':
  box('PRP_Battery_TopGuard',(0,0,1.08),(1.16,.8,.18),g,r)
  box('PRP_Battery_BottomGuard',(0,0,.09),(1.16,.8,.18),g,r)
 return {'kind':kind,'part_count':len(r.children)}
