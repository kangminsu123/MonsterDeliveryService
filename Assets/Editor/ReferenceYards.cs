using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ReferenceYards
{
    const float S = 160f / 1312f;
    const string RootName = "Layer_05_Yards";
    // left, back, right, front, in the original 1312 x 1199 reference plan.
    static readonly Vector4[] Lots = {
        new(190,104,427,227),new(575,104,787,227),new(874,104,1111,227),
        new(209,351,433,519),new(889,351,1105,519),
        new(209,642,433,802),new(563,642,770,802),new(889,642,1105,802),
        new(218,930,433,1055),new(885,930,1105,1060)
    };
    static readonly float[] StepFront = {-4.29f,-3.44f,-3.44f,-4.26f,-3.44f,-4.24f,-3.82f,-4.51f,-4.19f,-3.44f};
    static readonly List<Vector3> V = new();
    static readonly List<int> T = new();
    static readonly HashSet<Vector2> Posts = new();
    static Material fenceMaterial, pavingMaterial;
    static float ground;
    static Transform yard;
    static int slabs;

    [MenuItem("Tools/Level/Build Reference Yards")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var houses = GameObject.Find("Layer_04_Houses");
        var sidewalks = GameObject.Find("Layer_01_Sidewalks");
        var grass = GameObject.Find("Layer_00_GrassBase");
        var roads = GameObject.Find("Layer_02_Roads");
        if (EditorApplication.isPlaying || scene.name != "SampleScene" || !houses || houses.transform.childCount != 10 || !sidewalks || !grass || !roads)
            throw new Exception("Expected 10 placed houses and reference map in edit mode.");
        if (GameObject.Find(RootName)) throw new Exception("Yards already exist; do not duplicate them.");
        var pavement = sidewalks.GetComponentsInChildren<BoxCollider>();
        var grassCollider = grass.GetComponent<MeshCollider>();
        var asphalt = roads.GetComponentInChildren<MeshCollider>();
        ground = grass.GetComponent<Renderer>().bounds.max.y;
        pavingMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/MapMaterials/Sidewalk_GrayConcrete.mat");
        if (!pavingMaterial) throw new Exception("Existing concrete material missing.");
        string materialPath = "Assets/MapMaterials/Yard_WhitePicket.mat";
        fenceMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!fenceMaterial) {
            fenceMaterial = new Material(Shader.Find("Standard"));
            fenceMaterial.color = new Color(.90f,.885f,.82f);
            fenceMaterial.SetFloat("_Glossiness",.14f);
            AssetDatabase.CreateAsset(fenceMaterial, materialPath);
        }
        if (!AssetDatabase.IsValidFolder("Assets/MapMeshes/Yards")) AssetDatabase.CreateFolder("Assets/MapMeshes","Yards");
        EditorSceneManager.SaveScene(scene,"C:/Users/xlfhf/Documents/Codex/2026-09-24/glrt/work/before_yards.unity",true);
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create 10 entrance paths and white picket fences");
        var report = new List<string>();
        int oldSidewalkCount = pavement.Length;
        try {
            var root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root,"Reference yards");
            for (int i=0; i<10; i++) {
                var house = houses.transform.GetChild(i);
                if (!house.name.StartsWith("Plot_"+(i+1).ToString("D2"))) throw new Exception("Unexpected house order.");
                if(i>=8) {
                    var oldCenter=house.GetComponent<Renderer>().bounds.center;
                    Undo.RecordObject(house,"Face bottom houses toward road");
                    house.rotation=Quaternion.Euler(0,180,0);
                    house.position+=oldCenter-house.GetComponent<Renderer>().bounds.center;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(house);
                }
                var go = new GameObject("Yard_"+(i+1).ToString("D2")); go.transform.SetParent(root.transform); yard=go.transform;
                float scale = house.localScale.x;
                float width = (i==8 ? 1.81125f : 1.575f)*scale;
                var entry = house.TransformPoint(new Vector3(i==6 ? 1.65f : 0,0,StepFront[i]));
                var lot = Lots[i];
                float gateZ = World(0,i<8?lot.w:lot.y).y;
                var forward=i<8?Vector2.down:Vector2.up;
                var points = new List<Vector2> { new Vector2(entry.x,entry.z)-forward*.025f };
                points.Add(FindPavement(points[0],forward,pavement));
                slabs=0;
                for(int k=0;k<points.Count-1;k++) {
                    var direction=(points[k+1]-points[k]).normalized;
                    var a=points[k]+(k>0 ? direction*width*.5f : Vector2.zero);
                    var b=points[k+1]+(k<points.Count-2 ? direction*width*.5f : Vector2.zero);
                    Pave(a,b,width);
                }
                DrawFence(i,new Vector2(entry.x,entry.z),width,true);
                SaveFence(i+1);
                Physics.SyncTransforms();
                var houseBounds=house.GetComponent<Renderer>().bounds;
                foreach(var fence in yard.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("FenceCollision"))) {
                    if (fence.bounds.Intersects(houseBounds)) throw new Exception(go.name+" fence intersects house.");
                    foreach(var sidewalk in pavement) if(fence.bounds.Intersects(sidewalk.bounds)) throw new Exception(go.name+" fence intersects sidewalk "+sidewalk.name+" at "+fence.bounds+" / "+sidewalk.bounds);
                }
                foreach(var slab in yard.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Paver"))) {
                    var b=slab.bounds;
                    if(Mathf.Abs(b.max.y-(ground+.18f))>.002f)throw new Exception("Incorrect path height.");
                    if(asphalt.Raycast(new Ray(new Vector3(b.center.x,10,b.center.z),Vector3.down),out _,30))throw new Exception(go.name+" path intersects road.");
                    foreach(var p in new[]{b.min,b.max})
                        if(!grassCollider.Raycast(new Ray(new Vector3(p.x,10,p.z),Vector3.down),out _,30))throw new Exception("Path leaves island.");
                }
                // Walking down the center must pass between the fence colliders.
                var gateRay=new Ray(new Vector3(entry.x,ground+.7f,entry.z),i<8?Vector3.back:Vector3.forward);
                foreach(var fence in yard.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("FenceCollision")))
                    if(fence.Raycast(gateRay,out _,Mathf.Abs(entry.z-gateZ)+.5f))throw new Exception("Entrance gate is blocked.");
                report.Add(go.name+" | slabs="+slabs+" | width="+width.ToString("F2")+" | gate="+(width+.7f).ToString("F2")+
                    " | fence triangles="+(T.Count/3)+" | route="+string.Join(" -> ",points.Select(p=>p.ToString("F2"))));
            }
            if(root.transform.childCount!=10 || sidewalks.GetComponentsInChildren<BoxCollider>().Length!=oldSidewalkCount)throw new Exception("Unexpected layer count.");
            Undo.CollapseUndoOperations(undo); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("art-review"); File.WriteAllLines("art-review/reference-yards-audit.txt",report);
            Selection.activeGameObject=null; ReferenceHouses.Perspective();
            Debug.Log("REFERENCE_YARDS_OK: 10 connected paths, 10 white picket enclosures, 10 open gates; house/sidewalk clearance, ground, road and gate checks passed. Scene saved.\n"+string.Join("\n",report));
        } catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    [MenuItem("Tools/Level/Refresh Dense Fences")]
    public static void RefreshDenseFences()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var houses = GameObject.Find("Layer_04_Houses");
        var yards = GameObject.Find(RootName);
        var grass = GameObject.Find("Layer_00_GrassBase");
        if (EditorApplication.isPlaying || scene.name != "SampleScene" || !houses || !yards || !grass || houses.transform.childCount != 10 || yards.transform.childCount != 10)
            throw new Exception("Expected 10 houses and yards in SampleScene edit mode.");
        for (int i=0; i<10; i++) {
            var fence = yards.transform.GetChild(i).Find("WhitePicketFence");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/MapMeshes/Yards/Yard_"+(i+1).ToString("D2")+"_WhiteFence.asset");
            if (!fence || fence.GetComponent<MeshFilter>()?.sharedMesh != mesh || !mesh)
                throw new Exception("Missing fence mesh for Yard_"+(i+1).ToString("D2"));
        }
        ground = grass.GetComponent<Renderer>().bounds.max.y;
        for (int i=0; i<10; i++) {
            var house = houses.transform.GetChild(i);
            float width = (i==8 ? 1.81125f : 1.575f)*house.localScale.x;
            var entry = house.TransformPoint(new Vector3(i==6 ? 1.65f : 0,0,StepFront[i]));
            DrawFence(i,new Vector2(entry.x,entry.z),width,false);
            SaveFence(i+1,false);
        }
        AssetDatabase.SaveAssets();
        for (int i=0; i<10; i++) {
            string path="Assets/MapMeshes/Yards/Yard_"+(i+1).ToString("D2")+"_WhiteFence.asset";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var filter=yards.transform.GetChild(i).Find("WhitePicketFence").GetComponent<MeshFilter>();
            filter.sharedMesh=null;
            filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            EditorUtility.SetDirty(filter);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SceneView.RepaintAll();
        Debug.Log("Refreshed 10 dense fence meshes and their Scene View bindings; yard transforms and colliders unchanged.");
    }

    static void DrawFence(int i,Vector2 entry,float width,bool createColliders)
    {
        var lot=Lots[i];
        float gateZ=World(0,i<8?lot.w:lot.y).y;
        var backLeft=World(lot.x,lot.y); var backRight=World(lot.z,lot.y);
        var frontLeft=World(lot.x,lot.w); var frontRight=World(lot.z,lot.w);
        var gateLeft=new Vector2(entry.x-width*.5f-.35f,gateZ);
        var gateRight=new Vector2(entry.x+width*.5f+.35f,gateZ);
        V.Clear(); T.Clear(); Posts.Clear();
        if(i<8) {
            Fence(backLeft,backRight,createColliders); Fence(backRight,frontRight,createColliders); Fence(frontRight,gateRight,createColliders);
            Fence(gateLeft,frontLeft,createColliders); Fence(frontLeft,backLeft,createColliders);
        } else {
            Fence(frontLeft,frontRight,createColliders); Fence(frontRight,backRight,createColliders); Fence(backRight,gateRight,createColliders);
            Fence(gateLeft,backLeft,createColliders); Fence(backLeft,frontLeft,createColliders);
        }
    }

    static Vector2 World(float x,float y)=>new((x-656)*S,(599.5f-y)*S);
    static Vector2 FindPavement(Vector2 p,Vector2 direction,BoxCollider[] pavement)
    {
        var ray=new Ray(new Vector3(p.x,ground+.09f,p.y),new Vector3(direction.x,0,direction.y));
        float distance=60;
        foreach(var box in pavement) if(box.Raycast(ray,out var hit,60)) distance=Mathf.Min(distance,hit.distance);
        if(distance>=60)throw new Exception("No existing sidewalk found from "+p+" direction "+direction);
        return p+direction*distance;
    }
    static void Pave(Vector2 a,Vector2 b,float width)
    {
        float length=Vector2.Distance(a,b); var direction=(b-a).normalized; var side=new Vector2(-direction.y,direction.x);
        int rows=Mathf.Max(1,Mathf.RoundToInt(length/1.6f)); float tileLength=length/rows;
        for(int n=0;n<rows;n++)for(int col=0;col<2;col++) {
            var center=Vector2.Lerp(a,b,(n+.5f)/rows)+side*((col==0?-.25f:.25f)*width);
            var slab=GameObject.CreatePrimitive(PrimitiveType.Cube); slab.name="Paver_"+(++slabs).ToString("D3"); slab.transform.SetParent(yard);
            slab.transform.position=new Vector3(center.x,ground+.09f,center.y);
            slab.transform.rotation=Quaternion.Euler(0,-Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,0);
            slab.transform.localScale=new Vector3(tileLength-.025f,.18f,width*.5f-.025f);
            slab.GetComponent<Renderer>().sharedMaterial=pavingMaterial; slab.isStatic=true;
        }
    }
    static void Fence(Vector2 a,Vector2 b,bool createCollision)
    {
        float length=Vector2.Distance(a,b); if(length<.5f)throw new Exception("Fence segment too short.");
        var direction=(b-a).normalized;
        int bays=Mathf.Max(1,Mathf.CeilToInt(length/3));
        for(int j=0;j<=bays;j++) {
            var p=Vector2.Lerp(a,b,j/(float)bays);
            if(Posts.Add(new Vector2(Mathf.Round(p.x*1000),Mathf.Round(p.y*1000)))) {
                Box(p,Vector2.right,.24f,0,1.5f,.24f);
                Picket(p,Vector2.right,.3f,1.5f,1.64f,1.72f,.3f);
            }
        }
        for(int j=0;j<bays;j++) {
            var p=Vector2.Lerp(a,b,(j+.5f)/bays);
            Box(p,direction,length/bays,.35f,.51f,.13f); Box(p,direction,length/bays,.99f,1.15f,.13f);
            int count=Mathf.Max(2,Mathf.CeilToInt((length/bays)/.32f));
            for(int k=0;k<count;k++) {
                var q=Vector2.Lerp(a,b,(j+(k+.5f)/count)/bays);
                Picket(q,direction,.28f,.10f,1.26f,1.4f,.105f);
            }
        }
        if (!createCollision) return;
        var collision=new GameObject("FenceCollision"); collision.transform.SetParent(yard);
        var mid=(a+b)*.5f; collision.transform.position=new Vector3(mid.x,ground+.73f,mid.y);
        collision.transform.rotation=Quaternion.Euler(0,-Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,0);
        collision.AddComponent<BoxCollider>().size=new Vector3(length+.24f,1.46f,.26f); collision.isStatic=true;
    }
    static void Face(params Vector3[] points)
    {
        int start=V.Count; V.AddRange(points);
        for(int i=1;i<points.Length-1;i++)T.AddRange(new[]{start,start+i,start+i+1});
    }
    static Vector3 At(Vector2 p,Vector2 d,float x,float y,float z)=>new(p.x+d.x*x-d.y*z,ground+y,p.y+d.y*x+d.x*z);
    static void Box(Vector2 p,Vector2 d,float width,float bottom,float top,float depth)
    {
        var q=new[]{At(p,d,-width/2,bottom,-depth/2),At(p,d,width/2,bottom,-depth/2),At(p,d,width/2,top,-depth/2),At(p,d,-width/2,top,-depth/2),
            At(p,d,-width/2,bottom,depth/2),At(p,d,width/2,bottom,depth/2),At(p,d,width/2,top,depth/2),At(p,d,-width/2,top,depth/2)};
        Face(q[3],q[2],q[1],q[0]); Face(q[4],q[5],q[6],q[7]); Face(q[0],q[1],q[5],q[4]);
        Face(q[2],q[3],q[7],q[6]); Face(q[0],q[4],q[7],q[3]); Face(q[1],q[2],q[6],q[5]);
    }
    static void Picket(Vector2 p,Vector2 d,float width,float bottom,float shoulder,float top,float depth)
    {
        var profile=new[]{new Vector2(-width/2,bottom),new Vector2(width/2,bottom),new Vector2(width/2,shoulder),new Vector2(0,top),new Vector2(-width/2,shoulder)};
        var a=profile.Select(t=>At(p,d,t.x,t.y,-depth/2)).ToArray(); var b=profile.Select(t=>At(p,d,t.x,t.y,depth/2)).ToArray();
        Face(a.Reverse().ToArray()); Face(b);
        for(int i=0;i<5;i++){int j=(i+1)%5;Face(a[i],a[j],b[j],b[i]);}
    }
    static void SaveFence(int number,bool createObject=true)
    {
        var mesh=new Mesh {name="Yard_"+number.ToString("D2")+"_WhiteFence"}; mesh.SetVertices(V); mesh.SetTriangles(T,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path="Assets/MapMeshes/Yards/"+mesh.name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        if (!createObject) return;
        var obj=new GameObject("WhitePicketFence");obj.transform.SetParent(yard);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterial=fenceMaterial;obj.isStatic=true;
    }
    [MenuItem("Tools/Level/Inspect Yard Detail")]
    public static void Detail()
    {
        var view=SceneView.lastActiveSceneView;
        if(view!=null){view.orthographic=true;view.LookAtDirect(new Vector3(-40,0,19),Quaternion.Euler(48,-18,0),25);view.Repaint();}
    }
    [MenuItem("Tools/Level/Inspect Road Facing Bottom Yards")]
    public static void BottomDetail()
    {
        var view=SceneView.lastActiveSceneView;
        if(view!=null){view.orthographic=true;view.LookAtDirect(new Vector3(41,0,-47),Quaternion.Euler(45,165,0),23);view.Repaint();}
    }
}
