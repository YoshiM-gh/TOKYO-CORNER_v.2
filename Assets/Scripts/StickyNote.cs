using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 付箋1枚。TopBar ドラッグ移動・タグ別カラー・削除・テキスト編集・右下リサイズ。
/// 空のまま編集終了 → 自動削除。最大 2 倍サイズまでリサイズ可。
/// </summary>
public class StickyNote : MonoBehaviour
{
    public string NoteId { get; private set; }

    // ── カラー定義 ──────────────────────────────────────────
    private static readonly (string id, Color bg, string label)[] TAG_COLORS =
    {
        ("habit",   new Color(0.67f, 0.88f, 0.72f), "習"),
        ("yotei",   new Color(0.55f, 0.80f, 0.96f), "予"),
        ("mokuhyo", new Color(0.97f, 0.79f, 0.49f), "目"),
        ("todo",    new Color(0.97f, 0.95f, 0.50f), "T"),
        ("hobby",   new Color(0.87f, 0.70f, 0.97f), "遊"),
    };
    private static readonly Color DEFAULT_BG = Color.white; // ニュートラル＝白背景（黒文字は GetTextColor が自動選択）

    // ── サイズ制約 ────────────────────────────────────────
    private const float MIN_W = 160f, MIN_H = 120f;
    private const float MAX_W = 400f, MAX_H = 400f; // デフォルト 200×200 の 2 倍

    // ── 内部フィールド ─────────────────────────────────────
    private string         _dateKey;
    private string         _tagId = "";
    private Action         _onChanged;
    private bool           _saved;

    private RectTransform  _rt;
    private RectTransform  _canvasRT;
    private TMP_InputField _contentInput;
    private Image          _bgImage;
    private Image          _handleImage;
    private Image[]        _tagDots;

    // drag
    private bool    _dragging;
    private Vector2 _dragPointerOffset;

    // resize
    private bool    _resizing;
    private Vector2 _resizeStartPtr;
    private Vector2 _resizeStartSize;

    // カスタムカーレット（TMP 内部カーレットをバイパスした Image ベース実装）
    private RectTransform  _caretRT;
    private Image          _caretImg;
    private Coroutine      _caretCoroutine;
    private int            _lastCaretPos = -1;
    private Color          _caretColor   = new Color(0.08f, 0.08f, 0.14f);
    private bool           _suppressEmptyDelete;  // ノート自身の UI 操作中は空削除を抑制

    // ── Init（保存済みノード）────────────────────────────────
    public void Init(StickyNoteData data, Canvas rootCanvas,
                     RectTransform canvasRT, Action onChanged)
    {
        NoteId     = data.id;
        _dateKey   = data.dateKey;
        _tagId     = data.tagId ?? "";
        if (_tagId == "leisure") _tagId = "hobby";   // 旧ID互換（TagConfig の実IDは hobby）
        _onChanged = onChanged;
        _canvasRT  = canvasRT;
        _saved     = true;
        _rt = GetComponent<RectTransform>();

        _rt.anchorMin = new Vector2(0f, 1f);
        _rt.anchorMax = new Vector2(0f, 1f);
        _rt.pivot     = new Vector2(0f, 1f);
        _rt.sizeDelta = new Vector2(Mathf.Clamp(data.width,  MIN_W, MAX_W),
                                    Mathf.Clamp(data.height, MIN_H, MAX_H));

        Canvas.ForceUpdateCanvases();
        float cW = canvasRT.rect.width;
        float cH = canvasRT.rect.height;
        _rt.anchoredPosition = new Vector2(data.anchorX * cW, -data.anchorY * cH);

        BuildUI(rootCanvas);
        ApplyTagColor(_tagId);
        if (_contentInput != null) _contentInput.SetTextWithoutNotify(data.content);
    }

    // ── InitNew（新規・未保存）────────────────────────────────
    public void InitNew(string dateKey, Canvas rootCanvas,
                        RectTransform canvasRT, Vector2 pos, Action onChanged)
    {
        NoteId     = System.Guid.NewGuid().ToString();
        _dateKey   = dateKey;
        _tagId     = "";
        _onChanged = onChanged;
        _canvasRT  = canvasRT;
        _saved     = false;
        _rt = GetComponent<RectTransform>();

        _rt.anchorMin = new Vector2(0f, 1f);
        _rt.anchorMax = new Vector2(0f, 1f);
        _rt.pivot     = new Vector2(0f, 1f);
        _rt.sizeDelta = new Vector2(200f, 200f);

        Canvas.ForceUpdateCanvases();
        float cW = canvasRT.rect.width;
        float cH = canvasRT.rect.height;
        float x  = Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, cW - 200f));
        float y  = Mathf.Clamp(pos.y, Mathf.Min(0f, -(cH - 200f)), 0f);
        _rt.anchoredPosition = new Vector2(x, y);

        BuildUI(rootCanvas);
        ApplyTagColor(_tagId);
    }

    // ── UI 構築 ───────────────────────────────────────────────
    private void BuildUI(Canvas rootCanvas)
    {
        // 背景 + ノート本体クリックで最前面
        _bgImage = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        var noteBtn = gameObject.AddComponent<Button>();
        noteBtn.targetGraphic = _bgImage;
        noteBtn.transition    = Selectable.Transition.None;
        noteBtn.onClick.AddListener(() => transform.SetAsLastSibling());

        // ─ TopBar ────────────────────────────────────────────
        var barGO  = new GameObject("TopBar", typeof(RectTransform));
        barGO.transform.SetParent(transform, false);
        var barRT  = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 1f); barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot     = new Vector2(0.5f, 1f);
        barRT.sizeDelta = new Vector2(0f, 28f);
        barRT.anchoredPosition = Vector2.zero;
        _handleImage = barGO.AddComponent<Image>();
        _handleImage.color = new Color(0f, 0f, 0f, 0.18f);

        var et = barGO.AddComponent<EventTrigger>();
        AddEt(et, EventTriggerType.PointerDown, _ => _suppressEmptyDelete = true);
        AddEt(et, EventTriggerType.BeginDrag, OnBarBeginDrag);
        AddEt(et, EventTriggerType.Drag,      OnBarDrag);
        AddEt(et, EventTriggerType.EndDrag,   OnBarEndDrag);

        // タグドット（TopBar 左）
        _tagDots = new Image[TAG_COLORS.Length];
        for (int i = 0; i < TAG_COLORS.Length; i++)
        {
            int cap = i;
            var (tid, bg, lbl) = TAG_COLORS[i];
            var dGO  = new GameObject($"TagDot_{tid}", typeof(RectTransform));
            dGO.transform.SetParent(barGO.transform, false);
            var dRT  = dGO.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0f, 0.5f); dRT.anchorMax = new Vector2(0f, 0.5f);
            dRT.pivot     = new Vector2(0f, 0.5f);
            dRT.sizeDelta = new Vector2(22f, 22f);
            dRT.anchoredPosition = new Vector2(4f + i * 26f, 0f);
            // タグドット色も TagConfig 連動（なければ TAG_COLORS 色）
            var tagData = TagConfig.GetById(tid);
            var dotBg = tagData != null ? new Color(tagData.chipBG.r, tagData.chipBG.g, tagData.chipBG.b, 1f) : bg;
            var dImg  = dGO.AddComponent<Image>(); dImg.color = dotBg;
            var dBtn  = dGO.AddComponent<Button>(); dBtn.targetGraphic = dImg;
            dBtn.onClick.AddListener(() => { ApplyTagColor(TAG_COLORS[cap].id); SaveNote(); });
            var dET = dGO.AddComponent<EventTrigger>();
            AddEt(dET, EventTriggerType.PointerDown, _ => _suppressEmptyDelete = true);
            _tagDots[i] = dImg;
            var lGO  = new GameObject("L", typeof(RectTransform));
            lGO.transform.SetParent(dGO.transform, false);
            var lRT  = lGO.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = lRT.offsetMax = Vector2.zero;
            var lTxt = lGO.AddComponent<TextMeshProUGUI>();
            lTxt.text = lbl; lTxt.fontSize = 10f;
            lTxt.color = Color.white;
            lTxt.alignment = TextAlignmentOptions.Center;
            lTxt.raycastTarget = false;
        }

        // 削除ボタン（TopBar 右）
        var delGO  = new GameObject("DeleteBtn", typeof(RectTransform));
        delGO.transform.SetParent(barGO.transform, false);
        var delRT  = delGO.GetComponent<RectTransform>();
        delRT.anchorMin = new Vector2(1f, 0.5f); delRT.anchorMax = new Vector2(1f, 0.5f);
        delRT.pivot     = new Vector2(1f, 0.5f);
        delRT.sizeDelta = new Vector2(26f, 26f);
        delRT.anchoredPosition = new Vector2(-4f, 0f);
        var delImg = delGO.AddComponent<Image>(); delImg.color = new Color(0f,0f,0f,0f);
        var delBtn = delGO.AddComponent<Button>(); delBtn.targetGraphic = delImg;
        delBtn.onClick.AddListener(OnDeleteClicked);
        var xGO  = new GameObject("X", typeof(RectTransform));
        xGO.transform.SetParent(delGO.transform, false);
        var xRT  = xGO.GetComponent<RectTransform>();
        xRT.anchorMin = Vector2.zero; xRT.anchorMax = Vector2.one;
        xRT.offsetMin = xRT.offsetMax = Vector2.zero;
        var xTxt = xGO.AddComponent<TextMeshProUGUI>();
        xTxt.text = "×"; xTxt.fontSize = 14f;
        xTxt.color = new Color(0.08f, 0.08f, 0.14f, 0.65f);
        xTxt.alignment = TextAlignmentOptions.Center;
        xTxt.raycastTarget = false;

        // ─ ContentInput ──────────────────────────────────────
        // 【根本修正】TMP_InputField の Awake() は子の TMP_Text を GetComponentInChildren で探す。
        // AddComponent<TMP_InputField>() 時点で子が存在しないとカーレット初期化がスキップされる。
        // → TextArea/Text/Placeholder を先に作り、TMP_InputField を最後に AddComponent する。

        var fieldGO = new GameObject("ContentInput", typeof(RectTransform));
        fieldGO.transform.SetParent(transform, false);
        var fieldRT = fieldGO.GetComponent<RectTransform>();
        fieldRT.anchorMin = Vector2.zero; fieldRT.anchorMax = Vector2.one;
        fieldRT.offsetMin = new Vector2(4f, 4f);
        fieldRT.offsetMax = new Vector2(-4f, -30f);
        var fieldImg = fieldGO.AddComponent<Image>();
        fieldImg.color = new Color(0f, 0f, 0f, 0f);

        // ① TextArea（先に作成）
        var taGO = new GameObject("TextArea", typeof(RectTransform));
        taGO.transform.SetParent(fieldGO.transform, false);
        var taRT = taGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(4f, 2f); taRT.offsetMax = new Vector2(-4f, -2f);
        taGO.AddComponent<RectMask2D>();

        // フォントを EventModal の InputField から取得（動作保証あり）
        TMPro.TMP_FontAsset sceneFont = null;
        var modalInp = UnityEngine.Object.FindObjectOfType<EventModal>(true)
                           ?.GetComponentInChildren<TMPro.TMP_InputField>(true);
        sceneFont = modalInp?.textComponent?.font;
        if (sceneFont == null)
        {
            var anyTMP = UnityEngine.Object.FindObjectOfType<TextMeshProUGUI>(true);
            sceneFont = anyTMP?.font;
        }

        // ② Text を先に作成（Awake の GetComponentInChildren が最初に Text を見つけるため）
        var txtGO  = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(taGO.transform, false);
        SetStretch(txtGO);
        var txtTxt = txtGO.AddComponent<TextMeshProUGUI>();
        if (sceneFont != null) txtTxt.font = sceneFont;
        txtTxt.fontSize = 15f;
        txtTxt.color    = new Color(0.08f, 0.08f, 0.14f, 1f);
        txtTxt.alignment = TextAlignmentOptions.TopLeft;
        txtTxt.enableWordWrapping = true;
        txtTxt.raycastTarget = false;

        // ③ Placeholder を後に作成
        var phGO  = new GameObject("Placeholder", typeof(RectTransform));
        phGO.transform.SetParent(taGO.transform, false);
        SetStretch(phGO);
        var phTxt = phGO.AddComponent<TextMeshProUGUI>();
        if (sceneFont != null) phTxt.font = sceneFont;
        phTxt.text = "メモ..."; phTxt.fontSize = 15f;
        phTxt.color = new Color(0.08f, 0.08f, 0.14f, 0.40f);
        phTxt.alignment = TextAlignmentOptions.TopLeft;
        phTxt.enableWordWrapping = true;
        phTxt.raycastTarget = false;

        // ─ CustomCaret（TMP 内部カーレットをバイパスする Image GO）──────
        // TextArea の最後の子として追加（最前面にレンダリングされる）
        var caretGO = new GameObject("CustomCaret", typeof(RectTransform));
        caretGO.transform.SetParent(taGO.transform, false);
        _caretRT = caretGO.GetComponent<RectTransform>();
        _caretRT.anchorMin = new Vector2(0f, 1f);  // 左上アンカー
        _caretRT.anchorMax = new Vector2(0f, 1f);
        _caretRT.pivot     = new Vector2(0f, 1f);  // 左上ピボット
        _caretRT.sizeDelta = new Vector2(2f, 16f); // 2px × 行高（後で更新）
        _caretRT.anchoredPosition = Vector2.zero;
        _caretImg = caretGO.AddComponent<Image>();
        _caretImg.color = Color.clear;             // 初期は非表示
        _caretImg.raycastTarget = false;

        // ④ TMP_InputField を最後に AddComponent
        //    → Awake() が走る時点で txtTxt が子に存在 → 内部初期化が正しく通る
        _contentInput = fieldGO.AddComponent<TMP_InputField>();
        _contentInput.targetGraphic  = fieldImg;
        _contentInput.textViewport   = taRT;
        _contentInput.textComponent  = txtTxt;
        _contentInput.placeholder    = phTxt;
        _contentInput.lineType       = TMP_InputField.LineType.MultiLineNewline;

        // EventModal と同じカーレット設定（customCaretColor=false が正解）
        _contentInput.customCaretColor = true;   // TMP 内部カーレットを透明に
        _contentInput.caretColor       = Color.clear; // TMP カーレット非表示（カスタム使用）
        _contentInput.caretWidth       = 2;
        _contentInput.caretBlinkRate   = 0.85f;
        _contentInput.selectionColor   = new Color(0.1f, 0.4f, 0.9f, 0.4f);

        _contentInput.onEndEdit.AddListener(OnContentEndEdit);
        // カスタムカーレット用イベント
        _contentInput.onSelect.AddListener(_ => StartCaretBlink());
        _contentInput.onDeselect.AddListener(_ => StopCaretBlink());
        _contentInput.onValueChanged.AddListener(_ => UpdateCaretPosition());

        // ─ リサイズハンドル（右下コーナー）──────────────────
        var rhGO  = new GameObject("ResizeHandle", typeof(RectTransform));
        rhGO.transform.SetParent(transform, false);
        var rhRT  = rhGO.GetComponent<RectTransform>();
        rhRT.anchorMin = new Vector2(1f, 0f); rhRT.anchorMax = new Vector2(1f, 0f);
        rhRT.pivot     = new Vector2(1f, 0f); // 右下ピボット
        rhRT.sizeDelta = new Vector2(16f, 16f);
        rhRT.anchoredPosition = Vector2.zero;
        var rhImg = rhGO.AddComponent<Image>();
        rhImg.color = new Color(0f, 0f, 0f, 0.20f);
        // ↘ マーク
        var rhTGO = new GameObject("Icon", typeof(RectTransform));
        rhTGO.transform.SetParent(rhGO.transform, false);
        SetStretch(rhTGO);
        var rhTxt = rhTGO.AddComponent<TextMeshProUGUI>();
        rhTxt.text = "↘"; rhTxt.fontSize = 9f;
        rhTxt.color = new Color(0.08f, 0.08f, 0.14f, 0.55f);
        rhTxt.alignment = TextAlignmentOptions.BottomRight;
        rhTxt.raycastTarget = false;

        var rhET = rhGO.AddComponent<EventTrigger>();
        AddEt(rhET, EventTriggerType.PointerDown, _ => _suppressEmptyDelete = true);
        AddEt(rhET, EventTriggerType.BeginDrag, OnResizeBeginDrag);
        AddEt(rhET, EventTriggerType.Drag,      OnResizeDrag);
        AddEt(rhET, EventTriggerType.EndDrag,   OnResizeEndDrag);
    }

    private static void SetStretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void AddEt(EventTrigger et, EventTriggerType type,
                               UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(action);
        et.triggers.Add(e);
    }

    // ── タグカラー ─────────────────────────────────────────────
    private void ApplyTagColor(string tagId)
    {
        _tagId = tagId ?? "";
        var bg = GetBgColor(_tagId);
        var tc = GetTextColor(bg);
        _caretColor = tc;
        if (_bgImage)     _bgImage.color = bg;
        // テキスト・プレースホルダー・カーレット色を背景輝度に合わせて更新
        if (_contentInput != null)
        {
            if (_contentInput.textComponent != null) _contentInput.textComponent.color = tc;
            if (_contentInput.placeholder    is TextMeshProUGUI ph)
                ph.color = new Color(tc.r, tc.g, tc.b, 0.45f);
        }
        if (_handleImage) _handleImage.color = new Color(0f, 0f, 0f, 0.18f);
        if (_tagDots == null) return;
        for (int i = 0; i < TAG_COLORS.Length; i++)
        {
            bool sel = TAG_COLORS[i].id == _tagId;
            var td = TagConfig.GetById(TAG_COLORS[i].id);
            var bc = td != null ? new Color(td.chipBG.r, td.chipBG.g, td.chipBG.b, 1f) : TAG_COLORS[i].bg;
            _tagDots[i].color = sel ? Color.Lerp(bc, Color.white, 0.35f) : Color.Lerp(bc, new Color(0,0,0,1), 0.2f);
        }
    }

    /// <summary>背景輝度に応じて白/暗テキストを自動選択</summary>
    private static Color GetTextColor(Color bg)
    {
        float lum = 0.2126f * bg.r + 0.7152f * bg.g + 0.0722f * bg.b;
        return lum > 0.55f   // タグ色のCardBGは全て0.45未満 → 白文字。白デフォルトのみ暗文字
            ? new Color(0.08f, 0.08f, 0.14f, 1f) // 明るい背景 → 暗テキスト
            : Color.white;                        // 暗い背景 → 白テキスト
    }

    private static Color GetBgColor(string tagId)
    {
        // タグ色はアイテムカードと同じ CardBG（タグ色×背景ブレンドの不透明色）で統一
        if (!string.IsNullOrEmpty(tagId))
        {
            var tag = TagConfig.GetById(tagId);
            if (tag != null) return UITheme_FocusMode.CardBG(tag.chipBG); // アイテムカードと同色（落ち着いた不透明色）
        }
        // leisure など TagConfig 外のタグは TAG_COLORS を使用
        foreach (var (id, bg, _) in TAG_COLORS)
            if (id == tagId) return bg;
        return DEFAULT_BG;
    }

    // ── ドラッグ移動 ───────────────────────────────────────────
    private void OnBarBeginDrag(BaseEventData data)
    {
        _dragging = true;
        transform.SetAsLastSibling();
        var ped = (PointerEventData)data;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRT, ped.position, ped.pressEventCamera, out var lp);
        _dragPointerOffset = (Vector2)_rt.anchoredPosition - lp;
    }

    private void OnBarDrag(BaseEventData data)
    {
        if (!_dragging) return;
        var ped = (PointerEventData)data;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRT, ped.position, ped.pressEventCamera, out var lp);
        var newPos = lp + _dragPointerOffset;
        float cW = _canvasRT.rect.width;  float cH = _canvasRT.rect.height;
        float nW = _rt.sizeDelta.x;       float nH = _rt.sizeDelta.y;
        newPos.x = Mathf.Clamp(newPos.x, 0f, Mathf.Max(0f, cW - nW));
        newPos.y = Mathf.Clamp(newPos.y, Mathf.Min(0f, -(cH - nH)), 0f);
        _rt.anchoredPosition = newPos;
    }

    private void OnBarEndDrag(BaseEventData data)
    {
        _dragging = false;
        if (_saved) SaveNote();
    }

    // ── リサイズ ───────────────────────────────────────────────
    private void OnResizeBeginDrag(BaseEventData data)
    {
        _resizing = true;
        transform.SetAsLastSibling();
        var ped = (PointerEventData)data;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRT, ped.position, ped.pressEventCamera, out _resizeStartPtr);
        _resizeStartSize = _rt.sizeDelta;
    }

    private void OnResizeDrag(BaseEventData data)
    {
        if (!_resizing) return;
        var ped = (PointerEventData)data;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRT, ped.position, ped.pressEventCamera, out var curPtr);
        var delta = curPtr - _resizeStartPtr;
        // anchor は左上なので X は右方向（+）、Y は下方向（-）が拡大
        float newW = Mathf.Clamp(_resizeStartSize.x + delta.x,  MIN_W, MAX_W);
        float newH = Mathf.Clamp(_resizeStartSize.y - delta.y,  MIN_H, MAX_H);
        _rt.sizeDelta = new Vector2(newW, newH);
    }

    private void OnResizeEndDrag(BaseEventData data)
    {
        _resizing = false;
        if (_saved) SaveNote();
    }

    // ── コンテンツ編集終了 ─────────────────────────────────────
    private void OnContentEndEdit(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            // PointerDown ハンドラが _suppressEmptyDelete を立てた場合は削除を抑制
            // (タグ色変更・リサイズ・移動 など、ノート自身の UI 操作中)
            StartCoroutine(DelayedDeleteIfStillEmpty());
        }
        else
        {
            if (!_saved)
            {
                Canvas.ForceUpdateCanvases();
                float cW = _canvasRT.rect.width;
                float cH = _canvasRT.rect.height;
                var   pos = _rt.anchoredPosition;
                float ax  = cW > 0f ?  pos.x / cW : 0f;
                float ay  = cH > 0f ? -pos.y / cH : 0f;
                var   d   = NotebookManager.Instance?.AddStickyNote(_dateKey, ax, ay, _tagId);
                if (d != null) { NoteId = d.id; _saved = true; SaveNote(); }
            }
            else SaveNote();
        }
    }

    // ── 削除 ─────────────────────────────────────────────────
    private void OnDeleteClicked()
    {
        if (_saved) NotebookManager.Instance?.DeleteStickyNote(NoteId);
        Destroy(gameObject);
    }

    // ── 保存 ─────────────────────────────────────────────────
    private void SaveNote()
    {
        if (!_saved || NotebookManager.Instance == null) return;
        NotebookManager.Instance.UpdateStickyNote(ToData());
    }

    public StickyNoteData ToData()
    {
        float cW = _canvasRT != null ? _canvasRT.rect.width  : 1f;
        float cH = _canvasRT != null ? _canvasRT.rect.height : 1f;
        var   pos = _rt.anchoredPosition;
        return new StickyNoteData
        {
            id = NoteId, dateKey = _dateKey,
            content = _contentInput?.text ?? "",
            anchorX = cW > 0f ?  pos.x / cW : 0f,
            anchorY = cH > 0f ? -pos.y / cH : 0f,
            width   = _rt.sizeDelta.x, height = _rt.sizeDelta.y,
            colorIndex = 0, tagId = _tagId,
        };
    }

    public void FocusInput()
    {
        if (_contentInput == null) return;
        // TMP がメッシュ未生成だとカーレットが出ないため強制更新
        _contentInput.ForceLabelUpdate();
        Canvas.ForceUpdateCanvases();
        UpdateCaretPosition();
        _contentInput.ActivateInputField();
        StartCaretBlink();
    }

    // ═══════════════════════════════════════════════════════════
    // カスタムカーレット実装（B案）
    // ═══════════════════════════════════════════════════════════

    private void LateUpdate()   // InputField の内部更新後に caret を追従させる
    {
        if (_contentInput == null || !_contentInput.isFocused) return;
        if (_contentInput.caretPosition != _lastCaretPos)
        {
            _lastCaretPos = _contentInput.caretPosition;
            UpdateCaretPosition();
            if (_caretCoroutine != null) StopCoroutine(_caretCoroutine);
            _caretCoroutine = StartCoroutine(CaretBlinkCoroutine());
        }
    }

    /// <summary>
    /// 空テキストのまま EditEnd された際に1フレーム待って状況確認。
    /// ノート自身の UI 操作中 (_suppressEmptyDelete=true) は削除しない。
    /// </summary>
    private System.Collections.IEnumerator DelayedDeleteIfStillEmpty()
    {
        yield return null;  // 1フレーム待機（PointerDown → onEndEdit 順序の逆転を吸収）

        // ノート自身の UI を操作中 or 再フォーカスされた場合は削除しない
        if (_suppressEmptyDelete || (_contentInput != null && _contentInput.isFocused))
        {
            _suppressEmptyDelete = false;
            yield break;
        }

        // 本当に外部クリックで離れた = 空ノートを削除
        if (_saved) NotebookManager.Instance?.DeleteStickyNote(NoteId);

        // DailyCalendarUI に次の SpawnNewNote をスキップさせる
        var daily = UnityEngine.Object.FindObjectOfType<DailyCalendarUI>(true);
        if (daily != null)
        {
            var bf = typeof(DailyCalendarUI).GetField("_blockNextNoteSpawn",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bf?.SetValue(daily, true);
        }
        Destroy(gameObject);
    }

    private void OnDestroy() { StopCaretBlink(); }

    private void StartCaretBlink()
    {
        if (_caretRT == null) return;
        UpdateCaretPosition();
        if (_caretCoroutine != null) StopCoroutine(_caretCoroutine);
        _caretCoroutine = StartCoroutine(CaretBlinkCoroutine());
    }

    private void StopCaretBlink()
    {
        if (_caretCoroutine != null) { StopCoroutine(_caretCoroutine); _caretCoroutine = null; }
        if (_caretImg != null) _caretImg.color = Color.clear;
    }

    private System.Collections.IEnumerator CaretBlinkCoroutine()
    {
        bool visible = true;
        var wfs = new UnityEngine.WaitForSeconds(0.53f);
        do
        {
            if (_caretImg != null)
                _caretImg.color = visible ? _caretColor : Color.clear;
            visible = !visible;
            yield return wfs;
        } while (_contentInput != null && _contentInput.isFocused);
        if (_caretImg != null) _caretImg.color = Color.clear;
    }

    private void UpdateCaretPosition()
    {
        if (_caretRT == null || _contentInput?.textComponent == null) return;

        _contentInput.ForceLabelUpdate();   // IME確定直後など、ラベル未反映の旧テキストで計算しないようにする
        var txt    = _contentInput.textComponent;
        txt.ForceMeshUpdate(true, true);   // forceTextReparsing=true: IME確定直後の古いcharacterInfoでカーレットが1文字ズレる対策

        var info   = txt.textInfo;
        var taRect = _caretRT.parent.GetComponent<RectTransform>().rect;
        int cp     = _contentInput.caretPosition;

        float caretX, caretY, caretH;

        if (info == null || info.characterCount == 0 || cp <= 0 || info.lineCount == 0)
        {
            if (info != null && info.lineCount > 0)
            {
                var li = info.lineInfo[0];
                caretY = li.ascender;
                caretH = Mathf.Max(li.ascender - li.descender, 1f);
            }
            else { caretY = taRect.yMax; caretH = txt.fontSize * 1.15f; }
            caretX = taRect.xMin;
        }
        else
        {
            int idx  = Mathf.Clamp(cp - 1, 0, info.characterCount - 1);
            var ci   = info.characterInfo[idx];
            caretX   = ci.xAdvance;
            int li   = Mathf.Clamp(ci.lineNumber, 0, info.lineCount - 1);
            var line = info.lineInfo[li];
            caretY   = line.ascender;
            caretH   = Mathf.Max(line.ascender - line.descender, 1f);
        }

        // TMP ローカル座標 → TextArea アンカー(左上)座標に変換
        _caretRT.anchoredPosition = new Vector2(
            caretX - taRect.xMin,
            caretY - taRect.yMax
        );
        _caretRT.sizeDelta = new Vector2(2f, caretH);
    }
}
