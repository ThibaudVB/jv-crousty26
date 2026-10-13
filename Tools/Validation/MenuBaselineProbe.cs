using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class MenuBaselineProbe
{
    static int errors;
    static MenuBaselineProbe()
    {
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool("MenuBaselineProbe",false)&&t==LogType.Error)errors++;};
        EditorApplication.update+=()=>{
            if(!SessionState.GetBool("MenuBaselineProbe",false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<3)return;
            Debug.Log("BASELINE_MENU_ERRORS="+errors);SessionState.SetBool("MenuBaselineProbe",false);EditorApplication.Exit(0);
        };
    }
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/LeaderboardBaseline.unity");SessionState.SetBool("MenuBaselineProbe",true);EditorApplication.EnterPlaymode();}
}
