// Author the editable panel and clips only in the isolated validation project.
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AuthorNameEntry
{
    const string Folder="Assets/Menu/NameEntry/Animations/";
    static TMP_FontAsset font;
    static readonly Color Cream=new Color32(239,233,225,255);
    static readonly Color Ink=new Color32(38,38,50,255);
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/CroustyMainMenu.unity");
            var menu=UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();
            if(menu.nameEntry!=null)throw new InvalidOperationException("Name panel already exists; preserve manual edits.");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
            var panel=Rect(menu.mainPanel.transform.parent,"Nom du joueur",0,0,848,480);
            panel.anchorMin=Vector2.zero;panel.anchorMax=Vector2.one;panel.offsetMin=panel.offsetMax=Vector2.zero;
            panel.gameObject.AddComponent<CanvasGroup>();
            var hit=panel.gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,0);hit.raycastTarget=true;
            var entry=panel.gameObject.AddComponent<CroustyNameEntry>();entry.menu=menu;
            var frame=Rect(panel,"Cadre nom 848x480",0,0,848,480);
            frame.anchorMin=frame.anchorMax=new Vector2(.5f,.5f);frame.anchoredPosition=Vector2.zero;
            var form=Rect(frame,"Formulaire",0,0,450,480);
            entry.controls=form.gameObject.AddComponent<CanvasGroup>();
            var fieldPaper=Image(form,"YOUR NAME - champ éditable",Sprite("Assets/Menu/Leaderboard/Ligne classement orange.png"),0,185,420,72);
            fieldPaper.raycastTarget=true;
            var field=fieldPaper.gameObject.AddComponent<TMP_InputField>();
            field.transition=Selectable.Transition.None;field.targetGraphic=fieldPaper;
            field.characterLimit=CroustyPlayerProfile.MaximumNameLength;
            field.lineType=TMP_InputField.LineType.SingleLine;field.contentType=TMP_InputField.ContentType.Standard;
            field.richText=false;field.customCaretColor=true;field.caretColor=Cream;field.caretWidth=1;
            field.selectionColor=new Color(1,1,.8f,.3f);
            var area=Rect(field.transform,"Zone du texte",32,8,330,53);area.gameObject.AddComponent<RectMask2D>();
            var typed=Text(area,"Nom saisi","",0,0,330,53,36,Cream);
            typed.alignment=TextAlignmentOptions.MidlineLeft;
            typed.enableAutoSizing=false;
            var placeholder=Text(area,"Indication YOUR NAME","YOUR NAME",0,0,330,53,37,Cream);
            placeholder.alignment=TextAlignmentOptions.MidlineLeft;
            field.textViewport=area;field.textComponent=(TextMeshProUGUI)typed;field.placeholder=placeholder;
            entry.nameInput=field;
            UnityEventTools.AddPersistentListener(field.onValueChanged,entry.Refresh);
            UnityEventTools.AddPersistentListener(field.onSubmit,entry.Submit);
            var play=Button(form,"JOUER",70,285,250,68,"JOUER",entry.ConfirmPlay);
            entry.playButton=play;
            var back=Button(form,"Retour",0,18,150,40,"BACK",entry.Back);
            entry.hint=Text(form,"Indication de saisie","ENTRE TON NOM POUR PRENDRE LE VOLANT",35,265,345,17,11,Cream);
            Text(form,"Raccourci clavier","ENTRÉE POUR JOUER",85,357,220,22,13,Cream);
            var nav=field.navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnDown=play;nav.selectOnUp=back;field.navigation=nav;
            nav=play.navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnUp=field;nav.selectOnDown=back;play.navigation=nav;
            nav=back.navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnDown=field;nav.selectOnUp=play;back.navigation=nav;

            var wipe=Image(panel,"Volet orange de départ",null,0,0,848,480);
            wipe.color=new Color32(233,132,36,255);wipe.raycastTarget=false;
            wipe.rectTransform.anchorMin=Vector2.zero;wipe.rectTransform.anchorMax=Vector2.one;
            wipe.rectTransform.pivot=new Vector2(0,.5f);wipe.rectTransform.offsetMin=wipe.rectTransform.offsetMax=Vector2.zero;
            var wipeGroup=wipe.gameObject.AddComponent<CanvasGroup>();wipeGroup.alpha=0;
            var onPart=Text(wipe.transform,"ON PART","ON PART !",0,0,340,90,55,Cream);
            var onPartRect=onPart.rectTransform;onPartRect.anchorMin=onPartRect.anchorMax=new Vector2(.5f,.5f);onPartRect.anchoredPosition=Vector2.zero;
            var controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"Nom et départ.controller");
            controller.AddParameter("Launch",AnimatorControllerParameterType.Trigger);
            var machine=controller.layers[0].stateMachine;
            var arrival=new AnimationClip{name="Arrivée du nom",frameRate=60};
            string path="Cadre nom 848x480/Formulaire";
            float x=form.anchoredPosition.x;
            Curve(arrival,path,typeof(RectTransform),"m_AnchoredPosition.x",0,x-120,.3f,x+8,.48f,x-2,.6f,x);
            Curve(arrival,path,typeof(Transform),"m_LocalScale.x",0,.85f,.3f,1.08f,.48f,.98f,.6f,1);
            Curve(arrival,path,typeof(Transform),"m_LocalScale.y",0,.78f,.3f,1.08f,.48f,.98f,.6f,1);
            Curve(arrival,"Volet orange de départ",typeof(CanvasGroup),"m_Alpha",0,0,.6f,0);
            AssetDatabase.CreateAsset(arrival,Folder+"Arrivée.anim");
            var arrive=machine.AddState("Arrivée");arrive.motion=arrival;machine.defaultState=arrive;
            var idle=new AnimationClip{name="Nom prêt",frameRate=60};
            Curve(idle,path,typeof(RectTransform),"m_AnchoredPosition.x",0,x,4,x);
            Curve(idle,path,typeof(Transform),"m_LocalScale.x",0,1,4,1);
            Curve(idle,path,typeof(Transform),"m_LocalScale.y",0,1,4,1);
            Curve(idle,"Volet orange de départ",typeof(CanvasGroup),"m_Alpha",0,0,4,0);
            var settings=AnimationUtility.GetAnimationClipSettings(idle);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(idle,settings);
            AssetDatabase.CreateAsset(idle,Folder+"Prêt.anim");
            var rest=machine.AddState("Prêt");rest.motion=idle;
            var toRest=arrive.AddTransition(rest);toRest.hasExitTime=true;toRest.exitTime=1;toRest.duration=.05f;
            var launch=new AnimationClip{name="Départ goofy",frameRate=60};
            Curve(launch,path,typeof(RectTransform),"m_AnchoredPosition.x",0,x,.16f,x-12,.3f,x+30,.7f,x+650,.8f,x+650);
            Curve(launch,path,typeof(Transform),"m_LocalScale.x",0,1,.15f,.9f,.28f,1.14f,.7f,.9f,.8f,.9f);
            Curve(launch,path,typeof(Transform),"m_LocalScale.y",0,1,.15f,1.12f,.28f,.85f,.7f,.9f,.8f,.9f);
            Curve(launch,"Volet orange de départ",typeof(CanvasGroup),"m_Alpha",0,0,.18f,0,.22f,1,.8f,1);
            Curve(launch,"Volet orange de départ",typeof(Transform),"m_LocalScale.x",0,0,.18f,0,.6f,1.18f,.72f,1.05f,.8f,1.05f);
            Curve(launch,"Volet orange de départ",typeof(Transform),"m_LocalScale.y",0,1.3f,.8f,1.3f);
            Curve(launch,"Volet orange de départ",typeof(Transform),"localEulerAnglesRaw.z",0,-8,.22f,-8,.65f,1,.8f,0);
            AssetDatabase.CreateAsset(launch,Folder+"Départ.anim");
            var departure=machine.AddState("Départ");departure.motion=launch;
            var transition=machine.AddAnyStateTransition(departure);transition.hasExitTime=false;transition.duration=.05f;
            transition.canTransitionToSelf=false;transition.AddCondition(AnimatorConditionMode.If,0,"Launch");
            var animator=panel.gameObject.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
            animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            entry.departure=animator;entry.departureSeconds=.9f;
            menu.nameEntry=entry;
            panel.gameObject.SetActive(false);
            EditorUtility.SetDirty(menu);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("NAME_ENTRY_AUTHOR_OK");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Sprite Sprite(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>(path);
    static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=new Vector2(x+w/2,-y-h/2);rect.sizeDelta=new Vector2(w,h);return rect;
    }
    static Image Image(Transform p,string name,Sprite sprite,float x,float y,float w,float h)
    {
        var image=Rect(p,name,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image;
    }
    static TMP_Text Text(Transform p,string name,string value,float x,float y,float w,float h,float size,Color color)
    {
        var text=Rect(p,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.Center;
        text.enableAutoSizing=true;text.fontSizeMin=size*.7f;text.fontSizeMax=size;text.richText=false;
        text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;return text;
    }
    static Button Button(Transform p,string name,float x,float y,float w,float h,string label,UnityEngine.Events.UnityAction action)
    {
        var rect=Rect(p,name,x,y,w,h);var button=rect.gameObject.AddComponent<Button>();
        var paper=Image(rect,"Papier",Sprite("Assets/Menu/MainRibbonCream.png"),0,0,w,h);
        paper.raycastTarget=true;button.targetGraphic=paper;button.transition=Selectable.Transition.None;
        var text=Text(paper.transform,"Texte",label,10,5,w-35,h-12,h*.45f,Ink);
        var hover=rect.gameObject.AddComponent<CroustyMenuHover>();hover.target=paper.rectTransform;
        hover.ribbonImage=paper;hover.idleRibbon=paper.sprite;hover.activeRibbon=Sprite("Assets/Menu/MainRibbonOrange.png");
        hover.textTarget=text;hover.activeTextColor=Cream;hover.hoverScale=1.075f;
        UnityEventTools.AddPersistentListener(button.onClick,action);return button;
    }
    static void Curve(AnimationClip clip,string path,Type type,string property,params float[] pairs)
    {
        var curve=new AnimationCurve();for(int i=0;i<pairs.Length;i+=2)curve.AddKey(new Keyframe(pairs[i],pairs[i+1]));
        for(int i=0;i<curve.length;i++)
        {AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
        clip.SetCurve(path,type,property,curve);
    }
}
