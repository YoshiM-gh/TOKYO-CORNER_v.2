using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Dailyタブ。左 1/3 = タイムライン、右 2/3 = Google Keep 風付箋エリア。
/// BuildScaffold() で全 UI をコード生成（シーン上の Static 構造不要）。
/// </summary>
public class DailyCalendarUI : MonoBehaviour
{
    // ── 定数 ──────────────────────────────────────────────────
    private const float HOUR_HEIGHT    = 60f;
    private const float TIME_COL_W     = 72f;
    private const float SCROLLBAR_W    = 20f;  // D&Dしやすい幅・常時表示
    private const int   HOUR_COUNT     = 24;
    private float _hairline = 2f;   // 物理ピクセルにスナップ済みのヘアライン太さ（Refreshで更新）
    private const float NOTIME_ITEM_H  = 28f;
    private const int   NOTIME_VISIBLE = 3;
    private const float NOTIME_ROW_H   = 92f;
    private const float POLICY_ROW_H   = 80f;
    private const float DOW_ROW_H      = 56f;
    private const float NOTE_W         = 200f;
    private const float NOTE_H         = 200f;

    private static readonly string[] POLICY_OPTIONS =
    { "", "ガンガンいこうぜ", "しっかりマイペース", "いろいろやろうぜ",
      "ととのえていこうぜ", "かいふくゆうせん", "ともだちだいじに",
      "かぞくをだいじに", "じぶんをだいじに", "こいびとだいじに" };

    private static readonly string[] DOW_LABELS =
        { "日", "月", "火", "水", "木", "金", "土" };

    private static readonly HashSet<string> HOLIDAY_KEYS = new HashSet<string>
    {
        "2026-01-01","2026-01-12","2026-02-11","2026-02-23","2026-03-20",
        "2026-04-29","2026-05-03","2026-05-04","2026-05-05","2026-07-20",
        "2026-08-11","2026-09-21","2026-09-23","2026-10-12",
        "2026-11-03","2026-11-23",
    };

    // ── アセット参照 ─────────────────────────────────────────
    [SerializeField] private Sprite cardSprite; // 角丸カード用 9-slice（Rounded Filled 32px）

    // ── 状態 ──────────────────────────────────────────────────
    private DateTime _currentDate;
    private int      _weekStartDow = 0; // 0=日、1=月
    private bool     _blockNextNoteSpawn;  // 空ノート削除直後のスポーン抑制

    // ── UI 参照 ────────────────────────────────────────────────
    private Button           _prevBtn;
    private Button           _nextBtn;
    private TextMeshProUGUI  _dayLabel;
    private Button           _sunBtn;
    private Button           _monBtn;

    private Transform        _dowRow;
    private Transform        _policyRow;
    private Transform        _noTimeRow;
    private ScrollRect       _timelineScroll;
    private Transform        _timelineParent;
    private RectTransform    _stickyCanvas;

    // DowRow 内の直接 TMP 参照
    private TextMeshProUGUI  _dayDowTxt;
    private TextMeshProUGUI  _dayDateTxt;
    private Image            _dayCellImg;

    // モーダル参照
    private EventModal               _eventModal;
    private FloatingWindowController _floatingWindow;
    private DayEventsPopup           _dayEventsPopup;

    private bool _scaffoldBuilt;

    // ── ライフサイクル ─────────────────────────────────────────
    private void Awake()
    {
        _eventModal     = FindObjectOfType<EventModal>(true);
        _floatingWindow = FindObjectOfType<FloatingWindowController>(true);
        _dayEventsPopup = FindObjectOfType<DayEventsPopup>(true);
        BuildScaffold();
    }

    private void OnEnable()
    {
        UITheme_FocusMode.OnThemeChanged += Refresh;
        _currentDate = DateTime.Now.Date;
        SetupButtons();
        Refresh();
    }

    private void OnDisable()
    {
        UITheme_FocusMode.OnThemeChanged -= Refresh;
    }

    // =========================================================
    // BuildScaffold ── 一度だけ実行
    // =========================================================
private void BuildScaffold()
    {
        if (_scaffoldBuilt) return;
        _scaffoldBuilt = true;

        var content = transform.Find("Content");
        if (content == null) { Debug.LogError("[DailyCalendarUI] 'Content' child not found"); return; }

        // 既存の子を全削除
        var toKill = new System.Collections.Generic.List<GameObject>();
        foreach (Transform c in content) toKill.Add(c.gameObject);
        foreach (var g in toKill) DestroyImmediate(g);

        // Content 背景透明 + VLG ゟ刑除
        // Content 背景はシーン値（白1.6%・他タブと共通）を維持する — 透明化しない
        var cVLG = content.GetComponent<VerticalLayoutGroup>();
        if (cVLG != null) DestroyImmediate(cVLG);

        // ─ Header （アンカー直指定: 上 48px 固定） ──────────────────────
        var header = BuildHeader(content);
        var hRT    = header.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot     = new Vector2(0.5f, 1f);
        hRT.sizeDelta = new Vector2(0f, 48f);
        hRT.anchoredPosition = Vector2.zero;

        // ─ BodyArea （アンカー直指定: Header 下を充塘） ─────────────────
        var body  = BuildBodyArea(content);
        var bRT   = body.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 0f);
        bRT.anchorMax = new Vector2(1f, 1f);
        bRT.pivot     = new Vector2(0.5f, 0.5f);
        bRT.offsetMin = new Vector2(0f, 0f);
        bRT.offsetMax = new Vector2(0f, -48f);
    }

    // ── ヘッダー ──────────────────────────────────────────────
    private GameObject BuildHeader(Transform parent)
    {
        var go  = MakeGO("Header", parent);
        go.AddComponent<Image>().color = Color.clear;
        // VLG なし・アンカー直接指定（Weekly と同じ方式）

        var wContent = transform.parent.Find("Weekly/Content");
        var wHdr     = wContent?.Find("Header");

        // ── PrevDayBtn（左端固定、48px）───────────────────
        var prevGO = CloneSrcBtn(wHdr?.Find("PrevMonthBtn")?.gameObject, go.transform, "PrevDayBtn", "<");
        SetBtnRT(prevGO, ancLeft: true,  width: 48f, vMgn: 4f, offset: 4f);
        _prevBtn = prevGO?.GetComponent<Button>();
        _prevBtn?.onClick.RemoveAllListeners();

        // ── NextDayBtn（右端固定、48px）──────────────────
        var nextGO = CloneSrcBtn(wHdr?.Find("NextMonthBtn")?.gameObject, go.transform, "NextDayBtn", ">");
        SetBtnRT(nextGO, ancLeft: false, width: 48f, vMgn: 4f, offset: 4f);
        _nextBtn = nextGO?.GetComponent<Button>();
        _nextBtn?.onClick.RemoveAllListeners();

        // ── 月曜はじまり（右から 56px）──────────────────
        var monGO = CloneSrcBtn(wHdr?.Find("WeekStartMonBtn")?.gameObject, go.transform, "WeekStartMonBtn", "月曜はじまり");
        SetBtnRT(monGO, ancLeft: false, width: 108f, vMgn: 3f, offset: 56f);
        _monBtn = monGO?.GetComponent<Button>();
        _monBtn?.onClick.RemoveAllListeners();

        // ── 日曜はじまり（右から 168px）─────────────────
        var sunGO = CloneSrcBtn(wHdr?.Find("WeekStartSunBtn")?.gameObject, go.transform, "WeekStartSunBtn", "日曜はじまり");
        SetBtnRT(sunGO, ancLeft: false, width: 108f, vMgn: 3f, offset: 168f);
        _sunBtn = sunGO?.GetComponent<Button>();
        _sunBtn?.onClick.RemoveAllListeners();

        // ── DayLabel（ボタン間を充填）────────────────────
        var lblGO = MakeGO("DayLabel", go.transform);
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(56f, 4f);
        lblRT.offsetMax = new Vector2(-280f, -4f);
        _dayLabel = lblGO.AddComponent<TextMeshProUGUI>();
        var wWL = wHdr?.Find("WeekLabel")?.GetComponent<TextMeshProUGUI>();
        if (wWL != null) { _dayLabel.font = wWL.font; _dayLabel.fontSize = wWL.fontSize; }
        else _dayLabel.fontSize = 26f;
        _dayLabel.color     = UITheme_FocusMode.TextBody;
        _dayLabel.alignment = TextAlignmentOptions.Center;

        NavHeaderStyler.Style(go.transform);  // Phase1: ヘッダー部品規格
        return go;
    }

    /// <summary>Weekly ボタンをクローンしてテキストだけ差し替え</summary>
    private static GameObject CloneSrcBtn(GameObject src, Transform parent, string name, string label)
    {
        if (src == null) return null;
        var clone = UnityEngine.Object.Instantiate(src, parent, false);
        clone.name = name;
        var txt = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txt) txt.text = label;
        return clone;
    }

    /// <summary>ボタンを左/右アンカー固定で配置</summary>
    private static void SetBtnRT(GameObject go, bool ancLeft, float width, float vMgn, float offset)
    {
        if (go == null) return;
        var rt = go.GetComponent<RectTransform>();
        float ax = ancLeft ? 0f : 1f;
        rt.anchorMin = new Vector2(ax, 0f); rt.anchorMax = new Vector2(ax, 1f);
        rt.pivot     = new Vector2(ax, 0.5f);
        rt.sizeDelta = new Vector2(width, -vMgn * 2f);
        rt.anchoredPosition = new Vector2(ancLeft ? offset : -offset, 0f);
    }
    private Button MakeFallbackBtn(Transform parent, string name, string txt, float w)
    {
        var go  = MakeGO(name, parent);
        var img = go.AddComponent<Image>(); img.color = new Color(1f,1f,1f,0.08f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var le  = go.AddComponent<LayoutElement>(); le.preferredWidth = w; le.minWidth = w;
        var tGO = MakeGO("Text", go.transform);
        StretchRT(tGO);
        var t = tGO.AddComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = 15f;
        t.color = UITheme_FocusMode.TextBody; t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return btn;
    }

    private TextMeshProUGUI MakeLabel(Transform parent, string name)
    {
        var go = MakeGO(name, parent);
        go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = UITheme_FocusMode.FontSectionTitle;
        t.color = UITheme_FocusMode.TextBody; t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    private GameObject BuildBodyArea(Transform parent)
    {
        var go  = MakeGO("BodyArea", parent);
        go.AddComponent<Image>().color = Color.clear;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.spacing = 0f;
        go.AddComponent<LayoutElement>().flexibleHeight = 1f;

        BuildLeftPanel(go.transform);

        // 縦境界線
        var div  = MakeGO("BodyDivider", go.transform);
        div.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;
        var divLE = div.AddComponent<LayoutElement>(); divLE.preferredWidth = 2f; divLE.minWidth = 2f;

        BuildStickyPanel(go.transform);
        return go;
}

    private void BuildLeftPanel(Transform parent)
    {
        var go  = MakeGO("LeftPanel", parent);
        go.AddComponent<Image>().color = Color.clear;
        go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        // VLG を使わずアンカー直指定で各行を配置
        const float hDow    = 56f;
        const float hPolicy = 80f;
        const float hNoTime = 92f;
        float yOff = 0f;

        _dowRow = BuildDowRow(go.transform);
        var drRT = _dowRow.GetComponent<RectTransform>();
        drRT.anchorMin = new Vector2(0f, 1f); drRT.anchorMax = new Vector2(1f, 1f);
        drRT.pivot     = new Vector2(0.5f, 1f);
        drRT.sizeDelta = new Vector2(0f, hDow);
        drRT.anchoredPosition = new Vector2(0f, yOff);
        yOff -= hDow;

        _policyRow = BuildPolicyRow(go.transform);
        var prRT = _policyRow.GetComponent<RectTransform>();
        prRT.anchorMin = new Vector2(0f, 1f); prRT.anchorMax = new Vector2(1f, 1f);
        prRT.pivot     = new Vector2(0.5f, 1f);
        prRT.sizeDelta = new Vector2(0f, hPolicy);
        prRT.anchoredPosition = new Vector2(0f, yOff);
        yOff -= hPolicy;

        _noTimeRow = BuildNoTimeRow(go.transform);
        var ntRT = _noTimeRow.GetComponent<RectTransform>();
        ntRT.anchorMin = new Vector2(0f, 1f); ntRT.anchorMax = new Vector2(1f, 1f);
        ntRT.pivot     = new Vector2(0.5f, 1f);
        ntRT.sizeDelta = new Vector2(0f, hNoTime);
        ntRT.anchoredPosition = new Vector2(0f, yOff);
        yOff -= hNoTime;

        BuildTimelineScroll(go.transform);
        var tsGO = go.transform.Find("TimelineScroll");
        if (tsGO != null)
        {
            var tsRT = tsGO.GetComponent<RectTransform>();
            tsRT.anchorMin = new Vector2(0f, 0f); tsRT.anchorMax = new Vector2(1f, 1f);
            tsRT.pivot     = new Vector2(0.5f, 0.5f);
            tsRT.offsetMin = new Vector2(0f, 0f);
            tsRT.offsetMax = new Vector2(-3f, yOff); // 右 3px = スクロールバー幅

            // 垂直スクロールバー追加
            var vSb = BuildVScrollbar(go.transform, yOff);
            if (vSb != null)
            {
                var sr = tsGO.GetComponent<ScrollRect>();
                if (sr != null)
                {
                    sr.verticalScrollbar = vSb;
                    sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                }
            }
        }

    }
    // ─ DowRow ─────────────────────────────────────────────────
    private Transform BuildDowRow(Transform parent)
    {
        var row = MakeHLGRow("DowRow", parent, DOW_ROW_H);

        // TimeSpacer（ラベルなし）
        MakeTimeSpacer(row, "");

        // DayHeaderCell
        var cell   = MakeGO("DayHeaderCell", row.transform);
        _dayCellImg = cell.AddComponent<Image>(); _dayCellImg.color = Color.clear;
        cell.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var dtGO = MakeGO("DowText", cell.transform);
        var dtRT = dtGO.GetComponent<RectTransform>();
        dtRT.anchorMin = new Vector2(0f,0.5f); dtRT.anchorMax = new Vector2(1f,1f);
        dtRT.offsetMin = dtRT.offsetMax = Vector2.zero;
        _dayDowTxt = dtGO.AddComponent<TextMeshProUGUI>();
        _dayDowTxt.fontSize = UITheme_FocusMode.FontSectionTitle;
        _dayDowTxt.alignment = TextAlignmentOptions.Center;

        var dnGO = MakeGO("DateText", cell.transform);
        var dnRT = dnGO.GetComponent<RectTransform>();
        dnRT.anchorMin = new Vector2(0f,0f); dnRT.anchorMax = new Vector2(1f,0.5f);
        dnRT.offsetMin = dnRT.offsetMax = Vector2.zero;
        _dayDateTxt = dnGO.AddComponent<TextMeshProUGUI>();
        _dayDateTxt.fontSize = UITheme_FocusMode.FontCalendarDate;
        _dayDateTxt.alignment = TextAlignmentOptions.Center;

        return row.transform;
    }

    // ─ PolicyRow ──────────────────────────────────────────────
    private Transform BuildPolicyRow(Transform parent)
    {
        var row = MakeHLGRow("PolicyRow", parent, POLICY_ROW_H);
        MakeTimeSpacer(row, "方針");

        var cell = MakeGO("PolicyCell", row.transform);
        cell.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var img = cell.AddComponent<Image>(); img.color = new Color(1f,1f,1f,0.04f);
        var btn = cell.AddComponent<Button>(); btn.targetGraphic = img;
        var cb  = ColorBlock.defaultColorBlock;
        cb.normalColor      = new Color(1f,1f,1f,0.04f);
        cb.highlightedColor = new Color(0.314f,0.549f,1f,0.15f);
        cb.pressedColor     = new Color(0.314f,0.549f,1f,0.25f);
        cb.selectedColor    = new Color(0.314f,0.549f,1f,0.15f);
        cb.colorMultiplier  = 1f; btn.colors = cb;

        var lGO  = MakeGO("Label", cell.transform);
        StretchRT(lGO, 8f, 0f);
        var lbl  = lGO.AddComponent<TextMeshProUGUI>();
        lbl.enableAutoSizing = true; lbl.fontSizeMin = 12f; lbl.fontSizeMax = 20f;
        lbl.color = UITheme_FocusMode.TextMuted; lbl.alignment = TextAlignmentOptions.Center;
        lbl.overflowMode = TextOverflowModes.Ellipsis; lbl.enableWordWrapping = false;
        lbl.raycastTarget = false;

        return row.transform;
    }

    // ─ NoTimeRow ──────────────────────────────────────────────
    private Transform BuildNoTimeRow(Transform parent)
    {
        var row = MakeHLGRow("NoTimeRow", parent, NOTIME_ROW_H);
        MakeTimeSpacer(row, "");

        var col  = MakeGO("NoTimeCol_0", row.transform);
        var cImg = col.AddComponent<Image>(); cImg.color = Color.clear;
        col.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // 左縦罫線
        var bdr   = MakeGO("ColBorder", col.transform);
        var bdrRT = bdr.GetComponent<RectTransform>();
        bdrRT.anchorMin = new Vector2(0f,0f); bdrRT.anchorMax = new Vector2(0f,1f);
        bdrRT.sizeDelta = new Vector2(_hairline, 0f); bdrRT.anchoredPosition = Vector2.zero;
        bdr.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;
        bdr.AddComponent<LayoutElement>().ignoreLayout = true;

        // Scroll > Viewport > Container
        var sr    = MakeGO("Scroll", col.transform);
        var srRT  = sr.GetComponent<RectTransform>();
        srRT.anchorMin = Vector2.zero; srRT.anchorMax = Vector2.one; srRT.offsetMin = srRT.offsetMax = Vector2.zero;
        var scroll = sr.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = false;

        var vp   = MakeGO("Viewport", sr.transform);
        var vpRT = vp.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = new Vector2(-SCROLLBAR_W, 0f); // 右端をスクロールバー幅ぶん空ける（重ねない）
        vp.AddComponent<RectMask2D>();

        var ct   = MakeGO("Container", vp.transform);
        var ctRT = ct.GetComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(0f,1f); ctRT.anchorMax = new Vector2(1f,1f);
        ctRT.pivot = new Vector2(0.5f,1f); ctRT.sizeDelta = Vector2.zero;
        var ctVLG = ct.AddComponent<VerticalLayoutGroup>();
        ctVLG.childForceExpandWidth = true; ctVLG.childForceExpandHeight = false; ctVLG.spacing = 2f;
        ct.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = ctRT;

        return row.transform;
    }

    // ─ TimelineScroll ─────────────────────────────────────────
    private void BuildTimelineScroll(Transform parent)
    {
        var go  = MakeGO("TimelineScroll", parent);
        var le  = go.AddComponent<LayoutElement>(); le.flexibleHeight = 1f;
        var sr  = go.AddComponent<ScrollRect>(); sr.horizontal = false; sr.vertical = true;
        sr.scrollSensitivity = 60f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var vp  = MakeGO("Viewport", go.transform);
        var vpRT = vp.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = new Vector2(-SCROLLBAR_W, 0f); // 右端をスクロールバー幅ぶん空ける（重ねない）
        vp.AddComponent<RectMask2D>();

        var tl   = MakeGO("TimelineParent", vp.transform);
        var tlRT = tl.GetComponent<RectTransform>();
        tlRT.anchorMin = new Vector2(0f,1f); tlRT.anchorMax = new Vector2(1f,1f);
        tlRT.pivot = new Vector2(0.5f,1f); tlRT.sizeDelta = Vector2.zero;

        sr.content  = tlRT;
        sr.viewport = vpRT;
        _timelineScroll  = sr;
        _timelineParent  = tl.transform;
    }

    /// <summary>TimelineScroll 右端の垂直スクロールバー</summary>
    private Scrollbar BuildVScrollbar(Transform parent, float topOffset)
    {
        // topOffset: タイムライン開始位置までのオフセット（負値、例 -228）
        // スクロールバーはタイムライン領域（0:00〜24:00）のみに表示
        var sbGO = MakeGO("VScrollbar", parent);
        var sbRT = sbGO.GetComponent<RectTransform>();

        // Y ストレッチ: anchorMin.y=0 anchorMax.y=1
        // X ポイント: anchorMin.x=anchorMax.x=1 (右端)
        // offsetMin/Max で位置を指定（sizeDelta との競合を避ける）
        sbRT.anchorMin = new Vector2(1f, 0f);
        sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.pivot     = new Vector2(1f, 0.5f);
        // 幅 20px: X→ offsetMin.x=-SCROLLBAR_W, offsetMax.x=0（ビューポートは別途20px narrow済みなので重ならない）
        // 高さ: bottom から parent.top+topOffset まで（タイムライン領域）
        sbRT.offsetMin = new Vector2(-SCROLLBAR_W, 0f);
        sbRT.offsetMax = new Vector2(0f, topOffset); // topOffset は負値

        var trackImg = sbGO.AddComponent<Image>();
        trackImg.color = UITheme_FocusMode.ScrollTrack;
        ApplyRoundedSprite(trackImg, 10f); // トラック: 半径10px（はっきり角丸）
        sbGO.AddComponent<RectMask2D>(); // Handle をトラック幅内にクリップ
        var sb = sbGO.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;

        // Sliding Area
        var saGO = MakeGO("Sliding Area", sbGO.transform);
        var saRT = saGO.GetComponent<RectTransform>();
        saRT.anchorMin = Vector2.zero; saRT.anchorMax = Vector2.one;
        saRT.offsetMin = new Vector2(0f, 0f); saRT.offsetMax = new Vector2(0f, 0f);

        // Handle
        var hGO  = MakeGO("Handle", saGO.transform);
        var hImg = hGO.AddComponent<Image>();
        hImg.color = UITheme_FocusMode.ScrollHandle;
        ApplyRoundedSprite(hImg, 10f); // つまみ: 半径10px（幅20pxの上限＝完全ピル）
        // つまみを左右2px内側に寄せる（トラックの縁との間に余白を作り、はみ出し防止＋見栄え）
        var hRT = hGO.GetComponent<RectTransform>();
        hRT.offsetMin = new Vector2(0f, hRT.offsetMin.y); // パディング0：つまみ幅をトラック一杯(20px)にして半径上限を10pxへ
        hRT.offsetMax = new Vector2(0f, hRT.offsetMax.y);
        sb.handleRect    = hRT;
        sb.targetGraphic = hImg;

        return sb;
    }

    /// <summary>cardSprite(角丸9-slice)を指定半径(px)で当てる。
    /// ppuMultiplier = border.x * 100 / (pixelsPerUnit * radiusPx) で半径を逆算。
    /// 細い要素(スクロールバー等)でも角丸が潰れないよう半径ベースで指定する。</summary>
    private void ApplyRoundedSprite(Image img, float radiusPx)
    {
        if (img == null || cardSprite == null || radiusPx <= 0f) return;
        img.sprite = cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * radiusPx);
    }

    // ── 付箋パネル ─────────────────────────────────────────────
    private void BuildStickyPanel(Transform parent)
    {
        var sp  = MakeGO("StickyPanel", parent);
        sp.AddComponent<Image>().color = new Color(1f,1f,1f,0.015f);
        sp.AddComponent<LayoutElement>().flexibleWidth = 2f;

        var sc  = MakeGO("StickyCanvas", sp.transform);
        _stickyCanvas = sc.GetComponent<RectTransform>();
        _stickyCanvas.anchorMin = Vector2.zero; _stickyCanvas.anchorMax = Vector2.one;
        _stickyCanvas.offsetMin = _stickyCanvas.offsetMax = Vector2.zero;
        _stickyCanvas.pivot = new Vector2(0f, 1f); // 左上原点

        var bgImg = sc.AddComponent<Image>(); bgImg.color = new Color(0f,0f,0f,0f); bgImg.raycastTarget = true;

        // クリック → 付箋生成（PointerClick EventTrigger）
        var et    = sc.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener((evData) =>
        {
            var ped = (PointerEventData)evData;
            if (ped.dragging) return;
            // 空白部分のクリックのみ（付箋 GO をクリックした場合は付箋側 Button が先に消費する）
            if (ped.rawPointerPress != sc && ped.rawPointerPress != null &&
                ped.rawPointerPress.transform.IsChildOf(sc.transform) &&
                ped.rawPointerPress != sc) return;
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _stickyCanvas, ped.position, ped.pressEventCamera, out localPos);
            SpawnNewNote(localPos);
        });
        et.triggers.Add(entry);
    }

    // =========================================================
    // SetupButtons
    // =========================================================
    private void SetupButtons()
    {
        _prevBtn?.onClick.RemoveAllListeners();
        _nextBtn?.onClick.RemoveAllListeners();
        _sunBtn?.onClick.RemoveAllListeners();
        _monBtn?.onClick.RemoveAllListeners();

        _prevBtn?.onClick.AddListener(() => { _currentDate = _currentDate.AddDays(-1); Refresh(); });
        _nextBtn?.onClick.AddListener(() => { _currentDate = _currentDate.AddDays(1);  Refresh(); });
        _sunBtn?.onClick.AddListener(() => { _weekStartDow = 0; UpdateToggle(); });
        _monBtn?.onClick.AddListener(() => { _weekStartDow = 1; UpdateToggle(); });
        UpdateToggle();
    }

    private void UpdateToggle()
    {
        ApplyToggle(_sunBtn, _weekStartDow == 0);
        ApplyToggle(_monBtn, _weekStartDow == 1);
    }

    private static void ApplyToggle(Button btn, bool on)
    {
        if (!btn) return;
        var cb = btn.colors;
        cb.normalColor      = on ? new Color(0.06f,0.59f,0.99f,0.60f) : new Color(1f,1f,1f,0.08f);
        cb.highlightedColor = on ? new Color(0.06f,0.59f,0.99f,0.80f) : new Color(1f,1f,1f,0.16f);
        cb.pressedColor     = on ? new Color(0.06f,0.59f,0.99f,0.45f) : new Color(1f,1f,1f,0.05f);
        cb.selectedColor    = cb.normalColor; cb.colorMultiplier = 1f; btn.colors = cb;
    }

    // =========================================================
    // Refresh
    // =========================================================
    // ── ウィンドウリサイズ対応 ─────────────────────────────
    private Vector2Int _lastScreenSize;
    private float _resizeRefreshAt = -1f;

    private int _lastDataVersion = -1;

    private void Update()
    {
        // データ変更検知 → 自動Refresh
        var __nm = NotebookManager.Instance;
        if (__nm != null && __nm.DataVersion != _lastDataVersion) { Refresh(); return; }
        // リサイズを検知し、0.25秒静止後にグリッドを再構築（ヘアラインを再スナップ）
        if (Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
        {
            _lastScreenSize  = new Vector2Int(Screen.width, Screen.height);
            _resizeRefreshAt = Time.unscaledTime + 0.25f;
        }
        if (_resizeRefreshAt > 0f && Time.unscaledTime >= _resizeRefreshAt)
        {
            _resizeRefreshAt = -1f;
            Refresh();
        }
    }

    public void Refresh()
    {
        _lastDataVersion = NotebookManager.Instance != null ? NotebookManager.Instance.DataVersion : -1;
        var __cv = GetComponentInParent<Canvas>();
        _hairline = UITheme_FocusMode.Hairline(__cv != null ? __cv.rootCanvas.scaleFactor : 1f);

        if (!_scaffoldBuilt) return;
        Canvas.ForceUpdateCanvases();
        UpdateDayLabel();
        RefreshDowRow();
        RefreshPolicyRow();
        RefreshNoTimeRow();
        RefreshStickyNotes();
        StopAllCoroutines();
        StartCoroutine(BuildTimelineCo());
    }

    private void UpdateDayLabel()
    {
        if (!_dayLabel) return;
        int dow = (int)_currentDate.DayOfWeek;
        _dayLabel.text = $"{_currentDate:yyyy/MM/dd}（{DOW_LABELS[dow]}）";
    }

    // ── DowRow ────────────────────────────────────────────────
    private void RefreshDowRow()
    {
        if (!_dowRow) return;
        int  dow       = (int)_currentDate.DayOfWeek;
        bool isHoliday = HOLIDAY_KEYS.Contains(NotebookManager.DateKey(_currentDate));
        bool isToday   = _currentDate.Date == DateTime.Now.Date;
        bool isSun     = dow == 0 || isHoliday;
        bool isSat     = dow == 6;

        if (_dayDowTxt)
        {
            _dayDowTxt.text  = DOW_LABELS[dow];
            _dayDowTxt.color = isSun ? UITheme_FocusMode.AccentRed
                              : isSat ? UITheme_FocusMode.AccentSatBlue
                              : UITheme_FocusMode.TextMuted;
        }
        if (_dayDateTxt)
        {
            _dayDateTxt.text  = _currentDate.Day.ToString();
            _dayDateTxt.color = isToday ? Color.white
                               : isSun  ? UITheme_FocusMode.AccentRed
                               : isSat  ? UITheme_FocusMode.AccentSatBlue
                               : UITheme_FocusMode.TextBody;
            _dayDateTxt.fontStyle = isToday ? FontStyles.Bold : FontStyles.Normal;
        }
        if (_dayCellImg)
            _dayCellImg.color = isToday ? UITheme_FocusMode.AccentBlueFaint : Color.clear;

        AddBorder(_dowRow, false);
    }

    // ── PolicyRow ─────────────────────────────────────────────
    private void RefreshPolicyRow()
    {
        if (!_policyRow) return;
        var cell = _policyRow.Find("PolicyCell");
        if (!cell) return;
        var lbl  = cell.Find("Label")?.GetComponent<TextMeshProUGUI>();
        var btn  = cell.GetComponent<Button>();
        if (!lbl || !btn) return;

        string dk   = NotebookManager.DateKey(_currentDate);
        string cur  = NotebookManager.Instance?.GetWeeklyMemo(dk) ?? "";
        lbl.text  = cur;
        lbl.color = cur == "" ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextBody;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            var now = NotebookManager.Instance?.GetWeeklyMemo(dk) ?? "";
            int idx = Array.IndexOf(POLICY_OPTIONS, now);
            if (idx < 0) idx = 0;
            idx = (idx + 1) % POLICY_OPTIONS.Length;
            var next = POLICY_OPTIONS[idx];
            NotebookManager.Instance?.SetWeeklyMemo(dk, next);
            lbl.text  = next;
            lbl.color = next == "" ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextBody;
        });
        AddBorder(_policyRow, true); AddBorder(_policyRow, false);
        AddTimeSeparator(_policyRow);
    }

    // ── NoTimeRow ─────────────────────────────────────────────
    private void RefreshNoTimeRow()
    {
        if (!_noTimeRow) return;
        var col = _noTimeRow.Find("NoTimeCol_0");
        if (!col) return;
        var ct  = col.Find("Scroll/Viewport/Container");
        if (!ct) return;
        foreach (Transform c in ct) Destroy(c.gameObject);

        string dk  = NotebookManager.DateKey(_currentDate);
        var evs    = NotebookManager.Instance?.GetEventsByDate(_currentDate) ?? new List<ScheduleEvent>();
        var noTime = evs.Where(e => string.IsNullOrEmpty(e.time)).ToList();

        // 空白クリック → 追加フォーム
        var colBtn = col.GetComponent<Button>() ?? col.gameObject.AddComponent<Button>();
        colBtn.targetGraphic = col.GetComponent<Image>();
        colBtn.onClick.RemoveAllListeners();
        colBtn.onClick.AddListener(() => OpenAddForm(dk, null));

        int show = Mathf.Min(noTime.Count, NOTIME_VISIBLE);
        for (int i = 0; i < show; i++) AddNoTimeChip(ct, noTime[i]);
        if (noTime.Count > show) AddMoreChip(ct, noTime.Count - show, dk, noTime);

        AddBorder(_noTimeRow, true); AddBorder(_noTimeRow, false);
    }

    private void AddNoTimeChip(Transform ct, ScheduleEvent ev)
    {
        var tag    = TagConfig.GetById(ev.tagId);
        var go     = MakeGO("Chip", ct);
        go.AddComponent<LayoutElement>().preferredHeight = NOTIME_ITEM_H;
        var img    = go.AddComponent<Image>();
        var chipBase = tag != null ? tag.chipBG : UITheme_FocusMode.AccentBlueFaint;
        img.color = UITheme_FocusMode.CardBG(chipBase);
        if (cardSprite != null)
        {
            img.sprite = cardSprite; img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * 4f);
        }
        // タグ色ストライプ（タイムラインのカードと同仕様）
        var stGO = MakeGO("Stripe", go.transform);
        var stRT = stGO.GetComponent<RectTransform>();
        stRT.anchorMin = new Vector2(0f,0f); stRT.anchorMax = new Vector2(0f,1f);
        stRT.pivot = new Vector2(0f,0.5f);
        stRT.sizeDelta = new Vector2(3f,-8f); stRT.anchoredPosition = new Vector2(2f,0f);
        var stImg = stGO.AddComponent<Image>();
        stImg.color = tag != null ? tag.chipBorder : UITheme_FocusMode.AccentBlue;
        stImg.raycastTarget = false;
        if (cardSprite != null)
        {
            stImg.sprite = cardSprite; stImg.type = Image.Type.Sliced;
            stImg.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * 1.5f);
        }
        var tGO    = MakeGO("Text", go.transform);
        var tgRT   = tGO.GetComponent<RectTransform>();
        tgRT.anchorMin = Vector2.zero; tgRT.anchorMax = Vector2.one;
        tgRT.offsetMin = new Vector2(10f, 1f); tgRT.offsetMax = new Vector2(-4f, -1f);
        var txt    = tGO.AddComponent<TextMeshProUGUI>();
        txt.text   = ev.title; txt.fontSize = UITheme_FocusMode.FontChipTitle;
        txt.color  = Color.white; txt.enableWordWrapping = false;
        txt.overflowMode = TextOverflowModes.Overflow; // Ellipsis は日本語で誤動作するため手動省略
        txt.alignment = TextAlignmentOptions.MidlineLeft; txt.raycastTarget = false;
        // レイアウト確定後の実幅で手動省略（TMP Ellipsis の日本語バグ回避）
        Canvas.ForceUpdateCanvases();
        txt.text = UITextUtil.EllipsizeOneLine(txt, ev.title, tgRT.rect.width);
        var btn    = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cap    = ev;
        btn.onClick.AddListener(() => OpenEditForm(cap));
    }

    private void AddMoreChip(Transform ct, int extra, string dk, List<ScheduleEvent> allEvs)
    {
        var go   = MakeGO("MoreLabel", ct);
        go.AddComponent<LayoutElement>().preferredHeight = NOTIME_ITEM_H;
        var bg   = go.AddComponent<Image>(); bg.color = Color.clear;
        var btn  = go.AddComponent<Button>(); btn.targetGraphic = bg;
        var tGO  = MakeGO("Text", go.transform); StretchRT(tGO, 5f, 0f);
        var txt  = tGO.AddComponent<TextMeshProUGUI>();
        txt.text = $"他 {extra} 件"; txt.fontSize = UITheme_FocusMode.FontMoreLabel;
        txt.color = UITheme_FocusMode.AccentSatBlue;
        txt.alignment = TextAlignmentOptions.MidlineLeft; txt.raycastTarget = false;
        var capRT  = go.GetComponent<RectTransform>();
        var capEvs = new List<ScheduleEvent>(allEvs);
        string capDk = dk;
        btn.onClick.AddListener(() =>
        {
            if (!_dayEventsPopup) return;
            var corners = new Vector3[4]; capRT.GetWorldCorners(corners);
            var sp = new Vector2((corners[0].x + corners[2].x)*0.5f, (corners[0].y + corners[2].y)*0.5f);
            _dayEventsPopup.Show(capDk, capEvs, OpenEditForm, sp);
        });
    }

    // ── タイムライン ──────────────────────────────────────────
    private IEnumerator BuildTimelineCo()
    {
        var kills = new List<Transform>();
        foreach (Transform c in _timelineParent) kills.Add(c);
        foreach (var c in kills) Destroy(c.gameObject);
        yield return null;
        Canvas.ForceUpdateCanvases();

        var tlRT = _timelineParent.GetComponent<RectTransform>();
        float w  = tlRT != null ? tlRT.rect.width : 600f;
        if (w < 10f) w = 600f;

        float totalH = HOUR_COUNT * HOUR_HEIGHT;
        if (tlRT) tlRT.sizeDelta = new Vector2(tlRT.sizeDelta.x, totalH);

        string dk  = NotebookManager.DateKey(_currentDate);
        var evs    = NotebookManager.Instance?.GetEventsByDate(_currentDate) ?? new List<ScheduleEvent>();
        var timed  = evs.Where(e => !string.IsNullOrEmpty(e.time)).ToList();
        float colW = w - TIME_COL_W;
        bool  today = _currentDate.Date == DateTime.Now.Date;

        BuildTimeLabels(totalH);
        BuildDayColumn(dk, colW, totalH, today, timed);
        if (today) BuildNowLine(colW);

        if (today)
        {
            yield return null;
            float nowH = Mathf.Clamp(DateTime.Now.Hour - 1f, 0f, HOUR_COUNT);
            _timelineScroll.verticalNormalizedPosition = Mathf.Clamp01(1f - nowH * HOUR_HEIGHT / totalH);
        }
    }

    private void BuildTimeLabels(float totalH)
    {
        var go   = MakeGO("TimeLabels", _timelineParent);
        var rt   = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f,1f); rt.anchorMax = new Vector2(0f,1f);
        rt.pivot = new Vector2(0f,1f); rt.sizeDelta = new Vector2(TIME_COL_W, totalH); rt.anchoredPosition = Vector2.zero;

        for (int h = 0; h <= HOUR_COUNT; h++)
        {
            var lb   = MakeGO($"H{h:D2}", go.transform);
            var lbRT = lb.GetComponent<RectTransform>();
            lbRT.anchorMin = new Vector2(0f,1f); lbRT.anchorMax = new Vector2(1f,1f);
            lbRT.pivot = new Vector2(0.5f,1f);
            lbRT.sizeDelta = new Vector2(-4f, HOUR_HEIGHT);
            lbRT.anchoredPosition = new Vector2(0f, -h * HOUR_HEIGHT);
            var t = lb.AddComponent<TextMeshProUGUI>();
            t.text = $"{h:D2}:00"; t.fontSize = UITheme_FocusMode.FontSectionTitle;
            t.color = UITheme_FocusMode.TextBody; t.alignment = TextAlignmentOptions.TopRight;
        }
    }

    private void BuildDayColumn(string dk, float colW, float totalH, bool isToday, List<ScheduleEvent> evs)
    {
        var col   = MakeGO("DayCol", _timelineParent);
        var colRT = col.GetComponent<RectTransform>();
        colRT.anchorMin = new Vector2(0f,1f); colRT.anchorMax = new Vector2(0f,1f);
        colRT.pivot = new Vector2(0f,1f);
        colRT.sizeDelta = new Vector2(colW, totalH);
        colRT.anchoredPosition = new Vector2(TIME_COL_W, 0f);

        // ── ドラッグで時間範囲を選択してアイテム追加（Google Cal 風）────
        var dragCreator = col.AddComponent<TimelineDragCreator>();
        string dragDk   = dk;
        dragCreator.Setup(dragDk, HOUR_COUNT, HOUR_HEIGHT,
            (dk2, st, en) => OpenAddForm(dk2, st, en));

        // 当日列の背景塗りは廃止（ヘッダーのハイライト＋現在時刻ラインで当日を表現）

        for (int h = 0; h < HOUR_COUNT; h++)
        {
            var sl   = MakeGO($"Slot_{h:D2}", col.transform);
            var slRT = sl.GetComponent<RectTransform>();
            slRT.anchorMin = new Vector2(0f,1f); slRT.anchorMax = new Vector2(1f,1f);
            slRT.pivot = new Vector2(0.5f,1f);
            slRT.sizeDelta = new Vector2(0f, HOUR_HEIGHT);
            slRT.anchoredPosition = new Vector2(0f, -h * HOUR_HEIGHT);

            var ln   = MakeGO("HLine", sl.transform);
            var lnRT = ln.GetComponent<RectTransform>();
            lnRT.anchorMin = new Vector2(0f,1f); lnRT.anchorMax = new Vector2(1f,1f);
            lnRT.pivot = new Vector2(0.5f,1f); lnRT.sizeDelta = new Vector2(0f, _hairline); lnRT.anchoredPosition = Vector2.zero;
            var lnImg = ln.AddComponent<Image>();
            lnImg.color = h % 2 == 0 ? UITheme_FocusMode.BorderDivider : UITheme_FocusMode.BorderSubtle;
            lnImg.raycastTarget = false;

            var slImg = sl.AddComponent<Image>(); slImg.color = Color.clear;
            var btn   = sl.AddComponent<Button>(); btn.targetGraphic = slImg;
            int   capH = h; string capDk = dk;
            btn.onClick.AddListener(() => OpenAddForm(capDk, $"{capH:D2}:00"));
        }

        // 終端線
        var el   = MakeGO("EndLine", col.transform);
        var elRT = el.GetComponent<RectTransform>();
        elRT.anchorMin = new Vector2(0f,1f); elRT.anchorMax = new Vector2(1f,1f);
        elRT.pivot = new Vector2(0.5f,1f); elRT.sizeDelta = new Vector2(0f, _hairline);
        elRT.anchoredPosition = new Vector2(0f, -HOUR_COUNT * HOUR_HEIGHT);
        el.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;

        foreach (var (ev, lane, lanes) in AssignLanes(evs)) BuildBlock(col.transform, ev, colW, lane, lanes);

        // 縦境界線
        var cb   = MakeGO("ColBorder", col.transform);
        var cbRT = cb.GetComponent<RectTransform>();
        cbRT.anchorMin = new Vector2(0f,0f); cbRT.anchorMax = new Vector2(0f,1f);
        cbRT.sizeDelta = new Vector2(_hairline, 0f); cbRT.anchoredPosition = Vector2.zero;
        cb.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;
    }

    private void BuildNowLine(float colW)
    {
        float nowH = DateTime.Now.Hour + DateTime.Now.Minute / 60f;
        var ln   = MakeGO("NowLine", _timelineParent);
        var lnRT = ln.GetComponent<RectTransform>();
        lnRT.anchorMin = new Vector2(0f,1f); lnRT.anchorMax = new Vector2(0f,1f);
        lnRT.pivot = new Vector2(0f,0.5f);
        lnRT.sizeDelta = new Vector2(colW, 2f);
        lnRT.anchoredPosition = new Vector2(TIME_COL_W, -nowH * HOUR_HEIGHT);
        ln.AddComponent<Image>().color = UITheme_FocusMode.AccentRed;

        var dt   = MakeGO("Dot", ln.transform);
        var dtRT = dt.GetComponent<RectTransform>();
        dtRT.anchorMin = new Vector2(0f,0.5f); dtRT.anchorMax = new Vector2(0f,0.5f);
        dtRT.sizeDelta = new Vector2(8f,8f); dtRT.anchoredPosition = new Vector2(-4f,0f);
        dt.AddComponent<Image>().color = UITheme_FocusMode.AccentRed;
    }

    /// <summary>
    /// 重なりクラスタ単位でレーン割当。時間的に連結した予定グループ内だけで
    /// totalLanes を数えるため、無関係な時間帯の予定はフル幅を維持する。
    /// </summary>
    private List<(ScheduleEvent, int, int)> AssignLanes(List<ScheduleEvent> evs)
    {
        var sorted = evs.Where(e => !string.IsNullOrEmpty(e.time))
                        .OrderBy(e => e.time).ToList();
        var result   = new List<(ScheduleEvent,int,int)>();
        var laneEnds = new List<float>();                        // 現クラスタのレーン終端
        var cluster  = new List<(ScheduleEvent ev, int lane)>(); // 現クラスタの予定
        float clusterEnd = float.MinValue;

        void Flush()
        {
            int total = Mathf.Max(1, laneEnds.Count);
            foreach (var (ev, l) in cluster) result.Add((ev, l, total));
            cluster.Clear(); laneEnds.Clear(); clusterEnd = float.MinValue;
        }

        foreach (var ev in sorted)
        {
            float sH = ParseH(ev.time);
            if (sH < 0f) continue;
            float eH = string.IsNullOrEmpty(ev.endTime)
                ? sH + 1f
                : Mathf.Max(ParseH(ev.endTime), sH + 0.25f);

            // 開始がクラスタの最遠終端以降なら連結が切れた → 確定
            if (cluster.Count > 0 && sH >= clusterEnd - 0.01f) Flush();

            int lane = -1;
            for (int l = 0; l < laneEnds.Count; l++)
                if (sH >= laneEnds[l] - 0.01f) { lane = l; laneEnds[l] = eH; break; }
            if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(eH); }
            cluster.Add((ev, lane));
            clusterEnd = Mathf.Max(clusterEnd, eH);
        }
        Flush();
        return result;
    }

    private void BuildBlock(Transform parent, ScheduleEvent ev, float colW, int lane, int lanes)
    {
        float s  = ParseH(ev.time); if (s < 0) return;
        float e  = string.IsNullOrEmpty(ev.endTime) ? s + 1f : Mathf.Max(ParseH(ev.endTime), s + 0.25f);
        float bH = Mathf.Max((e - s) * HOUR_HEIGHT - 2f, 16f);
        // 右側に固定12pxのクリック余白（ガター）を確保し、同時間帯への追加を可能にする
        const float GUTTER = 12f;
        float lW = (colW - 2f - GUTTER) / Mathf.Max(1, lanes);
        float lX = 1f + lane * lW;
        var   tag = TagConfig.GetById(ev.tagId);

        var bk   = MakeGO("EventBlock", parent);
        var bkRT = bk.GetComponent<RectTransform>();
        bkRT.anchorMin = new Vector2(0f,1f); bkRT.anchorMax = new Vector2(0f,1f);
        bkRT.pivot = new Vector2(0f,1f);
        bkRT.sizeDelta = new Vector2(lW - 1f, bH);
        bkRT.anchoredPosition = new Vector2(lX, -s * HOUR_HEIGHT - 1f);
        var bkImg = bk.AddComponent<Image>();
        var baseC = tag != null ? tag.chipBG : UITheme_FocusMode.AccentBlueFaint;
        bkImg.color = UITheme_FocusMode.CardBG(baseC); // 淡色・不透明（パネル色とブレンド）
        if (cardSprite != null)
        {
            bkImg.sprite = cardSprite;
            bkImg.type   = Image.Type.Sliced;
            // 9-slice 角丸を 4px 相当に（CanvasScaler refPPU=100 前提）
            bkImg.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * 4f);
        }

        var bd   = MakeGO("Border", bk.transform);
        var bdRT = bd.GetComponent<RectTransform>();
        bdRT.anchorMin = new Vector2(0f,0f); bdRT.anchorMax = new Vector2(0f,1f);
        bdRT.pivot     = new Vector2(0f, 0.5f);
        bdRT.sizeDelta = new Vector2(3f, -8f);        // 上下4pxインセット（角丸からはみ出さない）
        bdRT.anchoredPosition = new Vector2(2f, 0f);  // 左から2px
        var bdImg = bd.AddComponent<Image>();
        bdImg.color = tag != null ? tag.chipBorder : UITheme_FocusMode.AccentBlue;
        bdImg.raycastTarget = false;
        if (cardSprite != null)
        {
            bdImg.sprite = cardSprite;
            bdImg.type   = Image.Type.Sliced;
            bdImg.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * 1.5f); // 半径1.5px=ピル形
        }

        var tx   = MakeGO("Title", bk.transform);
        var txRT = tx.GetComponent<RectTransform>();
        txRT.anchorMin = Vector2.zero; txRT.anchorMax = Vector2.one;
        txRT.offsetMin = new Vector2(10f, 3f); txRT.offsetMax = new Vector2(-4f, -3f);
        var tTxt = tx.AddComponent<TextMeshProUGUI>();
        tTxt.text = ev.title; tTxt.fontSize = UITheme_FocusMode.FontChipTitle;
        tTxt.color = Color.white; tTxt.fontStyle = FontStyles.Bold;
        tTxt.overflowMode = TextOverflowModes.Ellipsis; tTxt.raycastTarget = false;
        tTxt.lineSpacing = -70f; // 行間（Kotonoruは内部余白が大きいためメトリクス上は重なり気味が見た目の適正値）

        var btn = bk.AddComponent<Button>(); btn.targetGraphic = bkImg;
        var cap = ev;

        // ドラッグ移動・上下リサイズ（15分スナップ、ドロップで即保存）
        btn.gameObject.AddComponent<EventBlockDragger>().Init(ev, HOUR_HEIGHT,
            () => { NotebookManager.Instance?.SaveAll(); Refresh(); },
            () => OpenEditForm(cap),
            cardSprite);
    }

    // ── 付箋 ─────────────────────────────────────────────────
    private void RefreshStickyNotes()
    {
        if (!_stickyCanvas) return;
        Canvas.ForceUpdateCanvases();
        // 既存付箋を削除（StickyNote コンポーネントを持つ子のみ）
        var kill = new List<GameObject>();
        foreach (Transform c in _stickyCanvas)
            if (c.GetComponent<StickyNote>() != null) kill.Add(c.gameObject);
        foreach (var g in kill) Destroy(g);

        string dk   = NotebookManager.DateKey(_currentDate);
        var notes   = NotebookManager.Instance?.GetStickyNotes(dk) ?? new List<StickyNoteData>();
        var canvas  = GetComponentInParent<Canvas>();
        foreach (var n in notes) SpawnStickyNote(n, canvas);
    }

    private void SpawnNewNote(Vector2 localPos)
    {
        if (_blockNextNoteSpawn) { _blockNextNoteSpawn = false; return; }
        Canvas.ForceUpdateCanvases();
        string dk     = NotebookManager.DateKey(_currentDate);
        var    canvas = GetComponentInParent<Canvas>();

        var go   = new GameObject("StickyNote_new", typeof(RectTransform));
        go.transform.SetParent(_stickyCanvas, false);
        var note = go.AddComponent<StickyNote>();
        note.InitNew(dk, canvas, _stickyCanvas, localPos, null);
        note.FocusInput();
    }

    private StickyNote SpawnStickyNote(StickyNoteData data, Canvas canvas)
    {
        if (data == null || !_stickyCanvas) return null;
        var go   = new GameObject($"StickyNote_{data.id}", typeof(RectTransform));
        go.transform.SetParent(_stickyCanvas, false);
        var note = go.AddComponent<StickyNote>();
        note.Init(data, canvas, _stickyCanvas, null);
        return note;
    }

    // ── モーダル ──────────────────────────────────────────────
    private void OpenAddForm(string dk, string time, string endTime = null)
    {
        if (_eventModal) _eventModal.OpenAddForm(dk, Refresh, time, endTime);
        else _floatingWindow?.OpenAddForm(dk, null, time);
    }

    private void OpenEditForm(ScheduleEvent ev)
    {
        if (_eventModal) _eventModal.OpenEditForm(ev, Refresh);
        else _floatingWindow?.OpenEventDetail(ev, Refresh);
    }

    // ── ユーティリティ ─────────────────────────────────────────
    private static float ParseH(string t)
    {
        if (string.IsNullOrEmpty(t)) return -1f;
        if (!TimeSpan.TryParse(t, out var ts)) return -1f;
        float m = ts.Hours * 60f + ts.Minutes;
        return Mathf.Round(m / 15f) * 15f / 60f;
    }

    private static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private GameObject MakeHLGRow(string name, Transform parent, float h)
    {
        var go  = MakeGO(name, parent);
        go.AddComponent<Image>().color = Color.clear;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.spacing = 0f;
        go.AddComponent<LayoutElement>().preferredHeight = h;
        return go;
    }

    private static GameObject MakeTimeSpacer(GameObject parent, string label)
    {
        var go  = MakeGO("TimeSpacer", parent.transform);
        go.AddComponent<Image>().color = Color.clear;
        var le  = go.AddComponent<LayoutElement>(); le.preferredWidth = TIME_COL_W; le.minWidth = TIME_COL_W;
        if (!string.IsNullOrEmpty(label))
        {
            var lGO = MakeGO("RowLabel", go.transform); StretchRT(lGO);
            var t   = lGO.AddComponent<TextMeshProUGUI>();
            t.text  = label; t.fontSize = UITheme_FocusMode.FontSectionTitle;
            t.color = UITheme_FocusMode.TextBody; t.alignment = TextAlignmentOptions.Center;
        }
        return go;
    }

    private static void StretchRT(GameObject go, float hPad = 0f, float vPad = 0f)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(hPad, vPad); rt.offsetMax = new Vector2(-hPad, -vPad);
    }

    private void AddBorder(Transform row, bool top)
    {
        string key = top ? "HBorder_Top" : "HBorder_Bot";
        var ex = row.Find(key);
        if (ex) { var ei = ex.GetComponent<Image>(); if (ei) ei.color = UITheme_FocusMode.BorderDivider; return; }
        var go  = MakeGO(key, row);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f); rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f); rt.sizeDelta = new Vector2(0f, _hairline); rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        rt.SetAsLastSibling();
    }

    /// <summary>TimeSpacer と PolicyCell の間に縦区切り線を追加</summary>
    private void AddTimeSeparator(Transform row)
    {
        const string key = "TimeSeparator";
        if (row.Find(key) != null) return;
        var go  = MakeGO(key, row);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(_hairline, 0f);
        rt.anchoredPosition = new Vector2(TIME_COL_W, 0f); // TimeSpacer 右端
        go.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        rt.SetAsLastSibling();
    }
}
