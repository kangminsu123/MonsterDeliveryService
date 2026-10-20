from pathlib import Path
exec(Path(r'C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/work/blender_special_lib.py').read_text(encoding='utf-8'))
yellow=mat('SafetyYellow',(1,.58,.018));blue=mat('CannonBlue',(.075,.24,.58));dark=mat('Hardware',(.028,.046,.073),.25,.32);silver=mat('Metal',(.45,.55,.66),.72,.28);white=mat('Ivory',(.9,.88,.70),0,.40);red=mat('SignalRed',(.78,.028,.035));orange=mat('JacketOrange',(1,.19,.018),0,.48);cyan=mat('MachineTeal',(.008,.55,.57));purple=mat('MachinePurple',(.28,.055,.56));tan=mat('Cardboard',(.56,.28,.09),0,.72);tape=mat('TapeCream',(.88,.73,.43),0,.43);cobalt=mat('BatteryBlue',(.015,.13,.68),.05,.3);black=mat('Webbing',(.012,.019,.032),0,.78);amber=mat('BeaconAmber',(1,.24,.008),0,.25,1.4);ice=mat('EnergyCyan',(.06,.78,1),0,.26,1.6)
def assign(name,material):
 o=bpy.data.objects[name];o.data.materials.clear();o.data.materials.append(material)
def finish(kind):
 r=bpy.data.objects['HERO_'+kind]
 if kind=='AirCannon':
  for name,m in [('PressureHousing',yellow),('OpenMuzzle',blue),('AirTank',yellow),('CarryHandle',yellow),('TankSaddle',dark),('HandlePost_-0.28',dark),('HandlePost_0.28',dark)]:assign('PRP_Cannon_'+name,m)
  tank=bpy.data.objects['PRP_Cannon_AirTank']
  for v in tank.data.vertices:v.co.z-=.04
  for y in [-.22,.3]:lathe('PRP_Cannon_TankBand_'+str(y),[(y-.045,.235),(y+.045,.235),(y+.045,.219),(y-.045,.219)],(0,0,.23),material=dark,parent=r,n=24)
  lathe('PRP_Cannon_BoreBack',[(-.265,.248),(-.26,.248)],(0,0,.96),material=dark,parent=r,n=24,closed=False)
  lathe('PRP_Cannon_GaugeRim',[(-.045,.14),(.035,.14),(.035,.115),(-.045,.115)],(.28,-.42,.48),material=silver,parent=r,n=24)
  lathe('PRP_Cannon_GaugeFace',[(-.046,.113),(-.032,.113)],(.28,-.42,.48),material=white,parent=r,n=24,closed=False)
  needle=prism('PRP_Cannon_GaugeNeedle',[(.27,.47),(.30,.47),(.35,.56)],-.470,-.466,red,r)
  tube('PRP_Cannon_AirHose',[(.17,.43,.22),(.43,.44,.22),(.53,.40,.37),(.51,.28,.60),(.34,.2,.68)],.045,dark,r)
  box('PRP_Cannon_FireButton',(.26,.25,1.34),(.14,.15,.10),red,r,.025)
  for x in [-.33,.33]:lathe('PRP_Cannon_Fastener_'+str(x),[(-.36,.055),(-.32,.055)],(x,0,.84),material=silver,parent=r,n=8,closed=False)
 elif kind=='Broadcaster':
  assign('PRP_Broadcaster_Horn',white);assign('PRP_Broadcaster_RearHousing',red);assign('PRP_Broadcaster_Grip',dark);assign('PRP_Broadcaster_MouthRim',red);assign('PRP_Broadcaster_Beacon',amber)
  lathe('PRP_Broadcaster_CenterCone',[(-.62,.025),(-.48,.095),(-.12,.115)],(0,0,1.02),material=red,parent=r,n=24,closed=False)
  lathe('PRP_Broadcaster_BeaconBase',[(0,.17),(.08,.17)],(0,.29,1.24),'Z',dark,r,n=20,closed=False)
  box('PRP_Broadcaster_Trigger',(0,.10,.65),(.13,.10,.18),red,r,.025)
  for z in [.21,.31,.41]:box('PRP_Broadcaster_GripInset_'+str(z),(0,.105,z),(.20,.025,.025),black,r,.008)
  lathe('PRP_Broadcaster_RearCap',[(.52,.13),(.58,.13)],(0,0,1.02),material=dark,parent=r,n=16,closed=False)
 elif kind=='PackingMachine':
  for side in [-1,1]:
   assign('PRP_Packer_Frame_'+str(side),cyan);assign('PRP_Packer_TapeRoll_'+str(side),tape);assign('PRP_Packer_ReelBracket_'+str(side*.48),silver)
   lathe('PRP_Packer_ReelHub_'+str(side),[(-.17,.115),(.24,.115)],(side*.47,.22,1.22),material=purple,parent=r,n=16,closed=False)
   lathe('PRP_Packer_Axle_'+str(side),[(-.20,.052),(-.17,.052)],(side*.47,.22,1.22),material=silver,parent=r,n=8,closed=False)
   for z in [.25,.38,.51]:box('PRP_Packer_Vent_'+str(side)+'_'+str(z),(side*.68,-.575,z),(.17,.015,.035),dark,r,.01)
   box('PRP_Packer_Foot_'+str(side),(side*.68,0,.055),(.29,1.18,.11),dark,r,.025)
  assign('PRP_Packer_Conveyor',dark);assign('PRP_Packer_UpperBridge',purple);assign('PRP_Packer_Parcel',tan)
  box('PRP_Packer_ParcelTapeTop',(0,-.03,.919),(.09,.66,.012),tape,r)
  box('PRP_Packer_ParcelTapeFront',(0,-.36,.64),(.09,.014,.55),tape,r)
  for y in [-.48,-.33,-.18,.0,.18,.33,.48]:lathe('PRP_Packer_Roller_'+str(y),[(-.58,.057),(.58,.057)],(0,y,.39),'X',silver,r,n=12,closed=False)
  box('PRP_Packer_StatusButton',(.68,-.36,.82),(.16,.16,.07),yellow,r,.025)
  box('PRP_Packer_CuttingHead',(0,.33,1.02),(.30,.34,.28),purple,r,.04)
  box('PRP_Packer_ParcelLabel',(.17,-.369,.72),(.18,.012,.12),white,r,.01)
 elif kind=='LifeJacket':
  for name in ['LeftFloat','RightFloat']:assign('PRP_Jacket_'+name,orange)
  assign('PRP_Jacket_BackBridge',black)
  for z in [.43,.78]:
   assign('PRP_Jacket_Webbing_'+str(z),black)
   prism('PRP_Jacket_BuckleFrame_'+str(z),[(-.105,z-.075),(.105,z-.075),(.105,z+.075),(-.105,z+.075)],-.29,-.26,silver,r,.018)
   box('PRP_Jacket_BuckleInset_'+str(z),(-.025,-.305,z),(.10,.025,.09),dark,r,.01)
  for side in [-1,1]:
   strip=[(side*.27,1.15),(side*.46,1.15),(side*.38,1.51),(side*.24,1.53)]
   prism('PRP_Jacket_Reflector_'+str(side),strip,-.222,-.204,white,r,.012)
   tube('PRP_Jacket_Piping_'+str(side),[(side*.54,-.19,.15),(side*.55,-.19,.95),(side*.46,-.19,1.38),(side*.39,-.19,1.55)],.016,yellow,r)
  box('PRP_Jacket_Whistle',(.44,-.28,.98),(.065,.09,.16),yellow,r,.018)
  tube('PRP_Jacket_WhistleCord',[(.4,-.24,1.18),(.49,-.27,1.1),(.44,-.28,1.01)],.008,black,r)
 elif kind=='ExpressBattery':
  assign('PRP_Battery_Case',cobalt);assign('PRP_Battery_TopGuard',yellow);assign('PRP_Battery_BottomGuard',yellow)
  assign('PRP_Battery_Terminal_-1',red);assign('PRP_Battery_Terminal_1',dark)
  for side in [-1,1]:
   lathe('PRP_Battery_TerminalRing_'+str(side),[(1.14,.17),(1.19,.17)],(side*.29,0,0),'Z',silver,r,n=12,closed=False)
   for z in [.28,.52,.76]:box('PRP_Battery_SideRib_'+str(side)+'_'+str(z),(side*.58,0,z),(.075,.73,.085),blue,r,.025)
   for y in [-.34,.34]:box('PRP_Battery_CornerGuard_'+str(side)+'_'+str(y),(side*.51,y,.58),(.12,.14,.94),dark,r,.025)
  prism('PRP_Battery_FrontPanel',[(-.40,.20),(.40,.20),(.44,.28),(.44,.88),(.36,.98),(-.36,.98),(-.44,.88),(-.44,.28)],-.39,-.36,blue,r,.025)
  prism('PRP_Battery_Lightning',[(.07,.95),(-.23,.57),(-.05,.57),(-.14,.25),(.25,.70),(.05,.70)],-.414,-.393,white,r,.009)
  for i in range(3):box('PRP_Battery_ChargeBar_'+str(i),(.48,-.408,.39+i*.14),(.035,.018,.08),ice,r,.006)
 for o in r.children:
  if o.type=='MESH' and not o.name.startswith('PRX_') and not o.modifiers:
   b=o.modifiers.new('Edge chamfer','BEVEL');b.width=.018 if kind not in ['LifeJacket'] else .045;b.segments=2
   if any(p in o.name for p in ['Float','CarryHandle','TopGuard','BottomGuard','Case']):b.width=.055;b.segments=3
 return {'kind':kind,'part_count':len(r.children)}
