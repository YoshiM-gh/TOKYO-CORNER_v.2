using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 週カレンダーUI（Google Calendar スタイル）
/// - ヘッダー   : 前後週ボタン + 週ラベル + 日/月曜始まりトグル
/// - 曜日行     : 固定 7列（WeekHeaderCell）
/// - メモ行     : 固定・週単位 3行メモ（TMP_InputField MultiLine）
/// - 時間なし行 : 固定・3件表示 + 他N件→DayEventsPopup（Monthly同仕様）
/// - タイムライン: 0〜24h・15分スナップ配置・整時線のみ・縦スクロール
/// </summary>
public class WeeklyCalendarUI : MonoBehaviour
{
    [Header("ナビゲーション")]
    [SerializeField] private Button             prevWeekBtn;
    [SerializeField] private Sprite cardSprite; // 角丸カード用 9-slice（Rounded Filled 32px）
    [SerializeField] private Button             nextWeekBtn;
    [SerializeField] private TextMeshProUGUI    weekLabel;
    [SerializeField] private Button             weekStartSunBtn;
    [SerializeField] private Button             weekStartMonBtn;

    [Header("固定エリア")]
    [SerializeField] private Transform          dowRow;
    [SerializeField] private Transform          memoRow;
    [SerializeField] private Transform          noTimeRow;

    [Header("タイムライン")]
    [SerializeField] private ScrollRect         timelineScroll;
    [SerializeField] private Transform          timelineParent;

    [Header("フローティングウィンドウ")]
    [SerializeField] private FloatingWindowController floatingWindow;
    [SerializeField] private DayEventsPopup           dayEventsPopup;
    [SerializeField] private EventModal               eventModal;

    // ── 定数 ──────────────────────────────────────────────────
    private const float HOUR_HEIGHT    = 60f;
    private const float TIME_COL_W     = 72f;
    private const int   HOUR_COUNT     = 24;
    private const float NOTIME_ITEM_H  = 28f;
    private const int   NOTIME_VISIBLE = 3;    // 最大表示件数（超過 → 他N件）
    private const float MEMO_ROW_H     = 80f;  // 3行分の高さ (14pt対応)
    private static string[] POLICY_OPTIONS => PolicyOptions.All; // 単一ソース化(PolicyOptions.cs)。挙動不変
    private const float NOTIME_ROW_H   = 92f;  // 3行分 + 余白
    private float _hairline = 2f;   // 物理ピクセルにスナップ済みのヘアライン太さ（Refreshで更新）

    private static readonly string[] DowLabels =
        { "日", "月", "火", "水", "木", "金", "土" };

    private static readonly HashSet<string> HolidayKeys = new HashSet<string>
    {
        "2026-01-01","2026-01-12","2026-02-11","2026-02-23","2026-03-20",
        "2026-04-29","2026-05-03","2026-05-04","2026-05-05","2026-07-20",
        "2026-08-11","2026-09-21","2026-09-23","2026-10-12","2026-11-03","2026-11-23",
    };

    private DateTime weekStart;
    private int      weekStartDow = 0;   // 0=日曜, 1=月曜

    // ── ライフサイクル ────────────────────────────────────────
    private void OnEnable()
    {
        if (timelineScroll != null) timelineScroll.scrollSensitivity = 60f;
        NavHeaderStyler.LayoutAndGear(transform.Find("Content/Header"), CategorySettingsController.Toggle);  // Phase1: ヘッダー部品規格
        UITheme_FocusMode.OnThemeChanged += Refresh;
        weekStart = GetWeekStart(DateTime.Now, weekStartDow);
        SetupButtons();
        SetupRowLabels();
        Refresh();
    }

    private void OnDisable()
    {
        UITheme_FocusMode.OnThemeChanged -= Refresh;
    }

    // ── ボタン設定 ────────────────────────────────────────────
    private void SetupButtons()
    {
        prevWeekBtn?.onClick.RemoveAllListeners();
        nextWeekBtn?.onClick.RemoveAllListeners();
        weekStartSunBtn?.onClick.RemoveAllListeners();
        weekStartMonBtn?.onClick.RemoveAllListeners();

        prevWeekBtn?.onClick.AddListener(() => { weekStart = weekStart.AddDays(-7); Refresh(); });
        nextWeekBtn?.onClick.AddListener(() => { weekStart = weekStart.AddDays( 7); Refresh(); });
        weekStartSunBtn?.onClick.AddListener(() => ChangeWeekStartDow(0));
        weekStartMonBtn?.onClick.AddListener(() => ChangeWeekStartDow(1));

        UpdateToggleBtns();
    }

    private void ChangeWeekStartDow(int dow)
    {
        var mid     = weekStart.AddDays(3);
        weekStartDow = dow;
        weekStart    = GetWeekStart(mid, weekStartDow);
        UpdateToggleBtns();
        Refresh();
    }

    private void UpdateToggleBtns()
    {
        ApplyToggleStyle(weekStartSunBtn, weekStartDow == 0);
        ApplyToggleStyle(weekStartMonBtn, weekStartDow == 1);
    }

    private static void ApplyToggleStyle(Button btn, bool active)
    {
        if (btn == null) return;
        var cb = btn.colors;
        cb.normalColor      = active ? new Color(0.06f,0.59f,0.99f,0.60f) : new Color(1f,1f,1f,0.08f);
        cb.highlightedColor = active ? new Color(0.06f,0.59f,0.99f,0.80f) : new Color(1f,1f,1f,0.16f);
        cb.pressedColor     = active ? new Color(0.06f,0.59f,0.99f,0.45f) : new Color(1f,1f,1f,0.05f);
        cb.selectedColor    = cb.normalColor;
        cb.colorMultiplier  = 1f;
        btn.colors = cb;
    }

    // ── 行ラベル・高さのセットアップ（OnEnable 毎に呼び出し）──
    // ── 行ラベル・高さのセットアップ（OnEnable 毎に呼び出し）──
    private void SetupRowLabels()
    {
        if (dayEventsPopup == null)
            dayEventsPopup = FindObjectOfType<DayEventsPopup>(true);

        ClearRowBG(memoRow);
        ClearRowBG(noTimeRow);
        FixTimeSpacer(memoRow?.Find("TimeSpacer"));
        FixTimeSpacer(noTimeRow?.Find("TimeSpacer"));
        FixTimeSpacer(dowRow?.Find("TimeSpacer"));

        if (memoRow != null)
        {
            var le = memoRow.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = MEMO_ROW_H;
            AddTimeSpacerLabel(memoRow.Find("TimeSpacer"), "方針");
        }
        if (noTimeRow != null)
        {
            var le = noTimeRow.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = NOTIME_ROW_H;
        }
        SetupDowBorders();
        SetupNoTimeBorders();
        // 行区切り横罫線
        AddHLine(dowRow,    atTop:false);
        AddHLine(memoRow,   atTop:true);
        AddHLine(memoRow,   atTop:false);
        AddHLine(noTimeRow, atTop:true);  // NoTimeRow 上端（メモとの境界）
        AddHLine(noTimeRow, atTop:false);
    }

    private static void ClearRowBG(Transform row)
    {
        if (row == null) return;
        var img = row.GetComponent<Image>();
        if (img != null) img.color = Color.clear;
        foreach (Transform c in row) { var ci = c.GetComponent<Image>(); if (ci != null) ci.color = Color.clear; }
    }

    private static void FixTimeSpacer(Transform ts)
    {
        if (ts == null) return;
        var rt = ts.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(TIME_COL_W, rt.sizeDelta.y);
        var le = ts.GetComponent<LayoutElement>();
        if (le != null) { le.preferredWidth = TIME_COL_W; le.minWidth = TIME_COL_W; }
        // HLG を即座に再計算（TimeSpacer 変更を反映）
        if (ts.parent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(ts.parent.GetComponent<RectTransform>());

    }
    private void SetupDowBorders()
    {
        if (dowRow == null) return;
        foreach (var cell in dowRow.GetComponentsInChildren<WeekHeaderCell>(true))
        {
            if (cell.transform.Find("CellBorder") != null) continue;
            var bdr = new GameObject("CellBorder", typeof(RectTransform));
            bdr.transform.SetParent(cell.transform, false);
            var rt  = bdr.GetComponent<RectTransform>();
            // 下半分（日付番号エリア）のみ縦線表示
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(_hairline, 0f); rt.anchoredPosition = Vector2.zero;
            bdr.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;
            bdr.AddComponent<LayoutElement>().ignoreLayout = true;
            rt.SetAsLastSibling();
        }
    }

    private void SetupNoTimeBorders()
    {
        if (noTimeRow == null) return;
        for (int d = 0; d < 7; d++)
        {
            var col = noTimeRow.Find($"NoTimeCol_{d}");
            if (col == null || col.Find("ColBorder") != null) continue;
            var img = col.GetComponent<Image>();
            if (img != null) img.color = Color.clear;
            var bdr = new GameObject("ColBorder", typeof(RectTransform));
            bdr.transform.SetParent(col, false);
            var rt = bdr.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f,0f); rt.anchorMax = new Vector2(0f,1f);
            rt.sizeDelta = new Vector2(_hairline, 0f); rt.anchoredPosition = Vector2.zero;
            bdr.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;
            rt.SetAsFirstSibling();
            bdr.AddComponent<LayoutElement>().ignoreLayout = true;
        }
    }

    // ── 行区切り横罫線 ──────────────────────────────────────────────────
    private void AddHLine(Transform row, bool atTop)
    {
        if (row == null) return;
        string key = atTop ? "HBorder_Top" : "HBorder_Bot";
        var existingBorder = row.Find(key);
        if (existingBorder != null)
        {
            var existImg = existingBorder.GetComponent<Image>();
            if (existImg != null) existImg.color = UITheme_FocusMode.BorderDivider;
            return;
        }
        var go = new GameObject(key, typeof(RectTransform));
        go.transform.SetParent(row, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
        rt.pivot     = new Vector2(0.5f, atTop ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, _hairline);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color         = UITheme_FocusMode.BorderDivider;
        img.raycastTarget = false;
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        rt.SetAsLastSibling();
    }

    /// <summary>TimeSpacer に行ラベルを追加（重複防止付き）</summary>
    private void AddTimeSpacerLabel(Transform ts, string text)
    {
        if (ts == null) return;
        var existing = ts.Find("RowLabel");
        TextMeshProUGUI tmp;
        if (existing != null)
        {
            tmp = existing.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            var go = new GameObject("RowLabel", typeof(RectTransform));
            go.transform.SetParent(ts, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
        }
        tmp.text     = text;
        tmp.fontSize = UITheme_FocusMode.FontSectionTitle;
        tmp.color    = UITheme_FocusMode.TextBody;
    }

    // ── Refresh ───────────────────────────────────────────────
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

        var weekEnd = weekStart.AddDays(6);
        if (weekLabel != null)
            weekLabel.text = $"{weekStart:yyyy/MM/dd}〜{weekEnd:MM/dd}";

        RefreshDowRow();
        RefreshMemoRow();
        RefreshNoTimeRow();
        StopAllCoroutines();
        StartCoroutine(BuildTimelineCo());
    }

    // ── 曜日・日付行 ──────────────────────────────────────────
    private void RefreshDowRow()
    {
        if (dowRow == null) return;
        var cells = dowRow.GetComponentsInChildren<WeekHeaderCell>(true);
        var today = DateTime.Now.Date;
        for (int i = 0; i < 7 && i < cells.Length; i++)
        {
            var  date      = weekStart.AddDays(i);
            int  dow       = (int)date.DayOfWeek;
            bool isHoliday = HolidayKeys.Contains(NotebookManager.DateKey(date));
            cells[i].Set(DowLabels[dow], date.Day.ToString(),
                date.Date == today, dow == 0 || isHoliday, dow == 6);
            cells[i].SetClickAction(() => { }); // ヘッダークリック無効（旧UI非表示）

            // Todo/Routine インジケータ（ヘッダーセル右側・中央）
            var ind = CalendarDayIndicators.Build(cells[i].transform, date);
            if (ind != null)
            {
                ind.anchorMin = new Vector2(1f, 0.5f);
                ind.anchorMax = new Vector2(1f, 0.5f);
                ind.pivot     = new Vector2(1f, 0.5f);
                ind.anchoredPosition = new Vector2(-6f, 0f);
            }
        }
    }

    // ── Weeklyメモ行 ──────────────────────────────────────────
    private void RefreshMemoRow()
    {
        if (memoRow == null) return;

        // MemoContainer（旧・週1本）が残っていれば除去して7列化
        if (memoRow.Find("MemoCol_0") == null)
        {
            var kill = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in memoRow)
                if (c.name != "TimeSpacer") kill.Add(c.gameObject);
            foreach (var g in kill)
            {
                g.SetActive(false); // 即時入力ブロック解除
                if (Application.isPlaying) Destroy(g);
                else                       DestroyImmediate(g);
            }
            for (int d = 0; d < 7; d++)
            {
                var colGO = new GameObject($"MemoCol_{d}", typeof(RectTransform));
                colGO.transform.SetParent(memoRow, false);
                var colLE = colGO.AddComponent<LayoutElement>(); colLE.flexibleWidth = 1f; colLE.preferredHeight = MEMO_ROW_H;
                colGO.AddComponent<Image>().color = Color.clear;

                // 左縦境界線
                var bdr = new GameObject("ColBorder", typeof(RectTransform));
                bdr.transform.SetParent(colGO.transform, false);
                var bRT = bdr.GetComponent<RectTransform>();
                bRT.anchorMin = new Vector2(0f,0f); bRT.anchorMax = new Vector2(0f,1f);
                bRT.sizeDelta = new Vector2(_hairline, 0f); bRT.anchoredPosition = Vector2.zero;
                bdr.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;
                bdr.AddComponent<LayoutElement>().ignoreLayout = true;

                // 方針サイクルボタン
                var cellGO = new GameObject($"PolicyCell_{d}", typeof(RectTransform));
                cellGO.transform.SetParent(colGO.transform, false);
                var cellRT = cellGO.GetComponent<RectTransform>();
                cellRT.anchorMin = Vector2.zero; cellRT.anchorMax = Vector2.one;
                cellRT.offsetMin = new Vector2(6f, 4f); cellRT.offsetMax = new Vector2(-4f, -4f);
                var cellImg = cellGO.AddComponent<Image>();
                cellImg.color = Color.white;
                var btn = cellGO.AddComponent<Button>();
                var lblGO = new GameObject("Label", typeof(RectTransform));
                lblGO.transform.SetParent(cellGO.transform, false);
                var lblRT = lblGO.GetComponent<RectTransform>();
                lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
                lblRT.offsetMin = new Vector2(8f, 0f); lblRT.offsetMax = new Vector2(-4f, 0f);
                var lbl = lblGO.AddComponent<TextMeshProUGUI>();
                lbl.enableAutoSizing = true;
                lbl.fontSizeMin = 12f;
                lbl.fontSizeMax = 20f;
                lbl.color = UITheme_FocusMode.TextMuted;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                lbl.enableWordWrapping = false;
                lbl.raycastTarget = false;
                btn.targetGraphic = cellImg;
                var cbP = ColorBlock.defaultColorBlock;
                cbP.normalColor      = new Color(1f, 1f, 1f, 0.04f);
                cbP.highlightedColor = new Color(0.314f, 0.549f, 1f, 0.15f);
                cbP.pressedColor     = new Color(0.314f, 0.549f, 1f, 0.25f);
                cbP.selectedColor    = new Color(0.314f, 0.549f, 1f, 0.15f);
                cbP.colorMultiplier  = 1f;
                cbP.fadeDuration     = 0.1f;
                btn.colors = cbP;
                btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            }
        }

        // 方針値を曜日ごとに更新
        for (int d = 0; d < 7; d++)
        {
            var col = memoRow.Find($"MemoCol_{d}");
            if (col == null) continue;
            var cell = col.Find($"PolicyCell_{d}");
            if (cell == null) continue;
            var lbl2 = cell.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl2 == null) continue;
            var btn2 = cell.GetComponent<Button>();
            if (btn2 == null) continue;
            var dateKey = NotebookManager.DateKey(weekStart.AddDays(d));
            var saved   = NotebookManager.Instance?.GetWeeklyMemo(dateKey) ?? "";
            lbl2.text  = saved;
            lbl2.color = saved == "" ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextBody;
            btn2.onClick.RemoveAllListeners();
            var capKey = dateKey;
            var capLbl = lbl2;
            btn2.onClick.AddListener(() =>
            {
                var cur = NotebookManager.Instance?.GetWeeklyMemo(capKey) ?? "";
                var idx = System.Array.IndexOf(POLICY_OPTIONS, cur);
                if (idx < 0) idx = 0;
                idx = (idx + 1) % POLICY_OPTIONS.Length;
                var next = POLICY_OPTIONS[idx];
                NotebookManager.Instance?.SetWeeklyMemo(capKey, next);
                capLbl.text  = next;
                capLbl.color = next == "" ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextBody;
            });
        }
    }
    private void RefreshNoTimeRow()
    {
        if (noTimeRow == null) return;

        var events = NotebookManager.Instance != null
            ? NotebookManager.Instance.GetEventsByWeek(weekStart)
            : new List<ScheduleEvent>();
        var noTimeEvs = events.Where(e => string.IsNullOrEmpty(e.time)).ToList();

        for (int d = 0; d < 7; d++)
        {
            var col = noTimeRow.Find($"NoTimeCol_{d}");
            if (col == null) continue;

            // Container は Scroll/Viewport/Container に存在
            var container = col.Find("Scroll/Viewport/Container");
            if (container == null)
            {
                var sr = col.GetComponentInChildren<ScrollRect>(true);
                container = sr?.content?.transform;
            }
            if (container == null) continue;

            foreach (Transform old in container) Destroy(old.gameObject);

            string dateKey = NotebookManager.DateKey(weekStart.AddDays(d));

            // 空欄クリックで時間なし予定追加モーダルを開く
            var colBtn = col.GetComponent<Button>();
            if (colBtn == null) colBtn = col.gameObject.AddComponent<Button>();
            var colImg = col.GetComponent<Image>();
            if (colImg != null) colBtn.targetGraphic = colImg;
            colBtn.onClick.RemoveAllListeners();
            var capAddKey = dateKey;
            colBtn.onClick.AddListener(() =>
            {
                if (eventModal != null) eventModal.OpenAddForm(capAddKey, Refresh);
                else floatingWindow?.OpenAddForm(capAddKey, null);
            });
            var dayEvs     = noTimeEvs.Where(e => e.date == dateKey).ToList();

            // 最大 NOTIME_VISIBLE 件表示
            int showCount = Mathf.Min(dayEvs.Count, NOTIME_VISIBLE);
            for (int i = 0; i < showCount; i++)
                CreateNoTimeChip(container, dayEvs[i]);

            // 超過分：「他N件」チップ → DayEventsPopup
            int overflow = dayEvs.Count - showCount;
            if (overflow > 0)
                CreateOthersChip(container, overflow, dateKey, dayEvs);
        }
    }

    private void CreateNoTimeChip(Transform parent, ScheduleEvent ev)
    {
        var tag    = TagConfig.GetById(ev.tagId);
        var chipGO = new GameObject("Chip", typeof(RectTransform));
        chipGO.transform.SetParent(parent, false);
        var le = chipGO.AddComponent<LayoutElement>();
        le.preferredHeight = NOTIME_ITEM_H; le.flexibleWidth = 1f;
        var img = chipGO.AddComponent<Image>();
        var chipBase = tag != null ? tag.chipBG : UITheme_FocusMode.AccentBlueFaint;
        img.color = UITheme_FocusMode.CardBG(chipBase);
        if (cardSprite != null)
        {
            img.sprite = cardSprite; img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * 4f);
        }
        // タグ色ストライプ（タイムラインのカードと同仕様）
        var stGO = new GameObject("Stripe", typeof(RectTransform));
        stGO.transform.SetParent(chipGO.transform, false);
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

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(chipGO.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(10f, 1f); txtRT.offsetMax = new Vector2(-4f, -1f);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text             = ev.title;
        txt.fontSize         = UITheme_FocusMode.FontChipTitle;
        txt.color            = Color.white;
        txt.enableWordWrapping = false;
        txt.overflowMode     = TextOverflowModes.Overflow; // Ellipsis は日本語で誤動作するため手動省略
        txt.alignment        = TextAlignmentOptions.MidlineLeft;
        txt.raycastTarget = false;
        // レイアウト確定後の実幅で手動省略（TMP Ellipsis の日本語バグ回避）
        Canvas.ForceUpdateCanvases();
        txt.text = UITextUtil.EllipsizeOneLine(txt, ev.title, txtRT.rect.width);

        var btn = chipGO.AddComponent<Button>(); btn.targetGraphic = img;
        var cap = ev;
        btn.onClick.AddListener(() =>
        {
            if (eventModal != null) eventModal.OpenEditForm(cap, Refresh);
            else floatingWindow?.OpenEventDetail(cap, Refresh);
        });
    }

    /// <summary>「他N件」チップ（Monthly と同仕様）→ クリックで DayEventsPopup を表示</summary>
    private void CreateOthersChip(Transform parent, int count,
                                   string dateKey, List<ScheduleEvent> allEvs)
    {
        var moreGO  = new GameObject("MoreLabel", typeof(RectTransform));
        moreGO.transform.SetParent(parent, false);
        var le      = moreGO.AddComponent<LayoutElement>();
        le.preferredHeight = NOTIME_ITEM_H; le.flexibleWidth = 1f;
        var moreBG  = moreGO.AddComponent<Image>(); moreBG.color = Color.clear;
        var moreBtn = moreGO.AddComponent<Button>();
        var moreCB  = ColorBlock.defaultColorBlock;
        moreCB.highlightedColor = new Color(1f,1f,1f,0.10f);
        moreBtn.colors = moreCB; moreBtn.targetGraphic = moreBG;

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(moreGO.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(5f, 1f); txtRT.offsetMax = new Vector2(-2f, -1f);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text      = $"他 {count} 件";
        txt.fontSize  = UITheme_FocusMode.FontMoreLabel;
        txt.color     = UITheme_FocusMode.AccentSatBlue;
        txt.alignment = TextAlignmentOptions.MidlineLeft;

        string capDate = dateKey;
        var capEvs     = new List<ScheduleEvent>(allEvs);
        var capGO      = moreGO;
        moreBtn.onClick.AddListener(() =>
        {
            if (dayEventsPopup == null) return;
            var cRT     = capGO.GetComponent<RectTransform>();
            var corners = new Vector3[4]; cRT.GetWorldCorners(corners);
            var sp = new Vector2((corners[0].x + corners[2].x) * 0.5f,
                                  (corners[0].y + corners[2].y) * 0.5f);
            dayEventsPopup.Show(capDate, capEvs,
                ev => { if (eventModal != null) eventModal.OpenEditForm(ev, Refresh);
                        else floatingWindow?.OpenEventDetail(ev, Refresh); }, sp);
        });
    }

    // ── タイムライン ──────────────────────────────────────────
    private IEnumerator BuildTimelineCo()
    {
        var children = new List<Transform>();
        foreach (Transform c in timelineParent) children.Add(c);
        foreach (var c in children) Destroy(c.gameObject);
        yield return null;
        Canvas.ForceUpdateCanvases();

        var   tlRT = timelineParent.GetComponent<RectTransform>();
        float parW = tlRT != null ? tlRT.rect.width : 1200f;
        if (parW < 10f) parW = 1200f;
        BuildTimeline(parW);

        if (weekStart.Date <= DateTime.Now.Date &&
            DateTime.Now.Date <= weekStart.AddDays(6).Date)
        {
            yield return null;
            ScrollToNow();
        }
    }

    private void BuildTimeline(float parentW)
    {
        float totalH = HOUR_COUNT * HOUR_HEIGHT;
        var tlRT = timelineParent.GetComponent<RectTransform>();
        if (tlRT != null) tlRT.sizeDelta = new Vector2(tlRT.sizeDelta.x, totalH);

        float  colW  = (parentW - TIME_COL_W) / 7f;
        string today = NotebookManager.DateKey(DateTime.Now);
        var    evs   = NotebookManager.Instance != null
            ? NotebookManager.Instance.GetEventsByWeek(weekStart)
            : new List<ScheduleEvent>();

        BuildTimeLabels(totalH);

        for (int d = 0; d < 7; d++)
        {
            var    date   = weekStart.AddDays(d);
            string key    = NotebookManager.DateKey(date);
            var    dayEvs = evs.Where(e => e.date == key && !string.IsNullOrEmpty(e.time)).ToList();
            BuildDayColumn(d, TIME_COL_W + d * colW, colW, totalH,
                           key == today, (int)date.DayOfWeek, key, dayEvs);
        }
        BuildNowLine(today, colW, totalH);
    }

    private void BuildTimeLabels(float totalH)
    {
        var go = new GameObject("TimeLabels", typeof(RectTransform));
        go.transform.SetParent(timelineParent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f,1f); rt.anchorMax = new Vector2(0f,1f);
        rt.pivot     = new Vector2(0f,1f);
        rt.sizeDelta = new Vector2(TIME_COL_W, totalH);
        rt.anchoredPosition = Vector2.zero;

        for (int h = 0; h <= HOUR_COUNT; h++)
        {
            var lb   = new GameObject($"H{h:D2}", typeof(RectTransform));
            lb.transform.SetParent(go.transform, false);
            var lbRT = lb.GetComponent<RectTransform>();
            lbRT.anchorMin = new Vector2(0f,1f); lbRT.anchorMax = new Vector2(1f,1f);
            lbRT.pivot     = new Vector2(0.5f,1f);
            lbRT.sizeDelta = new Vector2(-4f, HOUR_HEIGHT);
            lbRT.anchoredPosition = new Vector2(0f, -h * HOUR_HEIGHT);
            var txt = lb.AddComponent<TextMeshProUGUI>();
            txt.text      = $"{h:D2}:00";
            txt.fontSize  = UITheme_FocusMode.FontSectionTitle;
            txt.color     = UITheme_FocusMode.TextBody;
            txt.alignment = TextAlignmentOptions.TopRight;
        }
    }

    private void BuildDayColumn(int dayIdx, float colX, float colW, float totalH,
        bool isToday, int dow, string dateKey, List<ScheduleEvent> dayEvs)
    {
        var col = new GameObject($"DayCol_{dayIdx}", typeof(RectTransform));
        col.transform.SetParent(timelineParent, false);
        var colRT = col.GetComponent<RectTransform>();
        colRT.anchorMin = new Vector2(0f,1f); colRT.anchorMax = new Vector2(0f,1f);
        colRT.pivot     = new Vector2(0f,1f);
        colRT.sizeDelta = new Vector2(colW, totalH);
        colRT.anchoredPosition = new Vector2(colX, 0f);

        // ── ドラッグで時間範囲を選択してアイテム追加（Google Cal 風）────
        var dragCreator = col.AddComponent<TimelineDragCreator>();
        string dragDk   = dateKey;
        dragCreator.Setup(dragDk, HOUR_COUNT, HOUR_HEIGHT, (dk2, st, en) =>
        {
            if (eventModal != null) eventModal.OpenAddForm(dk2, Refresh, st, en);
            else floatingWindow?.OpenAddForm(dk2, null, st);
        });


        // 当日列の背景塗りは廃止（ヘッダーのハイライト＋現在時刻ラインで当日を表現）

        // ── 整時線のみ（15分・30分線は引かない）─────────────
        for (int h = 0; h < HOUR_COUNT; h++)
        {
            var sl   = new GameObject($"Slot_{h:D2}", typeof(RectTransform));
            sl.transform.SetParent(col.transform, false);
            var slRT = sl.GetComponent<RectTransform>();
            slRT.anchorMin = new Vector2(0f,1f); slRT.anchorMax = new Vector2(1f,1f);
            slRT.pivot     = new Vector2(0.5f,1f);
            slRT.sizeDelta = new Vector2(0f, HOUR_HEIGHT);
            slRT.anchoredPosition = new Vector2(0f, -h * HOUR_HEIGHT);

            // 整時線（偶数時は少し濃く）
            AddHourLine(sl.transform, h % 2 == 0);

            var slImg = sl.AddComponent<Image>(); slImg.color = Color.clear;
            var btn   = sl.AddComponent<Button>(); btn.targetGraphic = slImg;
            int    capH = h;
            string capD = dateKey;
            btn.onClick.AddListener(() =>
            {
                if (eventModal != null) eventModal.OpenAddForm(capD, Refresh, $"{capH:D2}:00");
                else floatingWindow?.OpenAddForm(capD, null, $"{capH:D2}:00");
            });
        }

        // ── 24:00 終端線 ─────────────────────────────────────
        var endLine   = new GameObject("HourLine_24", typeof(RectTransform));
        endLine.transform.SetParent(col.transform, false);
        var endLineRT = endLine.GetComponent<RectTransform>();
        endLineRT.anchorMin = new Vector2(0f,1f); endLineRT.anchorMax = new Vector2(1f,1f);
        endLineRT.pivot     = new Vector2(0.5f,1f);
        endLineRT.sizeDelta = new Vector2(0f, _hairline);
        endLineRT.anchoredPosition = new Vector2(0f, -HOUR_COUNT * HOUR_HEIGHT);
        endLine.AddComponent<Image>().color = UITheme_FocusMode.BorderDivider;

        foreach (var (ev, lane, totalLanes) in AssignLanes(dayEvs))
            BuildEventBlock(col.transform, ev, colW, lane, totalLanes);

        // ── 縦境界線（最後に追加して TodayBG の上に描画）──────────────────
        var colBdrEnd   = new GameObject("ColBorder", typeof(RectTransform));
        colBdrEnd.transform.SetParent(col.transform, false);
        var colBdrEndRT = colBdrEnd.GetComponent<RectTransform>();
        colBdrEndRT.anchorMin = new Vector2(0f,0f); colBdrEndRT.anchorMax = new Vector2(0f,1f);
        colBdrEndRT.sizeDelta = new Vector2(_hairline, 0f); colBdrEndRT.anchoredPosition = Vector2.zero;
        var colBdrImg = colBdrEnd.AddComponent<Image>();
        colBdrImg.color = UITheme_FocusMode.BorderDivider;
        colBdrImg.raycastTarget = false;
    }

    /// <summary>整時線を1本だけ引く（15分・30分線なし）</summary>
    private void AddHourLine(Transform parent, bool isEven)
    {
        var go = new GameObject("HourLine", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f,1f); rt.anchorMax = new Vector2(1f,1f);
        rt.pivot     = new Vector2(0.5f,1f);
        rt.sizeDelta = new Vector2(0f, _hairline);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = UITheme_FocusMode.BorderDivider;
        img.raycastTarget = false;
    }

    /// <summary>
    /// 重なりクラスタ単位でレーン割当。時間的に連結した予定グループ内だけで
    /// totalLanes を数えるため、無関係な時間帯の予定はフル幅を維持する。
    /// </summary>
    private List<(ScheduleEvent ev, int lane, int totalLanes)>
        AssignLanes(List<ScheduleEvent> evs)
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
            float sH = ParseAndSnap(ev.time);
            if (sH < 0f) continue;
            float eH = string.IsNullOrEmpty(ev.endTime)
                ? sH + 1f
                : Mathf.Max(ParseAndSnap(ev.endTime), sH + 0.25f);

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

    private void BuildEventBlock(Transform parent, ScheduleEvent ev,
                                  float colW, int lane, int totalLanes)
    {
        float sH = ParseAndSnap(ev.time); if (sH < 0f) return;
        float eH = string.IsNullOrEmpty(ev.endTime)
            ? sH + 1f
            : Mathf.Max(ParseAndSnap(ev.endTime), sH + 0.25f);
        float bH = Mathf.Max((eH - sH) * HOUR_HEIGHT - 2f, 16f);
        // 右側に固定12pxのクリック余白（ガター）を確保し、同時間帯への追加を可能にする
        const float GUTTER = 12f;
        float lW = (colW - 2f - GUTTER) / Mathf.Max(1, totalLanes);
        float lX = 1f + lane * lW;
        var   tag = TagConfig.GetById(ev.tagId);

        var bk   = new GameObject("EventBlock", typeof(RectTransform));
        bk.transform.SetParent(parent, false);
        var bkRT = bk.GetComponent<RectTransform>();
        bkRT.anchorMin = new Vector2(0f,1f); bkRT.anchorMax = new Vector2(0f,1f);
        bkRT.pivot     = new Vector2(0f,1f);
        bkRT.sizeDelta = new Vector2(lW - 1f, bH);
        bkRT.anchoredPosition = new Vector2(lX, -sH * HOUR_HEIGHT - 1f);
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

        // 左ボーダー
        var bdr   = new GameObject("Border", typeof(RectTransform));
        bdr.transform.SetParent(bk.transform, false);
        var bdrRT = bdr.GetComponent<RectTransform>();
        bdrRT.anchorMin = new Vector2(0f,0f); bdrRT.anchorMax = new Vector2(0f,1f);
        bdrRT.pivot     = new Vector2(0f, 0.5f);
        bdrRT.sizeDelta = new Vector2(3f, -8f);        // 上下4pxインセット（角丸からはみ出さない）
        bdrRT.anchoredPosition = new Vector2(2f, 0f);  // 左から2px
        var bdrImg = bdr.AddComponent<Image>();
        bdrImg.color = tag != null ? tag.chipBorder : UITheme_FocusMode.AccentBlue;
        bdrImg.raycastTarget = false;
        if (cardSprite != null)
        {
            bdrImg.sprite = cardSprite;
            bdrImg.type   = Image.Type.Sliced;
            bdrImg.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * 1.5f); // 半径1.5px=ピル形
        }


        // タイトル
        var txGO = new GameObject("Title", typeof(RectTransform));
        txGO.transform.SetParent(bk.transform, false);
        var txRT = txGO.GetComponent<RectTransform>();
        txRT.anchorMin = Vector2.zero; txRT.anchorMax = Vector2.one;
        txRT.offsetMin = new Vector2(10f, 3f);
        txRT.offsetMax = new Vector2(-4f, -3f);
        var txTxt = txGO.AddComponent<TextMeshProUGUI>();
        txTxt.text       = ev.title;
        txTxt.fontSize   = UITheme_FocusMode.FontChipTitle;
        txTxt.color      = Color.white;
        txTxt.fontStyle  = FontStyles.Bold;
        txTxt.overflowMode = TextOverflowModes.Ellipsis;
        txTxt.lineSpacing = -70f; // 行間（Kotonoruは内部余白が大きいためメトリクス上は重なり気味が見た目の適正値）

        var btn = bk.AddComponent<Button>(); btn.targetGraphic = bkImg;
        var cap = ev;

        // ドラッグ移動・上下リサイズ（15分スナップ、ドロップで即保存）
        bk.AddComponent<EventBlockDragger>().Init(ev, HOUR_HEIGHT,
            () => { NotebookManager.Instance?.SaveAll(); Refresh(); },
            () => { if (eventModal != null) eventModal.OpenEditForm(cap, Refresh); },
            cardSprite);
    }

    private void BuildNowLine(string today, float colW, float totalH)
    {
        int idx = -1;
        for (int d = 0; d < 7; d++)
            if (NotebookManager.DateKey(weekStart.AddDays(d)) == today) { idx = d; break; }
        if (idx < 0) return;

        float nowH = DateTime.Now.Hour + DateTime.Now.Minute / 60f;
        var lnGO   = new GameObject("NowLine", typeof(RectTransform));
        lnGO.transform.SetParent(timelineParent, false);
        var lnRT   = lnGO.GetComponent<RectTransform>();
        lnRT.anchorMin = new Vector2(0f,1f); lnRT.anchorMax = new Vector2(0f,1f);
        lnRT.pivot     = new Vector2(0f,0.5f);
        lnRT.sizeDelta = new Vector2(colW, 2f);
        lnRT.anchoredPosition = new Vector2(TIME_COL_W + idx * colW, -nowH * HOUR_HEIGHT);
        lnGO.AddComponent<Image>().color = UITheme_FocusMode.AccentRed;

        var dot   = new GameObject("Dot", typeof(RectTransform));
        dot.transform.SetParent(lnGO.transform, false);
        var dotRT = dot.GetComponent<RectTransform>();
        dotRT.anchorMin = new Vector2(0f,0.5f); dotRT.anchorMax = new Vector2(0f,0.5f);
        dotRT.sizeDelta = new Vector2(8f,8f);
        dotRT.anchoredPosition = new Vector2(-4f,0f);
        dot.AddComponent<Image>().color = UITheme_FocusMode.AccentRed;
    }

    private void ScrollToNow()
    {
        if (timelineScroll == null) return;
        float totalH = HOUR_COUNT * HOUR_HEIGHT;
        float nowH   = Mathf.Clamp(DateTime.Now.Hour - 1f, 0f, HOUR_COUNT);
        timelineScroll.verticalNormalizedPosition =
            Mathf.Clamp01(1f - nowH * HOUR_HEIGHT / totalH);
    }

    private void OnDowCellClicked(int d)
    {
        var  date = weekStart.AddDays(d);
        var  key  = NotebookManager.DateKey(date);
        var  evs  = NotebookManager.Instance != null
            ? NotebookManager.Instance.GetEventsByDate(date)
            : new List<ScheduleEvent>();
        if (evs.Count > 0) floatingWindow?.OpenDayList(key, evs, Refresh);
        else               floatingWindow?.OpenAddForm(key, null);
    }

    // ── ユーティリティ ────────────────────────────────────────
    /// <summary>
    /// 時刻文字列を 15 分単位にスナップして時間（float）で返す。
    /// 0〜7分 → 切り捨て / 8〜14分 → 切り上げ
    /// </summary>
    private static float ParseAndSnap(string t)
    {
        if (string.IsNullOrEmpty(t)) return -1f;
        if (!TimeSpan.TryParse(t, out var ts)) return -1f;
        float mins    = ts.Hours * 60f + ts.Minutes;
        float snapped = Mathf.Round(mins / 15f) * 15f;
        return snapped / 60f;
    }

    private static DateTime GetWeekStart(DateTime d, int startDow)
    {
        int offset = ((int)d.DayOfWeek - startDow + 7) % 7;
        return d.Date.AddDays(-offset);
    }
}
