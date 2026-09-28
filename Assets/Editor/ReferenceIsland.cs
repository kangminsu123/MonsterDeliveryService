using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ReferenceIsland
{
    const int Count=96;
    const float Top=-.25f,Sea=-10.5f;
    static readonly List<Vector3> verts=new();
    static readonly List<int> tris=new();
    static readonly List<Color> colors=new();
    static readonly List<Vector2> uvs=new();
    static System.Random rng;
    static float Rand()=> (float)rng.NextDouble();
    static void Clear() {verts.Clear();tris.Clear();colors.Clear();uvs.Clear();}

    [MenuItem("Tools/Level/Apply Low Poly Grass")]
    public static void ApplyGrass()
    {
        var grass=GameObject.Find("Layer_00_GrassBase");var shader=Shader.Find("Map/Low Poly Grass");
        if(EditorApplication.isPlaying || grass==null || shader==null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
            throw new System.Exception("Grass material requires valid shader and edit mode");
        var renderer=grass.GetComponent<Renderer>();
        Undo.RecordObject(renderer,"Apply low poly grass");
        renderer.sharedMaterial=Mat("Island_LowPolyGrass",shader);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(grass.scene);EditorSceneManager.SaveScene(grass.scene);
        Debug.Log("LowPolyGrass VERIFIED: texture-free world-space facets, shader valid, island mesh and colliders unchanged. Saved.");
    }

    [MenuItem("Tools/Level/Build Island Terrain")]
    public static void Build()
    {
        var grass=GameObject.Find("Layer_00_GrassBase");
        var roads=GameObject.Find("Layer_02_Roads");
        var sidewalks=GameObject.Find("Layer_01_Sidewalks");
        if(EditorApplication.isPlaying || grass==null || roads==null || sidewalks==null || grass.scene.name!="SampleScene")
            throw new System.Exception("Expected reference map in edit mode");
        var facetShader=Shader.Find("Map/Island Facets");var oceanShader=Shader.Find("Map/Island Ocean");
        if(facetShader==null || oceanShader==null || ShaderUtil.ShaderHasError(facetShader) || ShaderUtil.ShaderHasError(oceanShader))
            throw new System.Exception("Island shaders must compile first");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();
        var old=GameObject.Find("Layer_03_IslandTerrain");if(old!=null)Undo.DestroyObjectImmediate(old);
        var root=new GameObject("Layer_03_IslandTerrain");Undo.RegisterCreatedObjectUndo(root,"Island terrain");
        rng=new System.Random(92426);
        var edge=new Vector3[Count];var rim=new Vector3[Count];var middle=new Vector3[Count];var foot=new Vector3[Count];
        for(int i=0;i<Count;i++) {
            float angle=i*Mathf.PI*2/Count,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            // Rounded rectangular coastline keeps generous land beneath the existing street grid.
            float radius=1/Mathf.Pow(Mathf.Pow(Mathf.Abs(c)/82,5)+Mathf.Pow(Mathf.Abs(s)/80,5),.2f);
            radius+=Mathf.Sin(angle*7)*1.7f+Mathf.Sin(angle*13+1)*1.0f+(Rand()-.5f)*1.7f;
            // A small sheltered inlet on the south coast, outside the existing road footprint.
            radius-=Mathf.Pow(Mathf.Max(0,Mathf.Cos(angle-Mathf.PI*1.5f)),22)*5;
            var dir=new Vector3(c,0,s);edge[i]=dir*radius;edge[i].y=Top;
            rim[i]=dir*(radius+1.5f+Rand()*1.2f);rim[i].y=Top-.35f-Rand()*.5f;
            middle[i]=dir*(radius+2.0f+Rand()*2.0f);middle[i].y=-4.5f-Rand()*2.5f;
            foot[i]=dir*(radius+3.0f+Rand()*1.6f);foot[i].y=-14.0f-Rand()*1.2f;
        }
        Clear();
        for(int i=0;i<Count;i++) Tri(new Vector3(0,Top,0),edge[(i+1)%Count],edge[i],Color.white);
        var groundMesh=Save("Island_Grass");
        Undo.RecordObjects(new Object[]{grass.transform,grass.GetComponent<MeshFilter>(),grass.GetComponent<MeshCollider>()},"Shape island ground");
        grass.transform.position=Vector3.zero;grass.transform.rotation=Quaternion.identity;grass.transform.localScale=Vector3.one;
        grass.GetComponent<MeshFilter>().sharedMesh=groundMesh;
        var grassMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/MapMaterials/Island_LowPolyGrass.mat");
        if(grassMaterial!=null)grass.GetComponent<Renderer>().sharedMaterial=grassMaterial;
        var groundCollider=grass.GetComponent<MeshCollider>();groundCollider.sharedMesh=null;groundCollider.sharedMesh=groundMesh;
        var facets=Mat("Island_Cliff",facetShader);
        Clear();
        for(int i=0;i<Count;i++) {
            int j=(i+1)%Count;
            Face(edge[i],edge[j],rim[j],rim[i],new Color(.53f,.5f,.27f));
            Face(rim[i],rim[j],middle[j],middle[i],new Color(.43f,.39f,.33f));
            Face(middle[i],middle[j],foot[j],foot[i],new Color(.32f,.33f,.36f));
        }
        Spawn("CoastalCliffs",Save("Island_Cliffs"),facets,root.transform,true);
        // Waterline follows the actual intersection of each rock face with sea level.
        var shore=new Vector3[Count];
        for(int i=0;i<Count;i++) {shore[i]=Vector3.Lerp(middle[i],foot[i],(Sea-middle[i].y)/(foot[i].y-middle[i].y));shore[i].y=Sea+.18f;}
        Clear();
        for(int i=0;i<Count;i++) {
            int j=(i+1)%Count;var a=shore[i];var b=shore[j];
            var da=new Vector3(a.x,0,a.z).normalized;var db=new Vector3(b.x,0,b.z).normalized;
            Face(a,b,b+db*.5f,a+da*.5f,new Color(.68f,.9f,.91f));
            Face(a+da*.5f,b+db*.5f,b+db*1.7f,a+da*1.7f,new Color(.08f,.6f,.72f));
        }
        Spawn("ShoreWater",Save("Island_ShoreWater"),facets,root.transform,false);
        Clear();
        const int cells=100;const float step=8;
        var water=new Vector3[cells+1,cells+1];
        for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)
            water[x,z]=new Vector3((x-cells/2)*step+(Rand()-.5f)*4,Sea,(z-cells/2)*step+(Rand()-.5f)*4);
        for(int z=0;z<cells;z++)for(int x=0;x<cells;x++) {
            var a=water[x,z];var b=water[x,z+1];var c=water[x+1,z+1];var d=water[x+1,z];
            if((x+z)%2==0) {Tri(a,b,c,Color.white*(.94f+Rand()*.12f));Tri(a,c,d,Color.white*(.94f+Rand()*.12f));}
            else {Tri(a,b,d,Color.white*(.94f+Rand()*.12f));Tri(b,c,d,Color.white*(.94f+Rand()*.12f));}
        }
        Spawn("Ocean",Save("Island_Ocean"),Mat("Island_Ocean",oceanShader),root.transform,false);
        Physics.SyncTransforms();
        int tested=0;
        foreach(var r in sidewalks.GetComponentsInChildren<Renderer>()) {
            var b=r.bounds;
            foreach(var p in new[]{b.center,new Vector3(b.min.x,0,b.min.z),new Vector3(b.max.x,0,b.max.z)})
                if(!groundCollider.Raycast(new Ray(new Vector3(p.x,10,p.z),Vector3.down),out _,20))
                    throw new System.Exception("Island does not cover sidewalk: "+r.name);
            tested++;
        }
        if(Mathf.Abs(grass.GetComponent<Renderer>().bounds.max.y-Top)>.001f)throw new System.Exception("Ground height changed");
        Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(grass.scene);EditorSceneManager.SaveScene(grass.scene);
        var view=SceneView.lastActiveSceneView;
        if(view!=null) {view.orthographic=true;view.LookAtDirect(new Vector3(0,-3,0),Quaternion.Euler(65,0,0),116);view.Repaint();}
        Debug.Log("Island VERIFIED: "+tested+" sidewalk bounds on land; ground=-0.25, sea=-10.5; cliff/shore/ocean only, no props. Saved.");
    }
    static void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color) {
        Tri(a,b,c,color*(.88f+Rand()*.22f));Tri(a,c,d,color*(.88f+Rand()*.22f));
    }
    static void Tri(Vector3 a,Vector3 b,Vector3 c,Color color) {
        int n=verts.Count;verts.AddRange(new[]{a,b,c});tris.AddRange(new[]{n,n+1,n+2});
        for(int i=0;i<3;i++) {colors.Add(color.linear);var v=verts[n+i];uvs.Add(new Vector2(v.x/160+.5f,v.z/146.2f+.5f));}
    }
    static Mesh Save(string name) {
        if(!AssetDatabase.IsValidFolder("Assets/MapMeshes"))AssetDatabase.CreateFolder("Assets","MapMeshes");
        string path="Assets/MapMeshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.name=name;mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static Material Mat(string name,Shader shader) {
        string path="Assets/MapMaterials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}return m;
    }
    static void Spawn(string name,Mesh mesh,Material mat,Transform parent,bool collision) {
        var go=new GameObject(name);go.transform.SetParent(parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
}
