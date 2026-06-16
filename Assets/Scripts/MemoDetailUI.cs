using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// メモタブ 右ペイン（詳細エディタ）。M-1 コア。
/// - 選択中の MemoNote を表示・編集（onEndEdit で自動保存）。
/// - 本文はプレーンテキスト1箱（6/16決定）。装飾(M-5)・画像(M-4)・カレンダー紐づけ(M-6)は後フェーズ。
/// - 削除はソフト削除（TrashMemoNote）＝ゴミ箱へ。ゴミ箱ビュー/30日復元は M-3。
/// - 入力欄はシーン配置（Inspector配線）。IME対応は全機能まとめて後で（ForceCleanFieldは暫定踏襲）。
/// </summary>
public class MemoDetailUI : MonoBehaviour
{
    [SerializeField] private GameObject emptyState;
    [SerializeField] private GameObject form;

    [Header("入力")]
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField bodyInput;
    [SerializeField] private TextMeshProUGUI metaText;

    [Header("操作")]
    [SerializeField] private Button deleteButton;

    private MemoNote _target;
    private bool _loading;   // ロード中の自動保存抑制

    public event Action OnChanged;          // 保存後（選択行のその場更新用）
    public event Action<string> OnDeleted;  // 削除後（id）

    public string CurrentId => _target?.id;

    private void Start()
    {
        // メモはタイトル空も許容（一覧では「（無題）」表示）。Todoのような空タイトル差し戻しはしない。
        titleInput?.onEndEdit.AddListener(_ => SaveNow());
        bodyInput?.onEndEdit.AddListener(_ => SaveNow());
        deleteButton?.onClick.AddListener(OnDelete);
        Clear();
    }

    // ── 公開API ──────────────────────────────

    public void Open(MemoNote note)
    {
        _target = note;
        _loading = true;
        ForceCleanField(titleInput, note.title ?? "");
        ForceCleanField(bodyInput, note.body ?? "");
        RefreshMeta();
        _loading = false;
        if (form != null) form.SetActive(true);
        if (emptyState != null) emptyState.SetActive(false);
    }

    public void Clear()
    {
        _target = null;
        if (form != null) form.SetActive(false);
        if (emptyState != null) emptyState.SetActive(true);
    }

    public void FocusTitle()
    {
        if (titleInput == null) return;
        titleInput.Select();
        titleInput.ActivateInputField();
    }

    // ── 保存・削除 ───────────────────────────

    private void SaveNow()
    {
        if (_target == null || _loading) return;
        var nm = NotebookManager.Instance;
        if (nm == null) return;

        _target.title = titleInput != null ? titleInput.text : _target.title;
        _target.body  = bodyInput  != null ? bodyInput.text  : _target.body;

        nm.UpdateMemoNote(_target);   // updatedAt は内部で更新される
        RefreshMeta();
        OnChanged?.Invoke();
    }

    private void OnDelete()
    {
        if (_target == null) return;
        var id = _target.id;
        NotebookManager.Instance?.TrashMemoNote(id);  // ソフト削除（ゴミ箱）。復元は M-3
        Clear();
        OnDeleted?.Invoke(id);
    }

    private void RefreshMeta()
    {
        if (metaText == null) return;
        if (_target == null) { metaText.text = ""; return; }
        string created = string.IsNullOrEmpty(_target.createdAt) ? "—" : _target.createdAt;
        string updated = string.IsNullOrEmpty(_target.updatedAt) ? "—" : _target.updatedAt;
        metaText.text = $"作成 {created}　·　更新 {updated}";
    }

    // IME 対策込みのフィールド初期化（TodoModal/EventModal から踏襲）。
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
