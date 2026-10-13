using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class AuthorEnglishTitles
{
    public static void Run()
    {
        try
        {
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
            foreach(var path in new[]{"Assets/SceneBase.unity","Assets/Scene A moi.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);
                foreach(var root in scene.GetRootGameObjects())foreach(var image in root.GetComponentsInChildren<Image>(true))
                {
                    if(image.sprite==null||AssetDatabase.GetAssetPath(image.sprite)!="Assets/Jojo/Resources/Gerant/titre.png")continue;
                    var go=image.gameObject;
                    UnityEngine.Object.DestroyImmediate(image);
                    var title=go.AddComponent<TextMeshProUGUI>();title.font=font;title.text="MANAGER";
                    title.fontSize=35;title.enableAutoSizing=true;title.fontSizeMin=26;title.fontSizeMax=35;
                    title.color=new Color32(239,233,225,255);title.alignment=TextAlignmentOptions.Center;
                    title.textWrappingMode=TextWrappingModes.NoWrap;title.raycastTarget=false;
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("ENGLISH_TITLES_DONE");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
