using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class HeldItemCheck
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    [MenuItem("Tools/Gameplay/Held Items/Verify")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        EditorApplication.isPaused = false;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var type = game.GetType(); object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game,v);
        void UpdateHeld() => type.GetMethod("UpdateHeldWeapon",Flags).Invoke(game,null);
        var inv = (WeaponKind[])Get("inventory"); var ammo = (int[])Get("weaponAmmo");
        var saved = (WeaponKind[])inv.Clone(); var savedAmmo = (int[])ammo.Clone();
        int slot = (int)Get("selectedSlot"); bool tp = game.thirdPersonPreview, background = Application.runInBackground;
        var originalKeyboard = Keyboard.current; var keyboard = InputSystem.AddDevice<Keyboard>();
        var report = new List<string>();
        void Check(bool ok,string message) { if (!ok) throw new Exception("HELD_ITEM: "+message); report.Add("PASS "+message); }
        void Equip(WeaponKind kind) { inv[0]=kind; ammo[0]=3; Set("selectedSlot",0); UpdateHeld(); }
        Application.runInBackground=true;
        try
        {
            yield return null;
            Check(!(bool)Get("itemPlaytestEnabled"),"item attacks remain gated");
            Check(File.ReadAllBytes("Assets/Resources/UI/InGame/item_BoxingGloves.png").SequenceEqual(File.ReadAllBytes("Assets/Art/DeliveryItemConcepts/boxing_glove_single_v1.png")),"single boxing glove icon bytes are applied");
            Check(File.ReadAllBytes("Assets/Resources/UI/InGame/item_WorkGloves.png").SequenceEqual(File.ReadAllBytes("Assets/Art/DeliveryItemConcepts/work_glove_single_v1.png")),"single work glove icon bytes are applied");
            game.thirdPersonPreview=false; Equip(WeaponKind.None); yield return null;
            Check(Get("heldWeapon")==null,"empty selection has no held model");
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if(kind==WeaponKind.None)continue;
                Equip(kind); yield return new WaitForSeconds(.25f);
                var held=(GameObject)Get("heldWeapon");
                if(kind==WeaponKind.RunningShoes || kind==WeaponKind.BubbleWrap)
                { Check(!held,"effect-only item has no model or arms: "+kind); continue; }
                Check(held && held.transform.parent == Camera.main.transform,"first-person camera-local carrier: "+kind);
                Check(held.GetComponentsInChildren<Collider>().Length==0,"held art cannot block interaction or movement: "+kind);
                var item=held.GetComponentInChildren<DeliveryHeldItem>(); var view=(Camera)Get("heldCamera");
                var renderers=item.GetComponentsInChildren<Renderer>();
                var projected=renderers.SelectMany(r=>Corners(r.bounds)).Select(view.WorldToViewportPoint).ToArray();
                Check(projected.Any(p=>p.z>.05f && p.x>.05f && p.x<.95f && p.y>.05f && p.y<.95f),"visible item geometry in first-person frame: "+kind);
                Check(item.transform.localPosition.z>=.8f && view.enabled && view.clearFlags==CameraClearFlags.Depth && view.cullingMask==(1<<31),"forward placement and dedicated overlay camera: "+kind);
                Check(held.GetComponentsInChildren<DeliveryRobotVisual>().Length==0 && !held.GetComponentsInChildren<Transform>().Any(t=>t.name=="FirstPersonArms"),"equipping does not create extra hands: "+kind);
                // Actual authored mesh front, before the saved presentation rotation.
                var sourceFront=kind==WeaponKind.Plunger?Vector3.down:Vector3.back;
                var forward=view.transform.InverseTransformDirection(item.transform.TransformDirection(sourceFront));
                Check(item.paired ? forward.z>.75f && forward.y>.5f : forward.z>.999f,"working face points forward; gloves use a raised guard: "+kind);
                if(item.paired)
                {
                    var centers=renderers.Select(r=>view.WorldToViewportPoint(r.bounds.center)).ToArray();
                    Check(centers.Length==2 && centers.Any(c=>c.x<.45f) && centers.Any(c=>c.x>.55f) && centers.All(c=>c.y>.05f && c.y<.45f),"both worn gloves sit below and beside the crosshair: "+kind);
                }
                UpdateHeld(); Check((GameObject)Get("heldWeapon")==held,"unchanged selection reuses its model: "+kind);
                yield return new WaitForEndOfFrame();
                Capture("held-natural-"+kind+"-20261009.png");
            }
            inv[0]=WeaponKind.BoxingGloves; inv[1]=WeaponKind.WorkGloves; inv[2]=WeaponKind.FountainPen;
            Set("selectedSlot",0); UpdateHeld(); yield return new WaitForSeconds(.25f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit2)); keyboard.MakeCurrent();
            yield return null; yield return null;
            Check((int)Get("selectedSlot")==1 && (WeaponKind)Get("heldKind")==WeaponKind.WorkGloves,"real 2 key changes selected held item");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G)); yield return null; yield return null;
            Check(inv[1]==WeaponKind.None && !(GameObject)Get("heldWeapon"),"real G key removes selected model with no dropped copy");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            Equip(WeaponKind.BoxingGloves); game.thirdPersonPreview=true; UpdateHeld(); yield return new WaitForSeconds(.25f);
            var third=(GameObject)Get("heldWeapon"); var tpitem=third.GetComponentInChildren<DeliveryHeldItem>();
            Check(third.transform.parent!=Camera.main.transform && !((Camera)Get("heldCamera")).enabled,"third-person switches back to normal world art");
            Check(tpitem.paired && tpitem.GetComponentsInChildren<Transform>().Count(t=>t.name=="Hand_R" || t.name=="Hand_L")==2,"gloves have independent left/right parts");
            var visual=(DeliveryRobotVisual)Get("playerVisual");
            var rightPart=tpitem.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand_R");
            var leftPart=tpitem.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand_L");
            Check(visual.transform.InverseTransformPoint(rightPart.position).x>0 && visual.transform.InverseTransformPoint(leftPart.position).x<0,"turned gloves remain on their physical right and left sides");
            yield return new WaitForEndOfFrame(); Capture("held-natural-third-person-20261009.png");
            game.thirdPersonPreview=false; Equip(WeaponKind.ToyHammer); yield return null;
            Check(((GameObject)Get("heldWeapon")).transform.parent==Camera.main.transform,"return to first-person rebuilds visible presentation");
            int budget=ammo[0];type.GetMethod("UseWeapon",Flags).Invoke(game,null);
            Check(ammo[0]==budget,"presentation does not enable item use or consume budgets");
            Directory.CreateDirectory("art-review");File.WriteAllLines("art-review/held-natural-check-20261009.txt",report);
            Debug.Log("HELD_ITEMS_OK: "+report.Count+" checks passed.\n"+string.Join("\n",report));
        }
        finally
        {
            Array.Copy(saved,inv,3);Array.Copy(savedAmmo,ammo,3);Set("selectedSlot",slot);game.thirdPersonPreview=tp;UpdateHeld();
            InputSystem.RemoveDevice(keyboard);originalKeyboard?.MakeCurrent();Application.runInBackground=background;
            EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
    }
    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for(int i=0;i<8;i++)yield return b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
    }
    static void Capture(string name)
    {
        var texture=ScreenCapture.CaptureScreenshotAsTexture();
        try { Directory.CreateDirectory("art-review");File.WriteAllBytes("art-review/"+name,texture.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(texture); }
    }
    [MenuItem("Tools/Gameplay/Held Items/Preview Boxing")]
    public static void PreviewBoxing() => Preview(WeaponKind.BoxingGloves);
    [MenuItem("Tools/Gameplay/Held Items/Preview Work Gloves")]
    public static void PreviewGloves() => Preview(WeaponKind.WorkGloves);
    [MenuItem("Tools/Gameplay/Held Items/Preview Ice Cream")]
    public static void PreviewIceCream() => Preview(WeaponKind.IceCream);
    static void Preview(WeaponKind kind)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play Mode required.");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var inv=(WeaponKind[])game.GetType().GetField("inventory",Flags).GetValue(game);
        var ammo=(int[])game.GetType().GetField("weaponAmmo",Flags).GetValue(game);
        inv[0]=WeaponKind.BoxingGloves;inv[1]=WeaponKind.WorkGloves;inv[2]=WeaponKind.IceCream;
        ammo[0]=3;ammo[1]=1;
        ((float[])game.GetType().GetField("weaponSeconds",Flags).GetValue(game))[2]=4;
        game.GetType().GetField("selectedSlot",Flags).SetValue(game,kind==WeaponKind.BoxingGloves?0:kind==WeaponKind.WorkGloves?1:2);
        game.thirdPersonPreview=false;game.GetType().GetMethod("UpdateHeldWeapon",Flags).Invoke(game,null);
        var visual=(DeliveryRobotVisual)game.GetType().GetField("playerVisual",Flags).GetValue(game);
        Debug.Log("HELD_PREVIEW: "+kind+"; bones "+string.Join(";",visual.model.GetComponentsInChildren<Transform>().Where(t=>t.name=="Arm.R"||t.name=="Arm.L").Select(t=>t.name+"="+visual.transform.InverseTransformPoint(t.position))));
        game.StartCoroutine(WarmPreview(game));
        EditorApplication.QueuePlayerLoopUpdate();
    }
    static IEnumerator WarmPreview(MonsterDeliveryPrototype game)
    {
        bool background=Application.runInBackground;
        Application.runInBackground = true;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        yield return new WaitForSeconds(.4f);
        Application.runInBackground=background;
        EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
    }
}
