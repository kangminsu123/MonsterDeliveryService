using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class LowPolyTownPlacement
{
    const string Folder="Assets/Art/LowPolyTown";
    const string RootName="Layer_06_LowPolyTownProps";
    const float S=160f/1312f;
    [Serializable] class Manifest { public Model[] assets; public Surface[] materials; }
    [Serializable] class Model { public string name; public float[] sizeBlender; public string[] materials; }
    [Serializable] class Surface { public string name; public float[] linearColor; public float roughness,metallic; }
    static string[] Names={"Broadleaf","Pine","Poplar","Fork_Tree","Umbrella","Tall_Boulder","Flat_Boulder","Rock_Spire","Round_Boulder","Rock_Cluster","Barrier","Stop_Barrier","Red_Car","School_Bus","Blue_Pickup","White_Van","AC_Condenser"};
    static Vector4[] Lots={new(190,104,427,227),new(575,104,787,227),new(874,104,1111,227),new(209,351,433,519),new(889,351,1105,519),new(209,642,433,802),new(563,642,770,802),new(889,642,1105,802),new(218,930,433,1055),new(885,930,1105,1060)};
    static Bounds BoundsOf(GameObject go) {var rr=go.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
    static Vector3 Pixel(float x,float y,float height=0)=>new((x-656)*S,height,(599.5f-y)*S);
    static bool Overlap(Bounds a,Bounds b,float gap=0)=>a.min.x<b.max.x+gap&&a.max.x>b.min.x-gap&&a.min.z<b.max.z+gap&&a.max.z>b.min.z-gap;
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}

    [MenuItem("Tools/Level/Low Poly Town/Prepare Assets and Inspect")]
    public static void Prepare()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit play mode first.");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/manifest.json"));
        if(manifest.assets.Length!=17)throw new Exception("Expected 17 independent FBX assets.");
        EnsureFolder(Folder+"/Materials");EnsureFolder(Folder+"/Prefabs");
        var shader=Shader.Find("Standard");if(!shader)throw new Exception("Existing Built-in pipeline Standard shader unavailable.");
        foreach(var source in manifest.materials){
            string path=Folder+"/Materials/"+source.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}
            m.color=new Color(source.linearColor[0],source.linearColor[1],source.linearColor[2],1).gamma;
            m.SetFloat("_Glossiness",1-source.roughness);m.SetFloat("_Metallic",source.metallic);m.enableInstancing=true;EditorUtility.SetDirty(m);
        }
        var report=new List<string>();
        foreach(var asset in manifest.assets){
            string path=Folder+"/Models/"+asset.name+".fbx";var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(!importer)throw new Exception("Missing "+path);
            importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;importer.importNormals=ModelImporterNormals.Import;
            importer.isReadable=true;importer.generateSecondaryUV=true;
            foreach(var name in asset.materials.Distinct())importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+name+".mat"));
            importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var go=(GameObject)PrefabUtility.InstantiatePrefab(model);go.name=asset.name;
            try{
                var b=BoundsOf(go);var expected=new Vector3(asset.sizeBlender[0],asset.sizeBlender[2],asset.sizeBlender[1]);
                if(Vector3.Distance(b.size,expected)>.03f)throw new Exception(asset.name+" axis/scale mismatch "+b.size+" expected "+expected);
                go.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
                foreach(var mf in go.GetComponentsInChildren<MeshFilter>()){
                    var mc=mf.GetComponent<MeshCollider>();if(!mc)mc=mf.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;
                    if(mf.GetComponent<Renderer>().sharedMaterials.Any(m=>!m||m.shader!=shader))throw new Exception("Material remap failed "+asset.name);
                }
                PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Prefabs/"+asset.name+".prefab");
                report.Add(asset.name+" | size "+b.size.ToString("F3")+" | triangles "+go.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3));
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        AssetDatabase.SaveAssets();Directory.CreateDirectory("art-review");File.WriteAllLines("art-review/low-poly-import-audit.txt",report);
        Inspect();Debug.Log("LOW_POLY_IMPORT_OK: 17 FBX, materials remapped, 17 prefabs, metric dimensions and Y-up checked.");
    }

    [MenuItem("Tools/Level/Low Poly Town/Inspect Existing Map")]
    public static void Inspect()
    {
        var report=new List<string>();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        report.Add("Scene "+scene.path+" pipeline "+(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name:"BuiltIn"));
        foreach(var root in scene.GetRootGameObjects()){
            var rr=root.GetComponentsInChildren<Renderer>();if(rr.Length>0)report.Add(root.name+" "+BoundsOf(root));
        }
        var houses=GameObject.Find("Layer_04_Houses");
        foreach(Transform h in houses.transform){
            var b=BoundsOf(h.gameObject);report.Add(h.name+" bounds "+b+" position "+h.position+" scale "+h.localScale);
            var triangles=new Dictionary<float,float>();
            foreach(var mf in h.GetComponentsInChildren<MeshFilter>()){
                var mesh=mf.sharedMesh;var verts=mesh.vertices;var tt=mesh.triangles;
                for(int i=0;i<tt.Length;i+=3){var a=mf.transform.TransformPoint(verts[tt[i]]);var c=mf.transform.TransformPoint(verts[tt[i+1]]);var d=mf.transform.TransformPoint(verts[tt[i+2]]);var cross=Vector3.Cross(c-a,d-a);if(cross.normalized.y>.999f&&a.y>b.min.y+b.size.y*.45f){float y=Mathf.Round(a.y*100)/100;triangles[y]=triangles.GetValueOrDefault(y)+cross.magnitude*.5f;}}
            }
            report.Add("Upward horizontal surface areas: "+string.Join(", ",triangles.OrderByDescending(p=>p.Key).Take(8).Select(p=>p.Key.ToString("F2")+"m="+p.Value.ToString("F2")+"m2")));
        }
        File.WriteAllLines("art-review/low-poly-map-inspection.txt",report);Debug.Log(string.Join("\n",report));
    }

    static MeshCollider grass,asphalt;
    static Bounds[] obstacles;
    static readonly List<Bounds> occupied=new();
    static readonly List<string> placementReport=new();
    static float ground;
    static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static GameObject Spawn(string asset,Vector3 at,float scale,float yaw,Transform parent,string label)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Prefabs/"+asset+".prefab");if(!prefab)throw new Exception("Missing prefab "+asset);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.name=asset+"_"+label;
        go.transform.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));go.transform.localScale=Vector3.one*scale;
        var b=BoundsOf(go);go.transform.position+=new Vector3(at.x-b.center.x,at.y-b.min.y,at.z-b.center.z);
        foreach(var t in go.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);return go;
    }
    static IEnumerable<Vector3> Samples(Bounds b)
    {
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)yield return new Vector3(b.center.x+x*b.extents.x,80,b.center.z+z*b.extents.z);
    }
    static bool OnSurface(MeshCollider surface,Bounds b)=>Samples(b).All(p=>surface.Raycast(new Ray(p,Vector3.down),out _,160));
    static bool Green(string asset,Vector3 at,float scale,float yaw,Transform parent,string label)
    {
        var go=Spawn(asset,new Vector3(at.x,ground,at.z),scale,yaw,parent,label);var b=BoundsOf(go);
        bool ok=OnSurface(grass,b)&&!Samples(b).Any(p=>asphalt.Raycast(new Ray(p,Vector3.down),out _,160))&&!obstacles.Any(o=>Overlap(b,o,.25f))&&!occupied.Any(o=>Overlap(b,o,.25f));
        if(!ok){UnityEngine.Object.DestroyImmediate(go);return false;}
        occupied.Add(b);placementReport.Add(go.name+" position="+go.transform.position.ToString("F2")+" size="+b.size.ToString("F2"));return true;
    }
    static void Road(string asset,float x,float y,float scale,float yaw,Transform parent)
    {
        var at=Pixel(x,y,asphalt.bounds.max.y);var go=Spawn(asset,at,scale,yaw,parent,parent.childCount.ToString("D2"));var b=BoundsOf(go);
        if(!OnSurface(asphalt,b)||obstacles.Any(o=>Overlap(b,o,.08f))||occupied.Any(o=>Overlap(b,o,.5f)))throw new Exception(go.name+" road clearance failed at "+at+" | bounds="+b+" | road="+OnSurface(asphalt,b)+" | obstacles="+string.Join(";",obstacles.Where(o=>Overlap(b,o,.08f)))+" | props="+occupied.Count(o=>Overlap(b,o,.5f)));
        occupied.Add(b);placementReport.Add(go.name+" road="+go.transform.position.ToString("F2")+" size="+b.size.ToString("F2"));
    }
    static bool RoofSupport(Transform house,Vector3 center,float half,out float height)
    {
        height=0;float low=1000,high=-1000;var colliders=house.GetComponentsInChildren<MeshCollider>();
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++){
            var ray=new Ray(new Vector3(center.x+x*half,80,center.z+z*half),Vector3.down);RaycastHit nearest=new();bool found=false;
            foreach(var c in colliders)if(c.Raycast(ray,out var hit,160)&&(!found||hit.distance<nearest.distance)){nearest=hit;found=true;}
            if(!found||nearest.normal.y<.999f)return false;
            low=Mathf.Min(low,nearest.point.y);high=Mathf.Max(high,nearest.point.y);
        }
        if(high-low>.018f)return false;height=(low+high)*.5f;return true;
    }

    [MenuItem("Tools/Level/Low Poly Town/Place Reference Props")]
    public static void Place()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.name!="SampleScene"||GameObject.Find(RootName))throw new Exception("Expected SampleScene edit mode without an existing prop layer.");
        grass=GameObject.Find("Layer_00_GrassBase").GetComponent<MeshCollider>();asphalt=GameObject.Find("Layer_02_Roads").GetComponentInChildren<MeshCollider>();ground=grass.GetComponent<Renderer>().bounds.max.y;
        var houses=GameObject.Find("Layer_04_Houses");var yards=GameObject.Find("Layer_05_Yards");var sidewalks=GameObject.Find("Layer_01_Sidewalks");
        obstacles=sidewalks.GetComponentsInChildren<Collider>().Select(c=>c.bounds).Concat(yards.GetComponentsInChildren<Collider>().Select(c=>c.bounds)).Concat(houses.transform.Cast<Transform>().Select(h=>BoundsOf(h.gameObject))).ToArray();
        int originalHouses=houses.transform.childCount,originalPavements=sidewalks.GetComponentsInChildren<Renderer>().Length;
        string backup="C:/Users/xlfhf/Documents/Codex/2026-09-27/new-chat-3/work/before_unity_props.unity";
        EditorSceneManager.SaveScene(scene,backup,true);Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Place reference low-poly town assets");
        occupied.Clear();placementReport.Clear();
        try{
            var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Place town props");
            var garden=Group(root.transform,"01_Yard_Trees");var park=Group(root.transform,"02_Park_and_Grove");var coastal=Group(root.transform,"03_Coastal_Trees");var rocks=Group(root.transform,"04_Shore_Rocks");var vehicles=Group(root.transform,"05_Parked_Vehicles");var barriers=Group(root.transform,"06_Road_Barriers");var hvac=Group(root.transform,"07_Flat_Roof_AC");
            var rng=new System.Random(927);string[] trees={"Broadleaf","Fork_Tree","Umbrella","Poplar","Pine"};string[] stones={"Tall_Boulder","Flat_Boulder","Rock_Spire","Round_Boulder","Rock_Cluster"};
            // House-side planting stays inside the lots while preserving their entrance paths.
            for(int i=0;i<Lots.Length;i++){
                var lot=Lots[i];int placed=0;
                foreach(float x in new[]{lot.x+21,lot.z-21})foreach(float y in new[]{lot.y+29,(lot.y+lot.w)/2,lot.w-26}){
                    string type=trees[(i+placed)%4];float scale=type=="Poplar"?1.3f:1.2f;
                    if(Green(type,Pixel(x,y),scale,rng.Next(360),garden,"Plot"+(i+1).ToString("D2")+"_"+placed))placed++;
                }
            }
            Green("Broadleaf",Pixel(670,423),2.55f,20,park,"Central_Park_Hero");
            var parkPoints=new[]{new Vector2(580,363),new Vector2(749,364),new Vector2(580,482),new Vector2(757,482),new Vector2(614,493),new Vector2(731,492)};
            for(int i=0;i<parkPoints.Length;i++)Green(trees[i%3],Pixel(parkPoints[i].x,parkPoints[i].y),i<4?1.35f:.75f,rng.Next(360),park,"Park_Edge_"+i);
            var grove=new[]{new Vector2(600,940),new Vector2(679,937),new Vector2(759,948),new Vector2(591,1004),new Vector2(672,1011),new Vector2(758,1020),new Vector2(625,1062),new Vector2(796,1062)};
            for(int i=0;i<grove.Length;i++)Green(trees[i%5],Pixel(grove[i].x,grove[i].y),1.75f+(float)rng.NextDouble()*.35f,rng.Next(360),park,"South_Grove_"+i);
            // Locate the actual island edge, then step inland; each footprint is tested against land and roads.
            for(int i=0;i<78;i++){
                float a=(i+.2f)*Mathf.PI*2/78;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));Vector3 edge=Vector3.zero;bool found=false;
                for(float radius=115;radius>30;radius-=.7f){var p=direction*radius;if(grass.Raycast(new Ray(p+Vector3.up*80,Vector3.down),out var hit,160)){edge=hit.point;found=true;break;}}
                if(!found)continue;
                if(i%2==0)Green(stones[(i/2)%5],edge-direction*(2.7f+(float)rng.NextDouble()*1.5f),1.05f+(float)rng.NextDouble()*.75f,rng.Next(360),rocks,"Coast_"+i);
                Green(trees[i%5],edge-direction*(7.7f+(float)rng.NextDouble()*3.8f),1.55f+(float)rng.NextDouble()*.65f,rng.Next(360),coastal,"Coast_"+i);
            }
            Road("Red_Car",620,281,1.42f,-90,vehicles);
            Road("Blue_Pickup",161,438,1.38f,0,vehicles);
            Road("Red_Car",1153,441,1.42f,180,vehicles);
            Road("White_Van",1032,594,1.42f,-90,vehicles);
            Road("School_Bus",132,745,1.5f,180,vehicles);
            Road("Blue_Pickup",650,851,1.38f,90,vehicles);
            Road("White_Van",515,985,1.38f,0,vehicles);
            Road("Barrier",134,485,1.2f,-5,barriers);
            Road("Stop_Barrier",1180,756,1.2f,0,barriers);
            Road("Barrier",925,279,1.1f,90,barriers);
            Road("Barrier",134,660,1.2f,5,barriers);
            Road("Stop_Barrier",707,874,1.1f,90,barriers);
            Road("Barrier",485,170,1.25f,0,barriers);
            // Only these house designs have flat main roofs. Pitched houses are excluded even if an eave is horizontal.
            foreach(int index in new[]{0,2,3}){
                var house=houses.transform.GetChild(index);var b=BoundsOf(house.gameObject);var taken=new List<Bounds>();int count=0;
                for(float z=b.center.z+2;z>=b.center.z-b.extents.z*.55f&&count<2;z-=2.2f)for(float x=b.center.x-b.extents.x*.5f;x<=b.center.x+b.extents.x*.5f&&count<2;x+=2.4f){
                    var point=new Vector3(x,0,z);if(!RoofSupport(house,point,1.36f,out float y)||y<b.min.y+b.size.y*.60f)continue;
                    var go=Spawn("AC_Condenser",new Vector3(x,y,z),.82f,house.eulerAngles.y,hvac,"Plot"+(index+1).ToString("D2")+"_"+count);var bounds=BoundsOf(go);
                    if(taken.Any(p=>Overlap(bounds,p,.8f))){UnityEngine.Object.DestroyImmediate(go);continue;}
                    taken.Add(bounds);count++;placementReport.Add(go.name+" FLAT_ROOF nine-point normal>0.999 height="+y.ToString("F3")+" footprint="+bounds.size.ToString("F2"));
                }
                if(count==0)throw new Exception("No safe flat-roof location on "+house.name);
            }
            Physics.SyncTransforms();
            if(houses.transform.childCount!=originalHouses||sidewalks.GetComponentsInChildren<Renderer>().Length!=originalPavements)throw new Exception("Existing map changed unexpectedly.");
            foreach(Transform group in root.transform)placementReport.Add("COUNT "+group.name+"="+group.childCount);
            Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllLines("art-review/low-poly-placement-audit.txt",placementReport);Selection.activeGameObject=null;ReferenceHouses.Perspective();
            Debug.Log("LOW_POLY_PLACEMENT_OK\n"+string.Join("\n",placementReport.Where(l=>l.StartsWith("COUNT")||l.Contains("FLAT_ROOF"))));
        }catch{Undo.RevertAllDownToGroup(undo);throw;}
    }

    [MenuItem("Tools/Level/Low Poly Town/Verify Placed Assets")]
    public static void Verify()
    {
        var root=GameObject.Find(RootName);if(!root)throw new Exception("Missing prop layer.");int count=0;var report=new List<string>();
        foreach(Transform group in root.transform)foreach(Transform t in group){
            if(!PrefabUtility.IsPartOfPrefabInstance(t)||t.localScale.x<=0||t.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>!m)))throw new Exception("Invalid instance "+t.name);
            var b=BoundsOf(t.gameObject);
            if(group.name=="07_Flat_Roof_AC"){
                string number=t.name.Split(new[]{"Plot"},StringSplitOptions.None)[1].Substring(0,2);int index=int.Parse(number)-1;
                if(!new[]{0,2,3}.Contains(index))throw new Exception("AC on disallowed pitched house.");
                var house=GameObject.Find("Layer_04_Houses").transform.GetChild(index);
                if(!RoofSupport(house,b.center,Mathf.Max(b.extents.x,b.extents.z)+.25f,out float height)||Mathf.Abs(b.min.y-height)>.025f)throw new Exception("Unsupported AC "+t.name);
                report.Add(t.name+" flat support verified, gap="+(b.min.y-height));
            }
            count++;
        }
        if(Directory.GetFiles(Folder+"/Models","*.fbx").Length!=17||Directory.GetFiles(Folder+"/Prefabs","*.prefab").Length!=17)throw new Exception("Asset count mismatch.");
        report.Add("VERIFIED "+count+" prefab instances; 17 FBX and 17 reusable prefabs; materials valid; AC only on approved flat house roofs.");File.WriteAllLines("art-review/low-poly-final-verification.txt",report);Debug.Log(string.Join("\n",report));
    }
}
