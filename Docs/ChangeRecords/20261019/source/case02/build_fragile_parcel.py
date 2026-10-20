from pathlib import Path
import bmesh
exec(Path(r'C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/work/blender_special_lib.py').read_text(encoding='utf-8'))
r=bpy.data.objects['HERO_FragileParcel']
card=mat('FragileCardboard',(.48,.24,.075),0,.7);lid=mat('FragileLid',(.65,.37,.13),0,.65);yellow=bpy.data.materials['MDS_SafetyYellow'];dark=bpy.data.materials['MDS_Hardware'];red=bpy.data.materials['MDS_SignalRed'];ivory=bpy.data.materials['MDS_Ivory'];silver=bpy.data.materials['MDS_Metal']
def assign(name,m):
 o=bpy.data.objects[name];o.data.materials.clear();o.data.materials.append(m)
assign('PRP_Fragile_CardboardShell',card);assign('PRP_Fragile_Lid',lid)
# The thick carton and lid are independent closed shells; seam and tape stay editable.
box('PRP_Fragile_SealedLidSeam',(0,0,1.252),(.012,1.08,.009),dark,r)
box('PRP_Fragile_TopTapeAcross',(0,0,1.265),(1.27,.18,.016),yellow,r,.006)
box('PRP_Fragile_TopTapeLength',(0,0,1.28),(.18,1.15,.016),yellow,r,.006)
for side in [-1,1]:
 box('PRP_Fragile_VerticalTape_'+str(side),(0,side*.556,.56),(.18,.016,1.11),yellow,r,.006)
 box('PRP_Fragile_WarningBand_'+str(side),(0,side*.562,.98),(1.22,.018,.14),yellow,r,.005)
 for i in range(6):
  x=-.58+i*.20
  prism('PRP_Fragile_DiagonalStripe_'+str(side)+'_'+str(i),[(x,.915),(x+.075,.915),(x+.165,1.045),(x+.09,1.045)],side*.574-.004,side*.574+.004,dark,r)
for x in [-.56,.56]:
 for y in [-.50,.50]:
  box('PRP_Fragile_CornerFoot_'+str(x)+'_'+str(y),(x,y,.085),(.17,.17,.17),dark,r,.035)
  box('PRP_Fragile_LidCorner_'+str(x)+'_'+str(y),(x,y,1.19),(.18,.18,.10),dark,r,.025)
# Front warning placard: raised native lettering, never a flattened image.
box('PRP_Fragile_FrontLabelFrame',(0,-.583,.67),(1.0,.035,.35),red,r,.025)
box('PRP_Fragile_FrontLabelFace',(0,-.605,.67),(.91,.012,.28),ivory,r,.012)
frontrot=(math.pi/2,0,0)
t=text('PRP_Fragile_KoreanWarning','취급주의',(0,-.617,.66),.155,red,frontrot,r)
t.data.extrude=.0015
text('PRP_Fragile_EnglishWarning','SPECIAL DELIVERY',(0,-.620,.56),.047,dark,frontrot,r)
# Fragile-glass icon on a small front handling tag.
box('PRP_Fragile_HandlingTag',(-.32,-.576,.29),(.27,.028,.31),ivory,r,.012)
prism('PRP_Fragile_GlassBowl',[(-.395,.38),(-.245,.38),(-.26,.29),(-.28,.265),(-.36,.265),(-.38,.29)],-.596,-.589,red,r,.005)
box('PRP_Fragile_GlassStem',(-.32,-.595,.235),(.02,.009,.07),red,r)
box('PRP_Fragile_GlassBase',(-.32,-.595,.197),(.13,.009,.018),red,r)
# Two upright arrows on the visible right wall, with orientation explicit.
for y in [-.25,.14]:
 a=prism('PRP_Fragile_UpArrow_'+str(y),[(-.03,.23),(.03,.23),(.03,.46),(.10,.46),(0,.58),(-.10,.46),(-.03,.46)],-.009,.009,red,r)
 a.rotation_euler.z=math.pi/2;a.location=(.64,y,0)
# Native shipping label on lid, with barcode bars and two text lines.
box('PRP_Fragile_ShippingTag',(-.30,.25,1.256),(.43,.39,.009),ivory,r,.008)
text('PRP_Fragile_ShippingCode','MDS  006',(-.30,.34,1.263),.052,dark,parent=r)
for i,width in enumerate([.01,.018,.006,.015,.008,.021,.009,.006,.018,.009,.014,.006]):
 x=-.475+i*.029
 box('PRP_Fragile_Barcode_'+str(i),(x,.22,1.265),(width,.12,.006),dark,r)
text('PRP_Fragile_ShippingPriority','PRIORITY',(-.30,.085,1.263),.045,red,parent=r)
# Right-side latch and steel fastener give the special parcel a reinforced read.
box('PRP_Fragile_SealLatch',(.40,-.59,1.13),(.14,.07,.19),dark,r,.018)
lathe('PRP_Fragile_LatchFastener',[(-.013,.032),(.013,.032)],(.40,-.63,1.13),material=silver,parent=r,n=8,closed=False)
for o in r.children:
 if o.type=='MESH':
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
  if not o.modifiers:
   b=o.modifiers.new('Edge chamfer','BEVEL');b.width=.018 if o.name.endswith(('CardboardShell','Lid')) else .002;b.segments=2
lathe('ENV_PlatformRim_FragileParcel',[(-.16,1.125),(-.13,1.125),(-.13,1.103),(-.16,1.103)],tuple(r.location),'Z',yellow,n=64)
label=bpy.data.materials['MDS_Label'];muted=bpy.data.materials['MDS_LabelMuted']
text('PRP_Label_FragileParcel','06  취급주의 택배',(2.7,-3.15,.023),.18,label)
text('PRP_Subtitle_FragileParcel','SPECIAL DELIVERY',(2.7,-3.4,.023),.105,muted)
bpy.data.objects['PRP_CollectionSubtitle'].data.body='FIVE SPECIAL ITEMS  +  FRAGILE DELIVERY'
for f,a in [(1,-.12),(48,.09),(96,-.33),(144,-.12)]:
 r.rotation_euler.z=a;r.keyframe_insert(data_path='rotation_euler',index=2,frame=f)
for layer in r.animation_data.action.layers:
 for strip in layer.strips:
  for bag in strip.channelbags:
   if bag.slot_handle==r.animation_data.action_slot.handle:
    for fc in bag.fcurves:
     for kp in fc.keyframe_points:kp.interpolation='BEZIER';kp.handle_left_type='AUTO_CLAMPED';kp.handle_right_type='AUTO_CLAMPED'
scene().frame_set(1)
render(O/'SpecialItems_WithFragileParcel_v1.png')
checkpoint('fragile_parcel_detailed')
result={'root':r.name,'parts':len(r.children),'preview':str(O/'SpecialItems_WithFragileParcel_v1.png'),'asset_route':'native editable geometry and font'}
