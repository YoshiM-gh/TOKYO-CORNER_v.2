using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ルーチンタブ左ペイン(リスト)
/// - セクション: 今日(OccursOn(today)) / 今日以外
/// - 今日の行のみチェック円(=今日のオカレンス完了トグル)。完了は今日セクション内の末尾に沈む
/// - 行末チップ: 繰り返しパターン(毎日/月・水/3日ごと) + 時刻
/// </summary>
public class RoutineListUI : MonoBehaviour
{
    /// <summary>FullList=Routineタブ（今日/今日以外の全件） / DailyToday=Daily右ペイン（その日の分だけ）</summary>
    public enum RoutineDisplayMode { FullList, DailyToday }

    [SerializeField] private RoutineDisplayMode displayMode = RoutineDisplayMode.FullList;
    private DateTime _viewDate = DateTime.Now.Date; // DailyToday時の表示日（Dailyの<>で動く）

    [SerializeField] private Button addButton;
    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private RoutineDetailUI detail;

    private string _selectedId;
    private string _editingRoutineId;          // インライン改名中のルーチンid（null=なし）
    private string _confirmDeleteRoutineId;    // 削除確認中のルーチンid（null=なし）
    private Coroutine _routineRenameExitCo;     // 改名確定後の編集解除（次フレーム）
    private bool _suppressInline; // インライン編集の同期更新中、value/endEditの誤発火を抑制

    // ── インライン編集の自前キャレット（動的生成InputFieldは標準キャレットが出ないため自前描画）──
    // 各行がLateUpdate/コルーチンを持つと重いので、RoutineListUIが1つだけ回す。
    private TMP_InputField _activeInput;
    private RectTransform  _activeCaretRT;
    private Image          _activeCaretImg;
    private int            _lastCaretPos = -1;
    private Coroutine      _caretCo;
    private static readonly Color CaretColor = new Color(0.85f, 0.85f, 0.88f, 1f);
    private const float CARET_X_PAD = 3f;
    private static readonly string[] DayNames = { "日", "月", "火", "水", "木", "金", "土" };

    private void OnEnable()
    {
        if (addButton != null) addButton.onClick.AddListener(OnAddClicked);
        if (detail != null)
        {
            detail.OnChanged += Rebuild;
            detail.OnDeleted += OnItemDeleted;
        }
        UITheme_FocusMode.OnThemeChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        if (addButton != null) addButton.onClick.RemoveListener(OnAddClicked);
        if (detail != null)
        {
            detail.OnChanged -= Rebuild;
            detail.OnDeleted -= OnItemDeleted;
        }
        UITheme_FocusMode.OnThemeChanged -= Rebuild;
    }

    private void OnAddClicked()
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        var item = nm.AddRoutine("新しいルーチン");
        Select(item);
        Rebuild();
        if (detail != null) detail.FocusTitle();
    }

    private void OnItemDeleted(string id)
    {
        if (_selectedId == id) _selectedId = null;
        Rebuild();
    }

    private void Select(RoutineItem item)
    {
        _selectedId = item?.id;
        if (detail != null && item != null) detail.Open(item);
    }

    // ── リスト構築 ────────────────────────────

    /// <summary>
    /// Daily右ペインから使うための初期化。TodoListUI.InitForDaily と同じ役割。
    /// Dailyでは「その日に出現するルーチン」だけを並べ、セクション見出しは出さない。
    /// </summary>
    public void InitForDaily(Transform content, TMP_FontAsset fontAsset, RoutineDetailUI detailUI)
    {
        listContent = content;
        font        = fontAsset;
        detail      = detailUI;
        displayMode = RoutineDisplayMode.DailyToday;
    }

    /// <summary>Daily の日付送りに追従する（FullListでは未使用）。</summary>
    public void SetViewDate(DateTime date)
    {
        _viewDate = date.Date;
        Rebuild();
    }

    public void Rebuild()
    {
        if (listContent == null) return;
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        var nm = NotebookManager.Instance;
        if (nm == null) return;

        var all = nm.GetRoutines();
        // 表示日: Daily は _viewDate（<>で動く）、Routineタブは常に今日
        var today = (displayMode == RoutineDisplayMode.DailyToday) ? _viewDate : DateTime.Now.Date;
        var todayKey = today.ToString("yyyy-MM-dd");

        var todays = all.Where(r => r.OccursOn(today))
                        .OrderBy(r => r.IsDoneOn(todayKey) ? 1 : 0)
                        .ThenByDescending(r => r.priorityHigh)
                        .ThenBy(r => r.createdAt)
                        .ToList();
        var others = all.Where(r => !r.OccursOn(today))
                        .OrderByDescending(r => r.priorityHigh)
                        .ThenBy(r => r.createdAt)
                        .ToList();

        // Daily は「その日のための画面」なので、当日分だけを並べる（見出し・今日以外は出さない）
        if (displayMode == RoutineDisplayMode.DailyToday)
        {
            foreach (var item in todays) BuildRow(item, todayKey, true);
            if (todays.Count == 0) BuildEmptyLabel();
            return;
        }

        if (todays.Count > 0)
        {
            int doneCount = todays.Count(r => r.IsDoneOn(todayKey));
            BuildSectionHeader($"今日 · {doneCount}/{todays.Count}件", UITheme_FocusMode.AccentSatBlue);
            foreach (var item in todays) BuildRow(item, todayKey, true);
        }
        if (others.Count > 0)
        {
            BuildSectionHeader($"今日以外 · {others.Count}件", UITheme_FocusMode.TextCaption);
            foreach (var item in others) BuildRow(item, todayKey, false);
        }
        if (all.Count == 0) BuildEmptyLabel();
    }

    // ── 行の生成 ──────────────────────────────

    private void BuildRow(RoutineItem item, string todayKey, bool isToday)
    {
        bool confirming = (item.id == _confirmDeleteRoutineId);
        bool editing = !confirming && (item.id == _editingRoutineId);
        bool done = isToday && item.IsDoneOn(todayKey);
        bool selected = item.id == _selectedId;
        var captured = item;

        var row = NewUI("Row_" + item.id, listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = selected ? new Color(0.31f, 0.55f, 0.95f, 0.40f) : done ? UITheme_FocusMode.DoneBG : UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 56; rowLE.preferredHeight = 56;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 9, 9);
        hlg.spacing = 12;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // 先頭：チェック円(通常かつ今日) / それ以外はスペーサー(桁揃え)
        if (!confirming && !editing && isToday)
        {
            var check = NewUI("Check", row.transform);
            var checkLE = check.AddComponent<LayoutElement>();
            checkLE.minWidth = 26; checkLE.minHeight = 26; checkLE.preferredWidth = 26; checkLE.preferredHeight = 26;
            var checkImg = check.AddComponent<Image>();
            checkImg.sprite = UISprites.Circle;
            if (done) checkImg.color = UITheme_FocusMode.AccentBlueSolid;
            else { UIStyleKit.ApplyCircleGhost(checkImg); checkImg.color = UITheme_FocusMode.WithAlpha(Color.white, 0.30f); }
            var checkBtn = check.AddComponent<Button>();
            checkBtn.targetGraphic = checkImg;
            checkBtn.onClick.AddListener(() => { NotebookManager.Instance.SetRoutineDone(captured.id, todayKey, !captured.IsDoneOn(todayKey)); Rebuild(); });
        }
        else
        {
            var sp = NewUI("CheckSpacer", row.transform);
            var spLE = sp.AddComponent<LayoutElement>();
            spLE.minWidth = 26; spLE.preferredWidth = 26;
        }

        // ── 削除確認 ──
        if (confirming)
        {
            var col = NewUI("ConfirmText", row.transform);
            var colLE = col.AddComponent<LayoutElement>(); colLE.minWidth = 0; colLE.flexibleWidth = 1;
            var q = NewText("Q", col.transform, "削除しますか？", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextPrimary);
            q.alignment = TextAlignmentOptions.MidlineLeft;
            string capId = item.id;
            var cancel = NewUI("Cancel", row.transform);
            var cancelLE = cancel.AddComponent<LayoutElement>(); cancelLE.minWidth = 56; cancelLE.preferredWidth = 56; cancelLE.minHeight = 32; cancelLE.preferredHeight = 32;
            var cancelImg = cancel.AddComponent<Image>(); UIStyleKit.ApplyControl(cancelImg); cancelImg.color = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.18f);
            var cancelLbl = NewText("Label", cancel.transform, "やめる", UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextSecondary); cancelLbl.alignment = TextAlignmentOptions.Center;
            var cancelRt = cancelLbl.GetComponent<RectTransform>(); cancelRt.anchorMin = Vector2.zero; cancelRt.anchorMax = Vector2.one; cancelRt.offsetMin = Vector2.zero; cancelRt.offsetMax = Vector2.zero;
            var cancelBtn = cancel.AddComponent<Button>(); cancelBtn.transition = Selectable.Transition.None; cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(() => { CancelRoutineEdit(); Rebuild(); });
            var del = NewUI("ConfirmDelete", row.transform);
            var delLE = del.AddComponent<LayoutElement>(); delLE.minWidth = 72; delLE.preferredWidth = 72; delLE.minHeight = 32; delLE.preferredHeight = 32;
            var delImg = del.AddComponent<Image>(); UIStyleKit.ApplyControl(delImg); delImg.color = new Color(0.80f, 0.29f, 0.29f, 1f);
            var delLbl = NewText("Label", del.transform, "削除する", UITheme_FocusMode.FontCaption, Color.white); delLbl.alignment = TextAlignmentOptions.Center;
            var delRt = delLbl.GetComponent<RectTransform>(); delRt.anchorMin = Vector2.zero; delRt.anchorMax = Vector2.one; delRt.offsetMin = Vector2.zero; delRt.offsetMax = Vector2.zero;
            var delBtn = del.AddComponent<Button>(); delBtn.transition = Selectable.Transition.None; delBtn.targetGraphic = delImg;
            delBtn.onClick.AddListener(() => DeleteRoutineRow(capId));
            return;
        }

        // タイトル
        var titleHost = NewUI("TitleHost", row.transform);
        var hostLE = titleHost.AddComponent<LayoutElement>();
        hostLE.flexibleWidth = 1; hostLE.minHeight = 30;
        if (editing)
        {
            AttachRoutineRenameInput(titleHost.transform, item);
        }
        else
        {
            string disp = string.IsNullOrEmpty(item.title) ? "ルーチン名" : item.title;
            Color tcol = string.IsNullOrEmpty(item.title) ? UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.5f) : (done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextSecondary);
            var titleTmp = NewText("Title", titleHost.transform, disp, UITheme_FocusMode.FontChipTitle, tcol);
            if (done) titleTmp.fontStyle = FontStyles.Strikethrough;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var ttRt = titleTmp.GetComponent<RectTransform>(); ttRt.anchorMin = Vector2.zero; ttRt.anchorMax = Vector2.one; ttRt.offsetMin = Vector2.zero; ttRt.offsetMax = Vector2.zero;
        }

        // ── 編集：削除ボタン ──
        if (editing)
        {
            var del = NewUI("Delete", row.transform);
            var delLE = del.AddComponent<LayoutElement>(); delLE.minWidth = 44; delLE.preferredWidth = 44; delLE.minHeight = 32; delLE.preferredHeight = 32;
            var delImg = del.AddComponent<Image>(); UIStyleKit.ApplyControl(delImg); delImg.color = new Color(0.80f, 0.29f, 0.29f, 1f);
            var delLbl = NewText("Label", del.transform, "削除", UITheme_FocusMode.FontCaption, Color.white); delLbl.alignment = TextAlignmentOptions.Center;
            var delRt = delLbl.GetComponent<RectTransform>(); delRt.anchorMin = Vector2.zero; delRt.anchorMax = Vector2.one; delRt.offsetMin = Vector2.zero; delRt.offsetMax = Vector2.zero;
            string capDel = item.id;
            var delTrigger = del.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var delEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            delEntry.callback.AddListener((_) => EnterConfirmDeleteRoutine(capDel));
            delTrigger.triggers.Add(delEntry);
            return;
        }

        // ── 通常：高チップ / 繰り返しチップ / … / 行クリック=選択 ──
        if (item.priorityHigh)
            BuildChip(row.transform, "高", done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.AccentRed, UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentRed, 0.16f));
        var label = RepeatLabel(item);
        if (!string.IsNullOrEmpty(item.time)) label += " " + item.time;
        BuildChip(row.transform, label, done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextMuted, UITheme_FocusMode.InputBG);

        var more = NewUI("More", row.transform);
        var moreLE = more.AddComponent<LayoutElement>(); moreLE.minWidth = 30; moreLE.preferredWidth = 30; moreLE.minHeight = 30; moreLE.preferredHeight = 30;
        var moreTmp = NewText("Glyph", more.transform, "…", UITheme_FocusMode.FontBody, UITheme_FocusMode.TextSecondary); moreTmp.alignment = TextAlignmentOptions.Center; moreTmp.raycastTarget = true;
        var moreRt = moreTmp.GetComponent<RectTransform>(); moreRt.anchorMin = Vector2.zero; moreRt.anchorMax = Vector2.one; moreRt.offsetMin = Vector2.zero; moreRt.offsetMax = Vector2.zero;
        var moreBtn = more.AddComponent<Button>(); moreBtn.transition = Selectable.Transition.None; moreBtn.targetGraphic = moreTmp;
        string capMore = item.id;
        moreBtn.onClick.AddListener(() => StartRoutineRename(capMore));

        var rowBtn = row.AddComponent<Button>();
        rowBtn.transition = Selectable.Transition.None;
        rowBtn.targetGraphic = rowImg;
        rowBtn.onClick.AddListener(() => { Select(captured); Rebuild(); });
    }

    // ── インライン編集タイトル入力（Todo/Daily の BuildInlineTitleInput を移植）──
    private TMP_InputField BuildInlineTitleInput(Transform parent, RoutineItem item, bool done)
    {
        var captured = item;
        var fieldGO = NewUI("TitleInput", parent);
        var fieldRT = fieldGO.GetComponent<RectTransform>();
        fieldRT.anchorMin = Vector2.zero; fieldRT.anchorMax = Vector2.one;
        fieldRT.offsetMin = Vector2.zero; fieldRT.offsetMax = Vector2.zero;
        var fieldImg = fieldGO.AddComponent<Image>();
        fieldImg.color = Color.clear; // 透明（背景は行が持つ）。raycastは受ける
        var taGO = NewUI("TextArea", fieldGO.transform);
        var taRT = taGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(2f, 0f); taRT.offsetMax = new Vector2(-2f, 0f);
        taGO.AddComponent<RectMask2D>();
        var txtTMP = NewText("Text", taGO.transform, item.title ?? "", UITheme_FocusMode.FontChipTitle,
            done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextSecondary);
        if (done) txtTMP.fontStyle = FontStyles.Strikethrough;
        var txtRT = txtTMP.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var phTMP = NewText("Placeholder", taGO.transform, "ルーチン名", UITheme_FocusMode.FontChipTitle,
            UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.5f));
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
        input.text          = item.title ?? "";
        input.customCaretColor = true;
        input.caretColor       = Color.clear;
        input.caretWidth       = 2;
        input.selectionColor   = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.4f);
        UIFonts.FinalizeRuntimeInput(input); // 全選択・ドラッグ選択・コピペを有効化
        input.onSelect.AddListener(_ =>
        {
            if (_suppressInline) return;
            Select(captured);                       // フォーカスイン→右ペインに表示
            ActivateCaret(input, caretRT, caretImg);
        });
        input.onDeselect.AddListener(_ => DeactivateCaret(input));
        input.onEndEdit.AddListener(v =>
        {
            if (_suppressInline) return;
            if (string.IsNullOrWhiteSpace(v))
            {
                _suppressInline = true; input.text = captured.title ?? ""; _suppressInline = false;
                return;
            }
            if (captured.title == v) return;
            captured.title = v;
            NotebookManager.Instance?.UpdateRoutine(captured);
            if (detail != null) detail.RefreshTitleIfOpen(captured.id, v);
        });
        return input;
    }

    // ───────── ルーチン行の編集/削除（フォルダ方式をミラー・削除は直接）─────────
    private void StartRoutineRename(string id)
    {
        _editingRoutineId = id;
        Rebuild();
    }

    private void CommitRoutineRename(RoutineItem item, string newName)
    {
        if (item == null || _editingRoutineId != item.id) return;
        string nt = (newName ?? "").Trim();
        if (nt.Length > 0 && item.title != nt)   // 空は据え置き（ルーチンは空タイトル不可）
        {
            item.title = nt;
            NotebookManager.Instance?.UpdateRoutine(item);
            if (detail != null) detail.RefreshTitleIfOpen(item.id, nt);
        }
        string id = item.id;
        if (_routineRenameExitCo != null) StopCoroutine(_routineRenameExitCo);
        _routineRenameExitCo = StartCoroutine(ExitRoutineEditNextFrame(id));
    }

    private System.Collections.IEnumerator ExitRoutineEditNextFrame(string id)
    {
        yield return null;
        _routineRenameExitCo = null;
        if (_editingRoutineId == id) { _editingRoutineId = null; Rebuild(); }
    }

    private void EnterConfirmDeleteRoutine(string id)
    {
        CancelRoutineEdit();
        _confirmDeleteRoutineId = id;
        Rebuild();
    }

    private void CancelRoutineEdit()
    {
        _editingRoutineId = null;
        _confirmDeleteRoutineId = null;
        if (_routineRenameExitCo != null) { StopCoroutine(_routineRenameExitCo); _routineRenameExitCo = null; }
    }

    private void DeleteRoutineRow(string id)
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        CancelRoutineEdit();
        nm.DeleteRoutine(id);                    // 直接削除（ゴミ箱なし）
        if (detail != null && detail.CurrentId == id) detail.Clear();
        if (_selectedId == id) _selectedId = null;
        Rebuild();
    }

    private void AttachRoutineRenameInput(Transform slot, RoutineItem item)
    {
        var captured = item;
        string current = item.title ?? "";
        var fieldGO = NewUI("TitleInput", slot);
        var fieldRT = fieldGO.GetComponent<RectTransform>();
        fieldRT.anchorMin = Vector2.zero; fieldRT.anchorMax = Vector2.one; fieldRT.offsetMin = Vector2.zero; fieldRT.offsetMax = Vector2.zero;
        var fieldImg = fieldGO.AddComponent<Image>(); fieldImg.color = Color.clear;
        var taGO = NewUI("TextArea", fieldGO.transform);
        var taRT = taGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one; taRT.offsetMin = new Vector2(2f, 0f); taRT.offsetMax = new Vector2(-2f, 0f);
        taGO.AddComponent<RectMask2D>();
        var txtTMP = NewText("Text", taGO.transform, current, UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextSecondary);
        var txtRT = txtTMP.GetComponent<RectTransform>(); txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one; txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var phTMP = NewText("Placeholder", taGO.transform, "ルーチン名", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.5f));
        var phRT = phTMP.GetComponent<RectTransform>(); phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one; phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var caretGO = NewUI("CustomCaret", taGO.transform);
        var caretRT = caretGO.GetComponent<RectTransform>(); caretRT.anchorMin = new Vector2(0f, 1f); caretRT.anchorMax = new Vector2(0f, 1f); caretRT.pivot = new Vector2(0f, 1f); caretRT.sizeDelta = new Vector2(2f, 16f); caretRT.anchoredPosition = Vector2.zero;
        var caretImg = caretGO.AddComponent<Image>(); caretImg.color = Color.clear; caretImg.raycastTarget = false;
        var input = fieldGO.AddComponent<TMP_InputField>();
        input.targetGraphic = fieldImg; input.textViewport = taRT; input.textComponent = txtTMP; input.placeholder = phTMP;
        input.lineType = TMP_InputField.LineType.SingleLine; input.text = current;
        input.customCaretColor = true; input.caretColor = Color.clear; input.caretWidth = 2;
        input.selectionColor = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.4f);
        UIFonts.FinalizeRuntimeInput(input); // 全選択・ドラッグ選択・コピペを有効化
        input.onSelect.AddListener(_ => { if (_suppressInline) return; ActivateCaret(input, caretRT, caretImg); });
        input.onDeselect.AddListener(_ => DeactivateCaret(input));
        input.onEndEdit.AddListener(v => { if (_suppressInline) return; CommitRoutineRename(captured, v); });
        input.Select(); input.ActivateInputField();
        int caret = current.Length; input.caretPosition = caret; input.selectionAnchorPosition = caret; input.selectionFocusPosition = caret;
        ActivateCaret(input, caretRT, caretImg);
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
            if (info != null && info.lineCount > 0)
            {
                var li0 = info.lineInfo[0];
                caretY = li0.ascender; caretH = Mathf.Max(li0.ascender - li0.descender, 1f);
            }
            else { caretY = taRect.yMax; caretH = txt.fontSize * 1.15f; }
            caretX = taRect.xMin;
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

    private static string RepeatLabel(RoutineItem item)
    {
        switch (item.repeatType)
        {
            case "weekly":
                if (item.weekdays == null || item.weekdays.Count == 0) return "毎週";
                if (item.weekdays.Count == 7) return "毎日";
                var sorted = item.weekdays.Where(d => d >= 0 && d <= 6).Distinct().OrderBy(d => d).ToList();
                return string.Join("・", sorted.Select(d => DayNames[d]));
            case "interval":
                return item.intervalDays <= 1 ? "毎日" : item.intervalDays + "日ごと";
            case "monthly":
                // 29〜31日は存在しない月があり月末に丸まるため、その旨を添える
                if (item.monthlyLastDay) return "毎月末";
                return item.monthlyDay >= 29
                    ? $"毎月{item.monthlyDay}日(なければ月末)"
                    : $"毎月{item.monthlyDay}日";
            default:
                return "毎日";
        }
    }

    private void BuildChip(Transform parent, string text, Color textColor, Color bg)
    {
        var chip = NewUI("Chip", parent);
        var img = chip.AddComponent<Image>();
        img.color = bg;
        UIStyleKit.ApplyRounded(img, 6f);
        img.raycastTarget = false;
        var hlg = chip.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 10, 3, 3);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        var label = NewText("Label", chip.transform, text, UITheme_FocusMode.FontBody, textColor);
        label.alignment = TextAlignmentOptions.Center;
    }

    private void BuildSectionHeader(string text, Color labelColor)
    {
        var sec = NewUI("Section", listContent);
        var le = sec.AddComponent<LayoutElement>();
        le.minHeight = 34; le.preferredHeight = 34;
        var hlg = sec.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(4, 4, 8, 4);
        hlg.spacing = 12;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        NewText("Label", sec.transform, text, UITheme_FocusMode.FontCaption, labelColor);
        var line = NewUI("Line", sec.transform);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = UITheme_FocusMode.BorderDivider;
        lineImg.raycastTarget = false;
        var lineLE = line.AddComponent<LayoutElement>();
        lineLE.flexibleWidth = 1; lineLE.minHeight = 1; lineLE.preferredHeight = 1;
    }

    private void BuildEmptyLabel()
    {
        var empty = NewUI("Empty", listContent);
        var le = empty.AddComponent<LayoutElement>();
        le.minHeight = 80;
        var label = NewText("Label", empty.transform, "ルーチンはありません。「+ 追加」から作成できます",
            UITheme_FocusMode.FontBody, UITheme_FocusMode.TextPlaceholder);
        label.alignment = TextAlignmentOptions.Center;
        var rt = label.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ── 部品ヘルパー ──────────────────────────

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
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        UIFonts.ApplySmall(tmp); // 小さい文字は可読性優先フォントに
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = false;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }
}
