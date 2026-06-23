using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// カレンダーのカテゴリー名エディタ。歯車(NavHeaderStyler)から CategorySettingsController.Toggle() で開閉。
/// 予定=固定、他4つ(目標/趣味・遊び/カスタム1/カスタム2)をインライン改名。永続化は TagConfig.SetCustomName。
/// 2b-2: アイテムがあるカテゴリーの改名時にインライン2択(このまま変更/削除して変更)。閉じる=既定(保持)でコミット。
/// </summary>
public class CategorySettingsController : MonoBehaviour
{
    class Row { public string id; public TMP_InputField input; public GameObject strip; public TextMeshProUGUI msgLabel; public TextMeshProUGUI redLabel; public string pending; }

    static CategorySettingsController _inst;
    GameObject _root, _panel;
    bool _open;
    TMP_FontAsset _font;
    readonly List<Row> _rows = new List<Row>();

    public static void Toggle() { EnsureInstance(); if (_inst == null) return; _inst.SetOpen(!_inst._open); }
    public static void Close() { if (_inst != null) _inst.SetOpen(false); }

    static void EnsureInstance()
    {
        if (_inst != null && _inst._root != null) return;
        var go = new GameObject("CategorySettingsController");
        _inst = go.AddComponent<CategorySettingsController>();
        _inst.Build();
    }

    // ───────── build ─────────
    void Build()
    {
        var canvas = FindCanvas();
        if (canvas == null) { Debug.LogWarning("[CategorySettings] Canvas が見つからない"); return; }
        var src = FindSourceInput();
        _font = (src != null && src.textComponent != null) ? src.textComponent.font : null;

        _root = new GameObject("CategorySettingsRoot", typeof(RectTransform));
        _root.transform.SetParent(canvas.transform, false);
        var rrt = (RectTransform)_root.transform;
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

        var bd = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        bd.transform.SetParent(_root.transform, false);
        var brt = (RectTransform)bd.transform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        bd.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        var bbtn = bd.GetComponent<Button>(); bbtn.transition = Selectable.Transition.None;
        bbtn.onClick.AddListener(() => SetOpen(false));

        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _panel.transform.SetParent(_root.transform, false);
        var prt = (RectTransform)_panel.transform;
        prt.anchorMin = new Vector2(1f, 1f); prt.anchorMax = new Vector2(1f, 1f); prt.pivot = new Vector2(1f, 1f);
        prt.sizeDelta = new Vector2(340f, 0f); prt.anchoredPosition = new Vector2(-24f, -76f);
        var pimg = _panel.GetComponent<Image>(); pimg.color = new Color(0.13f, 0.15f, 0.19f, 0.99f);
        UIStyleKit.ApplyCard(pimg);
        var vlg = _panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 14, 14); vlg.spacing = 8f;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        var fit = _panel.GetComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        MakeLabel(_panel.transform, "カテゴリー名を編集", 19f, new Color(1f, 1f, 1f, 0.96f), TextAlignmentOptions.Left, 26f);

        _rows.Clear();
        foreach (var def in TagConfig.Tags)
        {
            if (def.id == "yotei") BuildLockedRow(def);
            else BuildEditableRow(def, src);
        }

        BuildDoneButton();
        _root.SetActive(false); _open = false;
    }

    GameObject MakeRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        var le = row.GetComponent<LayoutElement>(); le.minHeight = 38f; le.preferredHeight = 38f;
        return row;
    }

    void MakeSwatch(Transform parent, Color c)
    {
        var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = new Color(c.r, c.g, c.b, 1f);
        UIStyleKit.ApplyRounded(img, 5f);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 16f; le.minWidth = 16f; le.preferredHeight = 16f; le.minHeight = 16f; le.flexibleWidth = 0f;
    }

    TextMeshProUGUI MakeLabel(Transform parent, string text, float size, Color col, TextAlignmentOptions align, float minH)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text; t.fontSize = size; t.color = col; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        var le = go.GetComponent<LayoutElement>(); le.minHeight = minH;
        return t;
    }

    GameObject MakeButton(Transform parent, string text, Color bg, Color fg, System.Action onClick)
    {
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = bg; UIStyleKit.ApplyControl(img);
        var b = go.GetComponent<Button>(); b.transition = Selectable.Transition.None;
        var cb = onClick; b.onClick.AddListener(() => { if (cb != null) cb(); });
        var le = go.GetComponent<LayoutElement>(); le.minHeight = 30f; le.preferredHeight = 30f; le.flexibleWidth = 1f;
        var t = MakeLabel(go.transform, text, 13f, fg, TextAlignmentOptions.Center, 0f);
        var trt = (RectTransform)t.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        t.GetComponent<LayoutElement>().ignoreLayout = true;
        return go;
    }

    void BuildLockedRow(TagDefinition def)
    {
        var row = MakeRow(_panel.transform, "Row_" + def.id);
        MakeSwatch(row.transform, def.barColor);
        var lbl = MakeLabel(row.transform, def.displayName, 17f, new Color(1f, 1f, 1f, 0.78f), TextAlignmentOptions.Left, 24f);
        lbl.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var sp = Resources.Load<Sprite>("Icons/Lock");
        if (sp != null)
        {
            var ic = new GameObject("Lock", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            ic.transform.SetParent(row.transform, false);
            var im = ic.GetComponent<Image>(); im.sprite = sp; im.preserveAspect = true; im.color = new Color(1f, 1f, 1f, 0.40f);
            var le = ic.GetComponent<LayoutElement>(); le.preferredWidth = 15f; le.preferredHeight = 15f; le.flexibleWidth = 0f;
        }
        MakeLabel(row.transform, "固定", 13f, new Color(1f, 1f, 1f, 0.40f), TextAlignmentOptions.Right, 18f);
    }

    void BuildEditableRow(TagDefinition def, TMP_InputField src)
    {
        var cont = new GameObject("Row_" + def.id, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        cont.transform.SetParent(_panel.transform, false);
        var cvlg = cont.GetComponent<VerticalLayoutGroup>();
        cvlg.spacing = 5f; cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
        cvlg.childControlWidth = true; cvlg.childControlHeight = true;
        cont.GetComponent<LayoutElement>().minHeight = 38f;

        var line = MakeRow(cont.transform, "Line");
        MakeSwatch(line.transform, def.barColor);
        var inputGO = CloneInput(src, line.transform, def.displayName);
        var row = new Row { id = def.id };
        if (inputGO == null)
        {
            MakeLabel(line.transform, def.displayName + " (入力欄複製失敗)", 15f, new Color(1f, 0.6f, 0.6f, 1f), TextAlignmentOptions.Left, 24f);
            _rows.Add(row); return;
        }
        var le = inputGO.GetComponent<LayoutElement>(); if (le == null) le = inputGO.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f; le.minHeight = 34f; le.preferredHeight = 34f;
        row.input = inputGO.GetComponent<TMP_InputField>();

        BuildStrip(cont.transform, row);

        var capRow = row;
        row.input.onEndEdit.AddListener((v) => OnNameEdited(capRow));
        _rows.Add(row);
    }

    void BuildStrip(Transform parent, Row row)
    {
        var strip = new GameObject("Strip", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        strip.transform.SetParent(parent, false);
        var img = strip.GetComponent<Image>(); img.color = new Color(0.20f, 0.22f, 0.27f, 0.96f); UIStyleKit.ApplyControl(img);
        var vlg = strip.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 8); vlg.spacing = 6f;
        vlg.childForceExpandWidth = true; vlg.childControlWidth = true; vlg.childControlHeight = true;
        row.msgLabel = MakeLabel(strip.transform, "", 12.5f, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.Left, 16f);
        row.msgLabel.textWrappingMode = TextWrappingModes.Normal; row.msgLabel.overflowMode = TextOverflowModes.Overflow;
        var btnRow = new GameObject("Btns", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        btnRow.transform.SetParent(strip.transform, false);
        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>(); hlg.spacing = 8f; hlg.childForceExpandWidth = true; hlg.childControlWidth = true; hlg.childControlHeight = true;
        btnRow.GetComponent<LayoutElement>().minHeight = 30f;
        var capRow = row;
        MakeButton(btnRow.transform, "このまま変更", new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.92f), () => CommitKeep(capRow));
        var del = MakeButton(btnRow.transform, "削除して変更", new Color(0.80f, 0.22f, 0.22f, 0.85f), new Color(1f, 0.92f, 0.92f, 1f), () => CommitDelete(capRow));
        row.redLabel = del.GetComponentInChildren<TextMeshProUGUI>();
        strip.SetActive(false);
        row.strip = strip;
    }

    GameObject CloneInput(TMP_InputField src, Transform parent, string initial)
    {
        if (src == null) return null;
        var go = Instantiate(src.gameObject, parent);
        go.name = "NameInput"; go.SetActive(true);
        var f = go.GetComponent<TMP_InputField>();
        f.onValueChanged.RemoveAllListeners(); f.onEndEdit.RemoveAllListeners();
        f.onSubmit.RemoveAllListeners(); f.onSelect.RemoveAllListeners(); f.onDeselect.RemoveAllListeners();
        f.interactable = true; f.lineType = TMP_InputField.LineType.SingleLine;
        f.SetTextWithoutNotify(initial);
        var rt = (RectTransform)go.transform; rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
        return go;
    }

    void BuildDoneButton()
    {
        var row = new GameObject("DoneRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(_panel.transform, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleRight; hlg.childForceExpandWidth = false;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        row.GetComponent<LayoutElement>().minHeight = 36f;
        var btn = MakeButton(row.transform, "完了", new Color(0.22f, 0.50f, 0.92f, 0.92f), new Color(1f, 1f, 1f, 0.98f), () => SetOpen(false));
        var le = btn.GetComponent<LayoutElement>(); le.flexibleWidth = 0f; le.preferredWidth = 84f; le.preferredHeight = 32f;
    }

    // ───────── behavior ─────────
    void SetOpen(bool open)
    {
        if (_root == null) return;
        if (!open) CommitAll();
        _open = open; _root.SetActive(open);
        if (open) { _root.transform.SetAsLastSibling(); Refresh(); }
    }

    void Refresh()
    {
        foreach (var r in _rows)
        {
            if (r.strip != null) r.strip.SetActive(false);
            r.pending = null;
            RefreshOne(r);
        }
    }

    void RefreshOne(Row row)
    {
        var def = TagConfig.GetById(row.id);
        if (def != null && row.input != null) row.input.SetTextWithoutNotify(def.displayName);
    }

    int CountItems(string id) { return NotebookManager.Instance != null ? NotebookManager.Instance.GetEventsByTag(id).Count : 0; }

    void OnNameEdited(Row row)
    {
        if (row.input == null) return;
        var def = TagConfig.GetById(row.id);
        string cur = def != null ? def.displayName : "";
        string name = (row.input.text ?? "").Trim();
        if (name.Length == 0 || name == cur) { RefreshOne(row); if (row.strip != null) row.strip.SetActive(false); row.pending = null; return; }
        int n = CountItems(row.id);
        if (n == 0) { TagConfig.SetCustomName(row.id, name); RefreshOne(row); if (row.strip != null) row.strip.SetActive(false); row.pending = null; return; }
        row.pending = name;
        if (row.msgLabel != null) row.msgLabel.text = "このカテゴリーに " + n + " 件のアイテムがあります";
        if (row.redLabel != null) row.redLabel.text = n + "件削除して変更";
        if (row.strip != null) row.strip.SetActive(true);
    }

    void CommitKeep(Row row)
    {
        if (!string.IsNullOrEmpty(row.pending)) TagConfig.SetCustomName(row.id, row.pending);
        row.pending = null; if (row.strip != null) row.strip.SetActive(false); RefreshOne(row);
    }

    void CommitDelete(Row row)
    {
        if (NotebookManager.Instance != null) NotebookManager.Instance.DeleteEventsByTag(row.id);
        if (!string.IsNullOrEmpty(row.pending)) TagConfig.SetCustomName(row.id, row.pending);
        row.pending = null; if (row.strip != null) row.strip.SetActive(false); RefreshOne(row);
    }

    void CommitAll()
    {
        foreach (var r in _rows)
        {
            if (r.input == null) continue;
            var def = TagConfig.GetById(r.id);
            string cur = def != null ? def.displayName : "";
            string name = (r.input.text ?? "").Trim();
            if (name.Length > 0 && name != cur) TagConfig.SetCustomName(r.id, name);
            if (r.strip != null) r.strip.SetActive(false);
            r.pending = null;
        }
    }

    // ───────── helpers ─────────
    Canvas FindCanvas()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas best = null; int bestOrder = int.MinValue;
        foreach (var c in canvases)
        {
            if (c.name == "Canvas") return c;
            var root = c.rootCanvas;
            if (root != null && root.sortingOrder >= bestOrder) { bestOrder = root.sortingOrder; best = root; }
        }
        return best;
    }

    TMP_InputField FindSourceInput()
    {
        var fields = Object.FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var f in fields) if (f.name == "FolderRenameInput") return f;
        foreach (var f in fields) if (f.name == "TitleInput" && f.lineType == TMP_InputField.LineType.SingleLine) return f;
        return fields.Length > 0 ? fields[0] : null;
    }
}
