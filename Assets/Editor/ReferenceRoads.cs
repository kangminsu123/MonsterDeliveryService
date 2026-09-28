using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Uses the same 1312 x 1199 reference coordinates as ReferenceSidewalks.
public static class ReferenceRoads
{
    const float S=160f/1312f;
    const float CrossingOffset=52, StripeLength=20, StripePitch=6, StripeWidth=2.7f;
    const float DashClearance=CrossingOffset+StripeLength/2+20;
    static readonly List<Vector2> crossingCenters=new();
    static readonly List<Vector3> vertices=new();
    static readonly List<int> triangles=new();
    static float height;
    static Transform root;

    [MenuItem("Tools/Level/Build Reference Roads")]
    public static void Build()
    {
        var grass=GameObject.Find("Layer_00_GrassBase");
        var sidewalks=GameObject.Find("Layer_01_Sidewalks");
        if(grass==null || sidewalks==null || grass.scene.name!="SampleScene" || EditorApplication.isPlaying)
            throw new System.Exception("Open SampleScene in edit mode with reference sidewalks");
        var asphaltShader=Shader.Find("Map/Reference Asphalt");
        if(asphaltShader==null || ShaderUtil.ShaderHasError(asphaltShader)) throw new System.Exception("Asphalt shader error");
        int sidewalkCount=sidewalks.GetComponentsInChildren<Renderer>().Length;
        var outer=Rounded(23,true,453,0,453,249,145,249,102,291,102,863,143,903,453,903,453,1069,544,1069,544,903,1168,903,1210,863,1210,620,1278,620,1278,542,1210,542,1210,291,1158,249,546,249,546,0);
        var contours=new List<List<Vector2>> {outer};
        float[] left={189,543,869}, right={453,790,1125};
        for(int row=0;row<2;row++) for(int col=0;col<3;col++) {
            float top=row==0?331:622, bottom=row==0?540:823;
            contours.Add(Rounded(32,false,left[col],top,right[col],top,right[col],bottom,left[col],bottom));
        }
        Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup();
        var previous=GameObject.Find("Layer_02_Roads"); if(previous!=null) Undo.DestroyObjectImmediate(previous);
        root=new GameObject("Layer_02_Roads").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Build reference roads");
        height=grass.GetComponent<Renderer>().bounds.max.y+0.02f;
        var asphalt=Material("Reference_Asphalt",asphaltShader,new Color(.25f,.255f,.26f));
        var paint=Material("Reference_RoadPaint",AssetDatabase.LoadAssetAtPath<Material>("Assets/MapMaterials/Sidewalk_White.mat").shader,new Color(.91f,.9f,.86f));
        vertices.Clear(); triangles.Clear();
        // Scanline trapezoids triangulate the outer boundary minus six lawn holes.
        var levels=contours.SelectMany(c=>c.Select(v=>v.y)).Distinct().OrderBy(y=>y).ToList();
        for(int n=0;n<levels.Count-1;n++) {
            float y0=levels[n],y1=levels[n+1],mid=(y0+y1)*.5f;
            if(y1-y0<0.001f) continue;
            var edges=new List<(Vector2 a,Vector2 b)>();
            foreach(var c in contours) for(int i=0;i<c.Count;i++) {
                var a=c[i]; var b=c[(i+1)%c.Count];
                if(mid>Mathf.Min(a.y,b.y) && mid<Mathf.Max(a.y,b.y)) edges.Add((a,b));
            }
            edges.Sort((a,b)=>At(a,mid).CompareTo(At(b,mid)));
            if(edges.Count%2!=0) throw new System.Exception("Invalid road boundary parity");
            for(int i=0;i<edges.Count;i+=2)
                Quad(new(At(edges[i],y0),y0),new(At(edges[i+1],y0),y0),new(At(edges[i+1],y1),y1),new(At(edges[i],y1),y1));
        }
        var road=SaveMesh("Asphalt",asphalt,true);
        vertices.Clear(); triangles.Clear(); height+=0.008f;
        EdgeLine(outer,15,true);
        foreach(var c in contours.Skip(1)) EdgeLine(c,-14,false);
        SaveMesh("EdgeLines",paint,false);
        vertices.Clear(); triangles.Clear();
        foreach(float y in new[]{292f,582f,866f}) {
            Dashes(true,y,147+(y==582?DashClearance:35),499-DashClearance);
            Dashes(true,y,499+DashClearance,831-DashClearance);
            Dashes(true,y,831+DashClearance,1167-(y==582?DashClearance:35));
        }
        Dashes(false,499,35,292-DashClearance);
        Dashes(false,499,292+DashClearance,582-DashClearance);
        Dashes(false,499,582+DashClearance,866-DashClearance);
        Dashes(false,499,866+DashClearance,1034);
        Dashes(false,831,292+DashClearance,582-DashClearance);
        Dashes(false,831,582+DashClearance,866-DashClearance);
        foreach(float x in new[]{147f,1167f}) {
            Dashes(false,x,327,582-DashClearance);
            Dashes(false,x,582+DashClearance,831);
        }
        SaveMesh("CenterDashes",paint,false);
        vertices.Clear(); triangles.Clear();
        crossingCenters.Clear();
        foreach(float y in new[]{292f,582f,866f}) Junction(499,y,true,true,true,true);
        Junction(831,292,true,true,false,true);
        Junction(831,582,true,true,true,true);
        Junction(831,866,true,true,true,false);
        Junction(147,582,false,true,true,true);
        Junction(1167,582,true,true,true,true);
        if(crossingCenters.Count!=29 || vertices.Count!=29*9*4)
            throw new System.Exception("Expected 29 crossings with 9 centered stripes each");
        // Check every painted crossing vertex lies on the asphalt collision surface.
        foreach(var v in vertices)
            if(!road.Raycast(new Ray(v+Vector3.up,Vector3.down),out _,2))
                throw new System.Exception("Crosswalk extends outside asphalt at "+v);
        SaveMesh("Crosswalks",paint,false);
        Physics.SyncTransforms();
        foreach(var p in new[]{new Vector2(499,292),new Vector2(660,582),new Vector2(147,710),new Vector2(499,1000),new Vector2(1240,582)}) {
            var ray=new Ray(World(p)+Vector3.up*10,Vector3.down);
            if(!road.Raycast(ray,out _,20)) throw new System.Exception("Road collider gap at "+p);
        }
        foreach(var p in new[]{new Vector2(320,430),new Vector2(660,730),new Vector2(1000,430),new Vector2(100,100)})
            if(road.Raycast(new Ray(World(p)+Vector3.up*10,Vector3.down),out _,20)) throw new System.Exception("Road covers lawn at "+p);
        if(sidewalks.GetComponentsInChildren<Renderer>().Length!=sidewalkCount) throw new System.Exception("Sidewalk count changed");
        Undo.CollapseUndoOperations(undo); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(grass.scene); EditorSceneManager.SaveScene(grass.scene);
        var view=SceneView.lastActiveSceneView;
        if(view!=null) { view.orthographic=true; view.LookAtDirect(new Vector3(0,0,7),Quaternion.Euler(90,0,0),88); view.Repaint(); }
        Debug.Log("ReferenceRoads VERIFIED: 8 junctions, 29 crossings, equal 52px setbacks, 9 stripes each; asphalt containment passed; sidewalks unchanged. Scene saved.");
    }
    static float At((Vector2 a,Vector2 b) e,float y)=>Mathf.Lerp(e.a.x,e.b.x,(y-e.a.y)/(e.b.y-e.a.y));
    static Vector3 World(Vector2 p)=>new((p.x-656)*S,height,(599.5f-p.y)*S);
    static void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d) {
        int n=vertices.Count; vertices.AddRange(new[]{World(a),World(b),World(c),World(d)});
        if(Vector3.Cross(vertices[n+1]-vertices[n],vertices[n+2]-vertices[n]).y>=0) triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        else triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
    }
    static void Line(Vector2 a,Vector2 b,float width) {
        var v=(b-a).normalized; var n=new Vector2(-v.y,v.x)*width*.5f;
        Quad(a+n,b+n,b-n,a-n);
    }
    static void Dashes(bool horizontal,float fixedCoord,float start,float end) {
        int count=Mathf.FloorToInt((end-start+30)/51);
        if(count<1) return;
        float padding=(end-start-(count*21+(count-1)*30))*.5f;
        for(int i=0;i<count;i++) {float t=start+padding+i*51,e=t+21; if(horizontal) Line(new(t,fixedCoord),new(e,fixedCoord),2.2f); else Line(new(fixedCoord,t),new(fixedCoord,e),2.2f);}
    }
    static void Junction(float x,float y,bool west,bool east,bool north,bool south) {
        int start=crossingCenters.Count;
        if(west) Crossing(true,x-CrossingOffset,y);
        if(east) Crossing(true,x+CrossingOffset,y);
        if(north) Crossing(false,x,y-CrossingOffset);
        if(south) Crossing(false,x,y+CrossingOffset);
        foreach(var c in crossingCenters.Skip(start))
            if(Mathf.Abs(Vector2.Distance(c,new Vector2(x,y))-CrossingOffset)>.001f)
                throw new System.Exception("Unequal crossing setback");
    }
    static void Crossing(bool horizontalRoad,float x,float y) {
        crossingCenters.Add(new Vector2(x,y));
        for(int i=-4;i<=4;i++) {
            float t=i*StripePitch;
            if(horizontalRoad) Line(new(x-StripeLength/2,y+t),new(x+StripeLength/2,y+t),StripeWidth);
            else Line(new(x+t,y-StripeLength/2),new(x+t,y+StripeLength/2),StripeWidth);
        }
    }
    static List<Vector2> Rounded(float radius,bool outer,params float[] xy) {
        int n=xy.Length/2; var p=new Vector2[n]; var result=new List<Vector2>();
        for(int i=0;i<n;i++) p[i]=new(xy[i*2],xy[i*2+1]);
        for(int i=0;i<n;i++) {
            var prev=p[(i+n-1)%n];var next=p[(i+1)%n];
            float trim=Mathf.Min(radius,Vector2.Distance(prev,p[i])*.45f,Vector2.Distance(next,p[i])*.45f);
            if(outer && (p[i].y==0 || p[i].y==1069 || p[i].x==1278)) trim=0;
            if(trim==0) {result.Add(p[i]); continue;}
            var a=p[i]+(prev-p[i]).normalized*trim;var b=p[i]+(next-p[i]).normalized*trim;
            for(int k=0;k<=6;k++) {float t=k/6f;result.Add((1-t)*(1-t)*a+2*(1-t)*t*p[i]+t*t*b);}
        }
        return result.Select(v=>new Vector2(Mathf.Round(v.x*1000)/1000,Mathf.Round(v.y*1000)/1000)).ToList();
    }
    static void EdgeLine(List<Vector2> c,float inward,bool openTop) {
        float area=0;for(int i=0;i<c.Count;i++) {var a=c[i];var b=c[(i+1)%c.Count];area+=a.x*b.y-b.x*a.y;}
        var shifted=new List<Vector2>();
        for(int i=0;i<c.Count;i++) {
            var a=(c[i]-c[(i+c.Count-1)%c.Count]).normalized;var b=(c[(i+1)%c.Count]-c[i]).normalized;
            var na=new Vector2(-a.y,a.x)*Mathf.Sign(area);var nb=new Vector2(-b.y,b.x)*Mathf.Sign(area);
            var m=(na+nb).normalized; shifted.Add(c[i]+m*inward/Mathf.Max(.2f,Vector2.Dot(m,nb)));
        }
        for(int i=0;i<c.Count;i++) {int j=(i+1)%c.Count;if(openTop && c[i].y==0 && c[j].y==0) continue;Line(shifted[i],shifted[j],1.6f);}
    }
    static Material Material(string name,Shader shader,Color color) {
        string path="Assets/MapMaterials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) {mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.color=color;if(mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness",.05f); EditorUtility.SetDirty(mat);return mat;
    }
    static MeshCollider SaveMesh(string name,Material material,bool collider) {
        if(!AssetDatabase.IsValidFolder("Assets/MapMeshes")) AssetDatabase.CreateFolder("Assets","MapMeshes");
        string path="Assets/MapMeshes/ReferenceRoad_"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null) {mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);} else mesh.Clear();
        mesh.name="ReferenceRoad_"+name;mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(root);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=material;
        if(!collider)return null; var mc=go.AddComponent<MeshCollider>();mc.sharedMesh=mesh;return mc;
    }
}
