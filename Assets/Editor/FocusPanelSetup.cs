#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class FocusPanelSetup
{
    const string FONT_TIMER  = "Assets/Fonts/Kotonoru/KotonoruBoldSDF.asset";
    const string FONT_UI     = "Assets/Fonts/NotoSansJP/NotoSansJP-Regular SDF.asset";
    const string SPRITE_ARC  = "Assets/UI/ArcFill.png";
    const string SPRITE_CARD = "Assets/UI/RoundedRect.png";

    const float CW     = 460f;
    const float CH     = 900f;
    const float ARC_SZ = 380f;

    const float Y_TITLE    =  410f;
    const float Y_ARC      =  185f;
    const float Y_DIVIDER  = -120f;
    const float Y_SETTINGS = -255f;
    const float Y_BUTTONS  = -400f;

    [MenuItem("Tools/Tokyo Corner/Build Focus Panel")]
    public static void Build()
    {
        var fontTimer  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_TIMER);
        var fontUI     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_UI);

        if (AssetDatabase.LoadAssetAtPath<Object>(SPRITE_ARC) != null)
            AssetDatabase.DeleteAsset(SPRITE_ARC);
        var spriteArc  = CreateArcSprite(185, 180);
        var spriteCard = GetOrCreateRoundedSprite();

        Deactivate(FindInactive("FocusRuntimePanel"));
        Deactivate(FindInactive("FocusSettingsPanel"));

        var focusUIGo = FindInactive("FocusUI");
        if (focusUIGo == null) { Debug.LogError("[Setup] FocusUI not found."); return; }

        var scaler = focusUIGo.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Undo.RecordObject(scaler, "CanvasScaler");
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 1f;
        }

        var existing = FindChildByName(focusUIGo.transform, "FocusPanel");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        // FocusPanel
        var panel = MakeUI("FocusPanel", focusUIGo.transform);
        StretchFull(panel);

        // Card
        var card   = MakeUI("Card", panel.transform);
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin=new Vector2(0.5f,0.5f); cardRT.anchorMax=new Vector2(0.5f,0.5f);
        cardRT.pivot=new Vector2(0.5f,0.5f); cardRT.anchoredPosition=Vector2.zero;
        cardRT.sizeDelta=new Vector2(CW,CH);
        var cardImg=card.AddComponent<Image>(); cardImg.color=new Color(0.07f,0.07f,0.07f,0.92f);
        if(spriteCard!=null){cardImg.sprite=spriteCard; cardImg.type=Image.Type.Sliced;}

        // Title
        var title=MakeFixed("TitleLabel",card.transform,0f,Y_TITLE,CW*0.9f,30f);
        var tt=title.AddComponent<TextMeshProUGUI>();
        tt.font=fontUI; tt.fontSize=13f; tt.color=new Color(1,1,1,0.30f);
        tt.text="フォーカスモード"; tt.alignment=TextAlignmentOptions.Center; tt.raycastTarget=false;

        // ArcRoot (正方形固定)
        var arcRoot=MakeFixed("ArcRoot",card.transform,0f,Y_ARC,ARC_SZ,ARC_SZ);
        var arcRootRT=arcRoot.GetComponent<RectTransform>();

        var trackGo=MakeUI("ArcTrack",arcRoot.transform); StretchFull(trackGo);
        var trackImg=trackGo.AddComponent<Image>();
        trackImg.sprite=spriteArc; trackImg.type=Image.Type.Filled;
        trackImg.fillMethod=Image.FillMethod.Radial360; trackImg.fillOrigin=(int)Image.Origin360.Top;
        trackImg.fillClockwise=true; trackImg.fillAmount=1f;
        trackImg.color=new Color(1f,1f,1f,0.08f); trackImg.raycastTarget=false;

        var fillGo=MakeUI("ArcFill",arcRoot.transform); StretchFull(fillGo);
        var fillImg=fillGo.AddComponent<Image>();
        fillImg.sprite=spriteArc; fillImg.type=Image.Type.Filled;
        fillImg.fillMethod=Image.FillMethod.Radial360; fillImg.fillOrigin=(int)Image.Origin360.Top;
        fillImg.fillClockwise=true; fillImg.fillAmount=1f;
        fillImg.color=new Color(0.22f,0.54f,0.87f,1f); fillImg.raycastTarget=false;

        var phaseLabel=MakeArcTMP("PhaseLabelText",arcRoot.transform,fontUI,18f,new Color(1,1,1,0.50f),0.67f);
        phaseLabel.text="設定"; phaseLabel.alignment=TextAlignmentOptions.Center;

        var timerDisp=MakeArcTMP("TimerDisplayText",arcRoot.transform,fontTimer,80f,Color.white,0.50f);
        timerDisp.text="25:00"; timerDisp.alignment=TextAlignmentOptions.Center; timerDisp.characterSpacing=-1f;

        var cycleText=MakeArcTMP("CycleText",arcRoot.transform,fontUI,14f,new Color(1,1,1,0.28f),0.30f);
        cycleText.text="サイクル 1 / 4"; cycleText.alignment=TextAlignmentOptions.Center;

        // Divider
        var divGo=MakeFixed("Divider",card.transform,0f,Y_DIVIDER,CW*0.86f,1f);
        divGo.AddComponent<Image>().color=new Color(1f,1f,1f,0.07f);

        // SettingsArea
        var sa=MakeFixed("SettingsArea",card.transform,0f,Y_SETTINGS,CW,200f);
        sa.AddComponent<CanvasGroup>();
        sa.AddComponent<RectMask2D>();
        var (wm,wv,wp)=MakeRow(sa.transform,"作業時間","分","Work", "25", 60f,fontUI,fontTimer,spriteCard);
        var (bm,bv,bp)=MakeRow(sa.transform,"休憩時間","分","Break","5",   0f,fontUI,fontTimer,spriteCard);
        var (cm,cv,cp)=MakeRow(sa.transform,"サイクル","回","Cycle","4", -60f,fontUI,fontTimer,spriteCard);

        // Control Buttons
        var rGo=MakeCtrl("ResetBtn",    card.transform,"R", 48f,fontUI,false,spriteCard,out _,       -74f,Y_BUTTONS);
        var pGo=MakeCtrl("PlayPauseBtn",card.transform,"▶",60f,fontUI,true, spriteCard,out var ppTxt, 0f,Y_BUTTONS);
        var sGo=MakeCtrl("SkipBtn",     card.transform,"✓", 48f,fontUI,false,spriteCard,out _,        74f,Y_BUTTONS);

        // FocusPanelController
        var ctrl=panel.AddComponent<FocusPanelController>();
        var so=new SerializedObject(ctrl);
        Ref(so,"timerController",  focusUIGo.GetComponent<TimerController>());
        Ref(so,"arcFill",          fillImg);
        Ref(so,"arcTrack",         trackImg);
        Ref(so,"arcRoot",          arcRootRT);
        Ref(so,"phaseLabelText",   phaseLabel);
        Ref(so,"timerDisplayText", timerDisp);
        Ref(so,"cycleText",        cycleText);
        Ref(so,"settingsArea",     sa);
        Ref(so,"workValueText",    wv);
        Ref(so,"breakValueText",   bv);
        Ref(so,"cycleValueText",   cv);
        Ref(so,"workMinusBtn",     wm.GetComponent<Button>());
        Ref(so,"workPlusBtn",      wp.GetComponent<Button>());
        Ref(so,"breakMinusBtn",    bm.GetComponent<Button>());
        Ref(so,"breakPlusBtn",     bp.GetComponent<Button>());
        Ref(so,"cycleMinusBtn",    cm.GetComponent<Button>());
        Ref(so,"cyclePlusBtn",     cp.GetComponent<Button>());
        Ref(so,"resetBtn",         rGo.GetComponent<Button>());
        Ref(so,"playPauseBtn",     pGo.GetComponent<Button>());
        Ref(so,"playPauseBtnText", ppTxt);
        Ref(so,"skipBtn",          sGo.GetComponent<Button>());
        so.ApplyModifiedProperties();

        // FocusOverlayController
        var oc=focusUIGo.GetComponent<FocusOverlayController>();
        if(oc!=null)
        {
            var oso=new SerializedObject(oc);
            var fp=oso.FindProperty("focusPanel");
            if(fp!=null){fp.objectReferenceValue=panel; oso.ApplyModifiedProperties();}
        }

        // Main Camera を Everything に戻す（以前の操作の後始末）
        var mainCam=Camera.main;
        if(mainCam!=null && mainCam.cullingMask != -1)
        {
            Undo.RecordObject(mainCam,"Restore MainCam cullingMask");
            mainCam.cullingMask=-1;
            EditorUtility.SetDirty(mainCam);
        }

        // CharacterPreviewCamera が残っていれば削除
        var oldCam=FindInactive("CharacterPreviewCamera");
        if(oldCam!=null) Undo.DestroyObjectImmediate(oldCam);

        EditorUtility.SetDirty(focusUIGo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[TokyoCorner] FocusPanel built successfully!");
        Selection.activeGameObject=card;
    }

    // ── Helpers ──
    static GameObject MakeFixed(string n,Transform p,float x,float y,float w,float h)
    {
        var go=MakeUI(n,p); var rt=go.GetComponent<RectTransform>();
        rt.anchorMin=new Vector2(0.5f,0.5f); rt.anchorMax=new Vector2(0.5f,0.5f);
        rt.pivot=new Vector2(0.5f,0.5f); rt.anchoredPosition=new Vector2(x,y); rt.sizeDelta=new Vector2(w,h);
        return go;
    }

    static TextMeshProUGUI MakeArcTMP(string n,Transform p,TMP_FontAsset f,float sz,Color c,float ancY)
    {
        var go=MakeUI(n,p); var rt=go.GetComponent<RectTransform>();
        rt.anchorMin=new Vector2(0.05f,ancY-0.09f); rt.anchorMax=new Vector2(0.95f,ancY+0.09f);
        rt.pivot=new Vector2(0.5f,0.5f); rt.offsetMin=Vector2.zero; rt.offsetMax=Vector2.zero;
        var tmp=go.AddComponent<TextMeshProUGUI>();
        tmp.font=f; tmp.fontSize=sz; tmp.color=c; tmp.raycastTarget=false;
        return tmp;
    }

    static (GameObject,TextMeshProUGUI,GameObject) MakeRow(
        Transform p,string label,string unit,string prefix,string def,
        float y,TMP_FontAsset fUI,TMP_FontAsset fNum,Sprite spr)
    {
        var lbl=MakeFixed(prefix+"Lbl",p,-80f,y,120f,44f);
        var lt=lbl.AddComponent<TextMeshProUGUI>();
        lt.font=fUI; lt.fontSize=15f; lt.color=new Color(1,1,1,0.50f);
        lt.text=label; lt.alignment=TextAlignmentOptions.Left; lt.raycastTarget=false;

        var minus=MakeFixed(prefix+"MinusBtn",p,40f,y,40f,40f);
        ApplySBtn(minus,"−",fUI,spr);

        var valGo=MakeFixed(prefix+"Val",p,100f,y,60f,44f);
        var vt=valGo.AddComponent<TextMeshProUGUI>();
        vt.font=fNum; vt.fontSize=30f; vt.color=Color.white;
        vt.text=def; vt.alignment=TextAlignmentOptions.Center; vt.raycastTarget=false;

        var plus=MakeFixed(prefix+"PlusBtn",p,158f,y,40f,40f);
        ApplySBtn(plus,"＋",fUI,spr);

        var ut=MakeFixed(prefix+"Unit",p,196f,y,30f,44f);
        var utt=ut.AddComponent<TextMeshProUGUI>();
        utt.font=fUI; utt.fontSize=13f; utt.color=new Color(1,1,1,0.28f);
        utt.text=unit; utt.alignment=TextAlignmentOptions.Left; utt.raycastTarget=false;

        return (minus,vt,plus);
    }

    static void ApplySBtn(GameObject go,string lbl,TMP_FontAsset f,Sprite spr)
    {
        var img=go.AddComponent<Image>(); img.color=new Color(0f,0f,0f,0f);
        if(spr!=null){img.sprite=spr; img.type=Image.Type.Sliced;}
        var ol=go.AddComponent<Outline>();
        ol.effectColor=new Color(1f,1f,1f,0.25f); ol.effectDistance=new Vector2(1f,-1f);
        var btn=go.AddComponent<Button>(); var cb=btn.colors;
        cb.normalColor=new Color(0f,0f,0f,0f);
        cb.highlightedColor=new Color(1f,1f,1f,0.10f);
        cb.pressedColor=new Color(1f,1f,1f,0.22f); btn.colors=cb;
        var txt=MakeUI("L",go.transform).AddComponent<TextMeshProUGUI>();
        txt.font=f; txt.fontSize=20f; txt.color=new Color(1f,1f,1f,0.75f);
        txt.text=lbl; txt.alignment=TextAlignmentOptions.Center; txt.raycastTarget=false;
        txt.rectTransform.anchorMin=Vector2.zero; txt.rectTransform.anchorMax=Vector2.one;
        txt.rectTransform.offsetMin=Vector2.zero; txt.rectTransform.offsetMax=Vector2.zero;
    }

    static GameObject MakeCtrl(string n,Transform p,string lbl,float sz,
                                TMP_FontAsset f,bool primary,Sprite spr,
                                out TextMeshProUGUI tr,float x,float y)
    {
        var go=MakeFixed(n,p,x,y,sz,sz);
        var img=go.AddComponent<Image>(); img.color=new Color(0f,0f,0f,0f);
        if(spr!=null){img.sprite=spr; img.type=Image.Type.Sliced;}
        var ol=go.AddComponent<Outline>();
        ol.effectColor=primary?new Color(1f,1f,1f,0.55f):new Color(1f,1f,1f,0.28f);
        ol.effectDistance=new Vector2(1.2f,-1.2f);
        var btn=go.AddComponent<Button>(); var cb=btn.colors;
        cb.normalColor=new Color(0f,0f,0f,0f);
        cb.highlightedColor=new Color(1f,1f,1f,0.10f);
        cb.pressedColor=new Color(1f,1f,1f,0.22f); btn.colors=cb;
        var txt=MakeUI("L",go.transform).AddComponent<TextMeshProUGUI>();
        txt.font=f; txt.fontSize=primary?22f:18f;
        txt.color=primary?Color.white:new Color(1f,1f,1f,0.65f);
        txt.text=lbl; txt.alignment=TextAlignmentOptions.Center; txt.raycastTarget=false;
        txt.rectTransform.anchorMin=Vector2.zero; txt.rectTransform.anchorMax=Vector2.one;
        txt.rectTransform.offsetMin=Vector2.zero; txt.rectTransform.offsetMax=Vector2.zero;
        tr=txt; return go;
    }

    static Sprite CreateArcSprite(int outerR,int innerR)
    {
        if(!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets","UI");
        int size=512; var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
        var px=new Color32[size*size]; float cx=size/2f,cy=size/2f;
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        { float dx=x-cx,dy=y-cy,d=Mathf.Sqrt(dx*dx+dy*dy);
          float a=Mathf.Clamp01(Mathf.Min(outerR-d,d-innerR));
          px[y*size+x]=new Color32(255,255,255,(byte)(a*255)); }
        tex.SetPixels32(px); tex.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath+"/UI/ArcFill.png",tex.EncodeToPNG());
        Object.DestroyImmediate(tex); AssetDatabase.Refresh();
        var imp=(TextureImporter)AssetImporter.GetAtPath(SPRITE_ARC);
        imp.textureType=TextureImporterType.Sprite; imp.spriteImportMode=SpriteImportMode.Single;
        imp.spritePivot=new Vector2(0.5f,0.5f); imp.mipmapEnabled=false; imp.filterMode=FilterMode.Bilinear;
        EditorUtility.SetDirty(imp); imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_ARC);
    }

    static Sprite GetOrCreateRoundedSprite()
    {
        var ex=AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_CARD); if(ex!=null) return ex;
        if(!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets","UI");
        int size=64,r=14; var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
        var px=new Color32[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            px[y*size+x]=InR(x,y,size,size,r)?new Color32(255,255,255,255):new Color32(0,0,0,0);
        tex.SetPixels32(px); tex.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath+"/UI/RoundedRect.png",tex.EncodeToPNG());
        Object.DestroyImmediate(tex); AssetDatabase.Refresh();
        var imp=(TextureImporter)AssetImporter.GetAtPath(SPRITE_CARD);
        imp.textureType=TextureImporterType.Sprite; imp.spriteImportMode=SpriteImportMode.Single;
        imp.spritePivot=new Vector2(0.5f,0.5f); int b=r+2; imp.spriteBorder=new Vector4(b,b,b,b);
        imp.mipmapEnabled=false; imp.filterMode=FilterMode.Bilinear;
        EditorUtility.SetDirty(imp); imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_CARD);
    }

    static bool InR(int x,int y,int w,int h,int r)
    { int cx=x<r?r:(x>=w-r?w-r-1:x),cy=y<r?r:(y>=h-r?h-r-1:y);
      float dx=x-cx,dy=y-cy; return dx*dx+dy*dy<=(r-0.5f)*(r-0.5f); }

    static void Ref(SerializedObject so,string prop,Object val)
    { var p=so.FindProperty(prop); if(p!=null) p.objectReferenceValue=val; }

    static void Deactivate(GameObject go)
    { if(go==null)return; Undo.RecordObject(go,"Deactivate"); go.SetActive(false); }

    static GameObject MakeUI(string n,Transform p)
    { var go=new GameObject(n,typeof(RectTransform)); go.transform.SetParent(p,false);
      Undo.RegisterCreatedObjectUndo(go,"Create "+n); return go; }

    static void StretchFull(GameObject go)
    { var rt=go.GetComponent<RectTransform>(); rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
      rt.offsetMin=Vector2.zero; rt.offsetMax=Vector2.zero; }

    static GameObject FindInactive(string name)
    { foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
        if(go.scene.IsValid()&&go.name==name) return go; return null; }

    static Transform FindChildByName(Transform root,string name)
    { foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
        if(t.name==name&&t!=root) return t; return null; }
}
#endif
