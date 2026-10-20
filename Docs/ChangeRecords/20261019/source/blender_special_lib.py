import bpy, math, os, json
from mathutils import Vector
from pathlib import Path
W=Path(r'C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/work/blender-special-items')
O=Path(r'C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/outputs')
SCENE='MDS_SpecialItems_Design_v1'
def scene(): return bpy.data.scenes[SCENE]
def col(name):
 c=bpy.data.collections.get(name)
 if not c:c=bpy.data.collections.new(name);scene().collection.children.link(c)
 return c
def mat(name,color,metal=0,rough=.4,emission=0):
 name='MDS_'+name;m=bpy.data.materials.get(name)
 if not m:m=bpy.data.materials.new(name)
 m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 if emission:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
 return m
def mesh(name,vs,fs,parent=None,material=None,collection='MDS_Props',bevel=0):
 if bpy.data.objects.get(name):return bpy.data.objects[name]
 m=bpy.data.meshes.new(name+'_Mesh');m.from_pydata(vs,[],fs);m.update();o=bpy.data.objects.new(name,m);col(collection).objects.link(o)
 if parent:o.parent=parent
 if material:m.materials.append(material)
 if bevel:
  b=o.modifiers.new('Edge chamfer','BEVEL');b.width=bevel;b.segments=2;b.affect='EDGES'
 return o
def box(name,center,size,material=None,parent=None,bevel=0,collection='MDS_Props'):
 x,y,z=(s/2 for s in size);cx,cy,cz=center
 vs=[(cx+a,cy+b,cz+c) for a,b,c in [(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]]
 return mesh(name,vs,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],parent,material,collection,bevel)
def prism(name,outline,y0,y1,material=None,parent=None,bevel=0):
 n=len(outline);vs=[(x,y,z) for y in [y0,y1] for x,z in outline];fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 return mesh(name,vs,fs,parent,material,bevel=bevel)
def lathe(name,profile,center=(0,0,0),axis='Y',material=None,parent=None,n=24,closed=True,bevel=0):
 cx,cy,cz=center;vs=[]
 for h,r in profile:
  for i in range(n):
   a=2*math.pi*i/n;v=(r*math.cos(a),h,r*math.sin(a)) if axis=='Y' else (r*math.cos(a),r*math.sin(a),h) if axis=='Z' else (h,r*math.cos(a),r*math.sin(a));vs.append((v[0]+cx,v[1]+cy,v[2]+cz))
 fs=[];rings=len(profile)
 for j in range(rings if closed else rings-1):
  k=(j+1)%rings
  for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,k*n+(i+1)%n,k*n+i))
 if not closed:fs+=[tuple(range(n-1,-1,-1)),tuple((rings-1)*n+i for i in range(n))]
 return mesh(name,vs,fs,parent,material,bevel=bevel)
def tube(name,points,radius,material,parent=None):
 c=bpy.data.curves.new(name+'_Curve','CURVE');c.dimensions='3D';c.resolution_u=1;c.bevel_depth=radius;c.bevel_resolution=2
 sp=c.splines.new('POLY');sp.points.add(len(points)-1)
 for p,v in zip(sp.points,points):p.co=(*v,1)
 o=bpy.data.objects.new(name,c);col('MDS_Props').objects.link(o);o.parent=parent;c.materials.append(material);return o
def root(name,location,angle):
 o=bpy.data.objects.get(name)
 if not o:o=bpy.data.objects.new(name,None);col('MDS_Props').objects.link(o)
 o.location=location;o.rotation_euler.z=angle;o.empty_display_type='PLAIN_AXES';o.empty_display_size=.25;o['asset_role']='special_item_design';return o
def text(name,body,loc,size,material,rotation=(0,0,0),parent=None):
 c=bpy.data.curves.new(name+'_Text','FONT');c.body=body;c.align_x='CENTER';c.size=size;c.extrude=.001
 f=Path('C:/Windows/Fonts/malgun.ttf')
 if f.exists():c.font=bpy.data.fonts.get('맑은 고딕') or bpy.data.fonts.load(str(f))
 o=bpy.data.objects.new(name,c);col('MDS_Stage').objects.link(o);o.location=loc;o.rotation_euler=rotation;o.parent=parent;c.materials.append(material);return o
def point(o,at):o.rotation_euler=(Vector(at)-o.location).to_track_quat('-Z','Y').to_euler()
def checkpoint(label):bpy.ops.wm.save_as_mainfile(filepath=str(W/('checkpoint_'+label+'_20261019.blend')),copy=True,check_existing=False)
def solid(path):
 s=scene();engine=s.render.engine;s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='MATERIAL';s.display.shading.show_shadows=True;s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH';s.render.filepath=str(path);s.render.resolution_x=1400;s.render.resolution_y=1100
 try:bpy.ops.render.render(write_still=True,scene=s.name)
 finally:s.render.engine=engine;s.render.resolution_x=1800;s.render.resolution_y=1400
def render(path,width=1800,height=1400):
 s=scene();s.render.resolution_x=width;s.render.resolution_y=height;s.render.filepath=str(path);bpy.ops.render.render(write_still=True,scene=s.name)
