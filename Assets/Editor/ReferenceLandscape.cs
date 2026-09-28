using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Authored neighborhood planting and a paved pocket park, in the reference map coordinates.
public static class ReferenceLandscape
{
    const string RootName = "Layer_07_ReferenceLandscape";
    const string Folder = "Assets/Art/LowPolyTown/Landscape";
    const string Prefabs = "Assets/Art/LowPolyTown/Prefabs/";
    const float S = 160f / 1312f;
    static System.Random rng;
    static MeshCollider grass, road, cliff;
    static Bounds[] houses, obstructions;
    static float ground;
    static readonly List<(Vector2 p, float width, bool tree)> plants = new();
    static readonly List<Vector2[]> paving = new();
    static readonly Dictionary<string, Material> palettes = new();
    static readonly List<string> report = new();
    static Vector2 P(float x, float y) => new((x - 656) * S, (599.5f - y) * S);
    static float R(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
    static Transform Group(Transform parent, string name) { var g = new GameObject(name); g.transform.SetParent(parent, false); return g.transform; }
    static Bounds BoundsOf(GameObject go) { var rr = go.GetComponentsInChildren<Renderer>(); var b = rr[0].bounds; foreach (var r in rr.Skip(1)) b.Encapsulate(r.bounds); return b; }
    static bool Near(Bounds b, Vector2 p, float radius) => p.x + radius > b.min.x && p.x - radius < b.max.x && p.y + radius > b.min.z && p.y - radius < b.max.z;
    static bool Hit(MeshCollider c, Vector2 p, out RaycastHit hit) => c.Raycast(new Ray(new Vector3(p.x, 100, p.y), Vector3.down), out hit, 220);
    static string Signature(Transform t) => t.name + t.position.ToString("F5") + t.rotation.ToString("F5") + t.localScale.ToString("F5");

    [MenuItem("Tools/Level/Reference Landscape/Rebuild Park and Planting")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var houseRoot = GameObject.Find("Layer_04_Houses");
        var props = GameObject.Find("Layer_06_LowPolyTownProps");
        if (EditorApplication.isPlaying || scene.name != "SampleScene" || !houseRoot || houseRoot.transform.childCount != 10 || !props)
            throw new Exception("Expected the reference neighborhood in edit mode.");
        var fixedTransforms = houseRoot.GetComponentsInChildren<Transform>().Concat(props.transform.Find("07_Flat_Roof_AC").GetComponentsInChildren<Transform>()).ToArray();
        var original = fixedTransforms.Select(Signature).ToArray();
        grass = GameObject.Find("Layer_00_GrassBase").GetComponent<MeshCollider>();
        road = GameObject.Find("Layer_02_Roads").GetComponentInChildren<MeshCollider>();
        cliff = GameObject.Find("Layer_03_IslandTerrain").transform.Find("CoastalCliffs").GetComponent<MeshCollider>();
        ground = grass.GetComponent<Renderer>().bounds.max.y;
        houses = houseRoot.GetComponentsInChildren<Renderer>().Select(r => r.bounds).ToArray();
        obstructions = GameObject.Find("Layer_01_Sidewalks").GetComponentsInChildren<BoxCollider>()
            .Concat(GameObject.Find("Layer_05_Yards").GetComponentsInChildren<BoxCollider>()).Select(c => c.bounds).ToArray();
        rng = new System.Random(92973); plants.Clear(); report.Clear(); palettes.Clear(); paving.Clear();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/LowPolyTown", "Landscape");
        Directory.CreateDirectory("art-review");
        if(!File.Exists("art-review/before-landscape-redesign.unity"))
            EditorSceneManager.SaveScene(scene, "art-review/before-landscape-redesign.unity", true);
        PrepareShrub();
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Rebuild reference park and landscaping");
        try
        {
            foreach (string name in new[] { RootName, "Layer_07_CentralParkPaving" }) { var old = GameObject.Find(name); if (old) Undo.DestroyObjectImmediate(old); }
            foreach (string name in new[] { "01_Yard_Trees", "02_Park_and_Grove", "03_Coastal_Trees", "04_Shore_Rocks" })
            { var old = props.transform.Find(name); if (old) Undo.DestroyObjectImmediate(old.gameObject); }
            var root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "Reference landscape");
            BuildPark(root.transform);
            BuildYards(Group(root.transform, "02_Garden_Planting"));
            BuildGrove(Group(root.transform, "03_South_Woodland"));
            BuildCoast(Group(root.transform, "04_Coastal_Groves"), Group(root.transform, "05_Cliff_Rock_Groups"));
            Physics.SyncTransforms();
            if (!fixedTransforms.Select(Signature).SequenceEqual(original)) throw new Exception("House or rooftop condenser transform changed.");
            var sun=GameObject.Find("Directional Light").GetComponent<Light>();
            Undo.RecordObject(sun,"Landscape contact shadows");sun.shadows=LightShadows.Soft;sun.shadowStrength=.72f;sun.shadowBias=.035f;sun.shadowNormalBias=.18f;
            Verify();
            report.Add("Ten house transforms and six rooftop AC transforms unchanged.");
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllLines("art-review/reference-landscape-audit.txt", report);
            Undo.CollapseUndoOperations(undo); Overview();
            Debug.Log("REFERENCE_LANDSCAPE_OK\n" + string.Join("\n", report));
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    static GameObject Spawn(string asset, Transform parent, Vector2 p, float width, float height, float baseY, int palette)
    {
        string path = asset == "Shrub" ? Folder + "/Shrub.prefab" : Prefabs + asset + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) throw new Exception("Missing " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = asset + "_" + parent.childCount.ToString("D3");
        var b = BoundsOf(go); float h = b.size.y, w = Mathf.Max(b.size.x, b.size.z);
        go.transform.localScale = new Vector3(width / w, height / h, width / w * R(.92f, 1.08f));
        go.transform.rotation = Quaternion.Euler(0, R(0, 360), 0);
        b = BoundsOf(go); go.transform.position += new Vector3(p.x - b.center.x, baseY - b.min.y, p.y - b.center.z);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].name.Contains("Foliage")) mats[i] = Foliage(mats[i], palette);
                else if(mats[i].name.Contains("MAT_Stone")) mats[i]=Stone(mats[i]);
            }
            r.sharedMaterials = mats; PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        go.isStatic = true; PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go;
    }

    static Material Foliage(Material source, int tone)
    {
        string key = "Tone" + tone + "_" + source.name.Split(new[] { " (" }, StringSplitOptions.None)[0];
        if (palettes.TryGetValue(key, out var material)) return material;
        string path = Folder + "/" + key + ".mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
        Color c = source.color;
        Color tint = tone == 0 ? new Color(.76f, .89f, .82f) : tone == 1 ? new Color(.92f, .96f, .79f) : new Color(.64f, .80f, .72f);
        material.color = c * tint; material.enableInstancing = true; EditorUtility.SetDirty(material); palettes[key] = material; return material;
    }

    static Material Stone(Material source)
    {
        string key="Coast_"+source.name;
        if(palettes.TryGetValue(key,out var m))return m;
        string path=Folder+"/"+key+".mat";m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}
        m.color=source.color*new Color(.88f,.875f,.85f);m.enableInstancing=true;EditorUtility.SetDirty(m);palettes[key]=m;return m;
    }

    static bool SafePlant(Vector2 p, float width, bool tree, bool park)
    {
        float foot = tree ? .30f + width * .095f : width * .28f;
        if (!Hit(grass, p, out _) || Hit(road, p, out _) || obstructions.Any(b => Near(b, p, foot)) || houses.Any(b => Near(b, p, width * .44f))) return false;
        foreach (var delta in new[] { new Vector2(foot, 0), new Vector2(-foot, 0), new Vector2(0, foot), new Vector2(0, -foot) })
        {
            if (!Hit(grass, p + delta, out _) || Hit(road, p + delta, out _)) return false;
            if (park && paving.Any(poly => Inside(poly, p + delta))) return false;
        }
        foreach (var other in plants)
        {
            float min = tree && other.tree ? (width + other.width) * .32f :
                !tree && !other.tree ? (width + other.width) * .25f : .65f + (tree ? other.width : width) * .22f;
            if (Vector2.Distance(p, other.p) < min) return false;
        }
        return true;
    }

    static bool Plant(Transform group, Vector2 p, float width, bool tree, int tone, bool park = false, string type = "Broadleaf")
    {
        foreach (float size in new[] { width, width * .84f })
        {
            if (!SafePlant(p, size, tree, park)) continue;
            Spawn(tree ? type : "Shrub", group, p, size, tree ? size * R(1.04f, 1.22f) : size * R(.48f, .69f), ground - (tree ? .03f : .10f), tone);
            plants.Add((p, size, tree)); return true;
        }
        return false;
    }

    static void BushPatch(Transform group, Vector2 at, int count, float spread, bool park, int tone, float sizeFactor=1)
    {
        for (int i = 0; i < count * 4 && count > 0; i++)
        {
            Vector2 p = at + new Vector2(R(-spread, spread), R(-spread, spread));
            if (Plant(group, p, R(1.3f, 2.7f)*sizeFactor, false, tone, park)) count--;
        }
    }

    static void BuildYards(Transform root)
    {
        Vector4[] lots = { new(190,104,427,227),new(575,104,787,227),new(874,104,1111,227),new(209,351,433,519),new(889,351,1105,519),new(209,642,433,802),new(563,642,770,802),new(889,642,1105,802),new(218,930,433,1055),new(885,930,1105,1060) };
        for (int i = 0; i < lots.Length; i++)
        {
            var g = Group(root, "Garden_" + (i + 1).ToString("D2")); var lot = lots[i];
            float mid = (lot.y + lot.w) * .5f;
            var anchors = new[] { P(lot.x+23,lot.y+24),P(lot.z-23,lot.y+33),P(lot.x+20,mid+8),P(lot.z-21,mid+21),P(lot.x+26,lot.w-23),P(lot.z-25,lot.w-25) };
            for (int j = 0; j < anchors.Length; j++) Plant(g, anchors[j], j < 2 ? R(5.8f,7.0f) : R(4.2f,5.8f), true, (i+j)%3, false, j%4==3 ? "Fork_Tree" : "Broadleaf");
            foreach (var a in anchors) BushPatch(g, a, 3, 2.0f, false, i%3);
            report.Add(g.name + ": " + g.childCount + " trees and low shrubs");
        }
    }

    static void BuildGrove(Transform group)
    {
        Vector2[] points = { P(595,944),P(655,959),P(721,948),P(789,969),P(601,1007),P(665,1021),P(742,1022),P(814,1039),P(619,1060),P(699,1080),P(774,1094) };
        for (int i=0;i<points.Length;i++) Plant(group,points[i],R(6.8f,9.0f),true,i%3,false,i==3 ? "Fork_Tree":"Broadleaf");
        foreach(var p in points) BushPatch(group,p,4,4.0f,false,1);
        report.Add("South woodland: " + group.childCount + " layered plants");
    }

    static void BuildCoast(Transform trees, Transform rocks)
    {
        float[] angles = { 3,12,23,39,48,56,72,80,99,111,119,132,148,164,173,183,193,207,220,237,244,253,267,284,292,309,320,333,348 };
        int patch=0;
        foreach(float degrees in angles)
        {
            float a=degrees*Mathf.Deg2Rad;var outward=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var tangent=new Vector2(-outward.y,outward.x);
            Vector2 edge=default; bool found=false;
            for(float radius=119;radius>30;radius-=.25f) if(Hit(grass,outward*radius,out _)){edge=outward*radius;found=true;break;}
            if(!found)continue;
            var cluster=Group(trees,"Grove_"+(++patch).ToString("D2"));
            Vector2 hub=edge-outward*R(6.0f,9.0f);
            Plant(cluster,hub,R(6.8f,8.8f),true,patch%3);
            Plant(cluster,hub+tangent*R(3.8f,5.5f)-outward*R(1,3),R(4.9f,6.8f),true,(patch+1)%3,false,patch%5==0?"Umbrella":"Broadleaf");
            Plant(cluster,hub-tangent*R(4.0f,6.2f)+outward*R(-1,1),R(4.8f,6.5f),true,(patch+2)%3,false,patch%4==0?"Fork_Tree":"Broadleaf");
            BushPatch(cluster,hub+outward*1.8f,9,5.8f,false,patch%3,1.28f);
            float mainWidth=patch%3==0?R(7.2f,9.1f):R(4.5f,6.8f);
            Rock(rocks,edge-outward*R(1.0f,2.1f),mainWidth,patch%3==0?"Rock_Cluster":"Tall_Boulder",true);
            Rock(rocks,edge+tangent*R(3.2f,5.2f)-outward*R(1.5f,3.0f),R(2.0f,3.8f),"Round_Boulder",true);
            if(patch%3!=0)Rock(rocks,edge-tangent*R(2.8f,4.8f)-outward*R(1.2f,3.0f),R(1.3f,2.5f),"Flat_Boulder",true);
            if(patch%2==0)
            {
                var p=edge+outward*R(1.7f,3.8f);
                if(Hit(cliff,p,out var hit))Spawn(patch%4==0?"Rock_Cluster":"Tall_Boulder",rocks,p,R(5.0f,8.0f),R(5.0f,8.0f),hit.point.y-R(1.4f,2.2f),0);
            }
        }
        report.Add("Coastal groves: "+trees.GetComponentsInChildren<Renderer>().Length+" plants; rock masses: "+rocks.childCount);
    }

    static void Rock(Transform group,Vector2 p,float width,string type,bool coastal)
    {
        if(!Hit(grass,p,out _)||Hit(road,p,out _)||obstructions.Any(b=>Near(b,p,width*.32f))||houses.Any(b=>Near(b,p,width*.5f)))return;
        if(!coastal&&paving.Any(poly=>Inside(poly,p)))return;
        float height=width*R(.58f,.95f);
        Spawn(type,group,p,width,height,ground-height*R(.22f,.38f),0);
    }

    static void BuildPark(Transform parent)
    {
        var root=Group(parent,"01_Central_Pocket_Park");
        var plaza=new[]{P(607,406),P(627,387),P(709,387),P(729,407),P(729,484),P(710,501),P(627,501),P(607,483)};
        paving.Add(plaza);paving.Add(Rect(P(654,342),P(680,387)));paving.Add(Rect(P(654,501),P(680,529)));
        paving.Add(Rect(P(554,430),P(607,454)));paving.Add(Rect(P(729,430),P(779,454)));
        var stone=new Material[5];
        for(int i=0;i<5;i++)
        {
            string path=Folder+"/Park_Limestone_"+i+".mat";stone[i]=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!stone[i]) {stone[i]=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/MapMaterials/Sidewalk_GrayConcrete.mat"));AssetDatabase.CreateAsset(stone[i],path);}
            stone[i].SetColor("_Color",new Color(.79f,.755f,.67f)*(1+(i-2)*.016f));stone[i].SetFloat("_NoiseAmount",.018f);EditorUtility.SetDirty(stone[i]);
        }
        var v=new List<Vector3>();var n=new List<Vector3>();var tri=Enumerable.Range(0,5).Select(_=>new List<int>()).ToArray();
        Vector2 treeAt=P(667,450);float hole=1.35f;
        foreach(var region in paving)
        {
            float minX=region.Min(p=>p.x),maxX=region.Max(p=>p.x),minZ=region.Min(p=>p.y),maxZ=region.Max(p=>p.y);
            for(int row=Mathf.FloorToInt(minZ/1.5f);row<=Mathf.CeilToInt(maxZ/1.5f);row++)
            for(int col=Mathf.FloorToInt(minX/2.1f)-1;col<=Mathf.CeilToInt(maxX/2.1f);col++)
            {
                float x=col*2.1f+(row%2)*1.05f,z=row*1.5f;
                var tile=Clip(Clip(Clip(Clip(region.ToList(),0,x+.015f,true),0,x+2.085f,false),1,z+.015f,true),1,z+1.485f,false);
                // Four disjoint clips leave an actual planting opening around the tree trunk.
                var parts=new[]{Clip(tile,0,treeAt.x-hole,false),Clip(tile,0,treeAt.x+hole,true),
                    Clip(Clip(Clip(tile,0,treeAt.x-hole,true),0,treeAt.x+hole,false),1,treeAt.y-hole,false),
                    Clip(Clip(Clip(tile,0,treeAt.x-hole,true),0,treeAt.x+hole,false),1,treeAt.y+hole,true)};
                foreach(var part in parts)if(part.Count>=3)Prism(part,ground+.18f,ground+.02f,v,n,tri[rng.Next(5)]);
            }
        }
        var mesh=new Mesh{name="Park_Limestone_Slabs"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.subMeshCount=5;
        for(int i=0;i<5;i++)mesh.SetTriangles(tri[i],i);mesh.RecalculateBounds();mesh=SaveMesh(mesh,Folder+"/Park_Limestone_Slabs.asset");
        var go=new GameObject("Paved_Plaza_and_Four_Entrances");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=stone;go.AddComponent<MeshCollider>().sharedMesh=mesh;go.isStatic=true;
        var plantGroup=Group(root,"Planted_Corners");
        Spawn("Broadleaf",plantGroup,treeAt,10.6f,9.4f,ground-.02f,1);plants.Add((treeAt,10.6f,true));
        Vector2[] corners={P(583,375),P(753,375),P(582,493),P(754,493)};
        for(int i=0;i<4;i++){Plant(plantGroup,corners[i],i<2?6.1f:5.2f,true,i%3,true);BushPatch(plantGroup,corners[i],8,3.5f,true,i%3);}
        foreach(var p in new[]{P(591,414),P(745,414),P(607,512),P(738,512)})BushPatch(plantGroup,p,4,1.8f,true,1);
        Rock(plantGroup,P(593,406),1.8f,"Flat_Boulder",false);Rock(plantGroup,P(749,474),1.4f,"Round_Boulder",false);
        var furniture=Group(root,"Seating");Bench(furniture,P(618,416),90);Bench(furniture,P(718,416),-90);Bench(furniture,P(699,490),0);
        report.Add("Park: clipped-corner limestone plaza, staggered rectangular slabs, four straight entrances, planting opening, three benches, "+plantGroup.childCount+" plants/rocks.");
    }

    static Material Plain(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Glossiness",.12f);EditorUtility.SetDirty(m);return m;
    }
    static void Bench(Transform parent,Vector2 p,float yaw)
    {
        var g=Group(parent,"Bench_"+parent.childCount);g.position=new Vector3(p.x,ground+.18f,p.y);g.rotation=Quaternion.Euler(0,yaw,0);
        var wood=Plain("Bench_Honey_Wood",new Color(.51f,.285f,.12f));var metal=Plain("Bench_Dark_Iron",new Color(.15f,.17f,.16f));
        for(int i=0;i<4;i++)Block(g,"Seat_Slat",new Vector3(0,.74f,-.43f+i*.28f),new Vector3(2.85f,.13f,.23f),wood);
        for(int i=0;i<3;i++)Block(g,"Back_Slat",new Vector3(0,1.05f+i*.23f,-.53f),new Vector3(2.85f,.18f,.12f),wood);
        foreach(float x in new[]{-1.06f,1.06f}) {Block(g,"Leg",new Vector3(x,.38f,0),new Vector3(.14f,.76f,.92f),metal);Block(g,"Back_Frame",new Vector3(x,1.06f,-.57f),new Vector3(.12f,1.00f,.12f),metal);Block(g,"Arm",new Vector3(x,1.0f,0),new Vector3(.15f,.12f,.95f),wood);}
        var collider=g.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,.8f,0);collider.size=new Vector3(2.85f,1.6f,1.15f);
    }
    static void Block(Transform parent,string name,Vector3 at,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;
    }

    static void PrepareShrub()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"Broadleaf.prefab");var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int[]>();var mats=new List<Material>();
        foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=mf.sharedMesh;var source=mesh.vertices;var ns=mesh.normals;var materials=mf.GetComponent<Renderer>().sharedMaterials;
            for(int s=0;s<mesh.subMeshCount;s++) if(materials[s].name.Contains("Foliage"))
            {
                var indices=new List<int>();foreach(int idx in mesh.GetTriangles(s)){indices.Add(vertices.Count);vertices.Add(prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(source[idx])));normals.Add(prefab.transform.InverseTransformDirection(mf.transform.TransformDirection(ns[idx])));}
                triangles.Add(indices.ToArray());mats.Add(materials[s]);
            }
        }
        if(vertices.Count<30)throw new Exception("Unable to extract original tree foliage for shrubs.");
        var min=new Vector3(vertices.Min(p=>p.x),vertices.Min(p=>p.y),vertices.Min(p=>p.z));var max=new Vector3(vertices.Max(p=>p.x),vertices.Max(p=>p.y),vertices.Max(p=>p.z));var origin=new Vector3((min.x+max.x)*.5f,min.y,(min.z+max.z)*.5f);
        for(int i=0;i<vertices.Count;i++)vertices[i]-=origin;
        var shrub=new Mesh{name="LowPoly_Shrub_Foliage"};shrub.SetVertices(vertices);shrub.SetNormals(normals);shrub.subMeshCount=triangles.Count;for(int i=0;i<triangles.Count;i++)shrub.SetTriangles(triangles[i],i);shrub.RecalculateBounds();shrub=SaveMesh(shrub,Folder+"/Shrub.asset");
        var go=new GameObject("Shrub");try{go.AddComponent<MeshFilter>().sharedMesh=shrub;go.AddComponent<MeshRenderer>().sharedMaterials=mats.ToArray();PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Shrub.prefab");}finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    static Mesh SaveMesh(Mesh mesh,string path){var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(mesh,path);return mesh;}
    static Vector2[] Rect(Vector2 a,Vector2 b)=>new[]{new Vector2(a.x,a.y),new Vector2(b.x,a.y),new Vector2(b.x,b.y),new Vector2(a.x,b.y)};
    static bool Inside(Vector2[] poly,Vector2 p){bool inside=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)inside=!inside;return inside;}
    static List<Vector2> Clip(List<Vector2> poly,int axis,float cut,bool greater)
    {
        var output=new List<Vector2>();if(poly.Count==0)return output;
        for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];float av=axis==0?a.x:a.y,bv=axis==0?b.x:b.y;bool ai=greater?av>=cut:av<=cut,bi=greater?bv>=cut:bv<=cut;if(ai)output.Add(a);if(ai!=bi)output.Add(Vector2.Lerp(a,b,(cut-av)/(bv-av)));}return output;
    }
    static void Prism(List<Vector2> poly,float top,float bottom,List<Vector3> v,List<Vector3> n,List<int> t)
    {
        float area=0;for(int i=0;i<poly.Count;i++)area+=poly[i].x*poly[(i+1)%poly.Count].y-poly[(i+1)%poly.Count].x*poly[i].y;if(Mathf.Abs(area)<.001f)return;if(area<0)poly.Reverse();int first=v.Count;
        foreach(var p in poly){v.Add(new Vector3(p.x,top,p.y));n.Add(Vector3.up);}for(int i=1;i<poly.Count-1;i++){t.Add(first);t.Add(first+i+1);t.Add(first+i);}
        for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];var d=(b-a).normalized;var normal=new Vector3(d.y,0,-d.x);int k=v.Count;v.Add(new Vector3(a.x,top,a.y));v.Add(new Vector3(b.x,top,b.y));v.Add(new Vector3(b.x,bottom,b.y));v.Add(new Vector3(a.x,bottom,a.y));for(int j=0;j<4;j++)n.Add(normal);t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
    }

    [MenuItem("Tools/Level/Reference Landscape/Verify")]
    public static void Verify()
    {
        var root=GameObject.Find(RootName);if(!root)throw new Exception("Landscape missing.");
        var rr=root.GetComponentsInChildren<Renderer>();if(rr.Any(r=>r.sharedMaterials.Any(m=>!m)))throw new Exception("Missing landscape material.");
        var pavingObject=root.transform.Find("01_Central_Pocket_Park/Paved_Plaza_and_Four_Entrances");var collider=pavingObject.GetComponent<MeshCollider>();
        foreach(var p in new[]{P(667,361),P(667,518),P(577,441),P(753,441),P(639,418)})if(!Hit(collider,p,out var hit)||Mathf.Abs(hit.point.y-(-.25f+.18f))>.01f)throw new Exception("Disconnected or unsupported park paving at "+p);
        if(root.transform.Find("01_Central_Pocket_Park/Seating").childCount!=3)throw new Exception("Missing park benches.");
        foreach(var bench in root.transform.Find("01_Central_Pocket_Park/Seating").GetComponentsInChildren<BoxCollider>())
        foreach(var arm in new[]{Rect(P(654,342),P(680,387)),Rect(P(654,501),P(680,529)),Rect(P(554,430),P(607,454)),Rect(P(729,430),P(779,454))})
        {
            var b=bench.bounds;
            if(b.min.x<arm.Max(p=>p.x)&&b.max.x>arm.Min(p=>p.x)&&b.min.z<arm.Max(p=>p.y)&&b.max.z>arm.Min(p=>p.y))
                throw new Exception("Park bench obstructs an entrance.");
        }
        var g=GameObject.Find("Layer_00_GrassBase").GetComponent<MeshCollider>();var r=GameObject.Find("Layer_02_Roads").GetComponentInChildren<MeshCollider>();int checkedCount=0;
        foreach(var tr in root.GetComponentsInChildren<Transform>())if(tr.name.StartsWith("Shrub_")||tr.name.StartsWith("Broadleaf_")||tr.name.StartsWith("Fork_Tree_")||tr.name.StartsWith("Umbrella_"))
        {var b=BoundsOf(tr.gameObject);var p=new Vector2(b.center.x,b.center.z);if(!Hit(g,p,out _)||Hit(r,p,out _))throw new Exception("Plant base outside grass: "+tr.name);checkedCount++;}
        var cliffs=GameObject.Find("Layer_03_IslandTerrain").transform.Find("CoastalCliffs").GetComponent<MeshCollider>();
        foreach(Transform rock in root.transform.Find("05_Cliff_Rock_Groups"))
        {
            var b=BoundsOf(rock.gameObject);var p=new Vector2(b.center.x,b.center.z);
            if(!Hit(g,p,out var support)&&!Hit(cliffs,p,out support))throw new Exception("Unsupported coast rock: "+rock.name);
            if(b.min.y>support.point.y+.02f||b.max.y<support.point.y)throw new Exception("Floating or fully buried coast rock: "+rock.name);
        }
        LowPolyTownPlacement.Verify();report.Add("Verified "+checkedCount+" plants on land and off asphalt; clear park entrances; grounded coast rocks; all materials; existing rooftop AC support.");Debug.Log("LANDSCAPE_VERIFIED "+checkedCount+" plants, clear paths, grounded rocks");
    }

    [MenuItem("Tools/Level/Reference Landscape/Capture Review")]
    public static void CaptureReview()
    {
        var go=new GameObject("Landscape_Review_Camera"){hideFlags=HideFlags.HideAndDontSave};
        var texture=new RenderTexture(1600,1600,24){antiAliasing=4};
        var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=96;
            camera.nearClipPlane=.1f;camera.farClipPlane=500;camera.allowHDR=false;camera.allowMSAA=true;
            go.transform.rotation=Quaternion.Euler(62,0,0);go.transform.position=new Vector3(0,-2,0)-go.transform.forward*185;
            camera.targetTexture=texture;texture.Create();camera.Render();RenderTexture.active=texture;
            image=new Texture2D(1600,1600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1600),0,0);image.Apply();
            File.WriteAllBytes("art-review/reference-landscape-final.png",image.EncodeToPNG());
            Debug.Log("LANDSCAPE_REVIEW_CAPTURED art-review/reference-landscape-final.png");
        }
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(go);texture.Release();UnityEngine.Object.DestroyImmediate(texture);if(image)UnityEngine.Object.DestroyImmediate(image);}
    }
    [MenuItem("Tools/Level/Reference Landscape/Overview")]
    public static void Overview(){var v=SceneView.lastActiveSceneView;if(v!=null){Selection.activeGameObject=null;v.orthographic=true;v.LookAtDirect(new Vector3(0,-1,1),Quaternion.Euler(62,0,0),99);v.Repaint();}}
    [MenuItem("Tools/Level/Reference Landscape/Park Detail")]
    public static void Detail(){var v=SceneView.lastActiveSceneView;var c=P(667,433);if(v!=null){Selection.activeGameObject=null;v.orthographic=true;v.LookAtDirect(new Vector3(c.x,1,c.y),Quaternion.Euler(62,0,0),25);v.Repaint();}}
}
