// Best-per-name persistence plus a real delivery round, results and authored leaderboard.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ScoreAndNameChecks
{
    const string Key="ScoreAndNameChecks";
    const string TestKey="Crousty.Leaderboard.BestPerNameValidation";
    const string PlayerA="Éloïse";
    const string PlayerB="Thibaud";
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static double deadline,ready;
    static DeliveryManager deliveries;
    static int expected,errors,oldHud,oldTextures;
    static ScoreAndNameChecks()
    {
        deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{
            if(SessionState.GetInt(Key,0)==0)return;
            if(t==LogType.Error&&m=="HUD : assigner les panels et indications dans la scene.")oldHud++;
            else if(t==LogType.Error&&m=="No texture data is available to upload.")oldTextures++;
            else if(t==LogType.Error||t==LogType.Exception)errors++;
        };
    }
    public static void Run()
    {
        foreach(var key in new[]{CroustyPlayerProfile.NameKey,CroustyLeaderboardScores.StorageKey})
        {
            SessionState.SetBool("ScoreAndName.Exists."+key,PlayerPrefs.HasKey(key));
            SessionState.SetString("ScoreAndName.Value."+key,PlayerPrefs.GetString(key));
        }
        SessionState.SetBool("ScoreAndName.BestExists",PlayerPrefs.HasKey("Crousty.BestScore"));
        SessionState.SetInt("ScoreAndName.Best",PlayerPrefs.GetInt("Crousty.BestScore"));
        PlayerPrefs.DeleteKey(CroustyLeaderboardScores.StorageKey);
        CroustyLeaderboardScores.Record(1000,PlayerA);CroustyLeaderboardScores.Record(2000,PlayerB);
        CroustyPlayerProfile.Save(PlayerA);
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
    }
    static void Restore()
    {
        foreach(var key in new[]{CroustyPlayerProfile.NameKey,CroustyLeaderboardScores.StorageKey})
        {
            if(SessionState.GetBool("ScoreAndName.Exists."+key,false))PlayerPrefs.SetString(key,SessionState.GetString("ScoreAndName.Value."+key,""));
            else PlayerPrefs.DeleteKey(key);
        }
        if(SessionState.GetBool("ScoreAndName.BestExists",false))PlayerPrefs.SetInt("Crousty.BestScore",SessionState.GetInt("ScoreAndName.Best",0));
        else PlayerPrefs.DeleteKey("Crousty.BestScore");
        PlayerPrefs.DeleteKey(TestKey);PlayerPrefs.Save();
    }
    static void Check(bool value,string message)
    {if(!value)throw new Exception("SCORE_NAME_FAILED: "+message);Debug.Log("SCORE_NAME_OK: "+message);}
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Flags).Invoke(target,args);
    static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
    static void StorageChecks()
    {
        PlayerPrefs.DeleteKey(TestKey);
        CroustyLeaderboardScores.Record(800," Thibaud ",TestKey);
        CroustyLeaderboardScores.Record(500,"thibaud",TestKey);
        var board=CroustyLeaderboardScores.Load(TestKey);
        Check(board.entries.Count==1&&board.entries[0].score==800,"Worse repeat keeps one personal best");
        Check(board.latestScore==500&&board.latestId==board.entries[0].id,"Last run score remains separate and highlights the record");
        CroustyLeaderboardScores.Record(1200,"THIBAUD",TestKey);
        board=CroustyLeaderboardScores.Load(TestKey);
        Check(board.entries.Count==1&&board.entries[0].score==1200,"Improvement updates same player across capitalization");
        CroustyLeaderboardScores.Record(1200,"Alice",TestKey);
        Check(CroustyLeaderboardScores.Load(TestKey).entries[0].player=="THIBAUD","Equal scores retain stable rank order");
        for(int i=0;i<10;i++)CroustyLeaderboardScores.Record(5000+i*100,"Joueur "+i,TestKey);
        board=CroustyLeaderboardScores.Load(TestKey);
        Check(board.entries.Count==8&&board.records.Count==12,"Top eight displayed while all personal records persist");
        Check(CroustyLeaderboardScores.BestForPlayer("thibaud",TestKey)==1200,"Personal best survives outside the top eight");
        CroustyLeaderboardScores.Record(6000,"Thibaud",TestKey);
        board=CroustyLeaderboardScores.Load(TestKey);
        Check(board.entries[0].player=="Thibaud"&&board.entries[0].score==6000&&board.records.Count==12,
            "Returning player can enter podium without a duplicate");
        var legacy=new CroustyLeaderboardScores.Board();
        legacy.records=null;
        legacy.entries.Add(new CroustyLeaderboardScores.Entry{id="old1",player="Éloïse",score=900});
        legacy.entries.Add(new CroustyLeaderboardScores.Entry{id="old2",player=" ÉLOÏSE ",score=1300});
        legacy.entries.Add(new CroustyLeaderboardScores.Entry{id="old3",player="Éloïse",score=500});
        legacy.latestId="old3";
        PlayerPrefs.SetString(TestKey,JsonUtility.ToJson(legacy));
        board=CroustyLeaderboardScores.Load(TestKey);
        Check(board.entries.Count==1&&board.entries[0].score==1300,"Legacy duplicate games merge without losing the strongest score");
        Check(board.latestId=="old2"&&board.latestScore==500,"Legacy latest run maps to retained personal best");
        CroustyLeaderboardScores.Record(600,"Éloïse",TestKey);
        Check(CroustyLeaderboardScores.Load(TestKey).entries[0].score==1300,"Migrated record survives a weaker next game");
        PlayerPrefs.DeleteKey(TestKey);PlayerPrefs.Save();
    }
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Score and name tests timed out");
            if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1||EditorApplication.timeSinceStartup<ready)return;
            if(stage==1)
            {
                StorageChecks();Physics.simulationMode=SimulationMode.Script;
                deliveries=DeliveryManager.Instance;deliveries.enabled=false;
                Check(deliveries.PlayerName==PlayerA&&deliveries.BestScore==1000,"Run captures entered name and that player's previous best");
                CroustyPlayerProfile.Save(PlayerB);
                Check(deliveries.PlayerName==PlayerA,"Changing saved profile cannot misattribute an active run");
                Call(deliveries,"LoadOrders");
                typeof(DeliveryManager).GetField("<RunElapsed>k__BackingField",Flags).SetValue(deliveries,10f);
                expected=deliveries.Score+deliveries.OrdersTotal*deliveries.pointsPerDelivery+
                    deliveries.completionBonus+Mathf.FloorToInt(deliveries.RunRemaining)*deliveries.pointsPerRemainingSecond;
                deliveries.throwDuration=.02f;
                foreach(var target in deliveries.deliveryPoints.Where(p=>p!=null&&p.IsWaiting).ToArray())Call(deliveries,"ThrowCrousty",target);
                ready=EditorApplication.timeSinceStartup+.5;SessionState.SetInt(Key,2);return;
            }
            if(stage==2)
            {
                if(!deliveries.AllDelivered)return;
                Check(deliveries.Score==expected&&deliveries.BestScore==expected,"Real deliveries, completion and remaining time produce the exact total");
                var board=CroustyLeaderboardScores.Load();
                Check(CroustyLeaderboardScores.BestForPlayer(PlayerA,board)==expected&&board.records.Count==2,
                    "Completed round saves personal best once");
                var results=UnityEngine.Object.FindAnyObjectByType<PoliceGameOverManager>();results.TriggerPoliceGameOver();
                var total=(TMPro.TMP_Text)Get(results,"totalText");
                Check(total.text==expected.ToString(),"Results screen matches the recorded score");
                board=CroustyLeaderboardScores.Load();
                Check(board.latestPlayer==PlayerA&&board.latestScore==expected&&board.records.Count==2,
                    "Defeat saves the run's original player without another row");
                results.TriggerTimeoutGameOver();
                Check(CroustyLeaderboardScores.Load().records.Count==2,"Repeated end-of-run callbacks do not duplicate records");
                Time.timeScale=1;SceneManager.LoadScene("CroustyMainMenu");
                SessionState.SetInt(Key,3);ready=EditorApplication.timeSinceStartup+1;return;
            }
            if(stage==3)
            {
                var menu=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();menu.OpenLeaderboard();
                SessionState.SetInt(Key,4);ready=EditorApplication.timeSinceStartup+1;return;
            }
            var panel=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>().leaderboardPanel.GetComponent<CroustyLeaderboard>();
            Check(panel.podiums[0].player.text==PlayerB&&panel.podiums[0].score.text=="2000","Podium displays highest player's actual name and score");
            Check(panel.podiums[1].player.text==PlayerA&&panel.podiums[1].score.text==expected.ToString(),
                "Entered name and earned score are displayed in their sorted podium position");
            Check(panel.status.text.Contains(PlayerA)&&panel.status.text.Contains(expected.ToString()),"Status reports latest run and personal record");
            Check(errors==0&&oldHud<=1&&oldTextures<=2,"No new Unity runtime errors");
            Restore();Debug.Log("SCORE_NAME_CHECKS_DONE failures=0");SessionState.SetInt(Key,0);EditorApplication.Exit(0);
        }
        catch(Exception e){Restore();Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
}
