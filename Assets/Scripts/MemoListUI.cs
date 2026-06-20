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
    private Transform _renameParkParent;       // folderRenameInput の待避先（初期親）
    private Coroutine _renameExitCo;           // リネーム確定後の編集解除（次フレーム）

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

    private TextMeshProUGUI _selTitleTmp;
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
        {
            bool untitled = string.IsNullOrWhiteSpace(note.title);
            _selTitleTmp.text = untitled ? "（無題）" : note.title;
            _selTitleTmp.color = untitled ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextPrimary;
        }
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
        if (titleText != null) titleText.text = _view == LeftView.Folders ? "メモ" : (_view == LeftView.Trash ? "ゴミ箱" : FolderName(_viewFolderId));

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
        _selTitleTmp = null; _selMetaTmp = null;
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
        BuildFolderRow("メモ", NotebookManager.DefaultMemoFolderId, editable: true, canDelete: false, canReorder: false, prevId: null, nextId: null);       // 既定（リネーム可・削除不可）
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
        bool editing = editable && folderId == _editingFolderId;

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

        if (editing)
        {
            // 名前スロット（ここへ共有 InputField を寄せる）
            var slot = NewUI("NameSlot", row.transform);
            var slotLE = slot.AddComponent<LayoutElement>();
            slotLE.minWidth = 0; slotLE.flexibleWidth = 1;

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
                delEntry.callback.AddListener((_) => DeleteFolderRow(capDel));
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
        if (folderRenameInput == null) return;
        var nm = NotebookManager.Instance;
        var f = nm != null ? nm.GetMemoFolders().Find(x => x.id == folderId) : null;
        string current = f != null ? f.name : "";

        var t = folderRenameInput.transform;
        t.SetParent(slot, false);
        var rt = folderRenameInput.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        folderRenameInput.gameObject.SetActive(true);
        folderRenameInput.SetTextWithoutNotify(current);
        folderRenameInput.ActivateInputField();
        folderRenameInput.Select();
        int caret = current != null ? current.Length : 0;
        folderRenameInput.caretPosition = caret;
        folderRenameInput.selectionAnchorPosition = caret;
        folderRenameInput.selectionFocusPosition = caret;
    }

    private void ParkRenameInput()
    {
        if (folderRenameInput == null) return;
        folderRenameInput.DeactivateInputField();
        folderRenameInput.gameObject.SetActive(false);
        if (_renameParkParent != null && folderRenameInput.transform.parent != _renameParkParent)
            folderRenameInput.transform.SetParent(_renameParkParent, false);
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
        bool selected = note.id == _selectedId;
        var captured = note;

        var row = NewUI("Row_" + note.id, listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = selected ? UITheme_FocusMode.SelectedBG : UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
        // ピン直後の行なら一瞬ハイライト（クリック結果のフィードバック）
        if (_flashNoteId == note.id)
        {
            _flashNoteId = null;
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashRow(rowImg, selected, _flashDelay));
        }

        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 60; rowLE.preferredHeight = 60;

        // 行は横並び：[テキスト列(残り幅)] [▲][▼] [星ボタン(固定28)]
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 9, 9);
        hlg.spacing = 14;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        AddIcon(row.transform, noteIcon, UITheme_FocusMode.TextMuted);

        // テキスト列（タイトル＋メタ）
        var textCol = NewUI("TextCol", row.transform);
        var colLE = textCol.AddComponent<LayoutElement>();
        colLE.minWidth = 0; colLE.flexibleWidth = 1;
        var colVlg = textCol.AddComponent<VerticalLayoutGroup>();
        colVlg.padding = new RectOffset(0, 0, 0, 0);
        colVlg.spacing = 3;
        colVlg.childControlWidth = true; colVlg.childControlHeight = true;
        colVlg.childForceExpandWidth = true; colVlg.childForceExpandHeight = false;
        colVlg.childAlignment = TextAnchor.MiddleLeft;

        // タイトル（空は「（無題）」）。長いタイトルは Viewport の RectMask2D で右端クリップ。
        bool untitled = string.IsNullOrWhiteSpace(note.title);
        var title = NewText("Title", textCol.transform,
            untitled ? "（無題）" : note.title,
            UITheme_FocusMode.FontChipTitle,
            untitled ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextPrimary);
        title.alignment = TextAlignmentOptions.MidlineLeft;

        // メタ（更新日時）
        var meta = NewText("Meta", textCol.transform,
            FormatMeta(note),
            UITheme_FocusMode.FontCaption,
            UITheme_FocusMode.TextMuted);
        meta.alignment = TextAlignmentOptions.MidlineLeft;

        if (selected) { _selTitleTmp = title; _selMetaTmp = meta; }

        // ▲▼ 手動並べ替え（同ピングループ内のみ・端では無効）
        var capPrev = prevInGroup; var capNext = nextInGroup;
        BuildReorderButton(row.transform, "\u25B2", capPrev != null, () => {
            if (capPrev != null) { NotebookManager.Instance?.SwapMemoNoteOrder(captured.id, capPrev.id); _flashNoteId = captured.id; _flashDelay = 0.05f; Rebuild(); }
        });
        BuildReorderButton(row.transform, "\u25BC", capNext != null, () => {
            if (capNext != null) { NotebookManager.Instance?.SwapMemoNoteOrder(captured.id, capNext.id); _flashNoteId = captured.id; _flashDelay = 0.05f; Rebuild(); }
        });

        // ピン（星）ボタン。未ピン=線の星(薄グレー)、ピン=塗りの星(アクセント青)。
        var pin = NewUI("PinBtn", row.transform);
        var pinLE = pin.AddComponent<LayoutElement>();
        pinLE.minWidth = 28; pinLE.preferredWidth = 28;
        pinLE.minHeight = 28; pinLE.preferredHeight = 28;
        var pinImg = pin.AddComponent<Image>();
        pinImg.preserveAspect = true;
        pinImg.sprite = note.isPinned ? pinOnSprite : pinOffSprite;
        Color offColor = UITheme_FocusMode.TextMuted; offColor.a = 0.5f;
        pinImg.color = note.isPinned ? UITheme_FocusMode.AccentBlueSolid : offColor;
        pinImg.raycastTarget = true;
        var pinBtn = pin.AddComponent<Button>();
        pinBtn.transition = Selectable.Transition.None;  // 明示色を保つ（ColorTint干渉を避ける）
        pinBtn.targetGraphic = pinImg;
        pinBtn.onClick.AddListener(() => OnPinClicked(captured, pinImg));

        // 行全体をクリック可能に（選択）。星は子なので競合しない（子のraycastが優先）。
        var btn = row.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;  // 行の色は選択(SelectedBG)とフラッシュのみで制御
        btn.targetGraphic = rowImg;
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
        var name = NewText("Name", row.transform, "ゴミ箱", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextPrimary);
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
            var label = NewText("Label", empty.transform, "ゴミ箱は空です", UITheme_FocusMode.FontBody, UITheme_FocusMode.TextPlaceholder);
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
