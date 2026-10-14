using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EssentialOptionsChecks
{
    const string Key="EssentialOptionsChecks";
    static double ready,deadline;
    static CroustyMenu menu;
    static EssentialOptionsChecks(){EditorApplication.update+=Tick;deadline=EditorApplication.timeSinceStartup+180;}
    public static void Run()
    {
        foreach(CroustySettings.Option option in Enum.GetValues(typeof(CroustySettings.Option)))
        {
            string key="Crousty.Option."+option;
            SessionState.SetBool(key+".Exists",PlayerPrefs.HasKey(key));SessionState.SetInt(key+".Before",PlayerPrefs.GetInt(key));
        }
        EditorSceneManager.OpenScene("Assets/Scenes/CroustyMainMenu.unity");SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
    }
    static void Restore()
    {
        foreach(CroustySettings.Option option in Enum.GetValues(typeof(CroustySettings.Option)))
        {
            string key="Crousty.Option."+option;
            if(SessionState.GetBool(key+".Exists",false))PlayerPrefs.SetInt(key,SessionState.GetInt(key+".Before",0));else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception("OPTIONS_FAILED: "+text);Debug.Log("OPTIONS_OK: "+text);}
    static void Next(int stage){SessionState.SetInt(Key,stage);ready=EditorApplication.timeSinceStartup+1;}
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Options checks timed out");
            if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1||EditorApplication.timeSinceStartup<ready)return;
            if(stage==1)
            {
                menu=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();
                Check(menu.optionsPanel.transform.Find("Trait bleu")==null,"Blue full-width top line removed");
                Check(menu.optionsPanel.GetComponentsInChildren<CroustyOptionRow>(true).Length==12,"All twelve setting selectors are authored");
                var virtualPad=InputSystem.AddDevice<Gamepad>();
                CroustySettings.Set(CroustySettings.Option.Input,0);Check(CroustyInput.Pad==null&&!CroustyInput.UsesController,"Keyboard mode ignores a connected controller");
                CroustySettings.Set(CroustySettings.Option.Input,1);Check(CroustyInput.Pad!=null,"Controller mode reads the connected controller");
                InputSystem.RemoveDevice(virtualPad);
                if(Gamepad.current==null)Check(!CroustyInput.UsesController,"Disconnected controller falls back to keyboard");
                CroustySettings.Set(CroustySettings.Option.Input,0);
                CroustySettings.Set(CroustySettings.Option.Preset,0);
                var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                Check(pipeline!=null&&!AssetDatabase.Contains(pipeline),"Graphics preferences use a runtime pipeline clone");
                Check(Mathf.Approximately(pipeline.renderScale,.67f)&&pipeline.shadowDistance==0f&&pipeline.msaaSampleCount==1,"Performance preset applies to actual URP render scale, shadows and MSAA");
                CroustySettings.Set(CroustySettings.Option.Preset,2);
                Check(pipeline.renderScale==1&&pipeline.msaaSampleCount==4&&pipeline.shadowDistance==110,"High preset restores full render quality");
                CroustySettings.Set(CroustySettings.Option.FrameRate,0);CroustySettings.Set(CroustySettings.Option.VSync,0);
                Check(Application.targetFrameRate==30&&QualitySettings.vSyncCount==0,"Frame limiter and v-sync apply");
                CroustySettings.Set(CroustySettings.Option.RenderScale,2);
                Check(CroustySettings.Get(CroustySettings.Option.Preset)==3&&Mathf.Approximately(pipeline.renderScale,.8f),"Individual adjustment becomes custom and applies");
                CroustySettings.Set(CroustySettings.Option.Preset,1);CroustySettings.Set(CroustySettings.Option.FrameRate,1);
                menu.OpenSettings();Next(2);return;
            }
            if(stage==2)
            {
                var row=menu.categoryPanels[0].GetComponentInChildren<CroustyOptionRow>();
                row.GetComponent<Button>().onClick.Invoke();Check(CroustySettings.Get(CroustySettings.Option.Input)==1&&row.value.text=="CONTROLLER","Actual authored button changes persisted control mode and label");
                row.GetComponent<Button>().onClick.Invoke();
                Capture("options-controls",1696,960);menu.SelectCategory(1);Next(3);return;
            }
            if(stage==3)
            {
                Check(menu.categoryPanels[1].GetComponentsInChildren<Slider>().Length==3,"Master, ambience and effects sliders available");
                Capture("options-audio",1696,960);menu.SelectCategory(2);Next(4);return;
            }
            if(stage==4)
            {
                Capture("options-video",1696,960);Capture("options-video-4x3",1280,960);Capture("options-video-wide",2520,1080);
                menu.categoryPanels[2].GetComponent<CroustyVideoPages>().Toggle();Next(5);return;
            }
            if(stage==5)
            {
                Check(menu.categoryPanels[2].GetComponentsInChildren<CroustyOptionRow>().Length==6,"Details page has six live selectors");
                Capture("options-details",1696,960);Capture("options-details-4x3",1280,960);
                Check(menu.nameEntry.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text=="LET'S GO!"),"Departure title is English");
                Check(menu.nameEntry.departureHoldSeconds>=.65f,"Finished departure has a readable hold");
                Restore();SessionState.SetInt(Key,0);Debug.Log("ESSENTIAL_OPTIONS_CHECKS_DONE failures=0");EditorApplication.Exit(0);
            }
        }
        catch(Exception e){Debug.LogException(e);Restore();SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
    static void Capture(string name,int width,int height)
    {
        var canvas=menu.optionsPanel.GetComponentInParent<Canvas>();var mode=canvas.renderMode;var original=canvas.worldCamera;
        var camera=new GameObject("Options preview").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var target=new RenderTexture(width,height,24);camera.targetTexture=target;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        Canvas.ForceUpdateCanvases();foreach(var text in menu.optionsPanel.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
        foreach(var row in menu.optionsPanel.GetComponentsInChildren<CroustyOptionRow>())
        {
            var corners=new Vector3[4];row.GetComponent<RectTransform>().GetWorldCorners(corners);
            Check(corners.All(p=>{var v=camera.WorldToViewportPoint(p);return v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1;}),row.option+" fits "+width+"x"+height);
            Check(!row.value.isTextOverflowing,row.option+" value fits selector");
        }
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        Directory.CreateDirectory("Previews");File.WriteAllBytes("Previews/"+name+".png",image.EncodeToPNG());RenderTexture.active=before;
        canvas.renderMode=mode;canvas.worldCamera=original;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
    }
}
