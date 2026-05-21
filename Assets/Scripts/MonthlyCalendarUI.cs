using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 月カレンダーUI + 選択日の予定詳細
/// </summary>
public class MonthlyCalendarUI : MonoBehaviour
{
    [Header("左カラム - ナビ")]
    [SerializeField] private Button prevMonthBtn;
    [SerializeField] private Button nextMonthBtn;
    [SerializeField] private TextMeshProUGUI monthLabel;

    [Header("左カラム - グリッド")]
    [SerializeField] private Transform dayGridParent;
    [SerializeField] private GameObject dayButtonPrefab;

    [Header("右カラム - 詳細")]
    [SerializeField] private TextMeshProUGUI selectedDateLabel;
    [SerializeField] private Transform eventListParent;
    [SerializeField] private GameObject eventItemPrefab;
    [SerializeField] private Button addEventBtn;

    [Header("右カラム - 予定詳細")]
    [SerializeField] private GameObject eventDetailPanel;
    [SerializeField] private TMP_InputField eventTitleInput;
    [SerializeField] private TMP_InputField eventMemoInput;
    [SerializeField] private Button saveEventBtn;
    [SerializeField] private Button deleteEventBtn;
    [SerializeField] private Button closeDetailBtn;

    private DateTime currentMonth;
    private DateTime selectedDate;
    private string editingEventId = null;

    private void OnEnable()
    {
        currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        selectedDate = DateTime.Now.Date;
        SetupButtons();
        Refresh();
    }

    private void SetupButtons()
    {
        if (prevMonthBtn) prevMonthBtn.onClick.AddListener(() => { currentMonth = currentMonth.AddMonths(-1); Refresh(); });
        if (nextMonthBtn) nextMonthBtn.onClick.AddListener(() => { currentMonth = currentMonth.AddMonths(1); Refresh(); });
        if (addEventBtn)  addEventBtn.onClick.AddListener(() => OpenEventDetail(null));
        if (saveEventBtn) saveEventBtn.onClick.AddListener(SaveEvent);
        if (deleteEventBtn) deleteEventBtn.onClick.AddListener(DeleteEvent);
        if (closeDetailBtn) closeDetailBtn.onClick.AddListener(() => eventDetailPanel?.SetActive(false));
    }

    public void Refresh()
    {
        RefreshCalendarGrid();
        RefreshEventList();
    }

    // ─── カレンダーグリッド ───────────────────────────────
    private void RefreshCalendarGrid()
    {
        if (monthLabel) monthLabel.text = currentMonth.ToString("yyyy / MM");

        foreach (Transform child in dayGridParent)
            Destroy(child.gameObject);

        // 月の最初の曜日（日曜始まり）
        int firstDow = (int)new DateTime(currentMonth.Year, currentMonth.Month, 1).DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month);

        // 空白セル
        for (int i = 0; i < firstDow; i++)
            CreateEmptyCell();

        // 日付セル
        for (int d = 1; d <= daysInMonth; d++)
        {
            var date = new DateTime(currentMonth.Year, currentMonth.Month, d);
            CreateDayButton(date);
        }
    }

    private void CreateEmptyCell()
    {
        if (dayButtonPrefab == null) return;
        var go = Instantiate(dayButtonPrefab, dayGridParent);
        go.GetComponentInChildren<TextMeshProUGUI>()?.gameObject.SetActive(false);
        var btn = go.GetComponent<Button>();
        if (btn) btn.interactable = false;
        var img = go.GetComponent<Image>();
        if (img) img.color = new Color(0, 0, 0, 0);
    }

    private void CreateDayButton(DateTime date)
    {
        if (dayButtonPrefab == null) return;
        var go = Instantiate(dayButtonPrefab, dayGridParent);
        var lbl = go.GetComponentInChildren<TextMeshProUGUI>();
        if (lbl) lbl.text = date.Day.ToString();

        // 今日ハイライト
        bool isToday    = date.Date == DateTime.Now.Date;
        bool isSelected = date.Date == selectedDate.Date;
        var img = go.GetComponent<Image>();
        if (img)
        {
            if (isSelected)     img.color = new Color(0.3f, 0.5f, 1.0f, 0.8f);
            else if (isToday)   img.color = new Color(1.0f, 1.0f, 1.0f, 0.15f);
            else                img.color = new Color(1.0f, 1.0f, 1.0f, 0.04f);
        }

        // 予定ありドット
        var entries = NotebookManager.Instance?.GetMonthlyEntry(date);
        var dotObj = go.transform.Find("Dot");
        if (dotObj) dotObj.gameObject.SetActive(entries != null && !string.IsNullOrEmpty(entries.text));

        // クリック
        var capturedDate = date;
        go.GetComponent<Button>()?.onClick.AddListener(() => SelectDate(capturedDate));
    }

    // ─── 日付選択 ─────────────────────────────────────────
    private void SelectDate(DateTime date)
    {
        selectedDate = date;
        RefreshCalendarGrid();
        RefreshEventList();
        if (eventDetailPanel) eventDetailPanel.SetActive(false);
    }

    // ─── 予定リスト ───────────────────────────────────────
    private void RefreshEventList()
    {
        if (selectedDateLabel)
            selectedDateLabel.text = selectedDate.ToString("yyyy年M月d日（ddd）");

        foreach (Transform child in eventListParent)
            Destroy(child.gameObject);

        var entry = NotebookManager.Instance?.GetMonthlyEntry(selectedDate);
        if (entry != null && !string.IsNullOrEmpty(entry.text))
        {
            var go = Instantiate(eventItemPrefab, eventListParent);
            var lbl = go.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl) lbl.text = entry.text;
            go.GetComponent<Button>()?.onClick.AddListener(() => OpenEventDetail(entry.text));
        }
    }

    // ─── 予定詳細パネル ───────────────────────────────────
    private void OpenEventDetail(string existingText)
    {
        if (eventDetailPanel == null) return;
        eventDetailPanel.SetActive(true);
        editingEventId = selectedDate.ToString("yyyy-MM-dd");
        if (eventTitleInput) eventTitleInput.text = existingText ?? "";
        if (eventMemoInput)  eventMemoInput.text  = "";
    }

    private void SaveEvent()
    {
        var title = eventTitleInput?.text ?? "";
        NotebookManager.Instance?.SetMonthlyEntry(selectedDate, title);
        eventDetailPanel?.SetActive(false);
        Refresh();
    }

    private void DeleteEvent()
    {
        NotebookManager.Instance?.SetMonthlyEntry(selectedDate, "");
        eventDetailPanel?.SetActive(false);
        Refresh();
    }
}
