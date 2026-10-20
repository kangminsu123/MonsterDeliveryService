using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class MinimapRotationCheck
{
    [MenuItem("Tools/Gameplay/Minimap/Verify Rotation Anchor")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play required");
        var log=new List<string>{"Actual Unity Editor Play; production marker matrices checked at fixed positions. Baseline uses UnityCsReference RotateAroundPivot matrix order, no network transport simulation."};
        var method=typeof(DeliveryMinimap).GetMethod("PlayerMarkerMatrix",BindingFlags.Static|BindingFlags.NonPublic);
        var anchor=typeof(DeliveryMinimap).GetMethod("MarkerCenter",BindingFlags.Static|BindingFlags.NonPublic);
        var image=new Rect(1600,70,254,254);
        foreach(float scale in new[]{.5f,2f/3,1f,4f/3,2f})
        {
            var basis=Matrix4x4.Scale(new Vector3(scale,scale,1));
            foreach(var point in new[]{new Vector2(.5f,.5f),Vector2.zero,Vector2.one})
            {
                var center=(Vector2)anchor.Invoke(null,new object[]{image,point,22f});float oldError=0,newError=0;
                foreach(float yaw in new[]{0f,45f,90f,180f,270f,359f})
                {
                    var expected=basis.MultiplyPoint3x4(center);
                    var oldCenter=new Vector2(image.x+point.x*image.width,image.y+point.y*image.height);
                    var oldMatrix=Matrix4x4.Translate(oldCenter)*Matrix4x4.Rotate(Quaternion.Euler(0,0,yaw))*Matrix4x4.Translate(-oldCenter)*basis;
                    oldError=Mathf.Max(oldError,Vector3.Distance(oldMatrix.MultiplyPoint3x4(center),expected));
                    var actual=(Matrix4x4)method.Invoke(null,new object[]{basis,center,yaw});
                    newError=Mathf.Max(newError,Vector3.Distance(actual.MultiplyPoint3x4(center),expected));
                }
                log.Add("BASELINE scale="+scale+" mapPoint="+point+" stationary marker displacement="+oldError+" pixels");
                log.Add((newError<.002f?"PASS ":"FAIL ")+"scale="+scale+" mapPoint="+point+" fixed anchor max displacement="+newError);
            }
        }
        Directory.CreateDirectory("Docs/ChangeRecords/20261020/evidence");
        File.WriteAllLines("Docs/ChangeRecords/20261020/evidence/minimap-rotation-after.txt",log);
        Debug.Log("MINIMAP_ROTATION\n"+string.Join("\n",log));
    }
}
