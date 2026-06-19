using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// メモタブ 左ペイン（ノート一覧）。M-1 コア + M-2a ピン。
/// - GetMemoNotes() を ピン優先 → 作成日降順 で表示。M-1 は単一フォルダ前提（フォルダUIは M-2b）。
/// - 行クリックで選択 → 右ペイン(MemoDetailUI)で編集。行は表示専用（インライン編集なし）。
/// - 行右端の星ボタンでピンの掛け外し。押すと①その場で星の色が変わり→②ひと呼吸おいて上へ移動→③移動先で一瞬フラッシュ。
/// - 「+ 追加」で新規ノート作成 → 選択しタイトルにフォーカス。
/// - メモ変更は本タブ内で完結するため Rebuild は操作時に直接呼ぶ（CanvasGroupフェードでOnEnableが
///   再発火しない問題があるが、外部からメモが変わることはないので DataVersion監視は不要）。
/// - 保存時はリスト全体を作り直さず「選択行のタイトル/メタだけその場更新」する。
///   全Rebuildだと、入力欄から別行クリックへ移る瞬間にクリック対象が破棄され選択が移らない事故が起きるため。
/// </summary>
public class MemoListUI : MonoBehaviour
{
    [SerializeField] private Button addButton;
    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private MemoDetailUI detail;

    [Header("ピン")]
    [SerializeField] private Sprite pinOnSprite;   // 塗りつぶし星（ピン時）
    [SerializeField] private Sprite pinOffSprite;  // 線の星（未ピン）

    private string _selectedId;
    private bool _wired;

    private string _flashNoteId;   // ピン操作直後に一瞬ハイライトする行のid（クリック結果のフィードバック）
    private Coroutine _flashCo;    // フラッシュは常に1本だけ（多重起動による色の揺れを防ぐ）
    private float     _flashDelay = 0.30f; // フラッシュ開始までの間（ピンは①で星を先に光らせるので移動後は短め）
    private Coroutine _pinCo;      // ピン演出（星を変える→ひと呼吸→移動）の進行

    // 選択行のテキスト参照（保存時にその場更新する対象）
    private TextMeshProUGUI _selTitleTmp;
    private TextMeshProUGUI _selMetaTmp;

    private void OnEnable() => Wire();
    private void OnDisable() => Unwire();

    private void Wire()
    {
        if (_wired) return;
        _wired = true;
        if (addButton != null) addButton.onClick.AddListener(OnAddClicked);
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
        if (detail != null)
        {
            detail.OnChanged -= OnDetailChanged;
            detail.OnDeleted -= OnItemDeleted;
        }
    }

    private void OnAddClicked()
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        var note = nm.AddMemoNote();   // 既定フォルダに空ノート作成
        Select(note);
        Rebuild();
        if (detail != null) detail.FocusTitle();
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

    // ── リスト構築 ────────────────────────────

    public void Rebuild()
    {
        if (listContent == null) return;
        _selTitleTmp = null; _selMetaTmp = null;
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        var nm = NotebookManager.Instance;
        if (nm == null) return;

        var notes = nm.GetMemoNotes();   // 全フォルダ・ゴミ箱除外・ピン優先→作成日降順
        if (notes.Count == 0) { BuildEmptyLabel(); return; }

        foreach (var note in notes) BuildRow(note);
    }

    private void BuildRow(MemoNote note)
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

        // 行は横並び：[テキスト列(残り幅)] [星ボタン(固定28)]
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 9, 9);
        hlg.spacing = 8;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

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
            UITheme_FocusMode.FontBody,
            untitled ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextPrimary);
        title.alignment = TextAlignmentOptions.MidlineLeft;

        // メタ（更新日時）
        var meta = NewText("Meta", textCol.transform,
            FormatMeta(note),
            UITheme_FocusMode.FontCaption,
            UITheme_FocusMode.TextMuted);
        meta.alignment = TextAlignmentOptions.MidlineLeft;

        if (selected) { _selTitleTmp = title; _selMetaTmp = meta; }

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

    // ── ヘルパー（TodoListUI と同方式） ──
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
