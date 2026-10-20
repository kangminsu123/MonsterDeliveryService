import bpy, json, os
from pathlib import Path
from mathutils import Vector
OUT=Path(r'C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/outputs/SpecialItemFBX');OUT.mkdir(exist_ok=True)
s=bpy.context.scene;frame=s.frame_current;s.frame_set(1);original=bpy.context.window.scene;selected=list(bpy.context.selected_objects)
deps=bpy.context.evaluated_depsgraph_get();assets=[];materials={}
for name in ['AirCannon','Broadcaster','PackingMachine','LifeJacket','ExpressBattery','FragileParcel']:
 root=bpy.data.objects['HERO_'+name];inv=root.matrix_world.inverted();vs=[];faces=[];indices=[];mats=[]
 for ob in root.children:
  if ob.type not in ['MESH','CURVE'] or ob.hide_render:continue
  ev=ob.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles();offset=len(vs);transform=inv@ob.matrix_world
  vs.extend(tuple(transform@v.co) for v in me.vertices)
  for tri in me.loop_triangles:
   source=me.materials[tri.material_index] if me.materials else None
   if source and source not in mats:mats.append(source)
   faces.append(tuple(offset+i for i in tri.vertices));indices.append(mats.index(source) if source else 0)
  ev.to_mesh_clear()
 for m in mats:
  p=m.node_tree.nodes.get('Principled BSDF');materials[m.name]={'name':m.name,'linearColor':list(p.inputs['Base Color'].default_value[:3]),'roughness':p.inputs['Roughness'].default_value,'metallic':p.inputs['Metallic'].default_value,'emission':list(p.inputs['Emission Color'].default_value[:3]),'emissionStrength':p.inputs['Emission Strength'].default_value}
 export_scene=bpy.data.scenes.new('MDS_TemporaryExport_'+name);mesh=bpy.data.meshes.new(name+'_ExportMesh');mesh.from_pydata(vs,[],faces);mesh.update()
 for m in mats:mesh.materials.append(m)
 for face,index in zip(mesh.polygons,indices):face.material_index=index
 obj=bpy.data.objects.new(name,mesh);export_scene.collection.objects.link(obj);bpy.context.window.scene=export_scene;obj.select_set(True);bpy.context.view_layer.objects.active=obj
 try:
  bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,use_triangles=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP',bake_space_transform=True)
  assets.append({'name':name,'sizeBlender':[max(v[i] for v in vs)-min(v[i] for v in vs) for i in range(3)],'triangles':len(faces),'materials':[m.name for m in mats],'filename':name+'.fbx'})
 finally:
  bpy.context.window.scene=original;bpy.data.objects.remove(obj,do_unlink=True);bpy.data.meshes.remove(mesh);bpy.data.scenes.remove(export_scene)
(OUT/'manifest.json').write_text(json.dumps({'assets':assets,'materials':list(materials.values()),'units':'metres','static_export':True,'review_animation_exported':False,'parcel_text_removed':True},indent=2),encoding='utf-8')
# Native model thumbnails for the game HUD, with the existing studio lights.
(OUT/'Icons').mkdir(exist_ok=True)
states={o.name:o.hide_render for o in s.objects};camera=s.camera;camloc=camera.location.copy();camrot=camera.rotation_euler.copy();scale=camera.data.ortho_scale;transparent=s.render.film_transparent;path=s.render.filepath;size=(s.render.resolution_x,s.render.resolution_y)
try:
 for name in ['AirCannon','Broadcaster','PackingMachine','LifeJacket','ExpressBattery']:
  r=bpy.data.objects['HERO_'+name]
  for o in s.objects:o.hide_render=o.type!='LIGHT' and o!=camera and o not in r.children
  camera.location=r.location+Vector((2,-3,2));camera.rotation_euler=((r.location+Vector((0,0,.78)))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.05
  s.render.film_transparent=True;s.render.resolution_x=s.render.resolution_y=256;s.render.filepath=str(OUT/'Icons'/('item_'+name+'.png'));bpy.ops.render.render(write_still=True)
finally:
 for name,value in states.items():bpy.data.objects[name].hide_render=value
 camera.location=camloc;camera.rotation_euler=camrot;camera.data.ortho_scale=scale;s.render.film_transparent=transparent;s.render.filepath=path;s.render.resolution_x,s.render.resolution_y=size;s.frame_set(frame)
 for o in selected:
  if o.name in bpy.data.objects:o.select_set(True)
result={'folder':str(OUT),'exported':assets,'active_blend_path':bpy.data.filepath,'original_scene_restored':bpy.context.scene.name}
