using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Daily 中央列の「⋯」から開く、タスク編集モーダル。
/// EventModal（予定編集）のモーダル機構を複製したガワに乗せ、保存ロジックは
/// TodoDetailUI（右ペイン）から移植する。データ層は NotebookManager を共有
/// （UpdateTodo / DeleteTodo）。Todoタブの右ペインとは独立したインスタンス。
///
/// 4-D-①: タイトル・メモ・優先度・削除＋日付/時刻の「保持・保存」まで。
///         日付ピッカーの起動と時刻の増減UIの配線は 4-D-②で追加する。
/// </summary>
public class TodoModal : MonoBehaviour
{
    [SerializeField] private Michsky.MUIP.ModalWindowManager modal;
    [SerializeField] private GameObject deleteBtnGO;
    [SerializeField] private Button deleteBtn;

    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField memoInput;

    [SerializeField] private TextMeshProUGUI dateDisplayText;
    [SerializeField] private Button dateTriggerBtn;
    [SerializeField] private DatePickerPopup datePicker;

    [SerializeField] private TMP_InputField hourInput;
    [SerializeField] private TMP_InputField minInput;
    [SerializeField] private Button hourDecBtn;
    [SerializeField] private Button hourIncBtn;
    [SerializeField] private Button minDecBtn;
    [SerializeField] private Button minIncBtn;
    [SerializeField] private Button timeClearBtn;
    [SerializeField] private TextMeshProUGUI timeClearBtnLabel;

    [SerializeField] private Toggle priorityToggle;

    private const int MIN_STEP = 15;
    private TodoItem _target;
    private string _selectedDate;
    private int _hour = -1, _minute = 0;
    private int _weekStartDow = 1;
    private bool _loading;

    public event Action OnChanged;          // 保存後（リスト再描画用）
    public event Action<string> OnDeleted;  // 削除後（id）

    private void Start()
    {
        modal?.onConfirm.AddListener(OnSave);
        deleteBtn?.onClick.AddListener(OnDeleteTask);
        dateTriggerBtn?.onClick.AddListener(TogglePicker);
        hourDecBtn?.onClick.AddListener(() => ShiftHour(-1));
        hourIncBtn?.onClick.AddListener(() => ShiftHour(+1));
        minDecBtn?.onClick.AddListener(() => ShiftMin(-MIN_STEP));
        minIncBtn?.onClick.AddListener(() => ShiftMin(+MIN_STEP));
        timeClearBtn?.onClick.AddListener(ClearTime);

        hourInput?.onEndEdit.AddListener(v =>
        {
            if (int.TryParse(v, out int h)) { if (_hour < 0) _hour = 0; _hour = Mathf.Clamp(h, 0, 23); }
            RefreshTimeDisplay();
        });
        minInput?.onEndEdit.AddListener(v =>
        {
            if (int.TryParse(v, out int m)) { if (_hour < 0) _hour = 0; _minute = Mathf.Clamp(m, 0, 59); }
            RefreshTimeDisplay();
        });

        datePicker?.gameObject.SetActive(false);
    }

    /// <summary>タスク編集モーダルを開く。</summary>
    public void OpenEdit(TodoItem item)
    {
        if (item == null) return;
        _target = item;
        _loading = true;

        ForceCleanField(titleInput, item.title ?? "");
        ForceCleanField(memoInput, item.memo ?? "");
        if (priorityToggle != null) priorityToggle.isOn = item.priorityHigh;

        SetDate(string.IsNullOrEmpty(item.dateKey) ? null : item.dateKey);
        ParseTime(item.time);
        RefreshTimeDisplay();

        _loading = false;
        if (deleteBtnGO != null) deleteBtnGO.SetActive(true);
        if (datePicker != null) datePicker.Close();
        if (modal != null) { modal.titleText = "タスクを編集"; modal.UpdateUI(); modal.OpenWindow(); }
    }

    private void OnSave()
    {
        if (_target == null) return;
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

    public void OnDeleteTask()
    {
        if (_target == null) return;
        var id = _target.id;
        NotebookManager.Instance?.DeleteTodo(id);
        if (modal != null) modal.CloseWindow();
        OnDeleted?.Invoke(id);
    }

    // ===== 日付 =====
    private void TogglePicker()
    {
        if (datePicker == null) return;
        if (datePicker.gameObject.activeSelf) datePicker.Close();
        else datePicker.Open(_selectedDate, _weekStartDow, SetDate);
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
            }
            else dateDisplayText.text = "日付なし";
        }
    }

    // ===== 時刻 =====
    private void ParseTime(string t)
    {
        _hour = -1; _minute = 0;
        if (string.IsNullOrEmpty(t)) return;
        var parts = t.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
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
        if (hourInput != null) hourInput.SetTextWithoutNotify(_hour >= 0 ? $"{_hour:D2}" : "--");
        if (minInput != null) minInput.SetTextWithoutNotify(_hour >= 0 ? $"{_minute:D2}" : "--");
    }

    // IME 対策込みのフィールド初期化（EventModal から移植）。
    private void ForceCleanField(TMP_InputField field, string value = "")
    {
        if (field == null) return;
        field.text = value;
        field.SetTextWithoutNotify(value);
        if (field.textComponent != null)
        {
            field.textComponent.text = value;
            field.textComponent.ForceMeshUpdate(true, true);
        }
        if (field.placeholder != null)
            field.placeholder.gameObject.SetActive(string.IsNullOrEmpty(value));
        field.caretPosition = 0;
    }
}
