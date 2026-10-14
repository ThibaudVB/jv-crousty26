// Validate authored input, transition and real SceneBase loading in the isolated project.
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class NameEntryChecks
{
    const string Key="NameEntryChecks";
    static double deadline,ready;
    static CroustyMenu menu;
    static CroustyNameEntry entry;
    static int errors,oldTextures,oldHud;
    static NameEntryChecks()
    {
        deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{
            if(SessionState.GetInt(Key,0)==0)return;
            if(s.Contains("UnityEditor.Search."))return;
            if(t==LogType.Error&&m=="No texture data is available to upload.")oldTextures++;
            else if(t==LogType.Error&&m=="HUD : assigner les panels et indications dans la scene.")oldHud++;
            else if(t==LogType.Error||t==LogType.Exception)errors++;
        };
    }
    public static void Run()
    {
        Backup(CroustyPlayerProfile.NameKey);Backup(CroustyLeaderboardScores.StorageKey);
        SessionState.SetBool("NameEntryChecks.BestExists",PlayerPrefs.HasKey("Crousty.BestScore"));
        SessionState.SetInt("NameEntryChecks.Best",PlayerPrefs.GetInt("Crousty.BestScore"));
        PlayerPrefs.DeleteKey(CroustyPlayerProfile.NameKey);
        PlayerPrefs.DeleteKey(CroustyLeaderboardScores.StorageKey);PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Scenes/CroustyMainMenu.unity");
        SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
    }
    static void Backup(string key)
    {SessionState.SetBool("NameEntryChecks.Exists."+key,PlayerPrefs.HasKey(key));SessionState.SetString("NameEntryChecks.Value."+key,PlayerPrefs.GetString(key));}
    static void Restore()
    {
        foreach(var key in new[]{CroustyPlayerProfile.NameKey,CroustyLeaderboardScores.StorageKey})
        {
            if(SessionState.GetBool("NameEntryChecks.Exists."+key,false))PlayerPrefs.SetString(key,SessionState.GetString("NameEntryChecks.Value."+key,""));
            else PlayerPrefs.DeleteKey(key);
        }
        if(SessionState.GetBool("NameEntryChecks.BestExists",false))PlayerPrefs.SetInt("Crousty.BestScore",SessionState.GetInt("NameEntryChecks.Best",0));
        else PlayerPrefs.DeleteKey("Crousty.BestScore");
        PlayerPrefs.Save();
    }
    static void Check(bool value,string label)
    {if(!value)throw new Exception("NAME_ENTRY_FAILED: "+label);Debug.Log("NAME_ENTRY_OK: "+label);}
    static void Next(int stage,double delay){SessionState.SetInt(Key,stage);ready=EditorApplication.timeSinceStartup+delay;}
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Name entry test timed out");
            if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1||EditorApplication.timeSinceStartup<ready)return;
            if(stage==1)
            {
                menu=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();entry=menu.nameEntry;
                Check(entry!=null&&entry.nameInput!=null&&entry.playButton!=null&&entry.departure!=null,"Input, play and animation references are authored");
                Check(!entry.gameObject.activeSelf,"Name panel is initially hidden");
                var start=menu.mainPanel.GetComponentsInChildren<Button>(true).First(b=>b.name=="JOUER");
                start.onClick.Invoke();
                Check(menu.IsNameEntryOpen&&SceneManager.GetActiveScene().name=="CroustyMainMenu","START opens name entry before loading the game");
                Next(2,3);return;
            }
            if(stage==2)
            {
                Check(entry.nameInput.isFocused&&!entry.playButton.interactable,"Empty field receives focus and cannot launch");
                var click=new PointerEventData(EventSystem.current)
                {position=RectTransformUtility.WorldToScreenPoint(null,entry.nameInput.transform.position),button=PointerEventData.InputButton.Left};
                var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(click,hits);
                Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<TMP_InputField>()==entry.nameInput,
                    "Orange name field receives actual mouse clicks, including its caret");
                EventSystem.current.SetSelectedGameObject(entry.playButton.gameObject);
                click.pointerPressRaycast=hits[0];
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,click,ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,click,ExecuteEvents.pointerClickHandler);
                Check(EventSystem.current.currentSelectedGameObject==entry.nameInput.gameObject,"Mouse click restores input selection after another control");
                Check(CroustyPlayerProfile.PlayerName=="You","Legacy games retain an English fallback name");
                entry.nameInput.text="   ";entry.ConfirmPlay();
                Check(!entry.IsLaunching&&!entry.playButton.interactable,"Whitespace name cannot start the game");
                Check(CroustyPlayerProfile.Normalize("  Éloïse   Test  ")=="Éloïse Test","Accented names survive whitespace normalization");
                Check(CroustyPlayerProfile.Normalize(new string('A',40)).Length==20,"Names are limited to twenty characters");
                entry.nameInput.text="";Capture("name-entry-empty",1696,960);
                entry.nameInput.text="Thibaud";
                Check(entry.playButton.interactable,"Entering a name enables JOUER");
                Capture("name-entry-preview",1696,960);Capture("name-entry-4x3",1280,960);Capture("name-entry-wide",2520,1080);
                entry.Back();Next(3,.8);return;
            }
            if(stage==3)
            {
                Check(!menu.IsNameEntryOpen&&menu.mainPanel.activeSelf&&!entry.gameObject.activeSelf,"BACK returns to the main menu");
                Check(CroustyPlayerProfile.StoredName=="","Cancelling does not save a draft name");
                CroustyPlayerProfile.Save(" Éloïse   Test ");
                menu.Play();Next(4,.8);return;
            }
            if(stage==4)
            {
                Check(entry.nameInput.text=="Éloïse Test","Reopening prefills the saved name");
                entry.nameInput.text="Thibaud";
                entry.nameInput.onSubmit.Invoke(entry.nameInput.text);
                Check(entry.IsLaunching&&CroustyPlayerProfile.PlayerName=="Thibaud","Enter validates and persists the player name");
                entry.ConfirmPlay();entry.Back();
                Check(menu.IsNameEntryOpen&&!entry.controls.interactable,"Repeated launch and back are blocked during departure");
                Next(5,.55);return;
            }
            if(stage==5)
            {
                if(SceneManager.GetActiveScene().name=="CroustyMainMenu")
                {
                    if(entry.departure.GetCurrentAnimatorStateInfo(0).normalizedTime<.95f)return;
                    Check(!CroustyLoadingScreen.IsLoading,"Loading waits for the departure title to finish and hold");
                    Check(entry.hint.text=="LET'S GO!","Departure caption is English");
                    Check(entry.departure.GetCurrentAnimatorStateInfo(0).IsName("Départ"),"Departure plays the authored goofy animation");
                    var wipe=entry.transform.Find("Volet orange de départ").GetComponent<CanvasGroup>();
                    Check(wipe.alpha>.99f,"Orange wipe covers the screen before game activation");
                    Capture("name-entry-departure",1696,960);
                }
                Next(6,1);return;
            }
            if(SceneManager.GetActiveScene().name!="SceneBase")return;
            Check(UnityEngine.Object.FindAnyObjectByType<BusController>()!=null,"Confirmed name launches actual SceneBase");
            Check(CroustyPlayerProfile.PlayerName=="Thibaud","Profile survives scene loading");
            var results=UnityEngine.Object.FindAnyObjectByType<PoliceGameOverManager>();
            results.TriggerPoliceGameOver();
            Check(CroustyLeaderboardScores.Load().entries.Any(e=>e.id==CroustyLeaderboardScores.Load().latestId&&e.player=="Thibaud"),
                "Actual end-of-run score records the entered player name");
            Check(errors==0&&oldTextures<=2&&oldHud<=1,"No new Unity runtime errors");
            Restore();Debug.Log("NAME_ENTRY_CHECKS_DONE failures=0");SessionState.SetInt(Key,0);EditorApplication.Exit(0);
        }
        catch(Exception e){Restore();Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
    static void Capture(string name,int width,int height)
    {
        var canvas=entry.GetComponentInParent<Canvas>();var original=canvas.renderMode;var originalCamera=canvas.worldCamera;
        var camera=new GameObject("Name preview camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(7,153,243,255);camera.nearClipPlane=.1f;camera.farClipPlane=100;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var target=new RenderTexture(width,height,24);camera.targetTexture=target;
        Canvas.ForceUpdateCanvases();foreach(var text in entry.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        File.WriteAllBytes("../"+name+".png",image.EncodeToPNG());
        var corners=new Vector3[4];entry.nameInput.GetComponent<RectTransform>().GetWorldCorners(corners);
        foreach(var point in entry.IsLaunching ? new Vector3[0] : corners)
        {
            var viewport=camera.WorldToViewportPoint(point);
            Check(viewport.x>=-.01f&&viewport.x<=1.01f&&viewport.y>=-.01f&&viewport.y<=1.01f,"Name field fits "+width+"x"+height);
        }
        RenderTexture.active=previous;camera.targetTexture=null;target.Release();canvas.renderMode=original;canvas.worldCamera=originalCamera;
        UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
    }
}
