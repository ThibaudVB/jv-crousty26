// One-shot authoring tool. Run only in the isolated validation project.
// Produces editable scene objects and Animation/Animator assets, never runtime UI construction.
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AuthorLeaderboard
{
    const string Root = "Assets/Menu/Leaderboard/";
    const string Motion = Root + "Animations/";
    static TMP_FontAsset font;
    static readonly Color Cream = new Color32(239,233,225,255);
    static readonly Color Ink = new Color32(38,38,50,255);
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Motion);
            AssetDatabase.Refresh();
            foreach (string file in Directory.GetFiles(Root,"*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/CroustyMainMenu.unity");
            var menu = UnityEngine.Object.FindAnyObjectByType<CroustyMenu>();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
            var container = (RectTransform)menu.leaderboardPanel.transform;
            while(container.childCount>0) UnityEngine.Object.DestroyImmediate(container.GetChild(0).gameObject);
            container.anchorMin=Vector2.zero;container.anchorMax=Vector2.one;
            container.offsetMin=container.offsetMax=Vector2.zero;
            container.gameObject.SetActive(true);
            var bg = Image(container,"Fond bleu",null,0,0,848,480);
            bg.rectTransform.anchorMin=Vector2.zero;bg.rectTransform.anchorMax=Vector2.one;
            bg.rectTransform.offsetMin=bg.rectTransform.offsetMax=Vector2.zero;
            bg.color = new Color32(7,153,243,255);
            // Solid backdrop blocks clicks through to the home menu during transitions.
            bg.raycastTarget = true;
            var panel=Rect(container,"Cadre classement 848x480",0,0,848,480);
            panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
            var paper = Image(panel,"Papier du classement",Sprite("Assets/Menu/OptionsPanel.png"),232,58,616,362);
            Animate(paper.rectTransform,"Papier",0,10,.2f);
            var title = Image(panel,"LEADERBOARD",Sprite("Assets/Menu/BlackTag.png"),190,18,198,67);
            Text(title.transform,"Titre","LEADERBOARD",10,12,178,40,23);
            Animate(title.rectTransform,"Titre",.06f,16,.8f);
            var back = Rect(panel,"Retour classement",0,12,170,42);
            var hit = back.gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,.001f);
            var button = back.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;
            UnityEventTools.AddPersistentListener(button.onClick,menu.CloseLeaderboard);
            var visual = Image(back,"Bandeau retour",Sprite("Assets/Menu/MainRibbonCream.png"),0,0,170,42);
            var label = Text(visual.transform,"Libellé","BACK",12,0,115,35,20);label.color=Ink;
            var hover = back.gameObject.AddComponent<CroustyMenuHover>();hover.target=visual.rectTransform;
            hover.hoverScale=1.075f;hover.ribbonImage=visual;
            hover.idleRibbon=visual.sprite;hover.activeRibbon=Sprite("Assets/Menu/MainRibbonOrange.png");
            hover.textTarget=label;hover.activeTextColor=Cream;

            var data = container.GetComponent<CroustyLeaderboard>();
            if(data==null)data=container.gameObject.AddComponent<CroustyLeaderboard>();
            data.normalRibbon=Sprite(Root+"Ligne classement noir.png");
            data.latestRibbon=Sprite(Root+"Ligne classement orange.png");
            data.podiums=new CroustyLeaderboard.Slot[3];
            data.podiums[0]=Podium(panel,1,455,68,153,150,.25f);
            data.podiums[1]=Podium(panel,2,294,102,153,115,.15f);
            data.podiums[2]=Podium(panel,3,625,112,153,105,.35f);
            var headings = Rect(panel,"En-têtes",306,224,455,29);
            Image(headings,"RANK et PLAYER",Sprite(Root+"RANK AND PLAYER LOGO.png"),0,0,180,29);
            Image(headings,"SCORE",Sprite(Root+"SCORE BUTTON.png"),354,0,80,29);
            Animate(headings,"En-têtes",.4f,10,.25f);
            data.rows = new CroustyLeaderboard.Slot[5];
            for(int i=0;i<5;i++)
            {
                var row = Image(panel,"Rang "+(i+4),data.normalRibbon,306,262+i*27,460,25);
                Text(row.transform,"Rang",(i+4).ToString("00")+".",16,0,44,25,19);
                var player = Text(row.transform,"Joueur","À TOI DE JOUER",98,0,225,25,19);
                player.alignment=TextAlignmentOptions.MidlineLeft;
                var score = Text(row.transform,"Score","—",355,0,73,25,19);
                data.rows[i]=new CroustyLeaderboard.Slot{player=player,score=score,ribbon=row};
                Animate(row.rectTransform,"Ligne "+(i+4),.44f+i*.065f,15,.12f);
            }
            data.status=Text(panel,"État du classement","TERMINE UNE PARTIE POUR INSCRIRE TON SCORE !",306,397,454,12,9);
            data.status.color=Ink;
            // Existing home buttons retain their hover/press feedback. Animate only their
            // text children so the authored positions and scrolling ticket remain untouched.
            foreach(var h in menu.mainPanel.GetComponentsInChildren<CroustyMenuHover>(true))
                if(h.textTarget!=null) Animate(h.textTarget.rectTransform,"Accueil "+h.gameObject.name,.1f,.0f,.75f);
            container.gameObject.SetActive(false);
            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("LEADERBOARD_AUTHORING_OK");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static CroustyLeaderboard.Slot Podium(Transform panel,int rank,float x,float y,float w,float h,float delay)
    {
        var art=Image(panel,"Podium "+rank,Sprite(Root+"PODIUM TOP "+rank+".png"),x,y,w,h);
        var name=Text(art.transform,"Joueur","À TOI DE JOUER",13,h-39,w-26,20,12);
        var score=Text(art.transform,"Score","—",13,h-22,w-26,14,10);
        Animate(art.rectTransform,"Podium "+rank,delay,32,rank==1?1.1f:.65f);
        return new CroustyLeaderboard.Slot{player=name,score=score};
    }
    static Sprite Sprite(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>(path);
    static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(0,1);
        rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x+w/2,-y-h/2);rect.sizeDelta=new Vector2(w,h);
        return rect;
    }
    static Image Image(Transform p,string name,Sprite sprite,float x,float y,float w,float h)
    {
        var image=Rect(p,name,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;
        return image;
    }
    static TMP_Text Text(Transform p,string name,string value,float x,float y,float w,float h,float size)
    {
        var text=Rect(p,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font;text.text=value;text.fontSize=size;text.color=Cream;text.alignment=TextAlignmentOptions.Center;
        text.enableAutoSizing=true;text.fontSizeMin=size*.65f;text.fontSizeMax=size;
        text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;
        text.richText=false;text.raycastTarget=false;
        return text;
    }
    static void Curve(AnimationClip clip,Type type,string prop,params float[] points)
    {
        var curve=new AnimationCurve();
        for(int i=0;i<points.Length;i+=2)curve.AddKey(new Keyframe(points[i],points[i+1]));
        for(int i=0;i<curve.length;i++)AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
        for(int i=0;i<curve.length;i++)AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
        clip.SetCurve("",type,prop,curve);
    }
    static void Animate(RectTransform rect,string name,float delay,float travel,float wobble)
    {
        // These three assets belong exclusively to this authoring tool.
        foreach(string suffix in new[]{" Entry.anim"," Idle.anim",".controller"})
            if(AssetDatabase.LoadMainAssetAtPath(Motion+name+suffix)!=null)AssetDatabase.DeleteAsset(Motion+name+suffix);
        var previous=rect.GetComponent<Animator>();if(previous!=null)UnityEngine.Object.DestroyImmediate(previous);
        float y=rect.anchoredPosition.y, z=rect.localEulerAngles.z;
        var group=rect.GetComponent<CanvasGroup>();if(group==null)group=rect.gameObject.AddComponent<CanvasGroup>();
        var intro=new AnimationClip{name=name+" - arrivée élastique",frameRate=60};
        Curve(intro,typeof(CanvasGroup),"m_Alpha",0,0,delay+.01f,0,delay+.24f,1,delay+.72f,1);
        Curve(intro,typeof(RectTransform),"m_AnchoredPosition.y",0,y-travel,delay+.01f,y-travel,delay+.32f,y+4,delay+.52f,y-1,delay+.72f,y);
        Curve(intro,typeof(Transform),"m_LocalScale.x",0,.82f,delay+.01f,.82f,delay+.3f,1.07f,delay+.49f,.98f,delay+.72f,1);
        Curve(intro,typeof(Transform),"m_LocalScale.y",0,.7f,delay+.01f,.7f,delay+.3f,1.11f,delay+.49f,.97f,delay+.72f,1);
        Curve(intro,typeof(Transform),"localEulerAnglesRaw.z",0,z-4,delay+.01f,z-4,delay+.3f,z+2,delay+.5f,z-.5f,delay+.72f,z);
        AssetDatabase.CreateAsset(intro,Motion+name+" Entry.anim");
        var idle=new AnimationClip{name=name+" - flottement",frameRate=60};
        float duration=3.6f+delay*3;
        Curve(idle,typeof(CanvasGroup),"m_Alpha",0,1,duration,1);
        Curve(idle,typeof(RectTransform),"m_AnchoredPosition.y",0,y,duration*.25f,y+wobble,duration*.75f,y-wobble,duration,y);
        Curve(idle,typeof(Transform),"localEulerAnglesRaw.z",0,z,duration*.25f,z+wobble,duration*.75f,z-wobble,duration,z);
        Curve(idle,typeof(Transform),"m_LocalScale.x",0,1,duration,1);
        Curve(idle,typeof(Transform),"m_LocalScale.y",0,1,duration,1);
        var settings=AnimationUtility.GetAnimationClipSettings(idle);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(idle,settings);
        AssetDatabase.CreateAsset(idle,Motion+name+" Idle.anim");
        var controller=AnimatorController.CreateAnimatorControllerAtPath(Motion+name+".controller");
        var machine=controller.layers[0].stateMachine;
        var arrival=machine.AddState("Arrivée");arrival.motion=intro;machine.defaultState=arrival;
        var floating=machine.AddState("Flottement");floating.motion=idle;
        var transition=arrival.AddTransition(floating);transition.hasExitTime=true;transition.exitTime=1;transition.duration=.15f;
        var animator=rect.gameObject.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
        animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    }
}
