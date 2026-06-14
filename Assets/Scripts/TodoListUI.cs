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
public class TodoListUI : MonoBehaviour
{
    [SerializeField] private Toggle showDoneToggle;
    [SerializeField] private Button addButton;
    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private TodoDetailUI detail;

    private string _selectedId;
    private bool _suppressInline; // インライン編集の同期更新中、value/endEditの誤発火を抑制

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

    private void OnEnable()
    {
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
        var today = DateTime.Now.Date;

        var open = all.Where(t => !t.isCompleted).ToList();
        var overdue = SortDefault(open.Where(t => HasDateBefore(t, today)));
        var todays  = SortDefault(open.Where(t => IsOnDate(t, today)));
        var future  = open.Where(t => HasDateAfter(t, today))
                          .OrderBy(t => t.dateKey)
                          .ThenByDescending(t => t.priorityHigh)
                          .ThenBy(t => t.sortOrder)
                          .ThenBy(t => t.createdAt)
                          .ToList();
        var noDate  = SortDefault(open.Where(t => string.IsNullOrEmpty(t.dateKey)));

        BuildSection("期限切れ", overdue, today, UITheme_FocusMode.AccentRed);
        BuildSection("今日", todays, today, UITheme_FocusMode.AccentSatBlue);
        BuildSection("今後", future, today, UITheme_FocusMode.TextCaption);
        BuildSection("日付なし", noDate, today, UITheme_FocusMode.TextCaption);

        bool showDone = showDoneToggle != null && showDoneToggle.isOn;
        if (showDone)
        {
            var done = all.Where(t => t.isCompleted)
                          .OrderByDescending(t => t.completedAt)
                          .ToList();
            if (done.Count > 0)
            {
                BuildSectionHeader($"完了済み · {done.Count}件", UITheme_FocusMode.TextCaption);
                foreach (var item in done) BuildRow(item, today);
            }
        }

        if (open.Count == 0 && !showDone) BuildEmptyLabel();
    }

    private void BuildSection(string label, List<TodoItem> items, DateTime today, Color labelColor)
    {
        if (items.Count == 0) return;
        BuildSectionHeader($"{label} · {items.Count}件", labelColor);
        foreach (var item in items) BuildRow(item, today);
    }

    private static List<TodoItem> SortDefault(IEnumerable<TodoItem> src) =>
        src.OrderByDescending(t => t.priorityHigh)
           .ThenBy(t => t.sortOrder)
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
    private static bool HasDateAfter(TodoItem t, DateTime today) => TryDate(t, out var d) && d.Date > today;

    // ── 行の生成 ──────────────────────────────

    private void BuildRow(TodoItem item, DateTime today)
    {
        bool done = item.isCompleted;
        bool selected = item.id == _selectedId;

        var row = NewUI("Row_" + item.id, listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = selected ? UITheme_FocusMode.SelectedBG
                     : done ? UITheme_FocusMode.DoneBG : UITheme_FocusMode.PanelBG;
        UIStyleKit.ApplyRounded(rowImg, 10f);
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

        // 日付時刻チップ(期限切れ=赤 / 今日=青)
        var dateLabel = FormatDateChip(item);
        if (dateLabel != null)
        {
            Color chipText = UITheme_FocusMode.TextMuted;
            Color chipBG = UITheme_FocusMode.InputBG;
            if (!done && TryDate(item, out var d))
            {
                if (d.Date < today)
                { chipText = UITheme_FocusMode.AccentRed; chipBG = UITheme_FocusMode.WithAlpha(UITheme_FocusMode.AccentRed, 0.14f); }
                else if (d.Date == today)
                { chipText = UITheme_FocusMode.AccentSatBlue; chipBG = UITheme_FocusMode.AccentBlueFaint; }
            }
            BuildChip(row.transform, dateLabel, chipText, chipBG);
        }

        // 「…」詳細を開くアイコン（右端）
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
        moreBtn.onClick.AddListener(() => { Select(captured); Rebuild(); });
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
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = false;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }

    private static string FormatDateChip(TodoItem item)
    {
        if (!TryDate(item, out var d)) return null;
        var s = $"{d.Month}/{d.Day}";
        if (!string.IsNullOrEmpty(item.time)) s += " " + item.time;
        return s;
    }
}
