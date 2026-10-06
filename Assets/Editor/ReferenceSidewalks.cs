using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Reference coordinates are pixels in the supplied 1312 x 1199 plan.
public static class ReferenceSidewalks
{
    static Transform root, group;
    static Material material;
    static float ground;
    static int count;
    const float S = 160f / 1312f;

    [MenuItem("Tools/Level/Inspect Concrete Detail")]
    public static void InspectConcrete()
    {
        var view=SceneView.lastActiveSceneView;
        if(view!=null) { view.orthographic=true; view.LookAtDirect(new Vector3(-40,0,30),Quaternion.Euler(60,0,0),14); view.Repaint(); }
    }

    [MenuItem("Tools/Level/Apply Gray Concrete Sidewalks")]
    public static void ApplyConcrete()
    {
        var shader=Shader.Find("Map/Sidewalk Concrete");
        if(shader==null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
            throw new System.Exception("Concrete shader unavailable or invalid");
        var sidewalks=GameObject.Find("Layer_01_Sidewalks");
        if(sidewalks==null) throw new System.Exception("Sidewalks missing");
        const string path="Assets/MapMaterials/Sidewalk_GrayConcrete.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) { mat=new Material(shader); AssetDatabase.CreateAsset(mat,path); }
        Undo.RecordObject(mat,"Light slab sidewalk material");
        mat.SetColor("_Color",new Color(0.84f,0.82f,0.76f));
        mat.SetColor("_JointColor",new Color(0.53f,0.52f,0.48f));
        mat.SetFloat("_JointWidth",0.025f);
        mat.SetFloat("_NoiseAmount",0.045f);
        EditorUtility.SetDirty(mat);
        var renderers=sidewalks.GetComponentsInChildren<Renderer>();
        foreach(var r in renderers) { Undo.RecordObject(r,"Apply gray sidewalk material"); r.sharedMaterial=mat; }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(sidewalks.scene);
        EditorSceneManager.SaveScene(sidewalks.scene);
        Debug.Log("Concrete VERIFIED: "+renderers.Length+" renderers assigned; shader supported, no shader errors.");
    }

    [MenuItem("Tools/Level/Rebuild Reference Sidewalks")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var grass = GameObject.Find("Layer_00_GrassBase");
        if (grass == null || scene.name != "SampleScene") throw new System.Exception("Expected grass and SampleScene");
        ground = grass.GetComponent<Renderer>().bounds.max.y;
        material = AssetDatabase.LoadAssetAtPath<Material>("Assets/MapMaterials/Sidewalk_Reference.mat");
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/MapMaterials/Sidewalk_White.mat"));
            material.color = new Color(0.79f, 0.75f, 0.66f);
            AssetDatabase.CreateAsset(material, "Assets/MapMaterials/Sidewalk_Reference.mat");
        }
        var concrete=AssetDatabase.LoadAssetAtPath<Material>("Assets/MapMaterials/Sidewalk_GrayConcrete.mat");
        if(concrete!=null) material=concrete;
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace sidewalks from reference");
        var old = GameObject.Find("Layer_01_Sidewalks");
        if (old != null) Undo.DestroyObjectImmediate(old);
        root = new GameObject("Layer_01_Sidewalks").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Reference sidewalks");
        count = 0;
        Path("Outer_Perimeter", false, 23, 24,
            453,0, 453,249, 145,249, 102,291, 102,863, 143,903,
            453,903, 453,1069, 544,1069, 544,903, 1168,903,
            1210,863, 1210,620, 1278,620, 1278,542, 1210,542,
            1210,291, 1158,249, 546,249, 546,0);
        Path("Upper_Left", false, 26, 20, 165,239, 165,88, 441,88);
        Path("Upper_Middle", false, 26, 20, 558,88, 813,88, 813,239);
        Path("Upper_Right", false, 26, 20, 845,239, 845,88, 1140,88, 1140,239);
        float[] left = {189,543,869};
        float[] right = {453,790,1125};
        for (int row=0; row<2; row++)
        for (int col=0; col<3; col++)
        {
            float top = row == 0 ? 331 : 622;
            float bottom = row == 0 ? 540 : 823;
            Path("Block_"+(row+1)+"_"+(col+1), true, 32, 22,
                left[col],top, right[col],top, right[col],bottom, left[col],bottom);
        }
        if (count < 300 || root.GetComponentsInChildren<BoxCollider>().Length != count)
            throw new System.Exception("Sidewalk validation failed");
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            if (Mathf.Abs(r.bounds.max.y-ground-0.18f)>0.002f) throw new System.Exception("Height mismatch");
        SidewalkCornerRepair.Repair();
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = null;
        var view = SceneView.lastActiveSceneView;
        if (view != null) { view.orthographic=true; view.LookAtDirect(new Vector3(0,0,7),Quaternion.Euler(90,0,0),88); view.Repaint(); }
        Debug.Log("ReferenceSidewalks VERIFIED: "+count+" cube tiles, 10 groups, rise 0.18, no roads. Ground="+ground);
    }

    static void Path(string name, bool closed, float radius, float width, params float[] xy)
    {
        group = new GameObject(name).transform; group.SetParent(root);
        int n=xy.Length/2;
        var p=new Vector2[n]; var a=new Vector2[n]; var b=new Vector2[n];
        for(int i=0;i<n;i++) p[i]=new Vector2(xy[i*2],xy[i*2+1]);
        for(int i=0;i<n;i++)
        {
            if(!closed && (i==0 || i==n-1)) {a[i]=b[i]=p[i]; continue;}
            var prev=p[(i+n-1)%n]; var next=p[(i+1)%n];
            float trim=Mathf.Min(radius,Vector2.Distance(prev,p[i])*0.45f,Vector2.Distance(next,p[i])*0.45f);
            if (name=="Outer_Perimeter" && (p[i].y==1069 || p[i].x==1278)) trim=0;
            a[i]=p[i]+(prev-p[i]).normalized*trim;
            b[i]=p[i]+(next-p[i]).normalized*trim;
        }
        for(int i=0;i<n;i++)
        {
            if (name=="Outer_Perimeter" && (p[i].y==1069 || p[i].x==1278))
                Tile(p[i]-Vector2.right*width*0.5f,p[i]+Vector2.right*width*0.5f,width,0);
            if(a[i]!=b[i])
            {
                var prev=a[i];
                for(int k=1;k<=6;k++) { float t=k/6f; var v=(1-t)*(1-t)*a[i]+2*(1-t)*t*p[i]+t*t*b[i]; Tile(prev,v,width,3.8f); prev=v; }
            }
            if(closed || i<n-1)
            {
                var end=a[(i+1)%n]; int steps=Mathf.Max(1,Mathf.RoundToInt(Vector2.Distance(b[i],end)/28));
                for(int j=0;j<steps;j++) Tile(Vector2.Lerp(b[i],end,j/(float)steps),Vector2.Lerp(b[i],end,(j+1f)/steps),width,0);
            }
        }
    }
    static void Tile(Vector2 a, Vector2 b, float width, float extra)
    {
        Vector2 c=(a+b)*0.5f; var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name="Paver_"+(++count).ToString("D4"); tile.transform.SetParent(group);
        tile.transform.position=new Vector3((c.x-656)*S,ground+0.09f,(599.5f-c.y)*S);
        tile.transform.rotation=Quaternion.Euler(0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,0);
        tile.transform.localScale=new Vector3((Vector2.Distance(a,b)+extra)*S,0.18f,width*S);
        tile.GetComponent<Renderer>().sharedMaterial=material;
    }
}
