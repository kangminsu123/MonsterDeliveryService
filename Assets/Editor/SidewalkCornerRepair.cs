using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Keep the authored cube colliders; partition only coplanar rendering surfaces.
public static class SidewalkCornerRepair
{
    const string AssetPath = "Assets/MapMeshes/Sidewalk_CornerSurfaces.asset";
    const float AreaEpsilon = .000001f;

    [MenuItem("Tools/Level/Repair Overlapping Sidewalk Corners")]
    public static void Repair()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Use Edit Mode.");
        var root = GameObject.Find("Layer_01_Sidewalks");
        var previous = new List<List<Vector2>>();
        var stored = AssetDatabase.LoadAllAssetsAtPath(AssetPath).OfType<Mesh>().ToDictionary(m => m.name);
        int changed = 0; float removedArea = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var t = filter.transform;
            var original = new List<Vector2>();
            foreach (var p in new[] { new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f) })
            { var world=t.TransformPoint(p); original.Add(new Vector2(world.x,world.z)); }
            var parts = new List<List<Vector2>> { original };
            bool overlaps = false;
            foreach (var other in previous)
            {
                // Skip disjoint rectangles before polygon clipping.
                if (original.Max(p=>p.x) <= other.Min(p=>p.x) || original.Min(p=>p.x) >= other.Max(p=>p.x) ||
                    original.Max(p=>p.y) <= other.Min(p=>p.y) || original.Min(p=>p.y) >= other.Max(p=>p.y)) continue;
                var next = new List<List<Vector2>>();
                foreach (var part in parts)
                {
                    var intersection = part;
                    for(int i=0;i<other.Count;i++) intersection=Clip(intersection,other[i],other[(i+1)%other.Count],true);
                    if(Area(intersection)<=AreaEpsilon) { next.Add(part); continue; }
                    overlaps=true;
                    // Partition A minus B into disjoint convex pieces.
                    var remainder=part;
                    for(int i=0;i<other.Count;i++)
                    {
                        var outside=Clip(remainder,other[i],other[(i+1)%other.Count],false);
                        if(Area(outside)>AreaEpsilon) next.Add(outside);
                        remainder=Clip(remainder,other[i],other[(i+1)%other.Count],true);
                    }
                }
                parts=next;
            }
            previous.Add(original);
            if(!overlaps) continue;
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            foreach(var part in parts) AddPrism(part,t,vertices,triangles);
            var mesh=new Mesh { name=filter.name+"_Surface" };
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            if(stored.TryGetValue(mesh.name,out var existing))
            { Undo.RecordObject(existing,"Repair sidewalk corner");EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh); }
            else if(!System.IO.File.Exists(AssetPath)) AssetDatabase.CreateAsset(mesh,AssetPath);
            else AssetDatabase.AddObjectToAsset(mesh,AssetPath);
            Undo.RecordObject(filter,"Repair sidewalk corner");filter.sharedMesh=mesh;
            removedArea+=Area(original)-parts.Sum(Area);changed++;
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        Debug.Log("SIDEWALK_CORNERS: "+changed+" overlapping surfaces partitioned; duplicate area removed="+removedArea.ToString("F3")+"; original colliders preserved.");
    }

    static float Area(List<Vector2> p)
    {
        float sum=0;for(int i=0;i<p.Count;i++)sum+=Cross(p[i],p[(i+1)%p.Count]);return Mathf.Abs(sum)*.5f;
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static List<Vector2> Clip(List<Vector2> poly,Vector2 a,Vector2 b,bool inside)
    {
        var output=new List<Vector2>();
        for(int i=0;i<poly.Count;i++)
        {
            var p=poly[i];var q=poly[(i+1)%poly.Count];
            float dp=Cross(b-a,p-a),dq=Cross(b-a,q-a);
            bool pin=inside?dp>=0:dp<=0,qin=inside?dq>=0:dq<=0;
            if(pin)output.Add(p);
            if(pin!=qin)output.Add(Vector2.Lerp(p,q,dp/(dp-dq)));
        }
        return output;
    }
    static void AddPrism(List<Vector2> poly,Transform t,List<Vector3> v,List<int> indices)
    {
        var top=poly.Select(p=>t.InverseTransformPoint(new Vector3(p.x,t.position.y+t.lossyScale.y*.5f,p.y))).ToArray();
        int start=v.Count;v.AddRange(top);
        for(int i=1;i<top.Length-1;i++)indices.AddRange(new[]{start,start+i+1,start+i});
        start=v.Count;v.AddRange(top.Select(p=>new Vector3(p.x,-.5f,p.z)));
        for(int i=1;i<top.Length-1;i++)indices.AddRange(new[]{start,start+i,start+i+1});
        for(int i=0;i<top.Length;i++)
        {
            var a=top[i];var b=top[(i+1)%top.Length];start=v.Count;
            v.AddRange(new[]{a,b,new Vector3(b.x,-.5f,b.z),new Vector3(a.x,-.5f,a.z)});
            indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
    }
}
