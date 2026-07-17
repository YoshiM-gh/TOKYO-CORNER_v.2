using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// タスクタブ左ペイン(リスト)
/// - 日付セクション固定: 期限切れ / 今日 / 今後 / 日付なし / 完了済み(トグルON時)
/// - 各セクション内: 優先度高→sortOrder→作成順(今後のみ日付昇順が最優先)
/// - 日付チップ色: 期限切れ=赤 / 今日=青 / それ以外=グレー
/// - 行クリックで選択→右ペイン(TodoDetailUI)で編集、チェック円で完了トグル
/// </summary>
// 表示モード: Todoタブ用(全グループ) と Daily中央列用(日付なし＋期限切れ＋当日＋完了済み)
public enum TodoDisplayMode { FullList, DailyToday }

public class TodoListUI : MonoBehaviour
{
    [SerializeField] private Toggle showDoneToggle;
    [SerializeField] private Button addButton;
    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private TodoDetailUI detail;
    [SerializeField] private TodoDisplayMode displayMode = TodoDisplayMode.FullList; // 既定はTodoタブ用
    private DateTime _viewDate = DateTime.Now.Date; // DailyToday時の表示日（Dailyの<>で動く）。FullListでは未使用
    private TodoModal _todoModal; // Daily編集モーダル（DailyTodayのとき「⋯」で開く）

    private string _selectedId;
    private string _editingTodoId;             // インライン改名中のタスクid（null=なし・Todoタブのみ）
    private string _confirmDeleteTodoId;       // 削除確認中のタスクid（null=なし・Todoタブのみ）
    private Coroutine _todoRenameExitCo;        // 改名確定後の編集解除（次フレーム）
    private bool _suppressInline; // インライン編集の同期更新中、value/endEditの誤発火を抑制
    private string _flashTodoId; // 並べ替え直後に一瞬ハイライトする行のid（クリック結果のフィードバック）
    private Coroutine _flashCo;     // フラッシュは常に1本だけ（多重起動による色の揺れを防ぐ）

    // ── インラインタイトル編集のカスタムキャレット（一元管理: 編集中の1行だけ追従） ──
    // 標準キャレットは動的生成InputFieldで見えないため、Imageで自前描画する（StickyNote方式）。
    // 各行がLateUpdate/コルーチンを持つと多数行で重いので、TodoListUIが1つだけ回す。
    private TMP_InputField _activeInput;     // いまフォーカス中の行のInputField
    private RectTransform  _activeCaretRT;   // その行のキャレットImageのRT
    private Image          _activeCaretImg;  // その行のキャレットImage
    private int            _lastCaretPos = -1;
    private Coroutine      _caretCo;
    private static readonly Color CaretColor = new Color(0.85f, 0.85f, 0.88f, 1f);
    private const float CARET_X_PAD = 3f; // 全角文字に密着しないようキャレットを右に少し寄せる

    private bool _wired;

    private void OnEnable() => Wire();

    // listeners 登録は二重防止しつつ1回だけ。Todoタブは OnEnable から、
    // Daily は InitForDaily から呼ぶ（どちらの経路でも確実に1回登録される）。
    private void Wire()
    {
        if (_wired) return;
        _wired = true;
        if (showDoneToggle != null) showDoneToggle.onValueChanged.AddListener(OnShowDoneChanged);
        if (addButton != null) addButton.onClick.AddListener(OnAddClicked);
        if (detail != null)
        {
            detail.OnChanged += Rebuild;
            detail.OnDeleted += OnItemDeleted;
        }
        UITheme_FocusMode.OnThemeChanged += Rebuild;
        Rebuild();
    }

    /// <summary>Daily 中央列用の初期化。listContent/トップバーを外部（DailyCalendarUI）が生成して渡す。
    /// detail（右ペイン）は Daily には無いので渡さない（編集モーダルは別途）。</summary>
    public void InitForDaily(Transform content, Toggle showDone, Button add, TMP_FontAsset fontAsset)
    {
        listContent = content;
        showDoneToggle = showDone;
        addButton = add;
        if (fontAsset != null) font = fontAsset;
        displayMode = TodoDisplayMode.DailyToday;
        Wire(); // まだ未登録ならここで登録＋Rebuild。登録済みなら明示Rebuildのみ。
        if (_wired) Rebuild();
    }

    /// <summary>Daily の表示日を変更して再描画（DailyCalendarUI の <> から呼ぶ）。</summary>
    public void SetViewDate(System.DateTime date)
    {
        _viewDate = date.Date;
        if (_wired) Rebuild();
    }

    /// <summary>Daily 編集モーダルを紐づける（DailyCalendarUI から渡す）。
    /// モーダルでの保存・削除後は Rebuild してリストに反映。</summary>
    public void SetTodoModal(TodoModal m)
    {
        if (_todoModal == m) return;
        if (_todoModal != null)
        {
            _todoModal.OnChanged -= Rebuild;
            _todoModal.OnDeleted -= OnModalDeleted;
        }
        _todoModal = m;
        if (_todoModal != null)
        {
            _todoModal.OnChanged += Rebuild;
            _todoModal.OnDeleted += OnModalDeleted;
        }
    }

    private void OnModalDeleted(string id) => Rebuild();



    // ── カスタムキャレット制御（一元管理）────────────────────────
    private void LateUpdate()
    {
        // フォーカス中の行のキャレットだけ追従。caretPositionが変わった時のみ更新＆点滅リスタート。
        if (_activeInput == null || !_activeInput.isFocused) return;
        if (_activeInput.caretPosition != _lastCaretPos)
        {
            _lastCaretPos = _activeInput.caretPosition;
            UpdateActiveCaret();
            RestartCaretBlink();
        }
    }

    /// <summary>行のタイトルがフォーカスされた時に呼ぶ。その行のキャレットをアクティブにする。</summary>
    private void ActivateCaret(TMP_InputField input, RectTransform caretRT, Image caretImg)
    {
        _activeInput    = input;
        _activeCaretRT  = caretRT;
        _activeCaretImg = caretImg;
        _lastCaretPos   = -1; // 次のLateUpdateで必ず更新させる
        UpdateActiveCaret();
        RestartCaretBlink();
    }

    /// <summary>フォーカスが外れた時に呼ぶ。キャレットを消す。</summary>
    private void DeactivateCaret(TMP_InputField input)
    {
        // 別の行へ移った後に古い行のdeselectが来るケースを考慮し、一致時のみ消す
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
        if (_activeCaretRT == null) return;
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

    /// <summary>アクティブな行のキャレット位置を計算してImageを配置（StickyNote方式・IME対策込み）。</summary>
    private void UpdateActiveCaret()
    {
        if (_activeCaretRT == null || _activeInput?.textComponent == null) return;
        _activeInput.ForceLabelUpdate();          // IME確定直後、旧テキストで計算しない
        var txt = _activeInput.textComponent;
        txt.ForceMeshUpdate(true, true);          // IME確定直後の1文字ズレ対策
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
            caretPad = 0f; // 文頭は最初の文字の左端（見切れ防止のため負値にしない。位置精緻化はIME対応とまとめて）
        }
        else
        {
            int idx = Mathf.Clamp(cp - 1, 0, info.characterCount - 1);
            var ci = info.characterInfo[idx];
            caretX = ci.xAdvance;
            int li = Mathf.Clamp(ci.lineNumber, 0, info.lineCount - 1);
            var line = info.lineInfo[li];
            caretY = line.ascender; caretH = Mathf.Max(line.ascender - line.descender, 1f);
            caretPad = CARET_X_PAD; // 文字の右端から少し余白（全角文字に密着しない）
        }
        _activeCaretRT.anchoredPosition = new Vector2(caretX - taRect.xMin + caretPad, caretY - taRect.yMax);
        _activeCaretRT.sizeDelta = new Vector2(2f, caretH);
    }

    private void OnDisable()
    {
        if (!_wired) return;
        _wired = false;
        if (showDoneToggle != null) showDoneToggle.onValueChanged.RemoveListener(OnShowDoneChanged);
        if (addButton != null) addButton.onClick.RemoveListener(OnAddClicked);
        if (detail != null)
        {
            detail.OnChanged -= Rebuild;
            detail.OnDeleted -= OnItemDeleted;
        }
        UITheme_FocusMode.OnThemeChanged -= Rebuild;
    }

    private void OnShowDoneChanged(bool _) => Rebuild();

    private void OnAddClicked()
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        var item = nm.AddTodo("新しいタスク");
        Select(item);
        Rebuild();
        if (detail != null) detail.FocusTitle();
    }

    private void OnItemDeleted(string id)
    {
        if (_selectedId == id) _selectedId = null;
        Rebuild();
    }

    private void Select(TodoItem item)
    {
        _selectedId = item?.id;
        if (detail != null && item != null) detail.Open(item);
    }

    // ── リスト構築 ────────────────────────────

    public void Rebuild()
    {
        if (listContent == null) return;
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        var nm = NotebookManager.Instance;
        if (nm == null) return;

        var all = nm.GetTodos(true);
        var today = DateTime.Now.Date;                 // 現実の今日（期限切れ判定に使う・常に固定）
        // 表示日: Daily(DailyToday)は _viewDate（<>で動く）、Todoタブ(FullList)は常に今日。
        var view = (displayMode == TodoDisplayMode.DailyToday) ? _viewDate : today;

        var open = all.Where(t => !t.isCompleted).ToList();
        // 各グループ内は「追加順」（sortOrder → createdAt）。優先度・時間は並びに影響させない。
        // 日付ありは日付昇順（期限切れ=過去日が自然に上位、今日、未来…の順）。
        var noDate  = SortDefault(open.Where(t => string.IsNullOrEmpty(t.dateKey)));
        // 期限切れは「現実の今日」基準（Dailyで未来日を見ていても、今日より前なら期限切れ）。
        var overdue = open.Where(t => HasDateBefore(t, today)).OrderBy(t => t.dateKey).ThenBy(t => t.sortOrder).ThenBy(t => t.createdAt).ToList();
        // 「その日のタスク」は表示日(view)基準。
        var todays  = SortDefault(open.Where(t => IsOnDate(t, view)));
        // 未来日セクション(FullListのみ)は今日より後。
        var future  = open.Where(t => HasDateAfter(t, today)).OrderBy(t => t.dateKey).ThenBy(t => t.sortOrder).ThenBy(t => t.createdAt).ToList();

        // 見出し: 表示日が今日なら「今日」、それ以外は実日付「M/d（曜）」。
        string todayHeader = (view == today) ? "今日" : FormatDateHeader(NotebookManager.DateKey(view));

        // グループ順: 日付なし → 期限切れ → その日のタスク → 未来日ごと →（完了済み）
        BuildSection("日付なし", noDate, today, UITheme_FocusMode.TextCaption, canReorder: true);
        BuildSection("期限切れ", overdue, today, UITheme_FocusMode.AccentRed, canReorder: false); // 期限切れは並べ替えなし
        BuildSection(todayHeader, todays, view, UITheme_FocusMode.AccentSatBlue, canReorder: true);
        // 未来日: Todoタブ(FullList)のみ。Daily(DailyToday)は当日のための画面なので未来日セクションは出さない。
        if (displayMode == TodoDisplayMode.FullList)
        {
            // dateKey ごとにグループ化し、各日付を見出し「M/d（曜）」で出す（昇順）。各日付内は並べ替え可。
            foreach (var g in future.GroupBy(t => t.dateKey).OrderBy(g => g.Key))
            {
                var items = g.OrderBy(t => t.sortOrder).ThenBy(t => t.createdAt).ToList();
                BuildSection(FormatDateHeader(g.Key), items, today, UITheme_FocusMode.TextCaption, canReorder: true);
            }
        }

        bool showDone = showDoneToggle != null && showDoneToggle.isOn;
        if (showDone)
        {
            // Daily(DailyToday)は「その日に完了したタスク」だけ（completedAtの日付==view）。
            // 過去の完了が延々と積もらないようにする。FullList(Todoタブ)は従来どおり全件。
            var doneQuery = all.Where(t => t.isCompleted);
            if (displayMode == TodoDisplayMode.DailyToday)
                doneQuery = doneQuery.Where(t => IsCompletedOn(t, view));
            var done = doneQuery.OrderByDescending(t => t.completedAt).ToList();
            if (done.Count > 0)
            {
                BuildSectionHeader($"完了済み · {done.Count}件", UITheme_FocusMode.TextCaption);
                foreach (var item in done) BuildRow(item, today, false, null, null); // 完了済みは並べ替え不可
            }
        }

        if (open.Count == 0 && !showDone) BuildEmptyLabel();
    }

    private void BuildSection(string label, List<TodoItem> items, DateTime today, Color labelColor, bool canReorder = false)
    {
        if (items.Count == 0) return;
        BuildSectionHeader($"{label} · {items.Count}件", labelColor);
        // 同一グループ内の前後関係を渡す。先頭/末尾は prev/next が null（矢印グレーアウト）。
        for (int i = 0; i < items.Count; i++)
        {
            var prev = (i > 0) ? items[i - 1] : null;
            var next = (i < items.Count - 1) ? items[i + 1] : null;
            BuildRow(items[i], today, canReorder, prev, next);
        }
    }

    // 各グループ内の既定の並び = 追加順（sortOrder → createdAt）。
    // 優先度・時間は並びに影響させない（フェーズ2確定仕様）。
    private static List<TodoItem> SortDefault(IEnumerable<TodoItem> src) =>
        src.OrderBy(t => t.sortOrder)
           .ThenBy(t => t.createdAt)
           .ToList();

    private static bool TryDate(TodoItem t, out DateTime d)
    {
        d = default;
        return !string.IsNullOrEmpty(t.dateKey) &&
               DateTime.TryParseExact(t.dateKey, "yyyy-MM-dd",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out d);
    }

    private static bool HasDateBefore(TodoItem t, DateTime today) => TryDate(t, out var d) && d.Date < today;
    private static bool IsOnDate(TodoItem t, DateTime today) => TryDate(t, out var d) && d.Date == today;

    // 完了日が指定日と一致するか（completedAt は "yyyy-MM-dd HH:mm" 形式、先頭10文字が日付）。
    private static bool IsCompletedOn(TodoItem t, System.DateTime day)
    {
        if (string.IsNullOrEmpty(t.completedAt) || t.completedAt.Length < 10) return false;
        return t.completedAt.Substring(0, 10) == NotebookManager.DateKey(day);
    }

    private static bool HasDateAfter(TodoItem t, DateTime today) => TryDate(t, out var d) && d.Date > today;
    // 期限切れ日数 = 今日 - 期限日（最低1日）。過去日でない場合は0。
    private static int DaysOverdue(TodoItem t, DateTime today)
    {
        if (!TryDate(t, out var d) || d.Date >= today) return 0;
        return Mathf.Max(1, (int)(today - d.Date).TotalDays);
    }

    // ── 行の生成 ──────────────────────────────

    // displayMode で分岐：FullList=Todoタブ(3状態) / DailyToday=Daily(モーダル・現状維持)
    private void BuildRow(TodoItem item, DateTime today, bool canReorder = false, TodoItem prevItem = null, TodoItem nextItem = null)
    {
        if (displayMode == TodoDisplayMode.FullList)
            BuildRowTab(item, today, canReorder, prevItem, nextItem);
        else
            BuildRowDaily(item, today, canReorder, prevItem, nextItem);
    }

    // Todoタブ専用：フォルダ方式の3状態（通常=表示テキスト＋…／編集=入力＋削除／確認=やめる・削除する）
    private void BuildRowTab(TodoItem item, DateTime today, bool canReorder, TodoItem prevItem, TodoItem nextItem)
    {
        bool confirming = (item.id == _confirmDeleteTodoId);
        bool editing = !confirming && (item.id == _editingTodoId);
        bool done = item.isCompleted;
        bool selected = item.id == _selectedId;
        var captured = item;

        var row = NewUI("Row_" + item.id, listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = selected ? UITheme_FocusMode.SelectedBG : done ? UITheme_FocusMode.DoneBG : UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
        if (_flashTodoId == item.id)
        {
            _flashTodoId = null;
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashRow(rowImg, selected, done));
        }
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 56; rowLE.preferredHeight = 56;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 9, 9);
        hlg.spacing = 12;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        if (!confirming && !editing)
        {
            var check = NewUI("Check", row.transform);
            var checkImg = check.AddComponent<Image>();
            checkImg.sprite = UISprites.Circle;
            if (done) checkImg.color = UITheme_FocusMode.AccentBlueSolid;
            else { UIStyleKit.ApplyCircleGhost(checkImg); checkImg.color = UITheme_FocusMode.WithAlpha(Color.white, 0.30f); }
            var checkLE = check.AddComponent<LayoutElement>();
            checkLE.minWidth = 26; checkLE.minHeight = 26; checkLE.preferredWidth = 26; checkLE.preferredHeight = 26;
            var checkBtn = check.AddComponent<Button>();
            checkBtn.targetGraphic = checkImg;
            checkBtn.onClick.AddListener(() => { NotebookManager.Instance.SetTodoCompleted(captured.id, !captured.isCompleted); Rebuild(); });
        }
        else
        {
            var sp = NewUI("CheckSpacer", row.transform);
            var spLE = sp.AddComponent<LayoutElement>();
            spLE.minWidth = 26; spLE.preferredWidth = 26;
        }

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
            cancelBtn.onClick.AddListener(() => { CancelTodoEdit(); Rebuild(); });
            var del = NewUI("ConfirmDelete", row.transform);
            var delLE = del.AddComponent<LayoutElement>(); delLE.minWidth = 72; delLE.preferredWidth = 72; delLE.minHeight = 32; delLE.preferredHeight = 32;
            var delImg = del.AddComponent<Image>(); UIStyleKit.ApplyControl(delImg); delImg.color = new Color(0.80f, 0.29f, 0.29f, 1f);
            var delLbl = NewText("Label", del.transform, "削除する", UITheme_FocusMode.FontCaption, Color.white); delLbl.alignment = TextAlignmentOptions.Center;
            var delRt = delLbl.GetComponent<RectTransform>(); delRt.anchorMin = Vector2.zero; delRt.anchorMax = Vector2.one; delRt.offsetMin = Vector2.zero; delRt.offsetMax = Vector2.zero;
            var delBtn = del.AddComponent<Button>(); delBtn.transition = Selectable.Transition.None; delBtn.targetGraphic = delImg;
            delBtn.onClick.AddListener(() => DeleteTodoRow(capId));
            return;
        }

        var titleHost = NewUI("TitleHost", row.transform);
        var hostLE = titleHost.AddComponent<LayoutElement>();
        hostLE.flexibleWidth = 1; hostLE.minHeight = 30;
        if (editing)
        {
            AttachTodoRenameInput(titleHost.transform, item);
        }
        else
        {
            string disp = string.IsNullOrEmpty(item.title) ? "タスク名" : item.title;
            Color tcol = string.IsNullOrEmpty(item.title) ? UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.5f) : (done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextSecondary);
            var titleTmp = NewText("Title", titleHost.transform, disp, UITheme_FocusMode.FontChipTitle, tcol);
            if (done) titleTmp.fontStyle = FontStyles.Strikethrough;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            var ttRt = titleTmp.GetComponent<RectTransform>(); ttRt.anchorMin = Vector2.zero; ttRt.anchorMax = Vector2.one; ttRt.offsetMin = Vector2.zero; ttRt.offsetMax = Vector2.zero;
        }

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
            delEntry.callback.AddListener((_) => EnterConfirmDeleteTodo(capDel));
            delTrigger.triggers.Add(delEntry);
            return;
        }

        if (item.priorityHigh)
            BuildChip(row.transform, "高", done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.AccentRed, UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentRed, 0.16f));
        if (!done && HasDateBefore(item, today))
        {
            int days = DaysOverdue(item, today);
            BuildChip(row.transform, days + "日経過", UITheme_FocusMode.AccentRed, UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentRed, 0.14f));
        }
        else if (!string.IsNullOrEmpty(item.time))
        {
            Color chipText = (!done && IsOnDate(item, today)) ? UITheme_FocusMode.AccentSatBlue : UITheme_FocusMode.TextMuted;
            Color chipBG   = (!done && IsOnDate(item, today)) ? UITheme_FocusMode.AccentBlueFaint : UITheme_FocusMode.InputBG;
            BuildChip(row.transform, item.time, chipText, chipBG);
        }
        if (canReorder)
        {
            BuildReorderButton(row.transform, "▲", prevItem != null, () => { if (prevItem != null) { _flashTodoId = item.id; NotebookManager.Instance?.SwapTodoOrder(item.id, prevItem.id); Rebuild(); } });
            BuildReorderButton(row.transform, "▼", nextItem != null, () => { if (nextItem != null) { _flashTodoId = item.id; NotebookManager.Instance?.SwapTodoOrder(item.id, nextItem.id); Rebuild(); } });
        }

        var more = NewUI("MoreBtn", row.transform);
        var moreImg = more.AddComponent<Image>(); moreImg.color = Color.clear;
        var moreLE = more.AddComponent<LayoutElement>(); moreLE.minWidth = 30; moreLE.preferredWidth = 30; moreLE.minHeight = 30;
                // 「→」翌日送り（期限切れタスクのみ・Todoタブ用）
        if (!done && HasDateBefore(item, today))
        {
            BuildReorderButton(row.transform, "→", true, () =>
            {
                NotebookManager.Instance?.MoveTodoToNextDay(item.id);
                Rebuild();
            });
        }

        
var moreTxt = NewText("Dots", more.transform, "…", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.TextMuted);
        moreTxt.alignment = TextAlignmentOptions.Center;
        var moreTxtRT = moreTxt.GetComponent<RectTransform>(); moreTxtRT.anchorMin = Vector2.zero; moreTxtRT.anchorMax = Vector2.one; moreTxtRT.offsetMin = Vector2.zero; moreTxtRT.offsetMax = Vector2.zero;
        var moreBtn = more.AddComponent<Button>(); moreBtn.transition = Selectable.Transition.None; moreBtn.targetGraphic = moreImg;
        string capMore = item.id;
        moreBtn.onClick.AddListener(() => StartTodoRename(capMore));

        var rowBtn = row.AddComponent<Button>();
        rowBtn.transition = Selectable.Transition.None;
        rowBtn.targetGraphic = rowImg;
        rowBtn.onClick.AddListener(() => { Select(captured); Rebuild(); });
    }

    private void BuildRowDaily(TodoItem item, DateTime today, bool canReorder = false, TodoItem prevItem = null, TodoItem nextItem = null)
    {
        bool done = item.isCompleted;
        bool selected = item.id == _selectedId;

        var row = NewUI("Row_" + item.id, listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = selected ? UITheme_FocusMode.SelectedBG
                     : done ? UITheme_FocusMode.DoneBG : UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
        // 並べ替え直後の行なら一瞬ハイライト（クリック結果のフィードバック）
        if (_flashTodoId == item.id)
        {
            _flashTodoId = null;
            if (_flashCo != null) StopCoroutine(_flashCo); // 前のフラッシュを止めて1本に保つ
            _flashCo = StartCoroutine(FlashRow(rowImg, selected, done));
        }
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 56; rowLE.preferredHeight = 56;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 9, 9);
        hlg.spacing = 12;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var captured = item; // 行内の各ボタンで参照

        // チェック円
        var check = NewUI("Check", row.transform);
        var checkImg = check.AddComponent<Image>();
        checkImg.sprite = UISprites.Circle;
        if (done)
        {
            checkImg.color = UITheme_FocusMode.AccentBlueSolid;
        }
        else
        {
            UIStyleKit.ApplyCircleGhost(checkImg);
            checkImg.color = UITheme_FocusMode.WithAlpha(Color.white, 0.30f);
        }
        var checkLE = check.AddComponent<LayoutElement>();
        checkLE.minWidth = 26; checkLE.minHeight = 26;
        checkLE.preferredWidth = 26; checkLE.preferredHeight = 26;
        var checkBtn = check.AddComponent<Button>();
        checkBtn.targetGraphic = checkImg;
        checkBtn.onClick.AddListener(() =>
        {
            NotebookManager.Instance.SetTodoCompleted(captured.id, !captured.isCompleted);
            Rebuild();
        });

        // タイトル(インライン編集InputField・クリックで直接打てる)
        var titleHost = NewUI("TitleHost", row.transform);
        var hostLE = titleHost.AddComponent<LayoutElement>();
        hostLE.flexibleWidth = 1; hostLE.minHeight = 30;
        BuildInlineTitleInput(titleHost.transform, item, done);

        // 「高」チップ
        if (item.priorityHigh)
            BuildChip(row.transform, "高",
                done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.AccentRed,
                UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentRed, 0.16f));

        // チップ: 期限切れ(過去日)は「○日経過」、それ以外は日付/時刻チップ。
        // 「○日経過」は日付チップとは別軸の“放置度”情報（Daily でも表示し積み残しを可視化）。
        if (!done && HasDateBefore(item, today))
        {
            int days = DaysOverdue(item, today);
            BuildChip(row.transform, days + "日経過",
                UITheme_FocusMode.AccentRed,
                UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentRed, 0.14f));
        }
        else if (!string.IsNullOrEmpty(item.time))
        {
            // 日付は見出し（実日付セクション）が持つので、行には時刻のみ表示する。
            // 時刻が無いタスクはチップ自体を出さない。
            Color chipText = (!done && IsOnDate(item, today)) ? UITheme_FocusMode.AccentSatBlue : UITheme_FocusMode.TextMuted;
            Color chipBG   = (!done && IsOnDate(item, today)) ? UITheme_FocusMode.AccentBlueFaint : UITheme_FocusMode.InputBG;
            BuildChip(row.transform, item.time, chipText, chipBG);
        }

        // ↑↓ 並べ替えボタン（同一日付グループ内のみ・「…」の手前）。
        // canReorder=false のグループ（期限切れ・完了済み）には出さない。
        if (canReorder)
        {
            BuildReorderButton(row.transform, "\u25B2", prevItem != null, () => // ▲
            {
                if (prevItem != null) { _flashTodoId = item.id; NotebookManager.Instance?.SwapTodoOrder(item.id, prevItem.id); Rebuild(); }
            });
            BuildReorderButton(row.transform, "\u25BC", nextItem != null, () => // ▼
            {
                if (nextItem != null) { _flashTodoId = item.id; NotebookManager.Instance?.SwapTodoOrder(item.id, nextItem.id); Rebuild(); }
            });
        }

        // 「→」翌日送り（Daily の当日タスク・期限切れタスクともに）。押すと即・翌日へ（確認なし）。
        if (displayMode == TodoDisplayMode.DailyToday && !done && (IsOnDate(item, today) || HasDateBefore(item, today)))
        {
            BuildReorderButton(row.transform, "\u2192", true, () => // →
            {
                NotebookManager.Instance?.MoveTodoToNextDay(item.id);
                Rebuild();
            });
        }

        // 「…」詳細を開くアイコン（右端）
        // 「…」はモーダルを開く Daily のときだけ表示。FullList(Todoタブ)では出さない：
        // タイトルのインライン編集にフォーカスすると onSelect→Select で右ペインが切り替わるため、「…」での右ペイン起動は不要・紛らわしい。
        if (displayMode != TodoDisplayMode.DailyToday) return;
        var more = NewUI("MoreBtn", row.transform);
        var moreImg = more.AddComponent<Image>();
        moreImg.color = Color.clear; // 透明の当たり判定（テキストで…を出す）
        var moreLE = more.AddComponent<LayoutElement>();
        moreLE.minWidth = 30; moreLE.preferredWidth = 30; moreLE.minHeight = 30;
        var moreTxt = NewText("Dots", more.transform, "\u22EF", UITheme_FocusMode.FontChipTitle, // ⋯ (U+22EF)
            UITheme_FocusMode.TextMuted);
        moreTxt.alignment = TextAlignmentOptions.Center;
        var moreTxtRT = moreTxt.GetComponent<RectTransform>();
        moreTxtRT.anchorMin = Vector2.zero; moreTxtRT.anchorMax = Vector2.one;
        moreTxtRT.offsetMin = Vector2.zero; moreTxtRT.offsetMax = Vector2.zero;
        var moreBtn = more.AddComponent<Button>();
        moreBtn.targetGraphic = moreImg;
        moreBtn.onClick.AddListener(() =>
        {
            if (_todoModal != null) _todoModal.OpenEdit(captured); // モーダルで編集
        });
    }

    // 並べ替え用の小さな矢印ボタン。enabled=false なら押せずグレーアウト表示。
    // 並べ替え直後の行を一瞬ハイライトしてフェードで戻す（クリックの結果が目で追える）。
    private System.Collections.IEnumerator FlashRow(Image rowImg, bool selected, bool done)
    {
        if (rowImg == null) yield break;
        Color baseColor = selected ? UITheme_FocusMode.SelectedBG
                        : done ? UITheme_FocusMode.DoneBG : UITheme_FocusMode.PanelBG;
        Color flashColor = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.32f);

        // 移動をしっかり見せてから光らせる開始ディレイ。
        float e = 0f, delay = 0.30f;
        while (e < delay) { if (rowImg == null) yield break; e += Time.unscaledDeltaTime; yield return null; }

        // 点灯した瞬間が最大、そこから単調に減衰するだけのカーブ（山を1つにして二峰感を排除）。
        //   強さ k = (1-progress)^2 … progress 0 で最大、なめらかに 0 へ。
        float dur = 0.9f; e = 0f; // フェードアウトの余韻を少し長く
        while (e < dur)
        {
            if (rowImg == null) yield break;
            e += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(e / dur);
            float k = (1f - p) * (1f - p);              // 先頭が最大→なめらかに0へ減衰（山は1つ）
            rowImg.color = Color.Lerp(baseColor, flashColor, k);
            yield return null;
        }
        if (rowImg != null) rowImg.color = baseColor;
        _flashCo = null;
    }

    private const float REORDER_GLYPH_SIZE = 14f; // ▲▼の固定サイズ
    private void BuildReorderButton(Transform parent, string glyph, bool enabled, UnityEngine.Events.UnityAction onClick)
    {
        var go = NewUI("ReorderBtn", parent);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 26; le.preferredWidth = 26; le.minHeight = 30;

        // 背景（当たり判定＆ホバー/押下の色遷移面）。type=Simple かつ sprite=null だと
        // Unity 既定の白テクスチャで矩形が描画されるので、ColorTint の色変化が見える。
        var bg = go.AddComponent<Image>();
        bg.sprite = null;
        bg.type = Image.Type.Simple;
        bg.color = Color.clear; // 通常は透明（ColorTintのnormal=clearと一致）

        // ▲▼ グリフ。Kotonoruは▲(U+25B2)グリフを持たずNotoSansJPにフォールバックして
        // ▲と▼でサイズが食い違う。→ 両方のグリフを持つ日本語フォールバックフォントに固定して揃える。
        var txt = NewText("Arrow", go.transform, glyph, UITheme_FocusMode.FontCaption,
            enabled ? UITheme_FocusMode.TextSecondary : UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.28f));
        var jp = GetJpFallbackFont();
        if (jp != null) txt.font = jp;             // ▲▼を同一フォントで描画＝サイズ統一
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
            cb.highlightedColor = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextSecondary, 0.22f); // ホバーで薄グレー
            cb.pressedColor     = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.55f); // 押下で青
            cb.selectedColor    = Color.clear;
            cb.disabledColor    = Color.clear;
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);
        }
    }

    // ▲▼ を同一フォントで揃えるための日本語フォールバックフォント取得（NotoSansJP想定）。
    private TMP_FontAsset _jpFallbackCache;
    private TMP_FontAsset GetJpFallbackFont()
    {
        if (_jpFallbackCache != null) return _jpFallbackCache;
        // TMPの設定にあるフォールバックの先頭（日本語）を使う。無ければResourcesから探す。
        var settings = TMPro.TMP_Settings.fallbackFontAssets;
        if (settings != null && settings.Count > 0) _jpFallbackCache = settings[0];
        if (_jpFallbackCache == null)
            _jpFallbackCache = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansJP-Regular SDF");
        return _jpFallbackCache;
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
        var label = NewText("Label", empty.transform, "タスクはありません。「+ 追加」から作成できます",
            UITheme_FocusMode.FontBody, UITheme_FocusMode.TextPlaceholder);
        label.alignment = TextAlignmentOptions.Center;
        var rt = label.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ── 部品ヘルパー ──────────────────────────

    /// <summary>行のタイトル用インラインInputFieldを動的生成する（B-3: 標準キャレット・シングルライン）。
    /// 【重要】TMP_InputFieldのAwakeは子のTMP_Textを探すため、TextArea/Text/Placeholderを
    /// 先に作ってから最後にAddComponent<TMP_InputField>()する（StickyNoteで確立した順序）。</summary>
    private TMP_InputField BuildInlineTitleInput(Transform parent, TodoItem item, bool done)
    {
        var captured = item;

        // フィールド親
        var fieldGO = NewUI("TitleInput", parent);
        var fieldRT = fieldGO.GetComponent<RectTransform>();
        fieldRT.anchorMin = Vector2.zero; fieldRT.anchorMax = Vector2.one;
        fieldRT.offsetMin = Vector2.zero; fieldRT.offsetMax = Vector2.zero;
        var fieldImg = fieldGO.AddComponent<Image>();
        fieldImg.color = Color.clear; // 透明（背景は行が持つ）。raycastは受ける

        // ① TextArea（先に作成・RectMask2Dでクリップ）
        var taGO = NewUI("TextArea", fieldGO.transform);
        var taRT = taGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(2f, 0f); taRT.offsetMax = new Vector2(-2f, 0f);
        taGO.AddComponent<RectMask2D>();

        // ② Text（先に作成：Awakeが最初にTextを見つける）
        var txtTMP = NewText("Text", taGO.transform, item.title ?? "", UITheme_FocusMode.FontChipTitle,
            done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextSecondary);
        if (done) txtTMP.fontStyle = FontStyles.Strikethrough;
        var txtRT = txtTMP.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;

        // ③ Placeholder（後に作成）
        var phTMP = NewText("Placeholder", taGO.transform, "タスク名", UITheme_FocusMode.FontChipTitle,
            UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.5f));
        var phRT = phTMP.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;

        // ─ CustomCaret（TextAreaの最後の子・自前描画。標準キャレットは見えないため）─
        var caretGO = NewUI("CustomCaret", taGO.transform);
        var caretRT = caretGO.GetComponent<RectTransform>();
        caretRT.anchorMin = new Vector2(0f, 1f); caretRT.anchorMax = new Vector2(0f, 1f);
        caretRT.pivot = new Vector2(0f, 1f);
        caretRT.sizeDelta = new Vector2(2f, 16f);
        caretRT.anchoredPosition = Vector2.zero;
        var caretImg = caretGO.AddComponent<Image>();
        caretImg.color = Color.clear;          // 普段は非表示（点滅で表示）
        caretImg.raycastTarget = false;

        // ④ 最後に TMP_InputField を AddComponent（子が揃った状態で初期化が通る）
        var input = fieldGO.AddComponent<TMP_InputField>();
        input.targetGraphic = fieldImg;
        input.textViewport  = taRT;
        input.textComponent = txtTMP;
        input.placeholder   = phTMP;
        input.lineType      = TMP_InputField.LineType.SingleLine;
        input.text          = item.title ?? "";
        // B-2: TMP標準キャレットを透明にして、CustomCaret(Image)で自前描画する
        input.customCaretColor = true;
        input.caretColor       = Color.clear;  // 標準キャレット非表示
        input.caretWidth       = 2;
        input.selectionColor   = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.4f);

        // onSelect: フォーカスイン → 右ペインに当該タスク表示 ＋ この行のキャレットをアクティブ化
        input.onSelect.AddListener(_ =>
        {
            if (_suppressInline) return;
            Select(captured);
            ActivateCaret(input, caretRT, caretImg);
        });
        // onDeselect: フォーカスアウト → キャレットを消す
        input.onDeselect.AddListener(_ => DeactivateCaret(input));
        // 【消失バグ対策】onValueChangedでの逐次 captured.title 更新は行わない。
        //   入力中の値は TMP_InputField 自身が保持し、確定時(onEndEdit)にまとめて保存する。
        //   （逐次更新がIME確定/フォーカス遷移/矢印キーと競合して内容消失を起こしていた）
        // onEndEdit: 確定時（Enter / フォーカス外れ / IME確定後）に保存＋詳細へ同期
        input.onEndEdit.AddListener(v =>
        {
            if (_suppressInline) return;
            if (string.IsNullOrWhiteSpace(v))
            {
                // 空は元に戻す（無名行を防ぐ）。表示だけ戻し、保存はしない。
                _suppressInline = true;
                input.text = captured.title ?? "";
                _suppressInline = false;
                return;
            }
            if (captured.title == v) return;   // 変化なしなら何もしない
            captured.title = v;
            NotebookManager.Instance?.UpdateTodo(captured);
            if (detail != null) detail.RefreshTitleIfOpen(captured.id, v); // 確定時同期
        });

        return input;
    }

    // ───────── Todoタブ行の編集/削除（フォルダ方式をミラー・削除は直接）─────────
    private void StartTodoRename(string id)
    {
        _editingTodoId = id;
        Rebuild();
    }

    private void CommitTodoRename(TodoItem item, string newName)
    {
        if (item == null || _editingTodoId != item.id) return;
        string nt = (newName ?? "").Trim();
        if (nt.Length > 0 && item.title != nt)   // 空は据え置き（タスクは空タイトル不可）
        {
            item.title = nt;
            NotebookManager.Instance?.UpdateTodo(item);
            if (detail != null) detail.RefreshTitleIfOpen(item.id, nt);
        }
        string id = item.id;
        if (_todoRenameExitCo != null) StopCoroutine(_todoRenameExitCo);
        _todoRenameExitCo = StartCoroutine(ExitTodoEditNextFrame(id));
    }

    private System.Collections.IEnumerator ExitTodoEditNextFrame(string id)
    {
        yield return null;
        _todoRenameExitCo = null;
        if (_editingTodoId == id) { _editingTodoId = null; Rebuild(); }
    }

    private void EnterConfirmDeleteTodo(string id)
    {
        CancelTodoEdit();
        _confirmDeleteTodoId = id;
        Rebuild();
    }

    private void CancelTodoEdit()
    {
        _editingTodoId = null;
        _confirmDeleteTodoId = null;
        if (_todoRenameExitCo != null) { StopCoroutine(_todoRenameExitCo); _todoRenameExitCo = null; }
    }

    private void DeleteTodoRow(string id)
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        CancelTodoEdit();
        nm.DeleteTodo(id);                       // 直接削除（ゴミ箱なし）
        if (detail != null && detail.CurrentId == id) detail.Clear();
        if (_selectedId == id) _selectedId = null;
        Rebuild();
    }

    private void AttachTodoRenameInput(Transform slot, TodoItem item)
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
        var phTMP = NewText("Placeholder", taGO.transform, "タスク名", UITheme_FocusMode.FontChipTitle, UITheme_FocusMode.WithAlpha(UITheme_FocusMode.TextMuted, 0.5f));
        var phRT = phTMP.GetComponent<RectTransform>(); phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one; phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var caretGO = NewUI("CustomCaret", taGO.transform);
        var caretRT = caretGO.GetComponent<RectTransform>(); caretRT.anchorMin = new Vector2(0f, 1f); caretRT.anchorMax = new Vector2(0f, 1f); caretRT.pivot = new Vector2(0f, 1f); caretRT.sizeDelta = new Vector2(2f, 16f); caretRT.anchoredPosition = Vector2.zero;
        var caretImg = caretGO.AddComponent<Image>(); caretImg.color = Color.clear; caretImg.raycastTarget = false;
        var input = fieldGO.AddComponent<TMP_InputField>();
        input.targetGraphic = fieldImg; input.textViewport = taRT; input.textComponent = txtTMP; input.placeholder = phTMP;
        input.lineType = TMP_InputField.LineType.SingleLine; input.text = current;
        input.customCaretColor = true; input.caretColor = Color.clear; input.caretWidth = 2;
        input.selectionColor = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentSatBlue, 0.4f);
        input.onSelect.AddListener(_ => { if (_suppressInline) return; ActivateCaret(input, caretRT, caretImg); });
        input.onDeselect.AddListener(_ => DeactivateCaret(input));
        input.onEndEdit.AddListener(v => { if (_suppressInline) return; CommitTodoRename(captured, v); });
        input.Select(); input.ActivateInputField();
        int caret = current.Length; input.caretPosition = caret; input.selectionAnchorPosition = caret; input.selectionFocusPosition = caret;
        ActivateCaret(input, caretRT, caretImg);
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
        // font未設定（Daily用にAddComponentした場合など）はTMP既定フォントにフォールバック
        tmp.font = font != null ? font : TMP_Settings.defaultFontAsset;
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

    // 未来日セクションの見出し用: "M/d（曜）"。dateKey は "yyyy-MM-dd"。
    private static readonly string[] _jpDow = { "日", "月", "火", "水", "木", "金", "土" };
    private static string FormatDateHeader(string dateKey)
    {
        if (DateTime.TryParse(dateKey, out var d))
            return $"{d.Month}/{d.Day}（{_jpDow[(int)d.DayOfWeek]}）";
        return dateKey;
    }

    private static string FormatDateChip(TodoItem item)
    {
        if (!TryDate(item, out var d)) return null;
        var s = $"{d.Month}/{d.Day}";
        if (!string.IsNullOrEmpty(item.time)) s += " " + item.time;
        return s;
    }
}
