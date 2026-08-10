using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Daily右ペインからルーチンを編集するモーダル（TodoModal と同じ役割・同じ操作感）。
///
/// 【なぜ必要か】
/// Daily の行末「⋯」を押したとき、Todoはモーダルが開くのにルーチンはインライン改名に
/// 切り替わる、という不一致があった。同じ位置の同じ記号は同じ挙動であるべきなので、
/// ルーチン側にもモーダルを用意する。
///
/// 編集項目は RoutineDetailUI（Routineタブの右ペイン）と揃える:
/// タイトル / 繰り返し(毎日・毎週・N日ごと・毎月) / 終了日 / 時間 / 優先度 / メモ
/// </summary>
public class RoutineModal : MonoBehaviour
{
    [SerializeField] private Michsky.MUIP.ModalWindowManager modal;
    [SerializeField] private GameObject deleteBtnGO;
    [SerializeField] private Button deleteBtn;

    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField memoInput;

    [Header("繰り返し")]
    [SerializeField] private Button dailyBtn;
    [SerializeField] private Button weeklyBtn;
    [SerializeField] private Button intervalBtn;
    [SerializeField] private Button monthlyBtn;
    [SerializeField] private GameObject weeklyRow;
    // 曜日は Toggle ではなく Button のチップ（RoutineDetailUI と同じ方式）。
    // 子として W0〜W6 を持つ Transform を割り当てる。
    [SerializeField] private Transform weekdayRow;
    private readonly Button[] _dowChips = new Button[7];
    [SerializeField] private GameObject intervalRow;
    [SerializeField] private TMP_InputField intervalInput;
    [SerializeField] private Button intervalDecBtn;
    [SerializeField] private Button intervalIncBtn;
    [SerializeField] private GameObject monthlyRow;
    [SerializeField] private TMP_InputField monthlyDayInput;
    [SerializeField] private Button monthlyDayDecBtn;
    [SerializeField] private Button monthlyDayIncBtn;
    [SerializeField] private Toggle monthlyLastDayToggle;

    [Header("終了日 / 時間 / 優先度")]
    [SerializeField] private TextMeshProUGUI endDateDisplayText;
    [SerializeField] private Button endDateTriggerBtn;
    [SerializeField] private DatePickerPopup datePicker;
    [SerializeField] private TMP_InputField hourInput;
    [SerializeField] private TMP_InputField minInput;
    [SerializeField] private Button hourDecBtn;
    [SerializeField] private Button hourIncBtn;
    [SerializeField] private Button minDecBtn;
    [SerializeField] private Button minIncBtn;
    [SerializeField] private Button timeClearBtn;
    [SerializeField] private Toggle priorityToggle;

    private const int MIN_STEP = 5;

    private RoutineItem _target;
    private string _repeatType = "daily";
    private readonly System.Collections.Generic.List<int> _weekdays = new System.Collections.Generic.List<int>();
    private int _intervalDays = 2;
    private int _monthlyDay = 1;
    private bool _monthlyLastDay;
    private string _endDate;
    private int _hour = -1, _minute = 0;
    private int _weekStartDow = 1;
    private bool _loading;

    public event Action OnChanged;
    public event Action<string> OnDeleted;

    private void Start()
    {
        modal?.onConfirm.AddListener(OnSave);
        deleteBtn?.onClick.AddListener(OnDeleteRoutine);
        endDateTriggerBtn?.onClick.AddListener(TogglePicker);

        dailyBtn?.onClick.AddListener(() => SetRepeatType("daily"));
        weeklyBtn?.onClick.AddListener(() => SetRepeatType("weekly"));
        intervalBtn?.onClick.AddListener(() => SetRepeatType("interval"));
        monthlyBtn?.onClick.AddListener(() => SetRepeatType("monthly"));

        for (int i = 0; i < 7; i++)
        {
            int dow = i;
            _dowChips[i] = weekdayRow != null ? weekdayRow.Find("W" + i)?.GetComponent<Button>() : null;
            _dowChips[i]?.onClick.AddListener(() =>
            {
                if (_loading) return;
                if (_weekdays.Contains(dow))
                {
                    if (_weekdays.Count <= 1) return; // 最低1つは残す
                    _weekdays.Remove(dow);
                }
                else _weekdays.Add(dow);
                RefreshRepeatUI();
            });
        }

        intervalDecBtn?.onClick.AddListener(() => ShiftInterval(-1));
        intervalIncBtn?.onClick.AddListener(() => ShiftInterval(+1));
        monthlyDayDecBtn?.onClick.AddListener(() => ShiftMonthlyDay(-1));
        monthlyDayIncBtn?.onClick.AddListener(() => ShiftMonthlyDay(+1));
        monthlyLastDayToggle?.onValueChanged.AddListener(v =>
        {
            if (_loading) return;
            _monthlyLastDay = v;
            RefreshRepeatUI();
        });

        hourDecBtn?.onClick.AddListener(() => ShiftHour(-1));
        hourIncBtn?.onClick.AddListener(() => ShiftHour(+1));
        minDecBtn?.onClick.AddListener(() => ShiftMin(-MIN_STEP));
        minIncBtn?.onClick.AddListener(() => ShiftMin(+MIN_STEP));
        timeClearBtn?.onClick.AddListener(ClearTime);

        datePicker?.gameObject.SetActive(false);
    }

    /// <summary>ルーチン編集モーダルを開く。</summary>
    public void OpenEdit(RoutineItem item)
    {
        if (item == null) return;
        _target = item;
        _loading = true;

        ForceCleanField(titleInput, item.title ?? "");
        ForceCleanField(memoInput, item.memo ?? "");
        if (priorityToggle != null) priorityToggle.isOn = item.priorityHigh;

        _repeatType = string.IsNullOrEmpty(item.repeatType) ? "daily" : item.repeatType;
        _weekdays.Clear();
        if (item.weekdays != null) _weekdays.AddRange(item.weekdays);
        _intervalDays   = Mathf.Clamp(item.intervalDays, 1, 365);
        _monthlyDay     = Mathf.Clamp(item.monthlyDay <= 0 ? 1 : item.monthlyDay, 1, 31);
        _monthlyLastDay = item.monthlyLastDay;

        _endDate = string.IsNullOrEmpty(item.endDate) ? null : item.endDate;
        ParseTime(item.time);

        RefreshRepeatUI();
        RefreshEndDateDisplay();
        RefreshTimeDisplay();

        _loading = false;
        if (deleteBtnGO != null) deleteBtnGO.SetActive(true);
        if (datePicker != null) datePicker.Close();
        if (modal != null) { modal.titleText = "ルーチンを編集"; modal.UpdateUI(); modal.OpenWindow(); }
    }

    private void OnSave()
    {
        if (_target == null) return;
        var nm = NotebookManager.Instance;
        if (nm == null) return;

        _target.title = titleInput != null ? titleInput.text : _target.title;
        _target.memo  = memoInput != null ? memoInput.text : _target.memo;
        _target.priorityHigh = priorityToggle != null && priorityToggle.isOn;

        _target.repeatType     = _repeatType;
        _target.weekdays       = new System.Collections.Generic.List<int>(_weekdays);
        _target.intervalDays   = _intervalDays;
        _target.monthlyDay     = _monthlyDay;
        _target.monthlyLastDay = _monthlyLastDay;
        _target.endDate        = _endDate;
        _target.time           = _hour >= 0 ? $"{_hour:D2}:{_minute:D2}" : null;

        nm.UpdateRoutine(_target);
        OnChanged?.Invoke();
    }

    public void OnDeleteRoutine()
    {
        if (_target == null) return;
        var id = _target.id;
        NotebookManager.Instance?.DeleteRoutine(id);
        if (modal != null) modal.CloseWindow();
        OnDeleted?.Invoke(id);
    }

    // ===== 繰り返し =====
    private void SetRepeatType(string type)
    {
        _repeatType = type;
        if (type == "weekly" && _weekdays.Count == 0) _weekdays.Add((int)DateTime.Now.DayOfWeek);
        if (type == "monthly" && _monthlyDay <= 0) _monthlyDay = DateTime.Now.Day;
        RefreshRepeatUI();
    }

    private void ShiftInterval(int d)
    {
        _intervalDays = Mathf.Clamp(_intervalDays + d, 1, 365);
        RefreshRepeatUI();
    }

    private void ShiftMonthlyDay(int d)
    {
        _monthlyDay = Mathf.Clamp(_monthlyDay + d, 1, 31);
        RefreshRepeatUI();
    }

    private void RefreshRepeatUI()
    {
        PaintSegment(dailyBtn,    _repeatType == "daily");
        PaintSegment(weeklyBtn,   _repeatType == "weekly");
        PaintSegment(intervalBtn, _repeatType == "interval");
        PaintSegment(monthlyBtn,  _repeatType == "monthly");

        if (weeklyRow   != null) weeklyRow.SetActive(_repeatType == "weekly");
        if (intervalRow != null) intervalRow.SetActive(_repeatType == "interval");
        if (monthlyRow  != null) monthlyRow.SetActive(_repeatType == "monthly");

        // 曜日チップの見た目（選択=青 / 非選択=通常）。RoutineDetailUI と同じ表現に揃える
        for (int i = 0; i < 7; i++)
        {
            var chip = _dowChips[i];
            if (chip == null) continue;
            bool on = _weekdays.Contains(i);
            var img = chip.GetComponent<Image>();
            if (img != null)
                img.color = on ? UITheme_FocusMode.AccentBlueSolid : UITheme_FocusMode.SelectedBG;
            var lbl = chip.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null)
                lbl.color = on ? Color.white : UITheme_FocusMode.TextSecondary;
        }

        if (intervalInput != null) intervalInput.text = _intervalDays.ToString();

        // 「月末」選択中は日付欄を空にして無効化（どちらが効くのか読み取れないため）
        if (monthlyDayInput != null)
        {
            monthlyDayInput.text = _monthlyLastDay ? string.Empty : _monthlyDay.ToString();
            monthlyDayInput.interactable = !_monthlyLastDay;
        }
        if (monthlyDayDecBtn != null) monthlyDayDecBtn.interactable = !_monthlyLastDay;
        if (monthlyDayIncBtn != null) monthlyDayIncBtn.interactable = !_monthlyLastDay;
        if (monthlyLastDayToggle != null) monthlyLastDayToggle.SetIsOnWithoutNotify(_monthlyLastDay);
    }

    /// <summary>RoutineDetailUI.PaintSegment と同じ配色にする（見た目を揃えるため）。</summary>
    private static void PaintSegment(Button btn, bool selected)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = selected ? UITheme_FocusMode.AccentBlueSolid : UITheme_FocusMode.SelectedBG;
        var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (lbl != null) lbl.color = selected ? Color.white : UITheme_FocusMode.TextSecondary;
    }

    // ===== 終了日 =====
    private void TogglePicker()
    {
        if (datePicker == null) return;
        if (datePicker.gameObject.activeSelf) datePicker.Close();
        else datePicker.Open(_endDate, _weekStartDow, SetEndDate);
    }

    private void SetEndDate(string key)
    {
        _endDate = string.IsNullOrEmpty(key) ? null : key;
        RefreshEndDateDisplay();
    }

    private void RefreshEndDateDisplay()
    {
        if (endDateDisplayText == null) return;
        if (string.IsNullOrEmpty(_endDate)) { endDateDisplayText.text = "終了日なし(ずっと続く)"; return; }
        if (DateTime.TryParse(_endDate, out var d))
            endDateDisplayText.text = d.ToString("yyyy年M月d日 (ddd)", new System.Globalization.CultureInfo("ja-JP"));
        else endDateDisplayText.text = _endDate;
    }

    // ===== 時間 =====
    private void ParseTime(string t)
    {
        _hour = -1; _minute = 0;
        if (string.IsNullOrEmpty(t)) return;
        var parts = t.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
        { _hour = Mathf.Clamp(h, 0, 23); _minute = Mathf.Clamp(m, 0, 59); }
    }

    private void ShiftHour(int d)
    {
        if (_hour < 0) _hour = 0;
        _hour = (_hour + d + 24) % 24;
        RefreshTimeDisplay();
    }

    private void ShiftMin(int d)
    {
        if (_hour < 0) _hour = 0;
        int total = _hour * 60 + _minute + d;
        total = (total + 24 * 60) % (24 * 60);
        _hour = total / 60; _minute = total % 60;
        RefreshTimeDisplay();
    }

    private void ClearTime()
    {
        _hour = -1; _minute = 0;
        RefreshTimeDisplay();
    }

    private void RefreshTimeDisplay()
    {
        bool has = _hour >= 0;
        if (hourInput != null) hourInput.text = has ? $"{_hour:D2}" : "--";
        if (minInput  != null) minInput.text  = has ? $"{_minute:D2}" : "--";
    }

    /// <summary>ランタイム生成TMP_InputFieldの表示崩れ対策（仕様書§8）。</summary>
    private static void ForceCleanField(TMP_InputField field, string value = "")
    {
        if (field == null) return;
        field.text = value;
        field.caretPosition = value.Length;
    }
}
