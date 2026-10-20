using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class RoundLayoutSetup
{
    const string RootName="Layer_13_PlayerSpawns";
    const string Folder="Docs/ChangeRecords/20261018";
    [MenuItem("Tools/Gameplay/Map Layout/Build100 Spots50 Parcels and Corner Spawns")]
    public static void Build()
    {
        if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="SampleScene")throw new Exception("Open SampleScene in Edit Mode.");
        var grass=GameObject.Find("Layer_00_GrassBase").GetComponent<Renderer>().bounds;
        var navigation=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Resources/Navigation/ExplorationIsland.asset");
        if(!navigation)throw new Exception("Existing island navigation data missing.");
        var nav=NavMesh.AddNavMeshData(navigation);var corners=new List<Vector3>();
        try
        {
            Physics.SyncTransforms();
            if(!NavMesh.SamplePosition(new Vector3(grass.center.x,0,grass.center.z),out var center,20,NavMesh.AllAreas))throw new Exception("No walkable map center.");
            foreach(var sign in new[]{new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-1)})
            {
                var edge=new Vector3(sign.x<0?grass.min.x:grass.max.x,0,sign.y<0?grass.min.z:grass.max.z);
                var candidates=new List<Vector3>();
                for(float x=3;x<grass.size.x*.25f;x+=1.25f)for(float z=3;z<grass.size.z*.25f;z+=1.25f)candidates.Add(edge-new Vector3(sign.x*x,0,sign.y*z));
                bool found=false;
                foreach(var point in candidates.OrderBy(p=>(p-edge).sqrMagnitude))
                {
                    if(!PlayerGround(point,out var hit)||!SafePlayerFloor(hit.point))continue;
                    if(!NavMesh.SamplePosition(hit.point,out var walkable,.4f,NavMesh.AllAreas))continue;
                    var route=new NavMeshPath();if(!NavMesh.CalculatePath(walkable.position,center.position,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete)continue;
                    if(!PlayerGround(walkable.position,out var ground)||!SafePlayerFloor(ground.point))continue;
                    corners.Add(ground.point+Vector3.up*DeliveryRobotVisual.HoverClearance);found=true;break;
                }
                if(!found)throw new Exception("No clear, reachable corner spawn near "+edge+"; saved scene preserved.");
            }
        }
        finally{nav.Remove();}
        ParcelSpawnSetup.Build();
        var old=GameObject.Find(RootName);if(old)Undo.DestroyObjectImmediate(old);
        var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Create corner spawns");
        string[] names={"SouthWest","NorthWest","NorthEast","SouthEast"};
        for(int i=0;i<4;i++)
        {
            var child=new GameObject("Spawn_"+names[i]).transform;child.SetParent(root.transform,false);child.position=corners[i];
            var inward=grass.center-child.position;inward.y=0;child.rotation=Quaternion.LookRotation(inward);
        }
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Verify();Selection.activeGameObject=root;
    }
    [MenuItem("Tools/Gameplay/Map Layout/Verify Corner Spawns")]
    public static void Verify()
    {
        var root=GameObject.Find(RootName);if(!root||root.transform.childCount!=4)throw new Exception("Four corner spawns required.");
        var report=new List<string>();var positions=new List<Vector3>();Physics.SyncTransforms();
        foreach(Transform point in root.transform)
        {
            var floor=point.position-Vector3.up*DeliveryRobotVisual.HoverClearance;
            if(!PlayerGround(floor,out var hit)||Mathf.Abs(hit.point.y-floor.y)>.02f||!SafePlayerFloor(floor))throw new Exception("Unsafe spawn "+point.name);
            positions.Add(point.position);report.Add("PASS "+point.name+" "+point.position.ToString("F3")+" floor="+hit.collider.transform.root.name);
        }
        if(positions.Select(p=>new Vector2(Mathf.Sign(p.x),Mathf.Sign(p.z))).Distinct().Count()!=4)throw new Exception("Expected four different map corners.");
        Directory.CreateDirectory(Folder+"/evidence");File.WriteAllLines(Folder+"/evidence/corner-layout-check.txt",report);
        File.WriteAllText(Folder+"/source/player-corner-spawns.json",JsonUtility.ToJson(new Positions{positions=positions.ToArray()},true));
        Debug.Log("CORNER_LAYOUT_OK:4 clear corner spawn regions saved.");
    }
    public static bool PlayerGround(Vector3 point,out RaycastHit ground)
    {
        var hits=Physics.RaycastAll(new Vector3(point.x,40,point.z),Vector3.down,60,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>h.collider.transform.root.name!="Layer_12_TestParcelDrops"&&!(h.collider is CharacterController)).OrderBy(h=>h.distance).ToArray();
        ground=hits.Length>0?hits[0]:default;
        if(hits.Length==0||ground.normal.y<.999f||ground.point.y>=.5f||ground.point.y<-.5f)return false;
        string root=ground.collider.transform.root.name;
        return root=="Layer_00_GrassBase"||root=="Layer_01_Sidewalks"||root=="Layer_02_Roads";
    }
    public static bool SafePlayerFloor(Vector3 floor)
    {
        if(!TestParcelDrops.Clear(floor))return false;
        // The hovering1.4m capsule reaches2.4m above the ground, higher than a parcel clearance box.
        var room=new Bounds(floor+Vector3.up*1.4f,new Vector3(2.4f,2.7f,2.4f));
        if(Physics.OverlapBox(room.center,room.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c.transform.root.name!="Layer_12_TestParcelDrops"&&!(c is CharacterController)))return false;
        var plants=GameObject.Find("Layer_07_ReferenceLandscape");
        if(plants&&plants.GetComponentsInChildren<Renderer>().Any(r=>r.bounds.Intersects(room)))return false;
        foreach(float x in new[]{-.65f,0,.65f})foreach(float z in new[]{-.65f,0,.65f})
            if(!PlayerGround(floor+new Vector3(x,0,z),out var hit)||Mathf.Abs(hit.point.y-floor.y)>.035f)return false;
        return true;
    }
    [Serializable]sealed class Positions{public Vector3[] positions;}
}
