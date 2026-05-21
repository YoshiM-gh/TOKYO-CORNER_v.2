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
    [SerializeField] private FloatingWindowController floatingWindow;

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

    private void OnEnable()
    {
        var now = DateTime.Now;
        currentYear  = now.Year;
        currentMonth = now.Month;
        selectedDate = NotebookManager.DateKey(now);
        SetupButtons();
        Refresh();
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
        RefreshDowHeader();
        RefreshGrid();
    }

    public void Refresh()
    {
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
    }

    private void SetCellEmpty(GameObject cell)
    {
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
            // 既存チップをクリア
            foreach (Transform c in chipsParent) Destroy(c.gameObject);

            int showCount = Mathf.Min(dayEvs.Count, MAX_CHIPS);
            for (int i = 0; i < showCount; i++)
            {
                var ev  = dayEvs[i];
                var tag = TagConfig.GetById(ev.tagId);
                CreateChip(chipsParent, ev, tag);
            }

            // 「他N件」
            int overflow = dayEvs.Count - showCount;
            if (overflow > 0)
            {
                var moreGO  = new GameObject("MoreLabel");
                moreGO.transform.SetParent(chipsParent, false);
                var moreRT  = moreGO.AddComponent<RectTransform>();
                moreRT.sizeDelta = new Vector2(0f, 13f);
                var moreTxt = moreGO.AddComponent<TextMeshProUGUI>();
                moreTxt.text      = $"他 {overflow} 件";
                moreTxt.fontSize  = UITheme_FocusMode.FontMicro;
                moreTxt.color     = UITheme_FocusMode.TextDisabled;
                var moreLE  = moreGO.AddComponent<LayoutElement>();
                moreLE.preferredHeight = 13f;
            }
        }

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

    private void CreateChip(Transform parent, ScheduleEvent ev, TagDefinition tag)
    {
        var chipGO  = new GameObject("Chip");
        chipGO.transform.SetParent(parent, false);
        var chipImg = chipGO.AddComponent<Image>();
        chipImg.color = tag != null ? tag.chipBG : UITheme_FocusMode.AccentBlueFaint;

        var chipRT = chipGO.GetComponent<RectTransform>();
        chipRT.sizeDelta = new Vector2(0f, 13f);

        var chipLE = chipGO.AddComponent<LayoutElement>();
        chipLE.preferredHeight = 13f;

        // テキスト
        var txtGO  = new GameObject("Text");
        txtGO.transform.SetParent(chipGO.transform, false);
        var txtRT  = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(3f, 0f);
        txtRT.offsetMax = Vector2.zero;
        var txt    = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text      = (ev.time != null ? ev.time + " " : "") + ev.title;
        txt.fontSize  = UITheme_FocusMode.FontMicro;
        txt.color     = Color.white;
        txt.overflowMode = TextOverflowModes.Ellipsis;

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

        if (floatingWindow == null) return;

        if (dayEvs.Count == 0)
        {
            // 予定なし → 追加フォームを直接開く
            floatingWindow.OpenAddForm(dateKey, null);
        }
        else
        {
            // 予定あり → 日の予定一覧を表示
            floatingWindow.OpenDayList(dateKey, dayEvs, () => Refresh());
        }
    }

    private void OpenEventDetail(ScheduleEvent ev)
    {
        if (floatingWindow == null) return;
        floatingWindow.OpenEventDetail(ev, () => Refresh());
    }
}
