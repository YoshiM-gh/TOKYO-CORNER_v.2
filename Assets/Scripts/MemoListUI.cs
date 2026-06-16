using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// メモタブ 左ペイン（ノート一覧）。M-1 コア。
/// - GetMemoNotes() を作成日降順（ピン優先）で表示。M-1 は単一フォルダ前提（フォルダUIは M-2）。
/// - 行クリックで選択 → 右ペイン(MemoDetailUI)で編集。行は表示専用（インライン編集なし）。
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

    private string _selectedId;
    private bool _wired;

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

        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 60; rowLE.preferredHeight = 60;

        var vlg = row.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 9, 9);
        vlg.spacing = 3;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.MiddleLeft;

        // タイトル（空は「（無題）」）。長いタイトルは Viewport の RectMask2D で右端クリップ。
        bool untitled = string.IsNullOrWhiteSpace(note.title);
        var title = NewText("Title", row.transform,
            untitled ? "（無題）" : note.title,
            UITheme_FocusMode.FontBody,
            untitled ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextPrimary);
        title.alignment = TextAlignmentOptions.MidlineLeft;

        // メタ（更新日時）
        var meta = NewText("Meta", row.transform,
            FormatMeta(note),
            UITheme_FocusMode.FontCaption,
            UITheme_FocusMode.TextMuted);
        meta.alignment = TextAlignmentOptions.MidlineLeft;

        if (selected) { _selTitleTmp = title; _selMetaTmp = meta; }

        // 行全体をクリック可能に（子テキストは raycastTarget=false なので行が拾う）
        var btn = row.AddComponent<Button>();
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
