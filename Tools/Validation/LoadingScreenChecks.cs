using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LoadingScreenChecks
{
    const string Key = "LoadingScreenChecks";
    static double deadline, started;
    static float previous;
    static bool sawCity, sawFade, captured;
    static int samples, errors, oldTextures, oldHud;
    static Vector3 busPosition;
    static LoadingScreenChecks()
    {
        deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (m,s,t) => {
            if (SessionState.GetInt(Key,0)==0) return;
            if (t==LogType.Error && m=="No texture data is available to upload.") oldTextures++;
            else if (t==LogType.Error && m=="HUD : assigner les panels et indications dans la scene.") oldHud++;
            else if (t==LogType.Error || t==LogType.Exception) errors++;
        };
    }
    public static void Run()
    {
        SessionState.SetBool(Key+".exists", PlayerPrefs.HasKey(CroustyPlayerProfile.NameKey));
        SessionState.SetString(Key+".name", PlayerPrefs.GetString(CroustyPlayerProfile.NameKey));
        EditorSceneManager.OpenScene("Assets/Scenes/CroustyMainMenu.unity");
        SessionState.SetInt(Key,1); EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition, string label)
    { if(!condition) throw new Exception("LOADING_FAILED: "+label); }
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0); if(stage==0) return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Loading timed out");
            if(!EditorApplication.isPlaying) return;
            if(stage==1)
            {
                if(Time.timeSinceLevelLoad<1) return;
                var menu=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();
                Check(menu.nameEntry.loadingScreen!=null,"Prefab reference authored in name panel");
                menu.Play(); menu.nameEntry.nameInput.text="Test chargement"; menu.nameEntry.ConfirmPlay();
                SessionState.SetInt(Key,2); return;
            }
            var loader=UnityEngine.Object.FindAnyObjectByType<CroustyLoadingScreen>();
            if(stage==2)
            {
                if(loader==null) return;
                started=EditorApplication.timeSinceStartup;
                SessionState.SetInt(Key,3); return;
            }
            if(loader!=null)
            {
                Check(Time.timeScale==0,"Gameplay paused behind the overlay");
                Check(loader.Progress>=previous && loader.Progress<=1,"Monotonic progress");
                if(loader.Progress>previous) samples++;
                previous=loader.Progress;
                Check(!loader.errorReturn.activeSelf,"No loading failure");
                if(!captured && loader.Progress>.5f)
                {Capture(loader,"loading-screen",1920,1080); Capture(loader,"loading-screen-4x3",1024,768); captured=true;}
                if(SceneManager.GetActiveScene().name=="SceneBase")
                {
                    var city=UnityEngine.Object.FindAnyObjectByType<ProceduralCity>();
                    if(!sawCity) { busPosition=city.player.position; sawCity=true; }
                    Check(Vector3.Distance(busPosition,city.player.position)<.001f,"Bus stays still during prewarm");
                    Check(DeliveryManager.Instance.RunElapsed==0,"Delivery timer does not run during loading");
                    if(loader.Ready)
                    {
                        var center=city.ChunkAt(city.player.position);
                        for(int x=-2;x<=2;x++) for(int z=-2;z<=2;z++)
                            Check(city.IsAreaReady(city.Origin+new Vector3((center.x+x+.5f)*128,0,(center.y+z+.5f)*128)),"All 25 districts complete");
                        Check(city.PrewarmProgress==1 && !city.IsPrewarming,"Generation really finished");
                        Check(city.deliveries.shop!=null && city.deliveries.deliveryPoints.Count>0,"Shop and deliveries registered");
                    }
                    if(loader.panel.alpha<1 && loader.panel.alpha>0)
                    { sawFade=true; Check(loader.Ready && loader.Progress==1,"Fade waits for completion"); }
                }
                return;
            }
            Check(sawCity && sawFade && captured && samples>10,"Real scene, gradual bar, preview and opacity fade observed");
            Check(EditorApplication.timeSinceStartup-started>=5,"Minimum five seconds elapsed");
            Check(Time.timeScale==1 && !CroustyLoadingScreen.IsLoading,"Gameplay restored after fade");
            Check(DeliveryManager.Instance.PlayerName=="Test chargement","Entered name reaches the level");
            Check(errors==0 && oldTextures<=2 && oldHud<=1,"No new runtime errors");
            Debug.Log("LOADING_CHECKS_DONE failures=0 samples="+samples+" elapsed="+(EditorApplication.timeSinceStartup-started));
            Finish(0);
        }
        catch(Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code)
    {
        if(SessionState.GetBool(Key+".exists",false)) PlayerPrefs.SetString(CroustyPlayerProfile.NameKey,SessionState.GetString(Key+".name",""));
        else PlayerPrefs.DeleteKey(CroustyPlayerProfile.NameKey);
        PlayerPrefs.Save(); SessionState.SetInt(Key,0); EditorApplication.Exit(code);
    }
    static void Capture(CroustyLoadingScreen loader,string name,int width,int height)
    {
        var canvas=loader.GetComponent<Canvas>(); var old=canvas.renderMode;
        var camera=new GameObject("Loading preview camera").AddComponent<Camera>(); camera.transform.position=new Vector3(0,0,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.nearClipPlane=.1f; camera.farClipPlane=100;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var target=new RenderTexture(width,height,24);camera.targetTexture=target;
        Canvas.ForceUpdateCanvases();foreach(var text in loader.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var previousTarget=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        File.WriteAllBytes("../"+name+".png",image.EncodeToPNG());
        RenderTexture.active=previousTarget;camera.targetTexture=null;target.Release();canvas.renderMode=old;canvas.worldCamera=null;
        UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
    }
}
