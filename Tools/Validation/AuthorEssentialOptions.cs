// Run only in the isolated validation copy; preserve the user's other scene edits.
using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AuthorEssentialOptions
{
    static readonly Color Ink = new Color32(38,38,50,255), Orange = new Color32(233,132,36,255), Cream = new Color32(255,250,240,255);
    static TMP_FontAsset font;
    public static void Run()
    {
        try
        {
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
            foreach(string path in new[]{"Assets/Scenes/CroustyMainMenu.unity","Assets/SceneBase.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);
                foreach(var root in scene.GetRootGameObjects()) foreach(var menu in root.GetComponentsInChildren<CroustyMenu>(true)) Build(menu);
                foreach(var root in scene.GetRootGameObjects()) foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
                    if(text.text=="ON PART !") text.text="LET'S GO!";
                // A duplicated bonus slot in the supplied scene also copied the global
                // HUD input component, without its X prompt. Keep the slot itself and
                // let the complete component own the shared objective/dialogue controls.
                var hudInputs=new System.Collections.Generic.List<CroustyHudControls>();
                foreach(var root in scene.GetRootGameObjects())hudInputs.AddRange(root.GetComponentsInChildren<CroustyHudControls>(true));
                foreach(var hud in hudInputs)
                    if((hud.xPrompt==null||hud.xKey==null)&&hudInputs.Exists(other=>other!=hud&&other.objective==hud.objective&&other.managerPanel==hud.managerPanel&&other.xPrompt!=null&&other.xKey!=null))
                        hud.enabled=false;
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();Debug.Log("OPTIONS_AUTHOR_DONE");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Build(CroustyMenu menu)
    {
        if(menu.optionsPanel==null||menu.categoryPanels.Length!=4)throw new Exception("Missing options layout");
        var blue=menu.optionsPanel.transform.Find("Trait bleu");if(blue!=null)UnityEngine.Object.DestroyImmediate(blue.gameObject);
        string[] tabs={"CONTROLS","AUDIO","VIDEO","CREDITS"};
        for(int i=0;i<4;i++)menu.tabLabels[i].text=tabs[i];
        for(int i=0;i<3;i++)
        {
            var parent=menu.categoryPanels[i].transform;
            while(parent.childCount>0)UnityEngine.Object.DestroyImmediate(parent.GetChild(0).gameObject);
        }
        menu.sensitivitySlider=null;menu.sensitivityLabel=null;menu.fullscreenLabel=null;
        var commands=menu.categoryPanels[0].transform;
        Heading(commands,"MAKE IT YOUR DRIVE","CONTROLS");
        Row(commands,165,"INPUT MODE",CroustySettings.Option.Input,190);
        Text(commands,"Switch hint","Switch between keyboard / mouse and controller.\nYou can change this from the pause menu too.",358,224,378,55,14,Ink);
        Text(commands,"Fallback hint","Controller disconnected? Keyboard / mouse stays available.",358,304,370,45,12,Ink);

        var audio=menu.categoryPanels[1].transform;
        Heading(audio,"TURN UP THE CRUNCH","AUDIO");
        var master=AudioRow(audio,151,"Master",out var masterValue);
        menu.volumeSlider=master;menu.volumeLabel=masterValue;
        UnityEventTools.AddPersistentListener(master.onValueChanged,menu.SetVolume);
        var music=AudioRow(audio,221,"Music",out var musicValue);
        var musicOption=music.gameObject.AddComponent<CroustyAudioOption>();musicOption.music=true;musicOption.value=musicValue;
        var effects=AudioRow(audio,291,"Effect",out var effectsValue);
        var effectOption=effects.gameObject.AddComponent<CroustyAudioOption>();effectOption.value=effectsValue;
        Text(audio,"Audio hint","Changes are saved automatically.",358,370,370,20,12,Ink);

        var video=menu.categoryPanels[2].transform;
        Heading(video,"FIND YOUR SWEET SPOT","VIDEO");
        var pages=video.GetComponent<CroustyVideoPages>();if(pages==null)pages=video.gameObject.AddComponent<CroustyVideoPages>();
        var display=Full(video,"Display settings");var detail=Full(video,"Detail settings");
        pages.displayPage=display.gameObject;pages.detailPage=detail.gameObject;
        string[] labels={"PRESET","DISPLAY MODE","FRAME LIMIT","V-SYNC","RENDER SCALE"};
        CroustySettings.Option[] settings={CroustySettings.Option.Preset,CroustySettings.Option.Display,CroustySettings.Option.FrameRate,CroustySettings.Option.VSync,CroustySettings.Option.RenderScale};
        for(int i=0;i<settings.Length;i++)Row(display,134+i*40,labels[i],settings[i]);
        Text(display,"Display hint","Lower render scale for more FPS.\nV-sync takes priority over the frame limit.",358,335,380,35,11,Ink);
        labels=new[]{"SHADOWS","ANTI-ALIASING","POST EFFECTS","VIEW DISTANCE","TRAFFIC","TEXTURES"};
        settings=new[]{CroustySettings.Option.Shadows,CroustySettings.Option.AntiAliasing,CroustySettings.Option.PostEffects,CroustySettings.Option.ViewDistance,CroustySettings.Option.Traffic,CroustySettings.Option.Textures};
        for(int i=0;i<settings.Length;i++)Row(detail,134+i*37,labels[i],settings[i]);
        Text(detail,"Details hint","Performance preset reduces graphics and city density.",358,356,380,18,10,Ink);
        var next=Button(video,"Video pages",548,380,195,26,Cream);
        pages.pageLabel=Text(next.transform,"Page label","DETAILS  >   1 / 2",0,0,195,26,14,Ink);
        pages.pageLabel.alignment=TextAlignmentOptions.Center;
        UnityEventTools.AddPersistentListener(next.onClick,pages.Toggle);
        detail.gameObject.SetActive(false);
        if(menu.nameEntry!=null)menu.nameEntry.departureHoldSeconds=.7f;
        EditorUtility.SetDirty(menu);
    }
    static void Heading(Transform parent,string caption,string title)
    {
        var tag=Rect(parent,"Section tag",354,99,116,26).gameObject.AddComponent<Image>();tag.color=Ink;tag.raycastTarget=false;
        Text(tag.transform,"Section",title,5,1,106,24,16,Cream).alignment=TextAlignmentOptions.Center;
        Text(parent,"Caption",caption,485,104,260,18,12,Ink).alignment=TextAlignmentOptions.MidlineRight;
    }
    static void Row(Transform parent,float y,string title,CroustySettings.Option setting,float width=202)
    {
        Text(parent,title,title,358,y,170,29,13,Ink);
        var button=Button(parent,title+" selector",743-width,y,width,29,Orange);
        var row=button.gameObject.AddComponent<CroustyOptionRow>();row.option=setting;
        row.value=Text(button.transform,"Value",CroustySettings.Label(setting),8,0,width-30,29,14,Cream);row.value.alignment=TextAlignmentOptions.Center;
        Text(button.transform,"Next",">",width-23,0,18,29,17,Cream).alignment=TextAlignmentOptions.Center;
    }
    static Slider AudioRow(Transform parent,float y,string label,out TMP_Text value)
    {
        Text(parent,label,label,358,y,180,22,15,Ink);
        value=Text(parent,label+" value","100%",675,y,66,22,15,Ink);value.alignment=TextAlignmentOptions.MidlineRight;
        var rect=Rect(parent,label+" slider",358,y+29,385,24);
        var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;
        var slider=rect.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.value=1;
        var rail=Rect(rect,"Rail",0,10,385,4).gameObject.AddComponent<Image>();rail.color=Ink;rail.raycastTarget=false;
        var area=Rect(rect,"Handle area",8,0,369,24);
        var handle=Rect(area,"Handle",0,0,10,16);handle.pivot=new Vector2(.5f,.5f);handle.anchorMin=handle.anchorMax=new Vector2(0,.5f);handle.anchoredPosition=Vector2.zero;
        var thumb=handle.gameObject.AddComponent<Image>();thumb.color=Orange;thumb.raycastTarget=false;
        slider.handleRect=handle;slider.targetGraphic=thumb;
        var colors=slider.colors;colors.highlightedColor=new Color(1,.84f,.6f);colors.selectedColor=colors.highlightedColor;slider.colors=colors;
        return slider;
    }
    static Button Button(Transform parent,string name,float x,float y,float w,float h,Color color)
    {
        var rect=Rect(parent,name,x,y,w,h);var image=rect.gameObject.AddComponent<Image>();image.color=color;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var hover=rect.gameObject.AddComponent<CroustyMenuHover>();hover.target=rect;hover.hoverScale=1.025f;
        return button;
    }
    static TMP_Text Text(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color)
    {
        var t=Rect(parent,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color;
        t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;return t;
    }
    static RectTransform Full(Transform parent,string name)
    {
        var r=Rect(parent,name,0,0,848,480);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
}
