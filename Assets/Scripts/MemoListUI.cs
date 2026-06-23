using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// メモタブ 左ペイン。M-1 コア + M-2a ピン + M-2b フォルダ（ドリルダウン式・iOSメモ流）。
/// 2つのビューを行き来する：
///   Notes   … ノート一覧（対象フォルダ _viewFolderId、null=すべて）。行クリックで右ペイン編集。
///   Folders … フォルダ一覧（すべて / メモ(既定) / 各フォルダ）。行クリックでそのフォルダの Notes へ潜る。
/// ヘッダー：[戻る「‹」(Notesのみ表示) / タイトル / AddButton]。AddButton はビューで「＋追加」/「＋フォルダ」を切替。
/// フォルダのリネーム/削除：各フォルダ行右端の「⋯」→ その行だけインライン編集（名前が入力欄＋削除ボタン）。
///   トグルもポップアップも使わない。「すべて」は⋯なし、「メモ」はリネーム可・削除不可。
/// リネーム入力はシーン配置の単一 InputField(folderRenameInput) を編集中の行へ寄せて使う（動的InputFieldの
///   キャレット問題を避けるため）。Rebuild の度に安全な親へ待避してから行へ再アタッチする。
/// 保存時はリスト全体を作り直さず「選択行のタイトル/メタだけその場更新」する（M-1 と同様）。
/// </summary>
public class MemoListUI : MonoBehaviour
{
    [Header("ヘッダー")]
    [SerializeField] private Button addButton;          // ＋追加 / ＋フォルダ（ビューで切替）
    [SerializeField] private Button backButton;         // 「‹」 Notes→Folders へ上がる
    [SerializeField] private TextMeshProUGUI titleText; // 「メモ」 or フォルダ名/「すべて」

    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private MemoDetailUI detail;

    [Header("フォルダ リネーム入力（シーン配置・編集行へ寄せる）")]
    [SerializeField] private TMP_InputField folderRenameInput;

    [Header("ピン")]
    [SerializeField] private Sprite pinOnSprite;   // 塗りつぶし星（ピン時）
    [SerializeField] private Sprite pinOffSprite;  // 線の星（未ピン）

    [Header("アイコン")]
    [SerializeField] private Sprite folderIcon;  // フォルダ行の左アイコン（MUIP Folder）
    [SerializeField] private Sprite noteIcon;    // メモ行の左アイコン（MUIP Document）
    [SerializeField] private Sprite trashIcon;   // ゴミ箱行の左アイコン（MUIP System/Trash）

    private enum LeftView { Folders, Notes, Trash }
    private LeftView _view = LeftView.Notes;   // 起動時は「すべて」ノート一覧
    private string _viewFolderId;              // null = すべて（Notesビューの対象）
    private string _editingFolderId;           // インラインリネーム中のフォルダid（null=なし）
    private string _confirmDeleteFolderId;     // 削除確認中のフォルダid（null=なし）
    private TMP_InputField _headerNameInput;   // ヘッダー(Notesビュー)のフォルダ名インライン入力
    private GameObject _headerNameHost;
    private string _headerEditingFolderId;     // ヘッダーで編集中のフォルダid
    private Transform _renameParkParent;       // folderRenameInput の待避先（初期親）
    private Coroutine _renameExitCo;           // リネーム確定後の編集解除（次フレーム）
    private string _editingNoteId;             // インライン改名中のメモid（null=なし）
    private string _confirmDeleteNoteId;       // 削除確認中のメモid（null=なし）
    private Coroutine _noteRenameExitCo;        // メモ改名確定後の編集解除（次フレーム）
    private TextMeshProUGUI _selTitleTmp;       // 選択行タイトル表示（右ペイン編集→ライブ反映先）

    private const int MaxFolders = 10;         // フォルダ上限（既定「メモ」含む）
    private const int MaxNotesPerFolder = 10;  // 1フォルダのメモ上限
    private const int MaxTotalNotes = 99;      // メモ総数の上限（全フォルダ合計・ゴミ箱除く）

    private string _selectedId;
    private bool _wired;

    private string _flashNoteId;
    private string _flashFolderId;
    private Coroutine _flashCo;
    private float     _flashDelay = 0.30f;
    private Coroutine _pinCo;

    private TMP_InputField _selTitleInput;   // 選択行のタイトル入力（右ペイン編集→ここへライブ反映）
    private bool _suppressInline;            // インライン同期更新中の onSelect/onEndEdit 誤発火抑制
    private Image _selectedRowImg;           // フォーカス＝選択を即ハイライトするための現在行Image
    private TMP_InputField _activeInput;
    private RectTransform  _activeCaretRT;
    private Image          _activeCaretImg;
    private int            _lastCaretPos = -1;
    private Coroutine      _caretCo;
    private static readonly Color CaretColor = new Color(0.85f, 0.85f, 0.88f, 1f);
    private const float CARET_X_PAD = 3f;
    private TextMeshProUGUI _selMetaTmp;

    private void OnEnable() => Wire();
    private void OnDisable() => Unwire();

    private void Wire()
    {
        if (_wired) return;
        _wired = true;
        if (addButton != null) addButton.onClick.AddListener(OnAddClicked);
        if (backButton != null) backButton.onClick.AddListener(GoToFolderList);
        if (folderRenameInput != null)
        {
            _renameParkParent = folderRenameInput.transform.parent;
            folderRenameInput.onEndEdit.AddListener(OnRenameEndEdit);
            folderRenameInput.gameObject.SetActive(false);
        }
        if (detail != null)
        {
            detail.OnChanged += OnDetailChanged;
            detail.OnDeleted += OnItemDeleted;
        }
        Rebuild();
    }

    private void Unwire()
    {
        if (!_wired) return;
        _wired = false;
        if (addButton != null) addButton.onClick.RemoveListener(OnAddClicked);
        if (backButton != null) backButton.onClick.RemoveListener(GoToFolderList);
        if (folderRenameInput != null) folderRenameInput.onEndEdit.RemoveListener(OnRenameEndEdit);
        if (detail != null)
        {
            detail.OnChanged -= OnDetailChanged;
            detail.OnDeleted -= OnItemDeleted;
        }
    }

    // ── 追加（ビューで分岐） ────────────────────────────
    private void OnAddClicked()
    {
        if (_view == LeftView.Folders) AddFolder();
        else AddNote();
    }

    private void AddNote()
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        string targetFolder = _viewFolderId ?? NotebookManager.DefaultMemoFolderId;
        if (nm.GetMemoNotes(null).Count >= MaxTotalNotes) return;               // メモ総数の上限
        if (nm.GetMemoNotes(targetFolder).Count >= MaxNotesPerFolder) return;   // 1フォルダのメモ上限
        var note = nm.AddMemoNote(_viewFolderId);   // 対象フォルダ（すべて時はnull→既定）に空ノート
        Select(note);
        Rebuild();
        if (detail != null) detail.FocusTitle();
    }

    private void AddFolder()
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        if (nm.GetMemoFolders().Count >= MaxFolders) return;   // 上限
        var f = nm.AddMemoFolder(NextFolderName(nm));
        _editingFolderId = f.id;   // 追加直後にその行をリネーム編集に（すぐ命名できる）
        Rebuild();
    }

    private void OnItemDeleted(string id)
    {
        if (_selectedId == id) _selectedId = null;
        Rebuild();
    }

    private void Select(MemoNote note)
    {
        _selectedId = note?.id;
        if (detail != null && note != null) detail.Open(note);
    }

    // 編集保存時：選択行のタイトル/メタだけその場更新（全Rebuildはしない）
    private void OnDetailChanged()
    {
        var nm = NotebookManager.Instance;
        if (nm == null || string.IsNullOrEmpty(_selectedId)) return;
        var note = nm.GetMemoNotes().Find(m => m.id == _selectedId);
        if (note == null) return;
        if (_selTitleTmp != null)
            _selTitleTmp.text = string.IsNullOrEmpty(note.title) ? "（無題）" : note.title;
        if (_selMetaTmp != null) _selMetaTmp.text = FormatMeta(note);
    }

    // ── ナビゲーション ────────────────────────────
    private void GoToFolderList()
    {
        CancelFolderEdit();
        _view = LeftView.Folders;
        Rebuild();
    }

    private void EnterFolder(string folderId)
    {
        CancelFolderEdit();
        _view = LeftView.Notes;
        _viewFolderId = folderId;
        _selectedId = null;
        Rebuild();
    }

    private void CancelFolderEdit()
    {
        _editingFolderId = null;
        _confirmDeleteFolderId = null;
        if (_renameExitCo != null) { StopCoroutine(_renameExitCo); _renameExitCo = null; }
    }

    private string FolderName(string folderId)
    {
        if (folderId == null) return "すべて";
        var nm = NotebookManager.Instance;
        var f = nm != null ? nm.GetMemoFolders().Find(x => x.id == folderId) : null;
        return f != null ? f.name : "すべて";
    }

    private int CountOf(string folderId)
    {
        var nm = NotebookManager.Instance;
        return nm != null ? nm.GetMemoNotes(folderId).Count : 0;
    }

    // ── ヘッダー更新 ────────────────────────────
    private void UpdateHeader()
    {
        if (backButton != null) backButton.gameObject.SetActive(_view != LeftView.Folders);
        // Notesビューでユーザーフォルダなら、フォルダ名をインライン編集に（「すべて」「メモ」は従来の固定表示）
        bool headerEditable = (_view == LeftView.Notes && _viewFolderId != null && _viewFolderId != NotebookManager.DefaultMemoFolderId);
        if (headerEditable)
        {
            EnsureHeaderNameInput();
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (_headerNameHost != null) _headerNameHost.SetActive(true);
            _headerEditingFolderId = _viewFolderId;
            if (_headerNameInput != null && !_headerNameInput.isFocused)
            {
                _suppressInline = true;
                _headerNameInput.text = FolderName(_viewFolderId);
                _suppressInline = false;
            }
        }
        else
        {
            if (_headerNameHost != null) _headerNameHost.SetActive(false);
            if (titleText != null)
            {
                titleText.gameObject.SetActive(true);
                titleText.text = _view == LeftView.Folders ? "メモ" : (_view == LeftView.Trash ? "メモのゴミ箱" : FolderName(_viewFolderId));
            }
            _headerEditingFolderId = null;
        }

        var addLbl = addButton != null ? addButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (_view == LeftView.Trash)
        {
            if (addButton != null) addButton.gameObject.SetActive(false);   // ゴミ箱では追加なし
        }
        else if (_view == LeftView.Folders)
        {
            var nm = NotebookManager.Instance;
            bool canAdd = nm != null && nm.GetMemoFolders().Count < MaxFolders;
            if (addButton != null) addButton.gameObject.SetActive(canAdd);
            if (addLbl != null) addLbl.text = "＋ フォルダ";
        }
        else
        {
            string targetFolder = _viewFolderId ?? NotebookManager.DefaultMemoFolderId;
            bool canAddNote = CountOf(null) < MaxTotalNotes && CountOf(targetFolder) < MaxNotesPerFolder;
            if (addButton != null) addButton.gameObject.SetActive(canAddNote);
            if (addLbl != null) addLbl.text = "＋ 追加";
        }
    }

    // ── リスト構築 ────────────────────────────
    public void Rebuild()
    {
        ParkRenameInput();   // 共有リネーム入力を安全な親へ退避（行破棄で巻き添えにしない）
        UpdateHeader();
        if (listContent == null) return;
        _selTitleInput = null; _selMetaTmp = null; _selectedRowImg = null;
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        var nm = NotebookManager.Instance;
        if (nm == null) return;

        if (_view == LeftView.Folders) BuildFolderList(nm);
        else if (_view == LeftView.Trash) BuildTrashList(nm);
        else BuildNoteList(nm);
    }

    private void BuildNoteList(NotebookManager nm)
    {
        var notes = nm.GetMemoNotes(_viewFolderId);   // null=すべて・ゴミ箱除外・ピン優先→作成日降順
        if (notes.Count == 0) { BuildEmptyLabel(); return; }
        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            // ▲▼は同じピングループ内のみ（ピンは常に上・境界では無効化）
            MemoNote prev = (i > 0 && notes[i - 1].isPinned == note.isPinned) ? notes[i - 1] : null;
            MemoNote next = (i < notes.Count - 1 && notes[i + 1].isPinned == note.isPinned) ? notes[i + 1] : null;
            BuildRow(note, prev, next);
        }
    }

    // ── フォルダ一覧（ドリルダウン） ────────────────────────────
    private void BuildFolderList(NotebookManager nm)
    {
        BuildFolderRow("すべて", null, editable: false, canDelete: false, canReorder: false, prevId: null, nextId: null);                                 // 横断
        BuildFolderRow("メモ", NotebookManager.DefaultMemoFolderId, editable: false, canDelete: false, canReorder: false, prevId: null, nextId: null);      // 既定（リネーム・削除・並べ替え不可。退避先の器なので固定）
        var userFolders = nm.GetMemoFolders().FindAll(x => x.id != NotebookManager.DefaultMemoFolderId);
        for (int i = 0; i < userFolders.Count; i++)
        {
            var f = userFolders[i];
            string prevId = i > 0 ? userFolders[i - 1].id : null;
            string nextId = i < userFolders.Count - 1 ? userFolders[i + 1].id : null;
            BuildFolderRow(f.name, f.id, editable: true, canDelete: true, canReorder: true, prevId: prevId, nextId: nextId);
        }
        BuildTrashEntryRow(nm.GetTrashedMemoNotes().Count);   // 末尾にゴミ箱（件数0でも常時表示）
    }

    private void BuildFolderRow(string label, string folderId, bool editable, bool canDelete, bool canReorder, string prevId, string nextId)
    {
        bool confirming = (folderId != null && folderId == _confirmDeleteFolderId);
        bool editing = !confirming && editable && folderId == _editingFolderId;

        var row = NewUI("Folder_" + (folderId ?? "all"), listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
        // 並べ替え直後のフォルダ行を一瞬フラッシュ（移動の視認性）
        if (_flashFolderId != null && _flashFolderId == folderId)
        {
            _flashFolderId = null;
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashRow(rowImg, false, _flashDelay));
        }

        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 52; rowLE.preferredHeight = 52;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 10, 8, 8);
        hlg.spacing = 14;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        AddIcon(row.transform, folderIcon, UITheme_FocusMode.AccentBlueSolid);

        if (confirming)
        {
            int n = CountOf(folderId);
            // 確認テキスト列：[削除しますか？][メモN件は「メモ」に移動します]
            var col = NewUI("ConfirmText", row.transform);
            var colLE = col.AddComponent<LayoutElement>();
            colLE.minWidth = 0; colLE.flexibleWidth = 1;
            var colVlg = col.AddComponent<VerticalLayoutGroup>();
            colVlg.padding = new RectOffset(0, 0, 0, 0); colVlg.spacing = 1;
            colVlg.childControlWidth = true; colVlg.childControlHeight = true;
            colVlg.childForceExpandWidth = true; colVlg.childForceExpandHeight = false;
            colVlg.childAlignment = TextAnchor.MiddleLeft;
            var q = NewText("Q", col.transform, "削除しますか？", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextPrimary);
            q.alignment = TextAlignmentOptions.MidlineLeft;
            if (n > 0)
            {
                var sub = NewText("Sub", col.transform, "メモ" + n + "件は「メモ」に移動します", UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextMuted);
                sub.alignment = TextAlignmentOptions.MidlineLeft;
            }
            string capId = folderId;
            // やめる（控えめ）
            var cancel = NewUI("Cancel", row.transform);
            var cancelLE = cancel.AddComponent<LayoutElement>();
            cancelLE.minWidth = 56; cancelLE.preferredWidth = 56; cancelLE.minHeight = 32; cancelLE.preferredHeight = 32;
            var cancelImg = cancel.AddComponent<Image>();
            UIStyleKit.ApplyControl(cancelImg);
            cancelImg.color = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.18f);
            var cancelLbl = NewText("Label", cancel.transform, "やめる", UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextSecondary);
            cancelLbl.alignment = TextAlignmentOptions.Center;
            var cancelRt = cancelLbl.GetComponent<RectTransform>();
            cancelRt.anchorMin = Vector2.zero; cancelRt.anchorMax = Vector2.one; cancelRt.offsetMin = Vector2.zero; cancelRt.offsetMax = Vector2.zero;
            var cancelBtn = cancel.AddComponent<Button>();
            cancelBtn.transition = Selectable.Transition.None; cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(() => { CancelFolderEdit(); Rebuild(); });
            // 削除する（濃い赤・白文字）
            var del = NewUI("ConfirmDelete", row.transform);
            var delLE = del.AddComponent<LayoutElement>();
            delLE.minWidth = 72; delLE.preferredWidth = 72; delLE.minHeight = 32; delLE.preferredHeight = 32;
            var delImg = del.AddComponent<Image>();
            UIStyleKit.ApplyControl(delImg);
            delImg.color = new Color(0.80f, 0.29f, 0.29f, 1f);
            var delLbl = NewText("Label", del.transform, "削除する", UITheme_FocusMode.FontCaption, Color.white);
            delLbl.alignment = TextAlignmentOptions.Center;
            var delRt = delLbl.GetComponent<RectTransform>();
            delRt.anchorMin = Vector2.zero; delRt.anchorMax = Vector2.one; delRt.offsetMin = Vector2.zero; delRt.offsetMax = Vector2.zero;
            var delBtn = del.AddComponent<Button>();
            delBtn.transition = Selectable.Transition.None; delBtn.targetGraphic = delImg;
            delBtn.onClick.AddListener(() => DeleteFolderRow(capId));
            return;
        }

        if (editing)
        {
            // 名前スロット（ここへ共有 InputField を寄せる）
            var slot = NewUI("NameSlot", row.transform);
            var slotLE = slot.AddComponent<LayoutElement>();
            slotLE.minWidth = 0; slotLE.flexibleWidth = 1;
            slotLE.minHeight = 26; slotLE.preferredHeight = 26;   // 自前InputFieldは高さを報告しないため明示（高さ0潰れ防止）

            // 削除ボタン（ユーザーフォルダのみ）
            if (canDelete)
            {
                var del = NewUI("Delete", row.transform);
                var delLE = del.AddComponent<LayoutElement>();
                delLE.minWidth = 44; delLE.preferredWidth = 44;
                delLE.minHeight = 32; delLE.preferredHeight = 32;
                var delImg = del.AddComponent<Image>();
                UIStyleKit.ApplyControl(delImg);
                delImg.color = new Color(0.80f, 0.29f, 0.29f, 1f);   // アクティブに見える濃い赤の塗り
                var delLbl = NewText("Label", del.transform, "削除",
                    UITheme_FocusMode.FontCaption, Color.white);   // 塗りの上で読める白文字
                delLbl.alignment = TextAlignmentOptions.Center;
                var delRt = delLbl.GetComponent<RectTransform>();
                delRt.anchorMin = Vector2.zero; delRt.anchorMax = Vector2.one;
                delRt.offsetMin = Vector2.zero; delRt.offsetMax = Vector2.zero;
                string capDel = folderId;
                // onClick(PointerUp)だと、入力フィールドの blur→次フレーム再構築でこのボタンが先に破棄され
                // クリックが発火しない。PointerDown で確定的に削除する（EventSystemの処理順に依存しない）。
                var delTrigger = del.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var delEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
                delEntry.callback.AddListener((_) => EnterConfirmDelete(capDel));
                delTrigger.triggers.Add(delEntry);
            }

            AttachRenameInput(slot.transform, folderId);
            return;
        }

        // 通常表示：[名前(flex)] [件数] [⋯(編集可のみ)]
        var name = NewText("Name", row.transform, label,
            UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextPrimary);
        name.alignment = TextAlignmentOptions.MidlineLeft;
        var nameLE = name.gameObject.AddComponent<LayoutElement>();
        nameLE.minWidth = 0; nameLE.flexibleWidth = 1;

        var count = NewText("Count", row.transform, CountOf(folderId).ToString(),
            UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextMuted);
        count.alignment = TextAlignmentOptions.MidlineRight;
        var countLE = count.gameObject.AddComponent<LayoutElement>();
        countLE.minWidth = 22;

        if (canReorder)
        {
            string capFolder = folderId; string capPrev = prevId; string capNext = nextId;
            BuildReorderButton(row.transform, "\u25B2", capPrev != null, () => {
                if (capPrev != null) { NotebookManager.Instance?.SwapMemoFolderOrder(capFolder, capPrev); _flashFolderId = capFolder; _flashDelay = 0.05f; Rebuild(); }
            });
            BuildReorderButton(row.transform, "\u25BC", capNext != null, () => {
                if (capNext != null) { NotebookManager.Instance?.SwapMemoFolderOrder(capFolder, capNext); _flashFolderId = capFolder; _flashDelay = 0.05f; Rebuild(); }
            });
        }

        if (editable)
        {
            var more = NewUI("More", row.transform);
            var moreLE = more.AddComponent<LayoutElement>();
            moreLE.minWidth = 30; moreLE.preferredWidth = 30;
            moreLE.minHeight = 30; moreLE.preferredHeight = 30;
            var moreTmp = NewText("Glyph", more.transform, "…",
                UITheme_FocusMode.FontBody, UITheme_FocusMode.TextSecondary);
            moreTmp.alignment = TextAlignmentOptions.Center;
            moreTmp.raycastTarget = true;
            var moreRt = moreTmp.GetComponent<RectTransform>();
            moreRt.anchorMin = Vector2.zero; moreRt.anchorMax = Vector2.one;
            moreRt.offsetMin = Vector2.zero; moreRt.offsetMax = Vector2.zero;
            var moreBtn = more.AddComponent<Button>();
            moreBtn.transition = Selectable.Transition.None;
            moreBtn.targetGraphic = moreTmp;
            string capMore = folderId;
            moreBtn.onClick.AddListener(() => StartFolderRename(capMore));
        }

        // 行本体クリック → そのフォルダへ潜る（⋯は子なので競合しない）
        var btn = row.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = rowImg;
        string capRow = folderId;
        btn.onClick.AddListener(() => EnterFolder(capRow));
    }

    private void StartFolderRename(string folderId)
    {
        _editingFolderId = folderId;
        Rebuild();
    }

    // 自前インライン入力からのフォルダ名確定（空は据え置き・削除で解除済みなら無視・編集解除は次フレーム）
    private void CommitFolderRename(string folderId, string newName)
    {
        if (_editingFolderId != folderId) return;   // 削除等で編集解除済みなら誤Renameしない
        var nm = NotebookManager.Instance;
        if (nm != null && !string.IsNullOrWhiteSpace(newName)) nm.RenameMemoFolder(folderId, newName.Trim());
        if (_renameExitCo != null) StopCoroutine(_renameExitCo);
        _renameExitCo = StartCoroutine(ExitFolderEditNextFrame(folderId));
    }

    private void OnRenameEndEdit(string text)
    {
        if (_editingFolderId == null) return;
        var nm = NotebookManager.Instance;
        if (nm != null && !string.IsNullOrWhiteSpace(text)) nm.RenameMemoFolder(_editingFolderId, text.Trim());
        // 編集解除は次フレームに遅延（削除ボタンの同フレーム破棄を避ける）
        if (_renameExitCo != null) StopCoroutine(_renameExitCo);
        _renameExitCo = StartCoroutine(ExitFolderEditNextFrame(_editingFolderId));
    }

    private System.Collections.IEnumerator ExitFolderEditNextFrame(string folderId)
    {
        yield return null;
        _renameExitCo = null;
        if (_editingFolderId == folderId)
        {
            _editingFolderId = null;
            Rebuild();
        }
    }

    // 編集モードの「削除」→ まず確認状態へ（1撃削除を防ぐ・モーダルなし）
    private void EnterConfirmDelete(string folderId)
    {
        CancelFolderEdit();               // 編集解除（_confirmも一旦クリアされる）
        _confirmDeleteFolderId = folderId;
        Rebuild();
    }

    private void DeleteFolderRow(string folderId)
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        CancelFolderEdit();
        ParkRenameInput();
        nm.DeleteMemoFolder(folderId);   // ノートは既定へ退避（NotebookManager側）
        if (_viewFolderId == folderId) _viewFolderId = null;
        Rebuild();
    }

    private void AttachRenameInput(Transform slot, string folderId)
    {
        var nm = NotebookManager.Instance;
        var f = nm != null ? nm.GetMemoFolders().Find(x => x.id == folderId) : null;
        string current = f != null ? f.name : "";
        string capId = folderId;

        // メモのタイトルと同じ自前InputField＋自前キャレット（共有InputFieldは廃止）
        RectTransform caretRT; Image caretImg;
        var input = BuildInlineFieldCore(slot, current, "フォルダ名", out caretRT, out caretImg);
        input.onSelect.AddListener(_ =>
        {
            if (_suppressInline) return;
            ActivateCaret(input, caretRT, caretImg);
        });
        input.onDeselect.AddListener(_ => DeactivateCaret(input));
        input.onEndEdit.AddListener(v =>
        {
            if (_suppressInline) return;
            CommitFolderRename(capId, v);
        });

        // 「…」で編集に入った直後に自動フォーカス（カーソルは末尾）
        input.Select();
        input.ActivateInputField();
        int caret = current != null ? current.Length : 0;
        input.caretPosition = caret;
        input.selectionAnchorPosition = caret;
        input.selectionFocusPosition = caret;
        ActivateCaret(input, caretRT, caretImg);
    }

    private void ParkRenameInput()
    {
        if (folderRenameInput == null) return;
        folderRenameInput.DeactivateInputField();
        folderRenameInput.gameObject.SetActive(false);
        if (_renameParkParent != null && folderRenameInput.transform.parent != _renameParkParent)
            folderRenameInput.transform.SetParent(_renameParkParent, false);
    }

    // ───────── メモ行の編集/削除（フォルダ方式をミラー）─────────
    private void StartNoteRename(string noteId)
    {
        _editingNoteId = noteId;
        Rebuild();
    }

    private void CommitNoteRename(string noteId, string newName)
    {
        if (_editingNoteId != noteId) return;
        var nm = NotebookManager.Instance;
        var note = nm != null ? nm.GetMemoNotes().Find(m => m.id == noteId) : null;
        if (note != null)
        {
            string nt = (newName ?? "").Trim();   // メモは空タイトルも許可
            if (note.title != nt)
            {
                note.title = nt;
                nm.UpdateMemoNote(note);
                if (detail != null) detail.RefreshTitleIfOpen(noteId, nt);
            }
        }
        if (_noteRenameExitCo != null) StopCoroutine(_noteRenameExitCo);
        _noteRenameExitCo = StartCoroutine(ExitNoteEditNextFrame(noteId));
    }

    private System.Collections.IEnumerator ExitNoteEditNextFrame(string noteId)
    {
        yield return null;
        _noteRenameExitCo = null;
        if (_editingNoteId == noteId) { _editingNoteId = null; Rebuild(); }
    }

    private void EnterConfirmDeleteNote(string noteId)
    {
        CancelNoteEdit();
        _confirmDeleteNoteId = noteId;
        Rebuild();
    }

    private void CancelNoteEdit()
    {
        _editingNoteId = null;
        _confirmDeleteNoteId = null;
        if (_noteRenameExitCo != null) { StopCoroutine(_noteRenameExitCo); _noteRenameExitCo = null; }
    }

    private void DeleteNoteRow(string noteId)
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        CancelNoteEdit();
        nm.TrashMemoNote(noteId);                       // ゴミ箱へ（メモはソフト削除）
        if (detail != null && detail.CurrentId == noteId) detail.Clear();
        if (_selectedId == noteId) _selectedId = null;
        Rebuild();
    }

    private void AttachNoteRenameInput(Transform slot, MemoNote note)
    {
        string capId = note.id;
        string current = note.title ?? "";
        RectTransform caretRT; Image caretImg;
        var input = BuildInlineFieldCore(slot, current, "（無題）", out caretRT, out caretImg, UITheme_FocusMode.FontChipTitle);
        input.onSelect.AddListener(_ =>
        {
            if (_suppressInline) return;
            ActivateCaret(input, caretRT, caretImg);
        });
        input.onDeselect.AddListener(_ => DeactivateCaret(input));
        input.onEndEdit.AddListener(v =>
        {
            if (_suppressInline) return;
            CommitNoteRename(capId, v);
        });
        input.Select();
        input.ActivateInputField();
        int caret = current.Length;
        input.caretPosition = caret;
        input.selectionAnchorPosition = caret;
        input.selectionFocusPosition = caret;
        ActivateCaret(input, caretRT, caretImg);
    }

    private string NextFolderName(NotebookManager nm)
    {
        var folders = nm.GetMemoFolders();
        System.Func<string,bool> exists = (s) => { foreach (var f in folders) if (f.name == s) return true; return false; };
        string baseName = "新規フォルダ";
        if (!exists(baseName)) return baseName;
        int n = 2;
        while (exists(baseName + " " + n)) n++;
        return baseName + " " + n;
    }

    // ── ノート行（M-1/M-2a・無改変） ────────────────────────────
    private void BuildRow(MemoNote note, MemoNote prevInGroup, MemoNote nextInGroup)
    {
        bool confirming = (note.id == _confirmDeleteNoteId);
        bool editing = !confirming && (note.id == _editingNoteId);
        bool selected = note.id == _selectedId;
        var captured = note;

        var row = NewUI("Row_" + note.id, listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = selected ? UITheme_FocusMode.SelectedBG : UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
        if (_flashNoteId == note.id)
        {
            _flashNoteId = null;
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashRow(rowImg, selected, _flashDelay));
        }

        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 60; rowLE.preferredHeight = 60;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 9, 9);
        hlg.spacing = 14;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        AddIcon(row.transform, noteIcon, UITheme_FocusMode.TextMuted);

        // ── 削除確認状態 ──
        if (confirming)
        {
            var col = NewUI("ConfirmText", row.transform);
            var colLE = col.AddComponent<LayoutElement>(); colLE.minWidth = 0; colLE.flexibleWidth = 1;
            var colVlg = col.AddComponent<VerticalLayoutGroup>();
            colVlg.padding = new RectOffset(0, 0, 0, 0); colVlg.spacing = 1;
            colVlg.childControlWidth = true; colVlg.childControlHeight = true;
            colVlg.childForceExpandWidth = true; colVlg.childForceExpandHeight = false;
            colVlg.childAlignment = TextAnchor.MiddleLeft;
            var q = NewText("Q", col.transform, "削除しますか？", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextPrimary);
            q.alignment = TextAlignmentOptions.MidlineLeft;
            var sub = NewText("Sub", col.transform, "ゴミ箱に移動します", UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextMuted);
            sub.alignment = TextAlignmentOptions.MidlineLeft;
            string capId = note.id;
            var cancel = NewUI("Cancel", row.transform);
            var cancelLE = cancel.AddComponent<LayoutElement>(); cancelLE.minWidth = 56; cancelLE.preferredWidth = 56; cancelLE.minHeight = 32; cancelLE.preferredHeight = 32;
            var cancelImg = cancel.AddComponent<Image>(); UIStyleKit.ApplyControl(cancelImg); cancelImg.color = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.18f);
            var cancelLbl = NewText("Label", cancel.transform, "やめる", UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextSecondary); cancelLbl.alignment = TextAlignmentOptions.Center;
            var cancelRt = cancelLbl.GetComponent<RectTransform>(); cancelRt.anchorMin = Vector2.zero; cancelRt.anchorMax = Vector2.one; cancelRt.offsetMin = Vector2.zero; cancelRt.offsetMax = Vector2.zero;
            var cancelBtn = cancel.AddComponent<Button>(); cancelBtn.transition = Selectable.Transition.None; cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(() => { CancelNoteEdit(); Rebuild(); });
            var del = NewUI("ConfirmDelete", row.transform);
            var delLE = del.AddComponent<LayoutElement>(); delLE.minWidth = 72; delLE.preferredWidth = 72; delLE.minHeight = 32; delLE.preferredHeight = 32;
            var delImg = del.AddComponent<Image>(); UIStyleKit.ApplyControl(delImg); delImg.color = new Color(0.80f, 0.29f, 0.29f, 1f);
            var delLbl = NewText("Label", del.transform, "削除する", UITheme_FocusMode.FontCaption, Color.white); delLbl.alignment = TextAlignmentOptions.Center;
            var delRt = delLbl.GetComponent<RectTransform>(); delRt.anchorMin = Vector2.zero; delRt.anchorMax = Vector2.one; delRt.offsetMin = Vector2.zero; delRt.offsetMax = Vector2.zero;
            var delBtn = del.AddComponent<Button>(); delBtn.transition = Selectable.Transition.None; delBtn.targetGraphic = delImg;
            delBtn.onClick.AddListener(() => DeleteNoteRow(capId));
            return;
        }

        // テキスト列（タイトル＋メタ）
        var textCol = NewUI("TextCol", row.transform);
        var tcLE = textCol.AddComponent<LayoutElement>(); tcLE.minWidth = 0; tcLE.flexibleWidth = 1;
        var tcVlg = textCol.AddComponent<VerticalLayoutGroup>();
        tcVlg.padding = new RectOffset(0, 0, 0, 0); tcVlg.spacing = 3;
        tcVlg.childControlWidth = true; tcVlg.childControlHeight = true;
        tcVlg.childForceExpandWidth = true; tcVlg.childForceExpandHeight = false;
        tcVlg.childAlignment = TextAnchor.MiddleLeft;

        var titleHost = NewUI("TitleHost", textCol.transform);
        var thLE = titleHost.AddComponent<LayoutElement>(); thLE.minHeight = 26; thLE.preferredHeight = 26; thLE.flexibleWidth = 1;
        if (editing)
        {
            AttachNoteRenameInput(titleHost.transform, note);
        }
        else
        {
            string disp = string.IsNullOrEmpty(note.title) ? "（無題）" : note.title;
            Color tcol = string.IsNullOrEmpty(note.title) ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextPrimary;
            var titleTmp = NewText("Title", titleHost.transform, disp, UITheme_FocusMode.FontChipTitle, tcol);
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var ttRt = titleTmp.GetComponent<RectTransform>(); ttRt.anchorMin = Vector2.zero; ttRt.anchorMax = Vector2.one; ttRt.offsetMin = Vector2.zero; ttRt.offsetMax = Vector2.zero;
            if (selected) _selTitleTmp = titleTmp;
        }

        var meta = NewText("Meta", textCol.transform, FormatMeta(note), UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextMuted);
        meta.alignment = TextAlignmentOptions.MidlineLeft;
        if (selected) { _selMetaTmp = meta; _selectedRowImg = rowImg; }

        // ── 編集状態：削除ボタン（…で入る。名前＋削除のみ）──
        if (editing)
        {
            var del = NewUI("Delete", row.transform);
            var delLE = del.AddComponent<LayoutElement>(); delLE.minWidth = 44; delLE.preferredWidth = 44; delLE.minHeight = 32; delLE.preferredHeight = 32;
            var delImg = del.AddComponent<Image>(); UIStyleKit.ApplyControl(delImg); delImg.color = new Color(0.80f, 0.29f, 0.29f, 1f);
            var delLbl = NewText("Label", del.transform, "削除", UITheme_FocusMode.FontCaption, Color.white); delLbl.alignment = TextAlignmentOptions.Center;
            var delRt = delLbl.GetComponent<RectTransform>(); delRt.anchorMin = Vector2.zero; delRt.anchorMax = Vector2.one; delRt.offsetMin = Vector2.zero; delRt.offsetMax = Vector2.zero;
            string capDel = note.id;
            var delTrigger = del.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var delEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            delEntry.callback.AddListener((_) => EnterConfirmDeleteNote(capDel));
            delTrigger.triggers.Add(delEntry);
            return;
        }

        // ── 通常状態：▲▼ / ピン / … / 行クリック=選択 ──
        var capPrev = prevInGroup; var capNext = nextInGroup;
        BuildReorderButton(row.transform, "▲", capPrev != null, () => {
            if (capPrev != null) { NotebookManager.Instance?.SwapMemoNoteOrder(captured.id, capPrev.id); _flashNoteId = captured.id; _flashDelay = 0.05f; Rebuild(); }
        });
        BuildReorderButton(row.transform, "▼", capNext != null, () => {
            if (capNext != null) { NotebookManager.Instance?.SwapMemoNoteOrder(captured.id, capNext.id); _flashNoteId = captured.id; _flashDelay = 0.05f; Rebuild(); }
        });

        var pin = NewUI("PinBtn", row.transform);
        var pinLE = pin.AddComponent<LayoutElement>(); pinLE.minWidth = 28; pinLE.preferredWidth = 28; pinLE.minHeight = 28; pinLE.preferredHeight = 28;
        var pinImg = pin.AddComponent<Image>(); pinImg.preserveAspect = true; pinImg.sprite = note.isPinned ? pinOnSprite : pinOffSprite;
        Color offColor = UITheme_FocusMode.TextMuted; offColor.a = 0.5f;
        pinImg.color = note.isPinned ? UITheme_FocusMode.AccentBlueSolid : offColor; pinImg.raycastTarget = true;
        var pinBtn = pin.AddComponent<Button>(); pinBtn.transition = Selectable.Transition.None; pinBtn.targetGraphic = pinImg;
        pinBtn.onClick.AddListener(() => OnPinClicked(captured, pinImg));

        var more = NewUI("More", row.transform);
        var moreLE = more.AddComponent<LayoutElement>(); moreLE.minWidth = 30; moreLE.preferredWidth = 30; moreLE.minHeight = 30; moreLE.preferredHeight = 30;
        var moreTmp = NewText("Glyph", more.transform, "…", UITheme_FocusMode.FontBody, UITheme_FocusMode.TextSecondary); moreTmp.alignment = TextAlignmentOptions.Center; moreTmp.raycastTarget = true;
        var moreRt = moreTmp.GetComponent<RectTransform>(); moreRt.anchorMin = Vector2.zero; moreRt.anchorMax = Vector2.one; moreRt.offsetMin = Vector2.zero; moreRt.offsetMax = Vector2.zero;
        var moreBtn = more.AddComponent<Button>(); moreBtn.transition = Selectable.Transition.None; moreBtn.targetGraphic = moreTmp;
        string capMore = note.id;
        moreBtn.onClick.AddListener(() => StartNoteRename(capMore));

        var btn = row.AddComponent<Button>();
        btn.transition = Selectable.Transition.None; btn.targetGraphic = rowImg;
        btn.onClick.AddListener(() => { Select(captured); Rebuild(); });
    }

    // ── ゴミ箱 ──────────────────────────────────────
    private void EnterTrash()
    {
        CancelFolderEdit();
        _view = LeftView.Trash;
        _selectedId = null;
        Rebuild();
    }

    // フォルダ一覧末尾の「ゴミ箱」行（タップでゴミ箱ビューへ）。
    private void BuildTrashEntryRow(int count)
    {
        var row = NewUI("Folder_trash", listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 52; rowLE.preferredHeight = 52;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 10, 8, 8);
        hlg.spacing = 14;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        AddIcon(row.transform, trashIcon, UITheme_FocusMode.TextMuted);
        var name = NewText("Name", row.transform, "メモのゴミ箱", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextPrimary);
        name.alignment = TextAlignmentOptions.MidlineLeft;
        var nameLE = name.gameObject.AddComponent<LayoutElement>();
        nameLE.minWidth = 0; nameLE.flexibleWidth = 1;
        var c = NewText("Count", row.transform, count.ToString(), UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextMuted);
        c.alignment = TextAlignmentOptions.MidlineRight;
        var cLE = c.gameObject.AddComponent<LayoutElement>();
        cLE.minWidth = 22;
        var btn = row.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = rowImg;
        btn.onClick.AddListener(() => EnterTrash());
    }

    private void BuildTrashList(NotebookManager nm)
    {
        var notes = nm.GetTrashedMemoNotes();
        notes.Sort((a, b) => string.CompareOrdinal(b.deletedAt, a.deletedAt));   // 新しく削除した順
        if (notes.Count == 0)
        {
            var empty = NewUI("Empty", listContent);
            var le = empty.AddComponent<LayoutElement>();
            le.minHeight = 80;
            var label = NewText("Label", empty.transform, "メモのゴミ箱は空です", UITheme_FocusMode.FontBody, UITheme_FocusMode.TextPlaceholder);
            label.alignment = TextAlignmentOptions.Center;
            var rt = label.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return;
        }
        foreach (var note in notes) BuildTrashRow(nm, note);
    }

    private void BuildTrashRow(NotebookManager nm, MemoNote note)
    {
        var captured = note;
        var row = NewUI("Trash_" + note.id, listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 60; rowLE.preferredHeight = 60;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 9, 9);
        hlg.spacing = 14;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        AddIcon(row.transform, noteIcon, UITheme_FocusMode.TextMuted);
        var textCol = NewUI("TextCol", row.transform);
        var colLE = textCol.AddComponent<LayoutElement>();
        colLE.minWidth = 0; colLE.flexibleWidth = 1;
        var colVlg = textCol.AddComponent<VerticalLayoutGroup>();
        colVlg.padding = new RectOffset(0, 0, 0, 0);
        colVlg.spacing = 3;
        colVlg.childControlWidth = true; colVlg.childControlHeight = true;
        colVlg.childForceExpandWidth = true; colVlg.childForceExpandHeight = false;
        colVlg.childAlignment = TextAnchor.MiddleLeft;
        bool untitled = string.IsNullOrWhiteSpace(note.title);
        var title = NewText("Title", textCol.transform, untitled ? "（無題）" : note.title, UITheme_FocusMode.FontChipTitle, untitled ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextPrimary);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        int left = nm.MemoTrashDaysLeft(note);
        string metaStr = left <= 0 ? "まもなく完全削除" : ("あと" + left + "日で完全削除");
        var meta = NewText("Meta", textCol.transform, metaStr, UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextMuted);
        meta.alignment = TextAlignmentOptions.MidlineLeft;
        BuildTrashActionButton(row.transform, "復元", false, () => { NotebookManager.Instance?.RestoreMemoNote(captured.id); Rebuild(); });
        BuildTrashActionButton(row.transform, "削除", true,  () => { NotebookManager.Instance?.DeleteMemoNotePermanently(captured.id); Rebuild(); });
    }

    // ゴミ箱の行アクション（復元=青 / 削除=赤）。Selectable.None で明示色を保つ。
    private void BuildTrashActionButton(Transform parent, string label, bool danger, UnityEngine.Events.UnityAction onClick)
    {
        var go = NewUI("Action", parent);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 52; le.preferredWidth = 52; le.minHeight = 32; le.preferredHeight = 32;
        var img = go.AddComponent<Image>();
        UIStyleKit.ApplyControl(img);
        img.color = danger ? new Color(0.80f, 0.29f, 0.29f, 1f) : UITheme_FocusMode.AccentBlueSolid;   // アクティブに見える塗り（削除=赤 / 復元=青）
        var lbl = NewText("Label", go.transform, label, UITheme_FocusMode.FontCaption, Color.white);   // 塗りの上で読める白文字
        lbl.alignment = TextAlignmentOptions.Center;
        var rt = lbl.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
    }

    // ── インライン編集タイトル入力（Memo版：空タイトル許可・確定で右ペイン同期）──
    private TMP_InputField BuildInlineTitleInput(Transform parent, MemoNote note)
    {
        var captured = note;
        var fieldGO = NewUI("TitleInput", parent);
        var fieldRT = fieldGO.GetComponent<RectTransform>();
        fieldRT.anchorMin = Vector2.zero; fieldRT.anchorMax = Vector2.one;
        fieldRT.offsetMin = Vector2.zero; fieldRT.offsetMax = Vector2.zero;
        var fieldImg = fieldGO.AddComponent<Image>();
        fieldImg.color = Color.clear;
        var taGO = NewUI("TextArea", fieldGO.transform);
        var taRT = taGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(2f, 0f); taRT.offsetMax = new Vector2(-2f, 0f);
        taGO.AddComponent<RectMask2D>();
        var txtTMP = NewText("Text", taGO.transform, note.title ?? "", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextPrimary);
        var txtRT = txtTMP.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var phTMP = NewText("Placeholder", taGO.transform, "（無題）", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextMuted);
        var phRT = phTMP.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var caretGO = NewUI("CustomCaret", taGO.transform);
        var caretRT = caretGO.GetComponent<RectTransform>();
        caretRT.anchorMin = new Vector2(0f, 1f); caretRT.anchorMax = new Vector2(0f, 1f);
        caretRT.pivot = new Vector2(0f, 1f);
        caretRT.sizeDelta = new Vector2(2f, 16f);
        caretRT.anchoredPosition = Vector2.zero;
        var caretImg = caretGO.AddComponent<Image>();
        caretImg.color = Color.clear;
        caretImg.raycastTarget = false;
        var input = fieldGO.AddComponent<TMP_InputField>();
        input.targetGraphic = fieldImg;
        input.textViewport  = taRT;
        input.textComponent = txtTMP;
        input.placeholder   = phTMP;
        input.lineType      = TMP_InputField.LineType.SingleLine;
        input.text          = note.title ?? "";
        input.customCaretColor = true;
        input.caretColor       = Color.clear;
        input.caretWidth       = 2;
        input.selectionColor   = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.4f);
        input.onSelect.AddListener(_ =>
        {
            if (_suppressInline) return;
            Select(captured);
            ActivateCaret(input, caretRT, caretImg);
        });
        input.onDeselect.AddListener(_ => DeactivateCaret(input));
        input.onEndEdit.AddListener(v =>
        {
            if (_suppressInline) return;
            if (captured.title == v) return;
            captured.title = v;                       // 空タイトルも許可
            NotebookManager.Instance?.UpdateMemoNote(captured);
            if (_selMetaTmp != null && captured.id == _selectedId) _selMetaTmp.text = FormatMeta(captured);
            if (detail != null) detail.RefreshTitleIfOpen(captured.id, v);
        });
        return input;
    }

    // フォーカス＝選択を即ハイライト（Rebuildすると入力欄が破棄されるため色だけ直接更新）。
    private void HighlightRowImmediate(Image rowImg)
    {
        if (_selectedRowImg != null && _selectedRowImg != rowImg) _selectedRowImg.color = UITheme_FocusMode.PanelBG;
        if (rowImg != null) rowImg.color = UITheme_FocusMode.SelectedBG;
        _selectedRowImg = rowImg;
    }

    private void LateUpdate()
    {
        if (_activeInput == null || !_activeInput.isFocused) return;
        if (_activeInput.caretPosition != _lastCaretPos)
        {
            _lastCaretPos = _activeInput.caretPosition;
            UpdateActiveCaret();
            RestartCaretBlink();
        }
    }

    private void ActivateCaret(TMP_InputField input, RectTransform caretRT, Image caretImg)
    {
        _activeInput = input; _activeCaretRT = caretRT; _activeCaretImg = caretImg;
        _lastCaretPos = -1;
        UpdateActiveCaret();
        RestartCaretBlink();
    }

    private void DeactivateCaret(TMP_InputField input)
    {
        if (_activeInput != input) { if (input == null) return; }
        if (_caretCo != null) { StopCoroutine(_caretCo); _caretCo = null; }
        if (_activeCaretImg != null) _activeCaretImg.color = Color.clear;
        if (_activeInput == input)
        {
            _activeInput = null; _activeCaretRT = null; _activeCaretImg = null; _lastCaretPos = -1;
        }
    }

    private void RestartCaretBlink()
    {
        if (_caretCo != null) StopCoroutine(_caretCo);
        _caretCo = StartCoroutine(CaretBlinkCo());
    }

    private System.Collections.IEnumerator CaretBlinkCo()
    {
        bool visible = true;
        var wfs = new WaitForSeconds(0.53f);
        do
        {
            if (_activeCaretImg != null) _activeCaretImg.color = visible ? CaretColor : Color.clear;
            visible = !visible;
            yield return wfs;
        } while (_activeInput != null && _activeInput.isFocused);
        if (_activeCaretImg != null) _activeCaretImg.color = Color.clear;
    }

    private void UpdateActiveCaret()
    {
        if (_activeCaretRT == null || _activeInput?.textComponent == null) return;
        _activeInput.ForceLabelUpdate();
        var txt = _activeInput.textComponent;
        txt.ForceMeshUpdate(true, true);
        var info = txt.textInfo;
        var taRect = _activeCaretRT.parent.GetComponent<RectTransform>().rect;
        int cp = _activeInput.caretPosition;
        float caretX, caretY, caretH;
        float caretPad = 0f;
        if (info == null || info.characterCount == 0 || cp <= 0 || info.lineCount == 0)
        {
            // 空（プレースホルダ表示中）：実際に見えているプレースホルダの行に合わせる
            // （空テキストの行は垂直アライメントの都合で上にズレるため、それは使わない）
            var ph = _activeInput.placeholder as TMP_Text;
            TMP_TextInfo phi = null;
            if (ph != null) { ph.ForceMeshUpdate(); phi = ph.textInfo; }
            if (phi != null && phi.lineCount > 0)
            {
                var pl0 = phi.lineInfo[0];
                caretY = pl0.ascender; caretH = Mathf.Max(pl0.ascender - pl0.descender, 1f);
                caretX = (phi.characterCount > 0) ? phi.characterInfo[0].origin : taRect.xMin;
            }
            else if (info != null && info.lineCount > 0)
            {
                var li0 = info.lineInfo[0];
                caretY = li0.ascender; caretH = Mathf.Max(li0.ascender - li0.descender, 1f);
                caretX = taRect.xMin;
            }
            else { caretY = taRect.yMax; caretH = txt.fontSize * 1.15f; caretX = taRect.xMin; }
            caretPad = 0f;
        }
        else
        {
            int idx = Mathf.Clamp(cp - 1, 0, info.characterCount - 1);
            var ci = info.characterInfo[idx];
            caretX = ci.xAdvance;
            int li = Mathf.Clamp(ci.lineNumber, 0, info.lineCount - 1);
            var line = info.lineInfo[li];
            caretY = line.ascender; caretH = Mathf.Max(line.ascender - line.descender, 1f);
            caretPad = CARET_X_PAD;
        }
        _activeCaretRT.anchoredPosition = new Vector2(caretX - taRect.xMin + caretPad, caretY - taRect.yMax);
        _activeCaretRT.sizeDelta = new Vector2(2f, caretH);
    }

    // 自前キャレット付きインライン入力の「箱」だけ作る共通ヘルパー（onSelect/onEndEdit等の配線は呼び出し側）。
    private TMP_InputField BuildInlineFieldCore(Transform parent, string initial, string placeholder,
                                                out RectTransform caretRT, out Image caretImg, float fontSize = -1f)
    {
        float fs = fontSize > 0f ? fontSize : UITheme_FocusMode.FontChipTitle;
        var fieldGO = NewUI("InlineInput", parent);
        var fieldRT = fieldGO.GetComponent<RectTransform>();
        fieldRT.anchorMin = Vector2.zero; fieldRT.anchorMax = Vector2.one;
        fieldRT.offsetMin = Vector2.zero; fieldRT.offsetMax = Vector2.zero;
        var fieldImg = fieldGO.AddComponent<Image>();
        fieldImg.color = Color.clear;
        var taGO = NewUI("TextArea", fieldGO.transform);
        var taRT = taGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(2f, 0f); taRT.offsetMax = new Vector2(-2f, 0f);
        taGO.AddComponent<RectMask2D>();
        var txtTMP = NewText("Text", taGO.transform, initial ?? "", fs, UITheme_FocusMode.TextPrimary);
        var txtRT = txtTMP.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var phTMP = NewText("Placeholder", taGO.transform, placeholder ?? "", fs, UITheme_FocusMode.TextMuted);
        var phRT = phTMP.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var caretGO = NewUI("CustomCaret", taGO.transform);
        caretRT = caretGO.GetComponent<RectTransform>();
        caretRT.anchorMin = new Vector2(0f, 1f); caretRT.anchorMax = new Vector2(0f, 1f);
        caretRT.pivot = new Vector2(0f, 1f);
        caretRT.sizeDelta = new Vector2(2f, 16f);
        caretRT.anchoredPosition = Vector2.zero;
        caretImg = caretGO.AddComponent<Image>();
        caretImg.color = Color.clear;
        caretImg.raycastTarget = false;
        var input = fieldGO.AddComponent<TMP_InputField>();
        input.targetGraphic = fieldImg;
        input.textViewport  = taRT;
        input.textComponent = txtTMP;
        input.placeholder   = phTMP;
        input.lineType      = TMP_InputField.LineType.SingleLine;
        input.text          = initial ?? "";
        input.customCaretColor = true;
        input.caretColor       = Color.clear;
        input.caretWidth       = 2;
        input.selectionColor   = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.4f);
        return input;
    }

    // ヘッダー(Notesビュー)のフォルダ名インライン入力を一度だけ生成（TopBar の TitleText 直後に置く）
    private void EnsureHeaderNameInput()
    {
        if (_headerNameInput != null) return;
        if (titleText == null) return;
        var topBar = titleText.transform.parent;
        _headerNameHost = NewUI("HeaderNameHost", topBar);
        var le = _headerNameHost.AddComponent<LayoutElement>();
        le.minWidth = 0; le.flexibleWidth = 1; le.minHeight = 26; le.preferredHeight = 26;
        _headerNameHost.transform.SetSiblingIndex(titleText.transform.GetSiblingIndex() + 1);
        RectTransform caretRT; Image caretImg;
        _headerNameInput = BuildInlineFieldCore(_headerNameHost.transform, "", "フォルダ名", out caretRT, out caretImg, titleText.fontSize);
        _headerNameInput.onSelect.AddListener(_ =>
        {
            if (_suppressInline) return;
            ActivateCaret(_headerNameInput, caretRT, caretImg);
        });
        _headerNameInput.onDeselect.AddListener(_ => DeactivateCaret(_headerNameInput));
        _headerNameInput.onEndEdit.AddListener(v =>
        {
            if (_suppressInline) return;
            var fid = _headerEditingFolderId;
            if (string.IsNullOrEmpty(fid)) return;
            if (!string.IsNullOrWhiteSpace(v)) NotebookManager.Instance?.RenameMemoFolder(fid, v.Trim());
            UpdateHeader();   // 表示反映（空は据え置き＝元の名前に戻す）
        });
        _headerNameHost.SetActive(false);
    }

    private void BuildEmptyLabel()
    {
        var empty = NewUI("Empty", listContent);
        var le = empty.AddComponent<LayoutElement>();
        le.minHeight = 80;
        var label = NewText("Label", empty.transform,
            "メモはありません。「+ 追加」から作成できます",
            UITheme_FocusMode.FontBody, UITheme_FocusMode.TextPlaceholder);
        label.alignment = TextAlignmentOptions.Center;
        var rt = label.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static readonly string[] _jpDow = { "日", "月", "火", "水", "木", "金", "土" };
    private static string FormatMeta(MemoNote note)
    {
        // 更新日時を「更新 M/d HH:mm」で。createdAt/updatedAt は "yyyy-MM-dd HH:mm"。
        string src = string.IsNullOrEmpty(note.updatedAt) ? note.createdAt : note.updatedAt;
        if (System.DateTime.TryParse(src, out var d))
            return $"更新 {d.Month}/{d.Day}（{_jpDow[(int)d.DayOfWeek]}） {d:HH:mm}";
        return src ?? "";
    }

    // ピン操作：① 押した位置で星の色を変える → ② ひと呼吸おいて Rebuild で移動 → ③ 移動先でフラッシュ。
    private void OnPinClicked(MemoNote note, Image starImg)
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        bool newPinned = !note.isPinned;

        // ① 移動前に、押した星の色をその場で切り替える（クリックの手応え）
        if (starImg != null)
        {
            starImg.sprite = newPinned ? pinOnSprite : pinOffSprite;
            Color off = UITheme_FocusMode.TextMuted; off.a = 0.5f;
            starImg.color = newPinned ? UITheme_FocusMode.AccentBlueSolid : off;
        }
        // データは即コミット（半端な状態を作らない）。updatedAt は更新しない。
        nm.SetMemoNotePinned(note.id, newPinned);

        // ② 星が変わったのを少し見せてから移動（Rebuild）。
        if (_pinCo != null) StopCoroutine(_pinCo);
        _pinCo = StartCoroutine(PinMoveCo(note.id));
    }

    private System.Collections.IEnumerator PinMoveCo(string id)
    {
        float e = 0f, hold = 0.18f;          // ①の星を見せる間
        while (e < hold) { e += Time.unscaledDeltaTime; yield return null; }
        _flashNoteId = id;                    // ③ 移動先の行をフラッシュ
        _flashDelay  = 0.05f;                 // ①でリードインは済ませたので移動直後にすぐ光らせる
        Rebuild();
        _pinCo = null;
    }

    // 行を一瞬ハイライトしてフェードで戻す（クリックの結果が目で追える）。TodoListUI と同方式。
    private System.Collections.IEnumerator FlashRow(Image rowImg, bool selected, float startDelay)
    {
        if (rowImg == null) yield break;
        Color baseColor = selected ? UITheme_FocusMode.SelectedBG : UITheme_FocusMode.PanelBG;
        Color flashColor = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.32f);

        float e = 0f;
        while (e < startDelay) { if (rowImg == null) yield break; e += Time.unscaledDeltaTime; yield return null; }

        float dur = 0.9f; e = 0f;                    // 点灯した瞬間が最大→なめらかに減衰（山は1つ）
        while (e < dur)
        {
            if (rowImg == null) yield break;
            e += Time.unscaledDeltaTime;
            float pr = Mathf.Clamp01(e / dur);
            float k = (1f - pr) * (1f - pr);
            rowImg.color = Color.Lerp(baseColor, flashColor, k);
            yield return null;
        }
        if (rowImg != null) rowImg.color = baseColor;
        _flashCo = null;
    }

    // 行頭アイコン（フォルダ/メモの識別）。スプライト未配線なら何もしない＝レイアウト不変。
    private void AddIcon(Transform row, Sprite sprite, Color tint)
    {
        if (sprite == null) return;
        var go = NewUI("Icon", row);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 20; le.preferredWidth = 20;
        le.minHeight = 20; le.preferredHeight = 20;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = tint;
        img.raycastTarget = false;
    }

    // ── ヘルパー（TodoListUI と同方式） ──
    // ───────── 並べ替え ▲▼ ボタン（Todoと同方式）─────────
    private const float REORDER_GLYPH_SIZE = 14f;
    private void BuildReorderButton(Transform parent, string glyph, bool enabled, UnityEngine.Events.UnityAction onClick)
    {
        var go = NewUI("ReorderBtn", parent);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 26; le.preferredWidth = 26; le.minHeight = 30;
        var bg = go.AddComponent<Image>();
        bg.sprite = null; bg.type = Image.Type.Simple; bg.color = Color.clear;
        // Kotonoruは▲(U+25B2)を持たずフォールバックでサイズが食い違うため、両グリフを持つ日本語フォントに固定。
        var txt = NewText("Arrow", go.transform, glyph, UITheme_FocusMode.FontCaption,
            enabled ? UITheme_FocusMode.TextSecondary : UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.28f));
        var jp = GetJpFallbackFont();
        if (jp != null) txt.font = jp;
        txt.fontSize = REORDER_GLYPH_SIZE;
        txt.enableAutoSizing = false;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        if (enabled)
        {
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.normalColor      = Color.clear;
            cb.highlightedColor = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextSecondary, 0.22f);
            cb.pressedColor     = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.55f);
            cb.selectedColor    = Color.clear;
            cb.disabledColor    = Color.clear;
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);
        }
    }

    private TMP_FontAsset _jpFallbackCache;
    private TMP_FontAsset GetJpFallbackFont()
    {
        if (_jpFallbackCache != null) return _jpFallbackCache;
        var settings = TMPro.TMP_Settings.fallbackFontAssets;
        if (settings != null && settings.Count > 0) _jpFallbackCache = settings[0];
        if (_jpFallbackCache == null)
            _jpFallbackCache = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansJP-Regular SDF");
        return _jpFallbackCache;
    }

    private GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = NewUI(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font != null ? font : TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = false;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }
}
