using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// タスクタブ右ペイン(詳細フォーム)
/// - 選択中の TodoItem を表示・編集(自動保存)
/// - 時刻まわりの挙動は EventModal と同一(15分刻み・時0-23循環・未設定=-1)
/// - 日付も任意: DateClearBtn で「日付なし」に戻せる
/// </summary>
public class TodoDetailUI : MonoBehaviour
{
    [SerializeField] private GameObject emptyState;
    [SerializeField] private GameObject form;

    [Header("タイトル/メモ")]
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField memoInput;

    [Header("日付")]
    [SerializeField] private Button dateTriggerBtn;
    [SerializeField] private TextMeshProUGUI dateDisplayText;
    [SerializeField] private Button dateClearBtn;
    [SerializeField] private TextMeshProUGUI dateClearBtnLabel;
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
    private TodoItem _target;
    private string _selectedDate;          // "yyyy-MM-dd" / null
    private int _hour = -1, _minute = 0;   // -1 = 時刻なし
    private bool _loading;                 // ロード中の自動保存抑制

    public event Action OnChanged;          // 保存後(リスト再描画用)
    public event Action<string> OnDeleted;  // 削除後(id)

    public string CurrentId => _target?.id;

    private void Start()
    {
        dateTriggerBtn?.onClick.AddListener(TogglePicker);
        dateClearBtn?.onClick.AddListener(ClearDate);

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

    public void Open(TodoItem item)
    {
        _target = item;
        _loading = true;

        if (titleInput != null) titleInput.text = item.title ?? "";
        if (memoInput != null) memoInput.text = item.memo ?? "";
        if (priorityToggle != null) priorityToggle.isOn = item.priorityHigh;

        SetDate(string.IsNullOrEmpty(item.dateKey) ? null : item.dateKey);
        ParseTime(item.time);
        RefreshTimeDisplay();

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
        _target.dateKey = _selectedDate;
        _target.time = _hour >= 0 ? $"{_hour:D2}:{_minute:D2}" : null;

        nm.UpdateTodo(_target);
        OnChanged?.Invoke();
    }

    private void OnTitleEndEdit(string v)
    {
        // 空タイトルは元の値に戻す(行が無名になるのを防ぐ)
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
        NotebookManager.Instance?.DeleteTodo(id);
        Clear();
        OnDeleted?.Invoke(id);
    }

    // ── 日付 ─────────────────────────────────

    private void TogglePicker()
    {
        if (datePicker == null) return;
        if (datePicker.gameObject.activeSelf) { datePicker.Close(); return; }
        datePicker.Open(_selectedDate, 1, key => { SetDate(key); SaveNow(); });
    }

    private void ClearDate()
    {
        if (string.IsNullOrEmpty(_selectedDate)) return;
        SetDate(null);
        SaveNow();
    }

    private void SetDate(string key)
    {
        _selectedDate = key;
        if (dateDisplayText != null)
        {
            if (!string.IsNullOrEmpty(key) &&
                DateTime.TryParseExact(key, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
            {
                dateDisplayText.text = dt.ToString("yyyy年M月d日 (ddd)",
                    new System.Globalization.CultureInfo("ja-JP"));
                dateDisplayText.color = UITheme_FocusMode.TextBody;
            }
            else
            {
                dateDisplayText.text = "日付を選択";
                dateDisplayText.color = UITheme_FocusMode.TextDisabled;
            }
        }
        if (dateClearBtnLabel != null)
            dateClearBtnLabel.text = string.IsNullOrEmpty(_selectedDate) ? "日付なし" : "クリア";
    }

    // ── 時刻(EventModalと同挙動) ─────────────

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
