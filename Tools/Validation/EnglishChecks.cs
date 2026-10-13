using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EnglishChecks
{
    const string Key="EnglishChecks";
    static double ready,deadline;
    static int errors;
    static readonly Regex French=new Regex(@"\b(RETOUR|REPRENDRE|OBJECTIF|LIVRAISONS|GÉRANT|COMMANDES|RÉGLAGES|CRÉDITS|prends|aucune|rejoins|épuisé|epuise|CHUTE|Passagers|Tournée|Récupère|Merci|CLAVIER|SOURIS)\b",RegexOptions.IgnoreCase);
    static EnglishChecks()
    {
        deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;
        Application.logMessageReceived+=(message,stack,type)=>
        {
            if(SessionState.GetInt(Key,0)==0||stack.Contains("UnityEditor.Search.")||message=="No texture data is available to upload.")return;
            if(type==LogType.Error||type==LogType.Exception)errors++;
        };
    }
    public static void Run()
    {
        foreach(var path in new[]{"Assets/Scenes/CroustyMainMenu.unity","Assets/SceneBase.unity","Assets/Jojo/SceneJojo.unity","Assets/Scene A moi.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var root in scene.GetRootGameObjects())foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))Check(!French.IsMatch(text.text),path+" / "+text.name+" is English");
            foreach(var root in scene.GetRootGameObjects())foreach(var text in root.GetComponentsInChildren<Text>(true))Check(!French.IsMatch(text.text),path+" legacy text is English");
            foreach(var root in scene.GetRootGameObjects())foreach(var image in root.GetComponentsInChildren<Image>(true))Check(image.sprite==null||AssetDatabase.GetAssetPath(image.sprite)!="Assets/Jojo/Resources/Gerant/titre.png",path+" has no French title image");
        }
        var loading=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Menu/Loading/Chargement.prefab");
        foreach(var text in loading.GetComponentsInChildren<TMP_Text>(true))Check(!French.IsMatch(text.text),"Loading text is English");
        Check(loading.GetComponent<CroustyLoadingScreen>().messages.All(m=>!m.Contains("C'EST")),"All loading phases are English");
        EditorSceneManager.OpenScene("Assets/Scenes/CroustyMainMenu.unity");SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception("ENGLISH_FAILED: "+label);Debug.Log("ENGLISH_OK: "+label);}
    static void Next(int stage,double delay=1){SessionState.SetInt(Key,stage);ready=EditorApplication.timeSinceStartup+delay;}
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("English checks timed out");
            if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1||EditorApplication.timeSinceStartup<ready)return;
            if(stage==1)
            {
                var menu=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();
                menu.OpenLeaderboard();menu.leaderboardPanel.GetComponent<CroustyLeaderboard>().Show(new CroustyLeaderboardScores.Board());
                Check(menu.leaderboardPanel.GetComponent<CroustyLeaderboard>().status.text.Contains("FINISH A GAME"),"Empty leaderboard is English at runtime");
                menu.CloseLeaderboard();menu.Play();Next(2);return;
            }
            if(stage==2)
            {
                var menu=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();
                menu.nameEntry.Refresh("");Check(menu.nameEntry.hint.text.StartsWith("ENTER YOUR NAME"),"Empty-name prompt stays English at runtime");
                menu.nameEntry.Refresh("Test");Check(menu.nameEntry.hint.text.StartsWith("READY"),"Valid-name prompt stays English at runtime");
                Capture("name-english",false);SceneManager.LoadScene("SceneBase");Next(3,3);return;
            }
            if(stage==3)
            {
                var hud=UnityEngine.Object.FindAnyObjectByType<CroustyHUD>();
                Check(hud.referencePickup.StartsWith("Pick up")&&hud.referenceDeliver.StartsWith("Deliver")&&hud.referenceComplete.StartsWith("Run complete")&&hud.referenceFailed.StartsWith("Run failed"),"All delivery objective states are English");
                var format=hud.GetType().GetMethod("FormatScore",BindingFlags.Instance|BindingFlags.NonPublic);
                Check((string)format.Invoke(hud,new object[]{6767})=="6.8k","HUD uses the English decimal separator");
                var gps=UnityEngine.Object.FindAnyObjectByType<BusGPS>();
                gps.SelectNext();Check(!French.IsMatch(gps.Status),"GPS runtime status is English");
                var manager=UnityEngine.Object.FindAnyObjectByType<GerantDialogue>();Check(manager.speakerName=="MANAGER","Manager title is English");
                foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))Check(!French.IsMatch(text.text),"Visible gameplay text is English: "+text.name);
                Capture("hud-english",true);
                Check(errors==0,"No new runtime errors");SessionState.SetInt(Key,0);Debug.Log("ENGLISH_CHECKS_DONE failures=0");EditorApplication.Exit(0);
            }
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
    static void Capture(string name,bool gameplay)
    {
        var camera=gameplay?Camera.main:new GameObject("English preview camera").AddComponent<Camera>();
        if(!gameplay){camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;camera.nearClipPlane=.1f;camera.farClipPlane=100;}
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
        var target=new RenderTexture(1696,960,24);var original=camera.targetTexture;camera.targetTexture=target;
        Canvas.ForceUpdateCanvases();foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))text.ForceMeshUpdate();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1696,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1696,960),0,0);image.Apply();Directory.CreateDirectory("Previews");File.WriteAllBytes("Previews/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=original;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
        target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);if(!gameplay)UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }
}
