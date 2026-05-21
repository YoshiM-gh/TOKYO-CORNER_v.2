using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 週カレンダーUI
/// - 7列グリッド＋縦スクロール（Google Cal週ビュー形式）
/// - 予定ブロッククリック → フローティングウィンドウ（詳細）
/// - 空き時間クリック → フローティングウィンドウ（追加、日付・時間自動入力）
/// </summary>
public class WeeklyCalendarUI : MonoBehaviour
{
    [Header("ナビゲーション")]
    [SerializeField] private Button prevWeekBtn;
    [SerializeField] private Button nextWeekBtn;
    [SerializeField] private TextMeshProUGUI weekLabel;

    [Header("グリッド")]
    [SerializeField] private Transform headerRow;       // 曜日ヘッダー行（7列）
    [SerializeField] private ScrollRect scrollRect;     // 縦スクロール
    [SerializeField] private Transform timelineParent;  // 時間軸グリッド親

    [Header("フローティングウィンドウ")]
    [SerializeField] private FloatingWindowController floatingWindow;

    private static readonly string[] DowLabels = { "日", "月", "火", "水", "木", "金", "土" };
    private const int START_HOUR = 5;
    private const int END_HOUR   = 23;
    private const float CELL_HEIGHT = 44f;

    private DateTime weekStart; // その週の日曜日

    private void OnEnable()
    {
        weekStart = GetWeekStart(DateTime.Now);
        SetupButtons();
        Refresh();
    }

    private void SetupButtons()
    {
        prevWeekBtn?.onClick.RemoveAllListeners();
        nextWeekBtn?.onClick.RemoveAllListeners();
        prevWeekBtn?.onClick.AddListener(() => { weekStart = weekStart.AddDays(-7); Refresh(); });
        nextWeekBtn?.onClick.AddListener(() => { weekStart = weekStart.AddDays(7);  Refresh(); });
    }

    private static DateTime GetWeekStart(DateTime d)
        => d.AddDays(-(int)d.DayOfWeek); // 日曜始まり

    public void Refresh()
    {
        var weekEnd = weekStart.AddDays(6);
        if (weekLabel != null)
            weekLabel.text = $"{weekStart.Year}年{weekStart.Month}月 {weekStart.Day}日〜{weekEnd.Day}日";

        BuildHeader();
        BuildTimeline();
    }

    // ─── 曜日ヘッダー ─────────────────────────────────────
    private void BuildHeader()
    {
        if (headerRow == null) return;
        var cells = headerRow.GetComponentsInChildren<WeekHeaderCell>(true);
        for (int i = 0; i < 7 && i < cells.Length; i++)
        {
            var date = weekStart.AddDays(i);
            var dow  = (int)date.DayOfWeek;
            cells[i].Set(
                DowLabels[dow],
                date.Day.ToString(),
                date.Date == DateTime.Now.Date,
                dow == 0 || IsHoliday(date),
                dow == 6
            );
            int captured = i;
            cells[i].SetClickAction(() => OnHeaderClicked(captured));
        }
    }

    // ─── タイムライングリッド ─────────────────────────────
private void BuildTimeline()
    {
        if (timelineParent == null) return;
        foreach (Transform child in timelineParent)
            Destroy(child.gameObject);

        var events = new List<ScheduleEvent>();
        if (NotebookManager.Instance != null)
            events = NotebookManager.Instance.GetEventsByWeek(weekStart);

        var today = NotebookManager.DateKey(DateTime.Now);

        for (int h = START_HOUR; h <= END_HOUR; h++)
        {
            var rowGO = new GameObject($"Row_{h:D2}", typeof(RectTransform));
            rowGO.transform.SetParent(timelineParent, false);
            var rowRT = rowGO.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0f, CELL_HEIGHT);
            var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowHLG.childForceExpandWidth  = true;
            rowHLG.childForceExpandHeight = true;
            rowHLG.spacing = 1f;

            var lblGO = new GameObject("TimeLabel", typeof(RectTransform));
            lblGO.transform.SetParent(rowGO.transform, false);
            var lblTxt = lblGO.AddComponent<TextMeshProUGUI>();
            lblTxt.text      = $"{h:D2}";
            lblTxt.fontSize  = UITheme_FocusMode.FontMicro;
            lblTxt.color     = UITheme_FocusMode.TextDisabled;
            lblTxt.alignment = TextAlignmentOptions.TopRight;
            var lblLE = lblGO.AddComponent<LayoutElement>();
            lblLE.preferredWidth = 28f;
            lblLE.flexibleWidth  = 0f;

            for (int d = 0; d < 7; d++)
            {
                var date    = weekStart.AddDays(d);
                var dateKey = NotebookManager.DateKey(date);
                var isToday = dateKey == today;
                var cellEvs = events.Where(e => e.date == dateKey && e.time == $"{h:D2}:00").ToList();

                var cellGO  = new GameObject($"Cell_{d}", typeof(RectTransform));
                cellGO.transform.SetParent(rowGO.transform, false);
                var cellImg = cellGO.AddComponent<Image>();
                cellImg.color = isToday
                    ? UITheme_FocusMode.AccentBlueFaint
                    : new Color(1f, 1f, 1f, h % 2 == 0 ? 0.02f : 0.01f);
                cellImg.raycastTarget = true;

                if (cellEvs.Count > 0)
                {
                    var cellVLG = cellGO.AddComponent<VerticalLayoutGroup>();
                    cellVLG.padding = new RectOffset(2, 2, 2, 2);
                    cellVLG.spacing = 1f;
                    cellVLG.childForceExpandWidth  = true;
                    cellVLG.childForceExpandHeight = false;
                    foreach (var ev in cellEvs)
                        BuildEventBlock(cellGO.transform, ev, dateKey);
                }
                else
                {
                    var btn = cellGO.AddComponent<Button>();
                    btn.targetGraphic = cellImg;
                    string capturedDate = dateKey;
                    int    capturedHour = h;
                    btn.onClick.AddListener(() =>
                        floatingWindow?.OpenAddForm(capturedDate, null, $"{capturedHour:D2}:00"));
                }
            }
        }
    }

    private void BuildEventBlock(Transform parent, ScheduleEvent ev, string dateKey)
    {
        var tag = TagConfig.GetById(ev.tagId);

        var blockGO  = new GameObject("EventBlock", typeof(RectTransform));
        blockGO.transform.SetParent(parent, false);
        var blockImg = blockGO.AddComponent<Image>();
        blockImg.color = tag != null ? tag.chipBG : UITheme_FocusMode.AccentBlueFaint;

        // 左ボーダー
        var borderGO = new GameObject("Border", typeof(RectTransform));
        borderGO.transform.SetParent(blockGO.transform, false);
        var borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(0f, 0f);
        borderRT.anchorMax = new Vector2(0f, 1f);
        borderRT.sizeDelta = new Vector2(2f, 0f);
        borderRT.anchoredPosition = Vector2.zero;
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color = tag != null ? tag.chipBorder : UITheme_FocusMode.AccentBlue;

        // テキスト
        var txtGO  = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(blockGO.transform, false);
        var txtRT  = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0f, 0f);
        txtRT.anchorMax = new Vector2(1f, 1f);
        txtRT.offsetMin = new Vector2(5f, 1f);
        txtRT.offsetMax = new Vector2(-2f, -1f);
        var txt    = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text   = ev.title;
        txt.fontSize = UITheme_FocusMode.FontMicro;
        txt.color  = Color.white;
        txt.overflowMode = TextOverflowModes.Ellipsis;

        var btn = blockGO.AddComponent<Button>();
        btn.targetGraphic = blockImg;
        var captured = ev;
        btn.onClick.AddListener(() =>
            floatingWindow?.OpenEventDetail(captured, Refresh));

        var blockLE = blockGO.AddComponent<LayoutElement>();
        blockLE.preferredHeight = CELL_HEIGHT - 6f;
    }

    // ─── ヘッダークリック（その日の追加） ────────────────
    private void OnHeaderClicked(int dayIndex)
    {
        var date    = weekStart.AddDays(dayIndex);
        var dateKey = NotebookManager.DateKey(date);
        var evs     = NotebookManager.Instance != null
            ? NotebookManager.Instance.GetEventsByDate(date)
            : new List<ScheduleEvent>();

        if (evs.Count > 0)
            floatingWindow?.OpenDayList(dateKey, evs, Refresh);
        else
            floatingWindow?.OpenAddForm(dateKey, null);
    }

    // ─── 祝日判定（簡易） ────────────────────────────────
    private static readonly HashSet<string> HolidayKeys = new HashSet<string>
    {
        "2026-01-01","2026-01-12","2026-02-11","2026-02-23","2026-03-20",
        "2026-04-29","2026-05-03","2026-05-04","2026-05-05","2026-07-20",
        "2026-08-11","2026-09-21","2026-09-23","2026-10-12","2026-11-03","2026-11-23",
    };
    private static bool IsHoliday(DateTime d)
        => HolidayKeys.Contains(NotebookManager.DateKey(d));
}

/// <summary>週ヘッダーセル（曜日・日付・今日強調）</summary>
public class WeekHeaderCell : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI dowText;
    [SerializeField] private TextMeshProUGUI dateText;
    private Button btn;

    private void Awake() { btn = GetComponent<Button>(); }

    public void Set(string dow, string date, bool isToday, bool isSunOrHol, bool isSat)
    {
        if (dowText  != null)
        {
            dowText.text  = dow;
            dowText.color = isSunOrHol
                ? UITheme_FocusMode.AccentRed
                : isSat
                    ? UITheme_FocusMode.AccentSatBlue
                    : UITheme_FocusMode.TextMuted;
        }
        if (dateText != null)
        {
            dateText.text  = date;
            dateText.color = isToday
                ? Color.white
                : isSunOrHol
                    ? UITheme_FocusMode.AccentRed
                    : isSat
                        ? UITheme_FocusMode.AccentSatBlue
                        : UITheme_FocusMode.TextBody;
            dateText.fontStyle = isToday ? FontStyles.Bold : FontStyles.Normal;
        }
        var img = GetComponent<Image>();
        if (img != null)
            img.color = isToday
                ? UITheme_FocusMode.AccentBlueFaint
                : Color.clear;
    }

    public void SetClickAction(UnityEngine.Events.UnityAction action)
    {
        if (btn == null) btn = GetComponent<Button>();
        btn?.onClick.RemoveAllListeners();
        btn?.onClick.AddListener(action);
    }
}
