using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventModal : MonoBehaviour
{
    [Header("MUIP")]
    [SerializeField] private Michsky.MUIP.ModalWindowManager modal;
    [Header("削除")]
    [SerializeField] private UnityEngine.GameObject deleteBtnGO;  // 削除ボタンGO
    [Header("カテゴリー")]
    [SerializeField] private UnityEngine.UI.Button[] tagButtons; // タグ選択ボタン
    [Header("タイトル")]
    [SerializeField] private TMP_InputField titleInput;
    [Header("日付")]
    [SerializeField] private TextMeshProUGUI dateDisplayText;
    [SerializeField] private Button dateTriggerBtn;
    [SerializeField] private DatePickerPopup datePicker;
    [Header("時間")]
    [SerializeField] private Button          hourDecBtn;
    [SerializeField] private TMP_InputField  hourInput;   // 直接入力対応
    [SerializeField] private Button          hourIncBtn;
    [SerializeField] private Button          minDecBtn;
    [SerializeField] private TMP_InputField  minInput;    // 直接入力対応
    [SerializeField] private Button          minIncBtn;
    [SerializeField] private Button          timeClearBtn;
    [SerializeField] private TextMeshProUGUI timeClearBtnLabel;
    [Header("メモ")]
    [SerializeField] private TMP_InputField memoInput;

    private string _selectedDate;
    private string _selectedTagId = "yotei";
    private string _editingId     = null;  // 編集中イベントID（null=新規追加）
    private int _hour = -1, _minute = 0;
    private ScheduleEvent _editTarget;
    private Action _onSaved;
    private const int MIN_STEP = 15;

    private void Start()
    {
        modal?.onConfirm.AddListener(OnSave);
        modal?.onCancel.AddListener(OnCancel);
        dateTriggerBtn?.onClick.AddListener(TogglePicker);

        hourDecBtn?.onClick.AddListener(() => ShiftHour(-1));
        hourIncBtn?.onClick.AddListener(() => ShiftHour(+1));
        minDecBtn?.onClick.AddListener(() => ShiftMin(-MIN_STEP));
        minIncBtn?.onClick.AddListener(() => ShiftMin(+MIN_STEP));
        timeClearBtn?.onClick.AddListener(ClearTime);

        // 時間の直接入力バリデーション
        hourInput?.onEndEdit.AddListener(v =>
        {
            if (int.TryParse(v, out int h))
            { if (_hour < 0) _hour = 0; _hour = Mathf.Clamp(h, 0, 23); }
            RefreshTimeDisplay();
        });
        minInput?.onEndEdit.AddListener(v =>
        {
            if (int.TryParse(v, out int m))
            { if (_hour < 0) _hour = 0; _minute = Mathf.Clamp(m, 0, 59); }
            RefreshTimeDisplay();
        });

        datePicker?.gameObject.SetActive(false);
        // 削除ボタン onClick（Play 時に必ず設定）
        if (deleteBtnGO != null)
        {
            var delBtn = deleteBtnGO.GetComponent<UnityEngine.UI.Button>();
            delBtn?.onClick.RemoveAllListeners();
            delBtn?.onClick.AddListener(OnDeleteEvent);
        }
        // タグボタン onClick
        if (tagButtons != null)
        {
            for (int i = 0; i < tagButtons.Length && i < TagConfig.Tags.Count; i++)
            {
                int idx = i;
                string tagId = TagConfig.Tags[idx].id;
                tagButtons[i]?.onClick.AddListener(() => SetTag(tagId));
            }
        }
    }

    // ── 公開 API ─────────────────────────────────────────────
    public void OpenAddForm(string dateKey, Action onSaved = null, string defaultTime = null)
    {
        _editTarget = null; _onSaved = onSaved;
        ForceCleanField(titleInput);
        ForceCleanField(memoInput);
        SetTag("yotei");
        _editingId = null;                 // 新規追加モード
        if (deleteBtnGO != null) deleteBtnGO.SetActive(false); // 削除ボタン非表示
        SetDate(dateKey);
        if (!string.IsNullOrEmpty(defaultTime) && defaultTime.Length >= 5 &&
            int.TryParse(defaultTime.Substring(0,2), out int dtH) &&
            int.TryParse(defaultTime.Substring(3,2), out int dtM))
        { _hour = dtH; _minute = dtM; RefreshTimeDisplay(); }
        else ClearTime();
        if (modal != null) { modal.titleText = "アイテムを追加"; modal.UpdateUI(); modal.OpenWindow(); }
        datePicker?.Close();
    }

    public void OpenEditForm(ScheduleEvent ev, Action onSaved = null)
    {
        _editTarget = ev; _onSaved = onSaved;
        ForceCleanField(titleInput, ev.title ?? "");
        ForceCleanField(memoInput,  ev.memo  ?? "");
        SetTag(ev.tagId ?? "yotei");
        _editingId = ev?.id;               // 編集モード（_editTarget.id と同じ値）
        if (deleteBtnGO != null) deleteBtnGO.SetActive(true);  // 削除ボタン表示
        SetDate(ev.date);
        if (!string.IsNullOrEmpty(ev.time) && ev.time.Length >= 5 &&
            int.TryParse(ev.time.Substring(0,2), out int h) &&
            int.TryParse(ev.time.Substring(3,2), out int m))
        { _hour = h; _minute = m; }
        else ClearTime();
        RefreshTimeDisplay();
        if (modal != null) { modal.titleText = "アイテムを編集"; modal.UpdateUI(); modal.OpenWindow(); }
        datePicker?.Close();
    }

    // ── 日付 ─────────────────────────────────────────────────
    private void SetDate(string key)
    {
        _selectedDate = key;
        if (dateDisplayText == null) return;
        if (!string.IsNullOrEmpty(key) &&
            DateTime.TryParseExact(key, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
        {
            dateDisplayText.text = dt.ToString("yyyy年M月d日 (ddd)",
                new System.Globalization.CultureInfo("ja-JP"));
            dateDisplayText.color = UITheme_FocusMode.TextBody;
        }
        else { dateDisplayText.text = "日付を選択"; dateDisplayText.color = UITheme_FocusMode.TextDisabled; }
    }

    private void TogglePicker()
    {
        if (datePicker == null) return;
        if (datePicker.gameObject.activeSelf) datePicker.Close();
        else datePicker.Open(_selectedDate, 1, key => SetDate(key));
    }

    // ── 時間 ─────────────────────────────────────────────────
    private void ClearTime() { _hour = -1; _minute = 0; RefreshTimeDisplay(); }

    private void ShiftHour(int d)
    { if (_hour < 0) _hour = 9; _hour = (_hour + d + 24) % 24; RefreshTimeDisplay(); }

    private void ShiftMin(int d)
    { if (_hour < 0) _hour = 9; _minute = ((_minute + d) % 60 + 60) % 60; RefreshTimeDisplay(); }

    private void RefreshTimeDisplay()
    {
        bool has = _hour >= 0;
        // placeholder は常に "00" のまま。text だけで有無を表現する
        if (hourInput) hourInput.text = has ? _hour.ToString("D2") : "";
        if (minInput)  minInput.text  = has ? _minute.ToString("D2") : "";
        if (timeClearBtnLabel) timeClearBtnLabel.text = has ? "クリア" : "時間なし";
    }

    // ── 保存 ─────────────────────────────────────────────────
    private void OnSave()
    {
        if (NotebookManager.Instance == null) return;
        string t = _hour >= 0 ? $"{_hour:D2}:{_minute:D2}" : null;
        if (_editTarget == null)
            NotebookManager.Instance.AddEvent(_selectedTagId, titleInput.text, _selectedDate ?? "", t, memoInput.text);
        else
        {
            _editTarget.tagId  = _selectedTagId;
            _editTarget.title = titleInput.text;
            _editTarget.date  = _selectedDate ?? _editTarget.date;
            _editTarget.time  = t;
            _editTarget.memo  = memoInput.text;
            NotebookManager.Instance.SaveAll();
        }
        _onSaved?.Invoke();
    }

    public void OnDeleteEvent()
    {
        if (string.IsNullOrEmpty(_editingId)) return;
        NotebookManager.Instance?.DeleteEvent(_editingId);
        _editingId = null;
        if (modal != null) modal.CloseWindow();
        _onSaved?.Invoke();  // カレンダー更新
    }

    /// <summary>InputField のテキストと内部状態を完全クリア</summary>
    private void ForceCleanField(TMP_InputField field, string value = "")
    {
        if (field == null) return;
        field.text = value;                        // 通常セット
        field.SetTextWithoutNotify(value);         // 通知なしセット
        if (field.textComponent != null)           // TMP コンポーネントを直接クリア
        {
            field.textComponent.text = value;
            field.textComponent.ForceMeshUpdate(true, true);
        }
        // プレースホルダーの表示状態を更新
        if (field.placeholder != null)
            field.placeholder.gameObject.SetActive(string.IsNullOrEmpty(value));
        field.caretPosition = 0;
    }

    private void SetTag(string tagId)
    {
        _selectedTagId = tagId;
        if (tagButtons == null) return;
        for (int i = 0; i < tagButtons.Length && i < TagConfig.Tags.Count; i++)
        {
            var tag  = TagConfig.Tags[i];
            var img  = tagButtons[i]?.GetComponent<UnityEngine.UI.Image>();
            var tmp  = tagButtons[i]?.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            bool sel = tag.id == tagId;
            // 選択: selectorBG（適切な彩度）/ 未選択: 薄い白
            // 選択: chipBG を濃く・鮮やかに / 未選択: ほんのり着色
            if (img != null) img.color = sel
                ? new UnityEngine.Color(tag.chipBG.r, tag.chipBG.g, tag.chipBG.b, 0.80f)
                : new UnityEngine.Color(tag.chipBG.r, tag.chipBG.g, tag.chipBG.b, 0.15f);
            // テキストはすべて白で統一（明るさで選択状態を区別）
            if (tmp != null) tmp.color = sel
                ? UnityEngine.Color.white
                : new UnityEngine.Color(1f, 1f, 1f, 0.45f);
        }
    }

    private void OnCancel() => datePicker?.Close();
}
