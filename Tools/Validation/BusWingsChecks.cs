// Run authoring and Play checks only in the isolated validation project.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class BusWingsChecks
{
    const string Key="BusWingsChecks.Stage";
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static double ready, deadline;
    static BusWings wings;
    static BusFlightController flight;
    static Rigidbody body;
    static Transform root;
    static Quaternion pose;
    static int errors, existingHudErrors;
    static BusWingsChecks()
    {
        deadline=EditorApplication.timeSinceStartup+150;
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{
            if(SessionState.GetInt(Key,0)<=0)return;
            // The original SceneBase contains an older CroustyHudControls instance
            // without the prompt references required by its current script.
            if(t==LogType.Error&&m=="HUD : assigner les panels et indications dans la scene.")existingHudErrors++;
            else if(t==LogType.Error||t==LogType.Exception)errors++;
        };
    }
    public static void Author()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/SceneBase.unity");
            var controller=UnityEngine.Object.FindAnyObjectByType<BusFlightController>();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Jojo/Resources/CroustyAile.fbx");
            Check(model!=null&&model.GetComponentInChildren<MeshFilter>()!=null,"Wing model imports with a mesh");
            var component=controller.GetComponent<BusWings>();
            if(component==null)component=controller.gameObject.AddComponent<BusWings>();
            component.wingModel=model;
            component.modelRotation=Vector3.zero;
            component.height=0f;
            component.alongBus=.1f;
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("WINGS_MODEL_BOUNDS "+model.GetComponentInChildren<MeshFilter>().sharedMesh.bounds);
            Debug.Log("WINGS_AUTHOR_OK");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
    }
    static object Get(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
    static void Check(bool value,string label)
    {if(!value)throw new Exception("WINGS_FAILED: "+label);Debug.Log("WINGS_OK: "+label);}
    static void Next(int stage,double delay){SessionState.SetInt(Key,stage);ready=EditorApplication.timeSinceStartup+delay;}
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Wings test timeout");
            if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1||EditorApplication.timeSinceStartup<ready)return;
            if(stage==1)
            {
                Physics.simulationMode=SimulationMode.Script;
                flight=UnityEngine.Object.FindAnyObjectByType<BusFlightController>();
                wings=flight.GetComponent<BusWings>();body=flight.GetComponent<Rigidbody>();
                Check(wings!=null&&wings.enabled&&wings.wingModel!=null,"Wings installed on the flight vehicle with model assigned");
                root=(Transform)Get(wings,"wingsRoot");
                Check(root!=null&&!root.gameObject.activeSelf,"Wings hidden on the ground");
                Check(root.childCount==2&&root.GetComponentsInChildren<Collider>(true).Length==0,"Two visual wings introduce no collision obstacles");
                var inventory=flight.GetComponent<PowerUpInventory>();inventory.Initialize();
                flight.AddCharges(1,3);
                Check(inventory.PrimaryPower==PowerUpInventory.PowerUpType.Flight,"Flight bonus equipped through inventory");
                body.position=new Vector3(0,300,0);body.rotation=Quaternion.identity;
                body.linearVelocity=Vector3.zero;
                Check(inventory.TryActivatePrimary(),"Real bonus activation succeeds");
                Check(flight.CurrentMode==BusFlightController.FlightMode.Flying&&flight.UsesRemaining==0,"Flight starts and consumes one charge");
                Next(2,.8);return;
            }
            if(stage==2)
            {
                Check(root.gameObject.activeSelf&&(float)Get(wings,"deploy")>.99f,"Wings deploy during flight");
                var meshes=root.GetComponentsInChildren<MeshRenderer>();
                Check(meshes.Length==2&&meshes[0].sharedMaterial!=null&&!meshes[0].sharedMaterial.shader.name.Contains("Error"),"Both wing meshes render with valid material");
                var hinges=(Transform[])Get(wings,"hinges");pose=hinges[0].localRotation;
                var right=root.GetChild(0).localPosition;var left=root.GetChild(1).localPosition;
                Check(right.x>0&&left.x<0&&Mathf.Abs(right.x+left.x)<.001f,"Wings attach symmetrically to vehicle sides");
                Capture();Next(3,.1);return;
            }
            if(stage==3)
            {
                Check(Quaternion.Angle(pose,((Transform[])Get(wings,"hinges"))[0].localRotation)>1,"Wings visibly flap over time");
                body.linearVelocity=Vector3.up*5;Next(4,.8);return;
            }
            if(stage==4)
            {
                Check((float)Get(wings,"flapMultiplier")>1.5f,"Flapping accelerates while climbing");
                Call(flight,"StartFalling");Next(5,.7);return;
            }
            if(stage==5)
            {
                Check(flight.IsFalling&&root.gameObject.activeSelf&&(float)Get(wings,"flapMultiplier")>2.5f,"Falling preserves wings and triggers panic flapping");
                var surface=new GameObject("Validation landing surface").AddComponent<BoxCollider>();
                surface.transform.position=body.position-Vector3.up*10;
                Call(flight,"FinishLanding",body.position,Vector3.up,surface);
                UnityEngine.Object.Destroy(surface.gameObject);
                Check(!flight.IsAirborne,"Landing restores ground flight state");Next(6,.8);return;
            }
            Check(!root.gameObject.activeSelf&&(float)Get(wings,"deploy")==0,"Wings fold away after landing");
            Check(errors==0&&existingHudErrors<=1,"No new Unity runtime errors; existing HUD issue counted separately");
            Debug.Log("WINGS_CHECKS_DONE failures=0");
            SessionState.SetInt(Key,0);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
    static void Capture()
    {
        var camera=new GameObject("Wings validation camera").AddComponent<Camera>();
        camera.transform.position=body.position+new Vector3(10,6,-10);
        camera.transform.LookAt(body.position+Vector3.up);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.18f,.26f);
        camera.nearClipPlane=.1f;camera.farClipPlane=70;
        var target=new RenderTexture(1440,900,24);camera.targetTexture=target;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes("../bus-wings-preview.png",image.EncodeToPNG());
        RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(camera.gameObject);
        UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);
    }
}
