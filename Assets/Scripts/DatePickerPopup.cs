using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DatePickerPopup : MonoBehaviour
{
    public TextMeshProUGUI monthYearLabel;
    public Button prevMonthBtn, nextMonthBtn;
    public Transform dowHeaderParent, dayGridParent;

    private int _year, _month;
    private string _selectedDate;
    private Action<string> _onSelected;
    private int _weekStartDow = 1;
    private static readonly string[] DOW = {"日","月","火","水","木","金","土"};

    public void Open(string currentDate, int weekStartDow, Action<string> callback)
    {
        _onSelected = callback; _weekStartDow = weekStartDow;
        if (!string.IsNullOrEmpty(currentDate) &&
            DateTime.TryParseExact(currentDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
        { _year = dt.Year; _month = dt.Month; _selectedDate = currentDate; }
        else { var n = DateTime.Now; _year = n.Year; _month = n.Month; _selectedDate = null; }
        gameObject.SetActive(true); Refresh();
    }

    public void Close() => gameObject.SetActive(false);


    private void OnEnable()
    {
        // DayGrid の幅が確定してからセルサイズを計算
        StartCoroutine(AdjustCellSize());
    }

    private System.Collections.IEnumerator AdjustCellSize()
    {
        yield return null; // 1フレーム待つ
        if (!dayGridParent) yield break;
        var glg = dayGridParent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        var rt  = dayGridParent.GetComponent<RectTransform>();
        if (glg == null || rt == null) yield break;
        float w = (rt.rect.width - glg.spacing.x * 6f) / 7f;
        glg.cellSize = new Vector2(Mathf.Max(20f, w), glg.cellSize.y);
    }
    private void Awake()
    {
        prevMonthBtn?.onClick.AddListener(() => Shift(-1));
        nextMonthBtn?.onClick.AddListener(() => Shift(+1));
    }

    private void Shift(int d)
    {
        _month += d;
        if (_month < 1) { _month = 12; _year--; }
        if (_month > 12) { _month = 1; _year++; }
        Refresh();
    }

    private void Refresh()
    {
        if (monthYearLabel) monthYearLabel.text = $"{_year}年 {_month:D2}月";
        RefreshDow(); RefreshGrid();
    }

    private void RefreshDow()
    {
        if (!dowHeaderParent) return;
        var labels = dowHeaderParent.GetComponentsInChildren<TextMeshProUGUI>();
        for (int i = 0; i < 7 && i < labels.Length; i++)
        {
            int d = (i + _weekStartDow) % 7;
            labels[i].text = DOW[d];
            labels[i].color = d == 0 ? UITheme_FocusMode.AccentRed
                : d == 6 ? UITheme_FocusMode.AccentSatBlue : UITheme_FocusMode.TextMuted;
        }
    }

    private void RefreshGrid()
    {
        if (!dayGridParent) return;
        foreach (Transform c in dayGridParent) Destroy(c.gameObject);
        int firstDow = (int)new DateTime(_year, _month, 1).DayOfWeek;
        int days = DateTime.DaysInMonth(_year, _month);
        int offset = ((firstDow - _weekStartDow) + 7) % 7;
        string today = $"{DateTime.Now.Year}-{DateTime.Now.Month:D2}-{DateTime.Now.Day:D2}";
        for (int i = 0; i < offset; i++) CreateEmpty();
        for (int d = 1; d <= days; d++) CreateDayCell(d, today);
    }

    private void CreateEmpty()
    {
        var go = new GameObject("_"); go.transform.SetParent(dayGridParent, false);
        var img = go.AddComponent<Image>(); img.color = Color.clear;
    }

    private void CreateDayCell(int day, string today)
    {
        string key = $"{_year}-{_month:D2}-{day:D2}";
        int dow = (int)new DateTime(_year, _month, day).DayOfWeek;
        bool isSel = key == _selectedDate, isToday = key == today;
        var go = new GameObject(key); go.transform.SetParent(dayGridParent, false);
        var img = go.AddComponent<Image>();
        img.color = isSel ? UITheme_FocusMode.AccentBlueFaint
            : isToday ? new Color(1f,1f,1f,0.12f) : new Color(1f,1f,1f,0.04f);
        var numGO = new GameObject("Num"); numGO.transform.SetParent(go.transform, false);
        var rt = numGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = numGO.AddComponent<TextMeshProUGUI>();
        tmp.text = day.ToString(); tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = (isSel || isToday) ? Color.white
            : dow == 0 ? UITheme_FocusMode.AccentRed
            : dow == 6 ? UITheme_FocusMode.AccentSatBlue : UITheme_FocusMode.TextBody;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cb = UnityEngine.UI.ColorBlock.defaultColorBlock;
        cb.normalColor = img.color; cb.highlightedColor = new Color(1f,1f,1f,0.2f);
        btn.colors = cb;
        string cap = key;
        btn.onClick.AddListener(() => { _selectedDate = cap; _onSelected?.Invoke(cap); Close(); });
    }
}