using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// メモタブUI
/// - 左：メモ一覧（タイトル・更新日・プレビュー）＋＋ボタン
/// - 右：選択したメモのタイトル入力＋本文テキストエリア
/// - メモはカレンダーと独立。日付に紐づかない自由ノート
/// </summary>
public class MemoUI : MonoBehaviour
{
    [Header("左：一覧")]
    [SerializeField] private Transform memoListParent;
    [SerializeField] private Button    addMemoBtn;

    [Header("右：編集")]
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private TMP_InputField bodyInput;
    [SerializeField] private TextMeshProUGUI metaText;
    [SerializeField] private Button          deleteMemoBtn;
    [SerializeField] private TextMeshProUGUI emptyText;

    private string selectedMemoId = null;
    private bool   isDirty        = false;

    private void OnEnable()
    {
        SetupButtons();
        RefreshList();
        ShowEmpty();
    }

    private void SetupButtons()
    {
        addMemoBtn?.onClick.RemoveAllListeners();
        addMemoBtn?.onClick.AddListener(CreateNewMemo);
        deleteMemoBtn?.onClick.RemoveAllListeners();
        deleteMemoBtn?.onClick.AddListener(DeleteCurrentMemo);

        titleInput?.onValueChanged.RemoveAllListeners();
        titleInput?.onValueChanged.AddListener(_ => OnTitleChanged());
        bodyInput?.onValueChanged.RemoveAllListeners();
        bodyInput?.onValueChanged.AddListener(_ => OnBodyChanged());
    }

    // ─── 一覧描画 ─────────────────────────────────────────
    private void RefreshList()
    {
        if (memoListParent == null) return;
        foreach (Transform child in memoListParent) Destroy(child.gameObject);

        var memos = NotebookManager.Instance?.GetAllMemos();
        if (memos == null) return;

        foreach (var memo in memos)
            AppendMemoItem(memo);
    }

    private void AppendMemoItem(MemoEntry memo)
    {
        var rowGO  = new GameObject("MemoItem_" + memo.id, typeof(RectTransform));
        rowGO.transform.SetParent(memoListParent, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = memo.id == selectedMemoId
            ? new Color(1f, 1f, 1f, 0.08f)
            : UITheme_FocusMode.DayCellBG;

        var rowBtn = rowGO.AddComponent<Button>();
        rowBtn.targetGraphic = rowImg;

        var rowVLG = rowGO.AddComponent<VerticalLayoutGroup>();
        rowVLG.padding = new RectOffset(8, 8, 6, 6);
        rowVLG.spacing = 2f;
        rowVLG.childForceExpandWidth  = true;
        rowVLG.childForceExpandHeight = false;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 54f;

        // タイトル
        var titleGO  = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(rowGO.transform, false);
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text      = string.IsNullOrEmpty(memo.title) ? "無題のメモ" : memo.title;
        titleTxt.fontSize  = UITheme_FocusMode.FontBody;
        titleTxt.color     = UITheme_FocusMode.TextBody;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.overflowMode = TextOverflowModes.Ellipsis;
        titleGO.AddComponent<LayoutElement>().preferredHeight = 16f;

        // 更新日
        var dateGO  = new GameObject("Date", typeof(RectTransform));
        dateGO.transform.SetParent(rowGO.transform, false);
        var dateTxt = dateGO.AddComponent<TextMeshProUGUI>();
        dateTxt.text     = memo.updatedAt ?? "";
        dateTxt.fontSize = UITheme_FocusMode.FontCaption;
        dateTxt.color    = UITheme_FocusMode.TextDisabled;
        dateGO.AddComponent<LayoutElement>().preferredHeight = 13f;

        // プレビュー
        var prevGO  = new GameObject("Preview", typeof(RectTransform));
        prevGO.transform.SetParent(rowGO.transform, false);
        var prevTxt = prevGO.AddComponent<TextMeshProUGUI>();
        prevTxt.text     = (memo.body ?? "").Replace("\n", " ");
        prevTxt.fontSize = UITheme_FocusMode.FontCaption;
        prevTxt.color    = UITheme_FocusMode.TextMuted;
        prevTxt.overflowMode = TextOverflowModes.Ellipsis;
        prevGO.AddComponent<LayoutElement>().preferredHeight = 13f;

        // クリック
        var capturedId = memo.id;
        rowBtn.onClick.AddListener(() => SelectMemo(capturedId));
    }

    // ─── メモ選択 ─────────────────────────────────────────
    private void SelectMemo(string id)
    {
        // 変更があれば自動保存
        if (isDirty) SaveCurrentMemo();

        selectedMemoId = id;
        var memo = NotebookManager.Instance?.GetAllMemos().Find(m => m.id == id);
        if (memo == null) { ShowEmpty(); return; }

        if (emptyText) emptyText.gameObject.SetActive(false);
        if (titleInput)
        {
            titleInput.onValueChanged.RemoveAllListeners();
            titleInput.text = memo.title;
            titleInput.onValueChanged.AddListener(_ => OnTitleChanged());
        }
        if (bodyInput)
        {
            bodyInput.onValueChanged.RemoveAllListeners();
            bodyInput.text = memo.body;
            bodyInput.onValueChanged.AddListener(_ => OnBodyChanged());
        }
        if (metaText) metaText.text = $"最終更新：{memo.updatedAt}";
        if (deleteMemoBtn) deleteMemoBtn.gameObject.SetActive(true);

        isDirty = false;
        RefreshList();
    }

    private void ShowEmpty()
    {
        selectedMemoId = null;
        isDirty = false;
        if (emptyText) emptyText.gameObject.SetActive(true);
        if (titleInput) { titleInput.onValueChanged.RemoveAllListeners(); titleInput.text = ""; titleInput.onValueChanged.AddListener(_ => OnTitleChanged()); }
        if (bodyInput)  { bodyInput.onValueChanged.RemoveAllListeners();  bodyInput.text  = ""; bodyInput.onValueChanged.AddListener(_ => OnBodyChanged()); }
        if (metaText)   metaText.text = "メモを選択してください";
        if (deleteMemoBtn) deleteMemoBtn.gameObject.SetActive(false);
    }

    // ─── 変更ハンドラ ─────────────────────────────────────
    private void OnTitleChanged()
    {
        if (selectedMemoId == null) return;
        isDirty = true;
    }

    private void OnBodyChanged()
    {
        if (selectedMemoId == null) return;
        isDirty = true;
    }

    private void SaveCurrentMemo()
    {
        if (selectedMemoId == null || NotebookManager.Instance == null) return;
        var title = titleInput?.text ?? "";
        var body  = bodyInput?.text  ?? "";
        NotebookManager.Instance.SaveMemo(selectedMemoId, title, body);
        isDirty = false;
        if (metaText) metaText.text = $"最終更新：{NotebookManager.NowKey()}";
    }

    // フォーカスが外れた時に自動保存
    private void OnDisable()
    {
        if (isDirty) SaveCurrentMemo();
    }

    // ─── メモ追加・削除 ───────────────────────────────────
    private void CreateNewMemo()
    {
        if (isDirty) SaveCurrentMemo();
        var memo = NotebookManager.Instance?.AddMemo();
        if (memo == null) return;
        RefreshList();
        SelectMemo(memo.id);
        // タイトルにフォーカス
        titleInput?.Select();
        titleInput?.ActivateInputField();
    }

    private void DeleteCurrentMemo()
    {
        if (selectedMemoId == null || NotebookManager.Instance == null) return;
        NotebookManager.Instance.DeleteMemo(selectedMemoId);
        isDirty = false;
        ShowEmpty();
        RefreshList();
    }
}
