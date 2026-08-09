using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ルーチンタブ右ペイン(詳細フォーム)
/// - 繰り返し: セグメント3択(毎日/毎週/N日ごと)+曜日チップ+ステッパー
/// - 終了日: 日付Row流用(クリアで「終了日なし」)
/// - 時刻・優先度・メモ・自動保存はTodoDetailUIと同挙動
/// </summary>
public class RoutineDetailUI : MonoBehaviour
{
    [SerializeField] private GameObject emptyState;
    [SerializeField] private GameObject form;

    [Header("タイトル/メモ")]
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField memoInput;

    [Header("繰り返し")]
    [SerializeField] private Button dailyBtn;
    [SerializeField] private Button weeklyBtn;
    [SerializeField] private Button intervalBtn;
    [SerializeField] private Transform weekdayRow;     // 子: W0〜W6
    [SerializeField] private GameObject intervalRow;
    [SerializeField] private Button monthlyBtn;          // 「毎月」セグメント
    [SerializeField] private GameObject monthlyRow;      // 毎月の設定行
    [SerializeField] private TMP_InputField monthlyDayInput;
    [SerializeField] private Button monthlyDayDecBtn;
    [SerializeField] private Button monthlyDayIncBtn;
    [SerializeField] private Toggle monthlyLastDayToggle; // 「月末」
    [SerializeField] private TMP_InputField intervalInput;
    [SerializeField] private Button intervalDecBtn;
    [SerializeField] private Button intervalIncBtn;

    [Header("終了日")]
    [SerializeField] private Button endDateTriggerBtn;
    [SerializeField] private TextMeshProUGUI endDateDisplayText;
    [SerializeField] private Button endDateClearBtn;
    [SerializeField] private TextMeshProUGUI endDateClearBtnLabel;
    [SerializeField] private DatePickerPopup datePicker;

    [Header("時刻")]
    [SerializeField] private TMP_InputField hourInput;
    [SerializeField] private TMP_InputField minInput;
    [SerializeField] private Button hourDecBtn;
    [SerializeField] private Button hourIncBtn;
    [SerializeField] private Button minDecBtn;
    [SerializeField] private Button minIncBtn;
    [SerializeField] private Button timeClearBtn;
    [SerializeField] private TextMeshProUGUI timeClearBtnLabel;

    [Header("その他")]
    [SerializeField] private Toggle priorityToggle;
    [SerializeField] private Button deleteButton;

    private const int MIN_STEP = 15;
    private RoutineItem _target;
    private string _repeatType = "daily";
    private readonly HashSet<int> _weekdays = new HashSet<int>();
    private int _intervalDays = 2;
    private int _monthlyDay = 1;
    private bool _monthlyLastDay = false;
    private string _endDate;
    private int _hour = -1, _minute = 0;
    private bool _loading;

    private readonly Button[] _chips = new Button[7];
    private readonly Image[] _chipImgs = new Image[7];
    private readonly TextMeshProUGUI[] _chipLabels = new TextMeshProUGUI[7];

    public event Action OnChanged;
    public event Action<string> OnDeleted;

    public string CurrentId => _target?.id;

    private void Start()
    {
        dailyBtn?.onClick.AddListener(() => SetRepeatType("daily"));
        weeklyBtn?.onClick.AddListener(() => SetRepeatType("weekly"));
        intervalBtn?.onClick.AddListener(() => SetRepeatType("interval"));
        monthlyBtn?.onClick.AddListener(() => SetRepeatType("monthly"));

        if (weekdayRow != null)
        {
            for (int i = 0; i < 7; i++)
            {
                var t = weekdayRow.Find("W" + i);
                if (t == null) continue;
                _chips[i] = t.GetComponent<Button>();
                _chipImgs[i] = t.GetComponent<Image>();
                _chipLabels[i] = t.GetComponentInChildren<TextMeshProUGUI>(true);
                int idx = i;
                _chips[i]?.onClick.AddListener(() => ToggleWeekday(idx));
            }
        }

        intervalDecBtn?.onClick.AddListener(() => ShiftInterval(-1));
        intervalIncBtn?.onClick.AddListener(() => ShiftInterval(+1));
        monthlyDayDecBtn?.onClick.AddListener(() => ShiftMonthlyDay(-1));
        monthlyDayIncBtn?.onClick.AddListener(() => ShiftMonthlyDay(+1));
        monthlyDayInput?.onEndEdit.AddListener(v =>
        {
            if (int.TryParse(v, out int n)) _monthlyDay = Mathf.Clamp(n, 1, 31);
            RefreshRepeatUI(); SaveNow();
        });
        monthlyLastDayToggle?.onValueChanged.AddListener(v =>
        {
            _monthlyLastDay = v;
            RefreshRepeatUI(); SaveNow();
        });
        intervalInput?.onEndEdit.AddListener(v =>
        {
            if (int.TryParse(v, out int n)) _intervalDays = Mathf.Clamp(n, 1, 365);
            RefreshRepeatUI(); SaveNow();
        });

        endDateTriggerBtn?.onClick.AddListener(TogglePicker);
        endDateClearBtn?.onClick.AddListener(ClearEndDate);

        hourDecBtn?.onClick.AddListener(() => ShiftHour(-1));
        hourIncBtn?.onClick.AddListener(() => ShiftHour(+1));
        minDecBtn?.onClick.AddListener(() => ShiftMin(-MIN_STEP));
        minIncBtn?.onClick.AddListener(() => ShiftMin(+MIN_STEP));
        timeClearBtn?.onClick.AddListener(ClearTime);
        hourInput?.onEndEdit.AddListener(v =>
        {
            if (int.TryParse(v, out int h))
            { if (_hour < 0) _hour = 0; _hour = Mathf.Clamp(h, 0, 23); }
            RefreshTimeDisplay(); SaveNow();
        });
        minInput?.onEndEdit.AddListener(v =>
        {
            if (int.TryParse(v, out int m))
            { if (_hour < 0) _hour = 0; _minute = Mathf.Clamp(m, 0, 59); }
            RefreshTimeDisplay(); SaveNow();
        });

        titleInput?.onEndEdit.AddListener(OnTitleEndEdit);
        memoInput?.onEndEdit.AddListener(_ => SaveNow());
        priorityToggle?.onValueChanged.AddListener(_ => SaveNow());
        deleteButton?.onClick.AddListener(OnDelete);

        datePicker?.gameObject.SetActive(false);
        Clear();
    }

    // ── 公開API ──────────────────────────────

    public void Open(RoutineItem item)
    {
        _target = item;
        _loading = true;

        if (titleInput != null) titleInput.text = item.title ?? "";
        if (memoInput != null) memoInput.text = item.memo ?? "";
        if (priorityToggle != null) priorityToggle.isOn = item.priorityHigh;

        _repeatType = string.IsNullOrEmpty(item.repeatType) ? "daily" : item.repeatType;
        _weekdays.Clear();
        if (item.weekdays != null) foreach (var d in item.weekdays) _weekdays.Add(d);
        _intervalDays = Mathf.Clamp(item.intervalDays, 1, 365);
        _monthlyDay = Mathf.Clamp(item.monthlyDay <= 0 ? 1 : item.monthlyDay, 1, 31);
        _monthlyLastDay = item.monthlyLastDay;
        SetEndDate(string.IsNullOrEmpty(item.endDate) ? null : item.endDate);
        ParseTime(item.time);
        RefreshTimeDisplay();
        RefreshRepeatUI();

        _loading = false;
        if (datePicker != null) datePicker.Close();
        if (form != null) form.SetActive(true);
        if (emptyState != null) emptyState.SetActive(false);
    }

    public void Clear()
    {
        _target = null;
        if (datePicker != null) datePicker.Close();
        if (form != null) form.SetActive(false);
        if (emptyState != null) emptyState.SetActive(true);
    }

    // インライン一覧編集→確定時に、開いている右ペインのタイトル欄も同期（全Rebuildせず）。
    public void RefreshTitleIfOpen(string id, string newTitle)
    {
        if (_target == null || _target.id != id) return;
        if (titleInput == null) return;
        _loading = true;                       // この更新で onValueChanged/onEndEdit を誤発火させない
        titleInput.text = newTitle ?? "";
        _loading = false;
    }

    public void FocusTitle()
    {
        if (titleInput == null) return;
        titleInput.Select();
        titleInput.ActivateInputField();
    }

    // ── 保存 ─────────────────────────────────

    private void SaveNow()
    {
        if (_target == null || _loading) return;
        var nm = NotebookManager.Instance;
        if (nm == null) return;

        _target.title = titleInput != null ? titleInput.text : _target.title;
        _target.memo = memoInput != null ? memoInput.text : _target.memo;
        _target.priorityHigh = priorityToggle != null && priorityToggle.isOn;
        _target.repeatType = _repeatType;
        _target.weekdays = new List<int>(_weekdays);
        _target.weekdays.Sort();
        _target.intervalDays = _intervalDays;
        _target.monthlyDay = _monthlyDay;
        _target.monthlyLastDay = _monthlyLastDay;
        _target.endDate = _endDate;
        _target.time = _hour >= 0 ? $"{_hour:D2}:{_minute:D2}" : null;

        nm.UpdateRoutine(_target);
        OnChanged?.Invoke();
    }

    private void OnTitleEndEdit(string v)
    {
        if (string.IsNullOrWhiteSpace(v) && _target != null)
        {
            if (titleInput != null) titleInput.text = _target.title ?? "";
            return;
        }
        SaveNow();
    }

    private void OnDelete()
    {
        if (_target == null) return;
        var id = _target.id;
        NotebookManager.Instance?.DeleteRoutine(id);
        Clear();
        OnDeleted?.Invoke(id);
    }

    // ── 繰り返し ─────────────────────────────

    private void SetRepeatType(string type)
    {
        _repeatType = type;
        // 毎週に切替時、未選択なら今日の曜日をプリセット(「選ばせる」の初期値)
        if (type == "weekly" && _weekdays.Count == 0)
            _weekdays.Add((int)DateTime.Now.DayOfWeek);
        if (type == "monthly" && _monthlyDay <= 0)
            _monthlyDay = DateTime.Now.Day;
        RefreshRepeatUI();
        SaveNow();
    }

    private void ToggleWeekday(int idx)
    {
        if (_weekdays.Contains(idx))
        {
            // 最後の1個は外させない(空集合=出現ゼロを防ぐ)
            if (_weekdays.Count <= 1) return;
            _weekdays.Remove(idx);
        }
        else _weekdays.Add(idx);
        RefreshRepeatUI();
        SaveNow();
    }

    /// <summary>毎月の日付を1〜31で送る。存在しない日は判定側で月末に丸まる。</summary>
    private void ShiftMonthlyDay(int d)
    {
        _monthlyDay = Mathf.Clamp(_monthlyDay + d, 1, 31);
        RefreshRepeatUI();
        SaveNow();
    }

    private void ShiftInterval(int d)
    {
        _intervalDays = Mathf.Clamp(_intervalDays + d, 1, 365);
        RefreshRepeatUI();
        SaveNow();
    }

    private void RefreshRepeatUI()
    {
        PaintSegment(dailyBtn, _repeatType == "daily");
        PaintSegment(weeklyBtn, _repeatType == "weekly");
        PaintSegment(intervalBtn, _repeatType == "interval");
        PaintSegment(monthlyBtn, _repeatType == "monthly");

        if (weekdayRow != null) weekdayRow.gameObject.SetActive(_repeatType == "weekly");
        if (intervalRow != null) intervalRow.SetActive(_repeatType == "interval");
        if (monthlyRow != null) monthlyRow.SetActive(_repeatType == "monthly");

        for (int i = 0; i < 7; i++)
        {
            if (_chipImgs[i] == null) continue;
            bool on = _weekdays.Contains(i);
            _chipImgs[i].color = on ? UITheme_FocusMode.AccentBlueSolid : UITheme_FocusMode.SelectedBG;
            if (_chipLabels[i] != null)
                _chipLabels[i].color = on ? Color.white : UITheme_FocusMode.TextSecondary;
        }

        if (intervalInput != null) intervalInput.text = _intervalDays.ToString();

        // 「月末」選択中は日付欄を空にして無効化する。
        // 数字が残っていると「15日と月末のどちらが効くのか」が読み取れないため。
        // 値自体は _monthlyDay に保持しており、月末を外すと元の日付に戻る。
        if (monthlyDayInput != null)
        {
            monthlyDayInput.text = _monthlyLastDay ? string.Empty : _monthlyDay.ToString();
            monthlyDayInput.interactable = !_monthlyLastDay;
        }
        if (monthlyDayDecBtn != null) monthlyDayDecBtn.interactable = !_monthlyLastDay;
        if (monthlyDayIncBtn != null) monthlyDayIncBtn.interactable = !_monthlyLastDay;
        if (monthlyLastDayToggle != null && monthlyLastDayToggle.isOn != _monthlyLastDay)
            monthlyLastDayToggle.SetIsOnWithoutNotify(_monthlyLastDay);
    }

    private void PaintSegment(Button btn, bool selected)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = selected ? UITheme_FocusMode.AccentBlueSolid : UITheme_FocusMode.SelectedBG;
        var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (lbl != null) lbl.color = selected ? Color.white : UITheme_FocusMode.TextSecondary;
    }

    // ── 終了日 ───────────────────────────────

    private void TogglePicker()
    {
        if (datePicker == null) return;
        if (datePicker.gameObject.activeSelf) { datePicker.Close(); return; }
        datePicker.Open(_endDate, 1, key => { SetEndDate(key); SaveNow(); });
    }

    private void ClearEndDate()
    {
        if (string.IsNullOrEmpty(_endDate)) return;
        SetEndDate(null);
        SaveNow();
    }

    private void SetEndDate(string key)
    {
        _endDate = key;
        if (endDateDisplayText != null)
        {
            if (!string.IsNullOrEmpty(key) &&
                DateTime.TryParseExact(key, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
            {
                endDateDisplayText.text = dt.ToString("yyyy年M月d日 (ddd)",
                    new System.Globalization.CultureInfo("ja-JP")) + " まで";
                endDateDisplayText.color = UITheme_FocusMode.TextBody;
            }
            else
            {
                endDateDisplayText.text = "終了日なし(ずっと続く)";
                endDateDisplayText.color = UITheme_FocusMode.TextDisabled;
            }
        }
        if (endDateClearBtnLabel != null)
            endDateClearBtnLabel.text = string.IsNullOrEmpty(_endDate) ? "終了日なし" : "クリア";
    }

    // ── 時刻(EventModal/TodoDetailと同挙動) ──

    private void ParseTime(string t)
    {
        _hour = -1; _minute = 0;
        if (string.IsNullOrEmpty(t)) return;
        var parts = t.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
        { _hour = Mathf.Clamp(h, 0, 23); _minute = Mathf.Clamp(m, 0, 59); }
    }

    private void ClearTime()
    {
        _hour = -1; _minute = 0;
        RefreshTimeDisplay();
        SaveNow();
    }

    private void ShiftHour(int d)
    { if (_hour < 0) _hour = 9; _hour = (_hour + d + 24) % 24; RefreshTimeDisplay(); SaveNow(); }

    private void ShiftMin(int d)
    { if (_hour < 0) _hour = 9; _minute = ((_minute + d) % 60 + 60) % 60; RefreshTimeDisplay(); SaveNow(); }

    private void RefreshTimeDisplay()
    {
        bool has = _hour >= 0;
        if (hourInput != null) hourInput.text = has ? _hour.ToString("D2") : "";
        if (minInput != null) minInput.text = has ? _minute.ToString("D2") : "";
        if (timeClearBtnLabel != null) timeClearBtnLabel.text = has ? "クリア" : "時間なし";
    }
}
