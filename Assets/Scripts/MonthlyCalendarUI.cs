using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 月カレンダーUI
/// - 各日セルに予定チップを最大3件表示、溢れたら「他N件」
/// - 日付クリックでフローティングウィンドウを開く
/// - NotebookManager.GetEventsByMonth() で ScheduleEvent を取得
/// </summary>
public class MonthlyCalendarUI : MonoBehaviour
{
    [Header("ナビゲーション")]
    [SerializeField] private Button prevMonthBtn;
    [SerializeField] private Button nextMonthBtn;
    [SerializeField] private TextMeshProUGUI monthLabel;
    [SerializeField] private Button weekStartSunBtn;
    [SerializeField] private Button weekStartMonBtn;

    [Header("グリッド")]
    [SerializeField] private Transform dowHeaderParent;   // 曜日ヘッダー行
    [SerializeField] private Transform calGridParent;     // 日付グリッド（GridLayoutGroup）

    [Header("Prefabs")]
    [SerializeField] private GameObject dayCellPrefab;    // 日付セルPrefab

    [Header("フローティングウィンドウ参照")]
    [SerializeField] private EventModal       eventModal;
    [SerializeField] private DayEventsPopup   dayEventsPopup;
    [SerializeField] private Sprite cardSprite; // 角丸カード用 9-slice（Rounded Filled 32px）

    // 内部状態
    private int currentYear;
    private int currentMonth;
    private int weekStartDow = 0;  // 0=日曜始まり, 1=月曜始まり
    private string selectedDate;

    // 祝日（2026年）
    private static readonly Dictionary<string, string> Holidays = new Dictionary<string, string>
    {
        {"2026-01-01","元日"}, {"2026-01-12","成人の日"},
        {"2026-02-11","建国記念の日"}, {"2026-02-23","天皇誕生日"},
        {"2026-03-20","春分の日"}, {"2026-04-29","昭和の日"},
        {"2026-05-03","憲法記念日"}, {"2026-05-04","みどりの日"},
        {"2026-05-05","こどもの日"}, {"2026-07-20","海の日"},
        {"2026-08-11","山の日"}, {"2026-09-21","敬老の日"},
        {"2026-09-23","秋分の日"}, {"2026-10-12","スポーツの日"},
        {"2026-11-03","文化の日"}, {"2026-11-23","勤労感謝の日"},
    };

    private static readonly string[] DowLabels = {"日","月","火","水","木","金","土"};
    private const int MAX_CHIPS = 3; // 1セルに表示する最大件数

    private int _lastDataVersion = -1;

    private void Update()
    {
        // データ変更検知 → 自動Refresh（他タブでの done 切替などを即反映）
        var nm = NotebookManager.Instance;
        if (nm != null && nm.DataVersion != _lastDataVersion) Refresh();
    }

    private void OnEnable()
    {
        UITheme_FocusMode.OnThemeChanged += Refresh;
        NavHeaderStyler.LayoutAndGear(transform.Find("Content/Header"), CategorySettingsController.Toggle);  // Phase1: ヘッダー部品規格
        var now = DateTime.Now;
        currentYear  = now.Year;
        currentMonth = now.Month;
        selectedDate = NotebookManager.DateKey(now);
        SetupButtons();
        UpdateGridCellSize();
        Refresh();
    }

    private void OnDisable()
    {
        UITheme_FocusMode.OnThemeChanged -= Refresh;
    }

    private void SetupButtons()
    {
        prevMonthBtn?.onClick.RemoveAllListeners();
        nextMonthBtn?.onClick.RemoveAllListeners();
        weekStartSunBtn?.onClick.RemoveAllListeners();
        weekStartMonBtn?.onClick.RemoveAllListeners();

        prevMonthBtn?.onClick.AddListener(() => { ChangeMonth(-1); });
        nextMonthBtn?.onClick.AddListener(() => { ChangeMonth(1);  });
        weekStartSunBtn?.onClick.AddListener(() => SetWeekStart(0));
        weekStartMonBtn?.onClick.AddListener(() => SetWeekStart(1));
    }

    private void ChangeMonth(int delta)
    {
        currentMonth += delta;
        if (currentMonth < 1)  { currentMonth = 12; currentYear--; }
        if (currentMonth > 12) { currentMonth = 1;  currentYear++; }
        Refresh();
    }

    private void SetWeekStart(int dow)
    {
        weekStartDow = dow;
        UpdateWeekBtn(weekStartSunBtn, dow == 0);
        UpdateWeekBtn(weekStartMonBtn, dow == 1);
        RefreshDowHeader();
        RefreshGrid();
    }

    private void UpdateWeekBtn(Button btn, bool isActive)
    {
        if (btn == null) return;
        var cb = btn.colors;
        cb.normalColor      = isActive ? new Color(0.06f,0.59f,0.99f,0.60f) : new Color(1f,1f,1f,0.08f);
        cb.highlightedColor = isActive ? new Color(0.06f,0.59f,0.99f,0.80f) : new Color(1f,1f,1f,0.16f);
        cb.pressedColor     = isActive ? new Color(0.06f,0.59f,0.99f,0.45f) : new Color(1f,1f,1f,0.05f);
        cb.selectedColor    = cb.normalColor;
        cb.colorMultiplier  = 1f;
        btn.colors = cb;
        var img = btn.GetComponent<Image>() ?? btn.targetGraphic as Image;
        if (img != null) img.color = Color.white;
    }

    public void Refresh()
    {
        _lastDataVersion = NotebookManager.Instance != null ? NotebookManager.Instance.DataVersion : -1;
        if (monthLabel != null)
            monthLabel.text = $"{currentYear} / {currentMonth:D2}";
        RefreshDowHeader();
        RefreshGrid();
    }

    // ─── 曜日ヘッダー ─────────────────────────────────────
    private void RefreshDowHeader()
    {
        if (dowHeaderParent == null) return;
        var labels = dowHeaderParent.GetComponentsInChildren<TextMeshProUGUI>();
        for (int i = 0; i < 7 && i < labels.Length; i++)
        {
            int dow = (i + weekStartDow) % 7;
            labels[i].text  = DowLabels[dow];
            labels[i].color = dow == 0
                ? UITheme_FocusMode.AccentRed
                : dow == 6
                    ? UITheme_FocusMode.AccentSatBlue
                    : UITheme_FocusMode.TextMuted;
        }
    }

    // ─── 日付グリッド ─────────────────────────────────────

    private void UpdateGridCellSize()
    {
        if (calGridParent == null) return;
        var grid = calGridParent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        var rt   = calGridParent.GetComponent<RectTransform>();
        if (grid == null || rt.rect.width <= 0) return;
        float w = (rt.rect.width  - grid.spacing.x * 6f) / 7f;
        float h = (rt.rect.height - grid.spacing.y * 4f) / 5f; // 最大5週表示
        grid.cellSize = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
    }

    private void RefreshGrid()
    {
        if (calGridParent == null || dayCellPrefab == null) return;

        // 子を全削除
        foreach (Transform child in calGridParent)
            Destroy(child.gameObject);

        // 今月のイベントを取得
        var events = NotebookManager.Instance != null
            ? NotebookManager.Instance.GetEventsByMonth(currentYear, currentMonth)
            : new List<ScheduleEvent>();

        var today = NotebookManager.DateKey(DateTime.Now);
        int firstDow   = (int)new DateTime(currentYear, currentMonth, 1).DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
        int offset      = ((firstDow - weekStartDow) + 7) % 7;

        // 空白セル
        for (int i = 0; i < offset; i++)
        {
            var empty = Instantiate(dayCellPrefab, calGridParent);
            empty.name = "Empty";
            SetCellEmpty(empty);
        }

        // 日付セル
        for (int d = 1; d <= daysInMonth; d++)
        {
            var dateKey = $"{currentYear}-{currentMonth:D2}-{d:D2}";
            var dt      = new DateTime(currentYear, currentMonth, d);
            var dow     = dt.DayOfWeek;
            bool isToday    = dateKey == today;
            bool isSelected = dateKey == selectedDate;
            bool isHoliday  = Holidays.ContainsKey(dateKey);
            bool isSun      = dow == DayOfWeek.Sunday;
            bool isSat      = dow == DayOfWeek.Saturday;

            var dayEvs = events.Where(e => e.date == dateKey)
                               .OrderBy(e => e.time ?? "99:99")
                               .ToList();

            var cell = Instantiate(dayCellPrefab, calGridParent);
            cell.name = dateKey;
            SetupDayCell(cell, d, dateKey, isToday, isSelected, isHoliday, isSun, isSat, dayEvs);
        }
            // 1フレーム待ってからチップ幅を修正（GLGレイアウト完了後）
        StartCoroutine(FixChipWidths());
    }

    private System.Collections.IEnumerator FixChipWidths()
    {
        yield return null;            // GLG がセルサイズを確定するまで待つ
        Canvas.ForceUpdateCanvases();

        if (calGridParent == null) yield break;
        var glg = calGridParent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (glg == null) yield break;

        float cellW = glg.cellSize.x;
        float chipW = cellW - 8f;
        const float DAY_NUM_H = 28f;
        const float CHIP_H    = 30f;
        const float SPACING   = 2f;

        foreach (Transform cell in calGridParent)
        {
            // ── DayNumber フォント設定 ───────────────────────────
            var dayNum = cell.Find("DayNumber");
            if (dayNum != null)
            {
                var dnTmp = dayNum.GetComponent<TextMeshProUGUI>();
                if (dnTmp != null)
                {
                    dnTmp.enableWordWrapping = false;
                    dnTmp.overflowMode       = TextOverflowModes.Overflow;
                    dnTmp.enableAutoSizing   = false;
                    dnTmp.fontSize           = UITheme_FocusMode.FontCalendarDate;
                }
            }

            // ── Chips: VLG・CSF を無効化して手動配置 ───────────
            var chips = cell.Find("Chips");
            if (chips == null) continue;

            // VLG と CSF を無効化（VLG が chip サイズを上書きしないように）
            var chipsVLG = chips.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (chipsVLG != null) chipsVLG.enabled = false;
            var chipsCSF = chips.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (chipsCSF != null) chipsCSF.enabled = false;

            // Chips コンテナを anchor=(0,1)-(0,1) で絶対配置
            var chipsRT = chips.GetComponent<RectTransform>();
            int chipCount = chips.childCount;
            float totalH  = chipCount > 0
                ? chipCount * CHIP_H + (chipCount - 1) * SPACING
                : 0f;

            chipsRT.anchorMin = new Vector2(0f, 1f);
            chipsRT.anchorMax = new Vector2(0f, 1f);
            chipsRT.pivot     = new Vector2(0f, 1f);
            chipsRT.offsetMin = new Vector2(4f, -(DAY_NUM_H + totalH));
            chipsRT.offsetMax = new Vector2(4f + chipW, -DAY_NUM_H);

            // 各チップを手動で積み上げ
            int idx = 0;
            foreach (Transform chip in chips)
            {
                var chipRT = chip.GetComponent<RectTransform>();
                if (chipRT == null) { idx++; continue; }

                // anchor = top-left of Chips
                chipRT.anchorMin = new Vector2(0f, 1f);
                chipRT.anchorMax = new Vector2(0f, 1f);
                chipRT.pivot     = new Vector2(0f, 1f);

                bool isMore = chip.name == "MoreLabel";
                float h = isMore ? 20f : CHIP_H;
                chipRT.sizeDelta = new Vector2(chipW, h);
                chipRT.anchoredPosition = new Vector2(0f, -(idx * (CHIP_H + SPACING)));
                idx++;
            }
        }

        // TMP を強制更新（Ellipsis が正確な幅で動くように）
        yield return null;
        foreach (Transform cell in calGridParent)
        {
            var chips = cell.Find("Chips");
            if (chips == null) continue;
            foreach (Transform chip in chips)
                chip.GetComponentInChildren<TextMeshProUGUI>()?.ForceMeshUpdate();
        }
    }

private void SetCellEmpty(GameObject cell)
    {
        CalendarDayIndicators.Clear(cell.transform);
        var img = cell.GetComponent<Image>();
        if (img != null) img.color = Color.clear;
        var btn = cell.GetComponent<Button>();
        if (btn != null) btn.interactable = false;
        foreach (var t in cell.GetComponentsInChildren<TextMeshProUGUI>())
            t.gameObject.SetActive(false);
    }

    private void SetupDayCell(
        GameObject cell, int day, string dateKey,
        bool isToday, bool isSelected, bool isHoliday,
        bool isSun, bool isSat,
        List<ScheduleEvent> dayEvs)
    {
        // 背景
        var img = cell.GetComponent<Image>();
        if (img != null)
        {
            if (isSelected)
                img.color = UITheme_FocusMode.AccentBlueFaint;
            else if (isToday)
                img.color = new Color(1f, 1f, 1f, 0.1f);
            else
                img.color = UITheme_FocusMode.DayCellBG;
        }

        // 日付番号
        var numText = cell.transform.Find("DayNumber")?.GetComponent<TextMeshProUGUI>();
        if (numText != null)
        {
            numText.text = day.ToString();
            if (isToday)
                numText.color = Color.white;
            else if (isHoliday || isSun)
                numText.color = UITheme_FocusMode.AccentRed;
            else if (isSat)
                numText.color = UITheme_FocusMode.AccentSatBlue;
            else
                numText.color = UITheme_FocusMode.TextBody;
        }

        // 予定チップエリア
        var chipsParent = cell.transform.Find("Chips");
        if (chipsParent != null)
        {
            // Chips: top-stretch アンカーに統一（FixChipWidthsと同じ）
            var chipsRT = chipsParent.GetComponent<RectTransform>();
            chipsRT.anchorMin = new Vector2(0f, 1f);
            chipsRT.anchorMax = new Vector2(1f, 1f);
            chipsRT.pivot     = new Vector2(0.5f, 1f);
            chipsRT.offsetMin = new Vector2(4f, -86f); // 左4px・高さの初期値（CSFが上書き）
            chipsRT.offsetMax = new Vector2(-4f, -30f); // DayNumber(28px)+2px 下

            // 既存チップをクリア
            foreach (Transform c in chipsParent) Destroy(c.gameObject);

            int showCount = Mathf.Min(dayEvs.Count, MAX_CHIPS);
            for (int i = 0; i < showCount; i++)
            {
                var ev  = dayEvs[i];
                var tag = TagConfig.GetById(ev.tagId);
                float _cw = 180f;
                var _glg = calGridParent?.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                if (_glg != null) _cw = _glg.cellSize.x - 16f;
                CreateChip(chipsParent, ev, tag, _cw);
            }

            // 「他N件」
            int overflow = dayEvs.Count - showCount;
            if (overflow > 0)
            {
                var moreGO  = new GameObject("MoreLabel");
                moreGO.transform.SetParent(chipsParent, false);
                var moreRT  = moreGO.AddComponent<RectTransform>();
                moreRT.sizeDelta = new Vector2(0f, 20f);
                var moreBG  = moreGO.AddComponent<UnityEngine.UI.Image>(); moreBG.color = Color.clear;
                var moreBtn = moreGO.AddComponent<UnityEngine.UI.Button>();
                var moreCB  = UnityEngine.UI.ColorBlock.defaultColorBlock;
                moreCB.highlightedColor = new Color(1f,1f,1f,0.1f);
                moreBtn.colors = moreCB; moreBtn.targetGraphic = moreBG;
                // TMP は子 GO に配置（Button と同 GO だと Awake 競合が発生するため）
                var moreTxtGO = new GameObject("Text");
                moreTxtGO.transform.SetParent(moreGO.transform, false);
                var moreTxtRT = moreTxtGO.AddComponent<RectTransform>();
                moreTxtRT.anchorMin = Vector2.zero; moreTxtRT.anchorMax = Vector2.one;
                moreTxtRT.offsetMin = moreTxtRT.offsetMax = Vector2.zero;
                var moreTxt = moreTxtGO.AddComponent<TextMeshProUGUI>();
                moreTxt.text      = $"他 {overflow} 件";
                moreTxt.fontSize  = UITheme_FocusMode.FontMoreLabel;
                moreTxt.color     = UITheme_FocusMode.AccentSatBlue;
                moreTxt.alignment = TextAlignmentOptions.MidlineLeft;
                // クリックでポップアップ
                string _capDate = dateKey;
                var _capEvs = new System.Collections.Generic.List<ScheduleEvent>(dayEvs);
                var _capCell = cell;
                moreBtn.onClick.AddListener(() => {
                    if (dayEventsPopup == null) return;
                    var _cRT = _capCell.GetComponent<RectTransform>();
                    var _corners = new Vector3[4]; _cRT.GetWorldCorners(_corners);
                    var _sp = new Vector2((_corners[0].x+_corners[2].x)*0.5f, (_corners[0].y+_corners[2].y)*0.5f);
                    dayEventsPopup.Show(_capDate, _capEvs, ev => OpenEventDetail(ev), _sp);
                });
                var moreLE  = moreGO.AddComponent<LayoutElement>();
                moreLE.preferredHeight = 13f;
            }
        }

        // Todo/Routine インジケータ（セル右上）
        if (System.DateTime.TryParseExact(dateKey, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var cellDate))
            CalendarDayIndicators.Build(cell.transform, cellDate);

        // クリック
        var btn = cell.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            string captured = dateKey;
            var capturedEvs = new List<ScheduleEvent>(dayEvs);
            btn.onClick.AddListener(() => OnDayCellClicked(captured, capturedEvs));
        }
    }

    private void CreateChip(Transform parent, ScheduleEvent ev, TagDefinition tag, float chipWidth = 180f)
    {
        var chipGO  = new GameObject("Chip");
        chipGO.transform.SetParent(parent, false);
        var chipImg = chipGO.AddComponent<Image>();
        var baseC = tag != null ? tag.chipBG : UITheme_FocusMode.AccentBlueFaint;
        chipImg.color = UITheme_FocusMode.CardBG(baseC); // Weekly/Daily のカードと同色
        if (cardSprite != null)
        {
            chipImg.sprite = cardSprite; chipImg.type = Image.Type.Sliced;
            chipImg.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * 4f); // 角丸4px
        }

        var chipRT = chipGO.GetComponent<RectTransform>();
        // 幅は VLG が自動設定（sizeDelta.x は不要）

        var chipLE = chipGO.AddComponent<LayoutElement>();
        chipLE.preferredHeight = 30f;

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
            stImg.pixelsPerUnitMultiplier = cardSprite.border.x * 100f / (cardSprite.pixelsPerUnit * 1.5f); // ピル形
        }

        // テキスト
        var txtGO  = new GameObject("Text");
        txtGO.transform.SetParent(chipGO.transform, false);
        var txtRT  = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(10f, 2f);
        txtRT.offsetMax = new Vector2(-4f, -2f);
        var txt    = txtGO.AddComponent<TextMeshProUGUI>();
        txt.fontSize  = UITheme_FocusMode.FontChipTitle;
        txt.text      = UITextUtil.EllipsizeOneLine(txt, ev.title, chipWidth - 14f); // 左10+右4を除いた幅で「…」省略
        txt.color     = Color.white;
        txt.alignment = TextAlignmentOptions.MidlineLeft;  // 縦中央
        txt.enableWordWrapping = false;
        txt.overflowMode    = TextOverflowModes.Overflow;
        if (chipGO.GetComponent<UnityEngine.UI.RectMask2D>() == null)
            chipGO.AddComponent<UnityEngine.UI.RectMask2D>();

        // クリック
        var btn = chipGO.AddComponent<Button>();
        btn.targetGraphic = chipImg;
        var capturedEv = ev;
        btn.onClick.AddListener(() => OpenEventDetail(capturedEv));
    }

    // ─── クリックハンドラ ─────────────────────────────────
    private void OnDayCellClicked(string dateKey, List<ScheduleEvent> dayEvs)
    {
        selectedDate = dateKey;
        RefreshGrid();

        if (eventModal == null) return;

        if (dayEvs.Count == 0)
        {
            // 予定なし → 追加フォームを直接開く
            eventModal.OpenAddForm(dateKey, () => Refresh());
        }
        else
        {
            // 予定あり → 日の予定一覧を表示
            eventModal.OpenAddForm(dateKey, () => Refresh());
        }
    }

    private void OpenEventDetail(ScheduleEvent ev)
    {
        if (eventModal == null) return;
        eventModal.OpenEditForm(ev, () => Refresh());
    }
}
