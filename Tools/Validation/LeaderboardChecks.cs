// Run in the isolated project with -executeMethod LeaderboardChecks.Run.
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[InitializeOnLoad]
public static class LeaderboardChecks
{
    const string Key="LeaderboardChecksStage";
    const string TestKey="Crousty.Leaderboard.ValidationOnly";
    static double ready, deadline;
    static int errors, count, baselineTextureMessages;
    static CroustyMenu menu;
    static CroustyLeaderboard board;
    static CroustyLeaderboardScores.Board sample;
    static LeaderboardChecks()
    {
        deadline=EditorApplication.timeSinceStartup+120;
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{
            if(SessionState.GetInt(Key,0)<=0)return;
            // Reproduced twice in the unmodified menu by MenuBaselineProbe.
            if(t==LogType.Error&&m=="No texture data is available to upload.")baselineTextureMessages++;
            else if(t==LogType.Error||t==LogType.Exception)errors++;
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CroustyMainMenu.unity");
        SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string label)
    {if(!value)throw new Exception("LEADERBOARD_FAILED: "+label);Debug.Log("LEADERBOARD_OK: "+label);}
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Leaderboard check timeout");
            if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1)return;
            if(EditorApplication.timeSinceStartup<ready)return;
            if(stage==1)
            {
                menu=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();
                board=menu.leaderboardPanel.GetComponent<CroustyLeaderboard>();
                Check(board.podiums.Length==3&&board.rows.Length==5,"Three authored podiums and five rows");
                Check(menu.leaderboardPanel.GetComponentsInChildren<Animator>(true).Length==11,"Eleven editable animation controllers");
                PlayerPrefs.DeleteKey(TestKey);
                Check(CroustyLeaderboardScores.Load(TestKey).entries.Count==0,"Empty storage has no invented scores");
                int[] points={9700,9300,9000,8493,7890,7600,6790,100,50,8209};
                string[] names={"TastyCroustillant","Loubiteuhpromax","ZEUBLAWALOUH","PouletMan","Loubibe","Zhanna7e","Ericloubite","Low","Lower","Toi"};
                for(int i=0;i<points.Length;i++)CroustyLeaderboardScores.Record(points[i],names[i],TestKey);
                sample=CroustyLeaderboardScores.Load(TestKey);
                Check(sample.entries.Count==8&&sample.entries[0].score==9700&&sample.entries[7].score==6790,"Eight highest scores persist in descending order");
                CroustyLeaderboardScores.Record(6790,"Tie",TestKey);
                Check(CroustyLeaderboardScores.Load(TestKey).entries[7].player=="Ericloubite","Ties preserve existing records");
                PlayerPrefs.SetString(TestKey,"invalid json");
                Check(CroustyLeaderboardScores.Load(TestKey).entries.Count==0,"Malformed storage falls back to empty state");
                PlayerPrefs.DeleteKey(TestKey);PlayerPrefs.Save();
                count=AuthoredObjectCount();
                menu.OpenLeaderboard();
                Time.timeScale=0;
                ready=EditorApplication.timeSinceStartup+2;SessionState.SetInt(Key,2);return;
            }
            if(stage==2)
            {
                Check(menu.IsLeaderboardOpen&&!menu.mainPanel.activeSelf,"Leaderboard opens while time is paused");
                board.Show(new CroustyLeaderboardScores.Board());
                Check(board.podiums[0].score.text=="—","Empty podium displays a placeholder");
                Capture("leaderboard-empty",1696,960);
                board.Show(sample);
                Check(board.rows[1].ribbon.sprite==board.latestRibbon&&board.rows[0].ribbon.sprite==board.normalRibbon,"Latest qualifying run alone uses the orange row");
                Check(board.podiums[0].player.text=="TastyCroustillant"&&board.rows[1].score.text=="8209","Scores bind to correct authored slots");
                Capture("leaderboard-preview",1696,960);
                Capture("leaderboard-4x3",1280,960);
                Capture("leaderboard-ultrawide",2520,1080);
                Check(AuthoredObjectCount()==count,"Refresh and navigation preserve the authored UI hierarchy");
                var back=menu.leaderboardPanel.transform.Find("Cadre classement 848x480/Retour classement").GetComponent<Button>();
                Check(back.onClick.GetPersistentTarget(0)==menu,"Back button is wired in scene");
                back.onClick.Invoke();
                ready=EditorApplication.timeSinceStartup+.8;SessionState.SetInt(Key,3);return;
            }
            if(stage==3)
            {
                Check(!menu.IsLeaderboardOpen&&!menu.leaderboardPanel.activeSelf&&menu.mainPanel.activeSelf,"Back returns to the home menu");
                menu.OpenLeaderboard();
                ready=EditorApplication.timeSinceStartup+2;SessionState.SetInt(Key,4);return;
            }
            Check(menu.IsLeaderboardOpen&&menu.leaderboardPanel.GetComponent<CanvasGroup>().interactable,"Reopening completes and restores interaction");
            foreach(var animator in board.GetComponentsInChildren<Animator>())
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Flottement"),"Entry settles into idle: "+animator.name);
            Check(errors==0&&baselineTextureMessages<=2,"No new Unity runtime errors relative to original menu");
            Debug.Log("LEADERBOARD_CHECKS_DONE failures=0");
            SessionState.SetInt(Key,0);EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            PlayerPrefs.DeleteKey(TestKey);PlayerPrefs.Save();Debug.LogException(e);
            SessionState.SetInt(Key,0);EditorApplication.Exit(1);
        }
    }
    static int AuthoredObjectCount()
    {
        int result=0;
        foreach(var item in menu.leaderboardPanel.GetComponentsInChildren<Transform>(true))
            if(item.GetComponent<TMP_SubMeshUI>()==null)result++;
        return result;
    }
    static void Capture(string name,int width,int height)
    {
        var canvas=board.GetComponentInParent<Canvas>();
        var camera=new GameObject("Validation camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color32(7,153,243,255);camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var target=new RenderTexture(width,height,24);camera.targetTexture=target;
        Canvas.ForceUpdateCanvases();
        foreach(var text in board.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        File.WriteAllBytes("../"+name+".png",image.EncodeToPNG());
        var corners=new Vector3[4];((RectTransform)board.transform).GetWorldCorners(corners);
        foreach(var point in corners)
        {
            var viewport=camera.WorldToViewportPoint(point);
            Check(viewport.x>=-.01f&&viewport.x<=1.01f&&viewport.y>=-.01f&&viewport.y<=1.01f,"Panel fits "+width+"x"+height);
        }
        RenderTexture.active=previous;camera.targetTexture=null;target.Release();
        canvas.renderMode=mode;canvas.worldCamera=oldCamera;
        UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
    }
}
