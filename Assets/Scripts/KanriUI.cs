using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 管理タブUI
/// - 左：フィルター（タグ・完了済みトグル）＋検索＋＋ボタン＋リスト
/// - 右：選択アイテムの詳細・編集フォーム
/// - 日付なしは最上部に📌で固定
/// - 完了済みは打ち消し線＋グレーアウト、トグルで表示/非表示
/// </summary>
public class KanriUI : MonoBehaviour
{
    [Header("左：フィルター")]
    [SerializeField] private Button[] tagFilterBtns;   // すべて・習慣・予定・目標・Todo の順
    [SerializeField] private Toggle   showDoneToggle;
    [SerializeField] private Button   addBtn;

    [Header("左：リスト")]
    [SerializeField] private Transform itemListParent;
    [SerializeField] private GameObject itemRowPrefab;  // アイテム行Prefab
    [SerializeField] private GameObject sectionLabelPrefab; // セクションラベルPrefab

    [Header("右：詳細")]
    [SerializeField] private TextMeshProUGUI detailTitle;
    [SerializeField] private Transform       detailContent;
    [SerializeField] private GameObject      detailFooter;
    [SerializeField] private Button          saveBtn;
    [SerializeField] private Button          deleteBtn;
    [SerializeField] private TextMeshProUGUI emptyText;

    // フィルター状態
    private string currentTagFilter = "all"; // "all" or tagId
    private bool   showDone         = false;
    private string selectedId       = null;

    // フィルターボタンのtagId対応（インデックス順）
    private static readonly string[] FilterTagIds = { "all", "habit", "yotei", "mokuhyo", "todo" };

    private void OnEnable()
    {
        SetupButtons();
        Refresh();
    }

    private void SetupButtons()
    {
        // タグフィルターボタン
        for (int i = 0; i < tagFilterBtns.Length && i < FilterTagIds.Length; i++)
        {
            int captured = i;
            tagFilterBtns[i]?.onClick.RemoveAllListeners();
            tagFilterBtns[i]?.onClick.AddListener(() => SetTagFilter(FilterTagIds[captured]));
        }
        // 完了済みトグル
        showDoneToggle?.onValueChanged.RemoveAllListeners();
        showDoneToggle?.onValueChanged.AddListener(v => { showDone = v; Refresh(); });
        // 追加ボタン
        addBtn?.onClick.RemoveAllListeners();
        addBtn?.onClick.AddListener(OpenNewItem);
        // 保存・削除
        saveBtn?.onClick.RemoveAllListeners();
        deleteBtn?.onClick.RemoveAllListeners();
        saveBtn?.onClick.AddListener(SaveItem);
        deleteBtn?.onClick.AddListener(DeleteItem);
    }

    private void SetTagFilter(string tagId)
    {
        currentTagFilter = tagId;
        UpdateFilterBtnVisuals();
        Refresh();
    }

    private void UpdateFilterBtnVisuals()
    {
        for (int i = 0; i < tagFilterBtns.Length && i < FilterTagIds.Length; i++)
        {
            if (tagFilterBtns[i] == null) continue;
            var img = tagFilterBtns[i].GetComponent<Image>();
            var txt = tagFilterBtns[i].GetComponentInChildren<TextMeshProUGUI>();
            bool isActive = FilterTagIds[i] == currentTagFilter;

            if (FilterTagIds[i] == "all")
            {
                if (img) img.color = isActive
                    ? new Color(1f, 1f, 1f, 0.12f)
                    : new Color(1f, 1f, 1f, 0.04f);
                if (txt) txt.color = isActive
                    ? new Color(1f, 1f, 1f, 0.9f)
                    : UITheme_FocusMode.TextMuted;
            }
            else
            {
                var tag = TagConfig.GetById(FilterTagIds[i]);
                if (img) img.color = isActive && tag != null
                    ? tag.selectorBG
                    : new Color(1f, 1f, 1f, 0.04f);
                if (txt) txt.color = isActive && tag != null
                    ? tag.selectorText
                    : UITheme_FocusMode.TextMuted;
            }
        }
    }

    // ─── リスト描画 ───────────────────────────────────────
    public void Refresh()
    {
        UpdateFilterBtnVisuals();
        BuildList();
        // 選択中アイテムが削除されていたら右パネルをリセット
        if (selectedId != null && NotebookManager.Instance != null)
        {
            var still = NotebookManager.Instance.GetAllEvents().Find(e => e.id == selectedId);
            if (still == null) ShowEmpty();
        }
    }

    private void BuildList()
    {
        if (itemListParent == null) return;
        foreach (Transform child in itemListParent) Destroy(child.gameObject);

        if (NotebookManager.Instance == null) return;
        var all = NotebookManager.Instance.GetAllEvents();

        // フィルタ
        var filtered = currentTagFilter == "all"
            ? all
            : all.Where(e => e.tagId == currentTagFilter).ToList();
        if (!showDone)
            filtered = filtered.Where(e => !e.isCompleted).ToList();

        // 日付なし と 日付ありに分ける
        var noDate   = filtered.Where(e => string.IsNullOrEmpty(e.date)).ToList();
        var withDate = filtered.Where(e => !string.IsNullOrEmpty(e.date))
                               .OrderBy(e => e.date).ThenBy(e => e.time ?? "99:99").ToList();

        // 日付なしセクション
        if (noDate.Count > 0)
        {
            AppendSectionLabel("📌 日付なし");
            foreach (var ev in noDate) AppendItemRow(ev);
        }

        // 日付ありセクション（日付でグループ化）
        var dates = withDate.Select(e => e.date).Distinct().OrderBy(d => d).ToList();
        foreach (var date in dates)
        {
            AppendSectionLabel(FormatDate(date));
            foreach (var ev in withDate.Where(e => e.date == date))
                AppendItemRow(ev);
        }
    }

    private void AppendSectionLabel(string text)
    {
        if (sectionLabelPrefab == null)
        {
            // Prefabがなければ動的生成
            var go  = new GameObject("SectionLabel", typeof(RectTransform));
            go.transform.SetParent(itemListParent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text     = text;
            txt.fontSize = UITheme_FocusMode.FontCaption;
            txt.color    = UITheme_FocusMode.TextDisabled;
            go.AddComponent<LayoutElement>().preferredHeight = 20f;
            return;
        }
        var lbl = Instantiate(sectionLabelPrefab, itemListParent);
        lbl.GetComponentInChildren<TextMeshProUGUI>()?.SetText(text);
    }

    private void AppendItemRow(ScheduleEvent ev)
    {
        GameObject row;
        if (itemRowPrefab != null)
        {
            row = Instantiate(itemRowPrefab, itemListParent);
        }
        else
        {
            row = BuildItemRowDynamic(ev);
            row.transform.SetParent(itemListParent, false);
            SetupItemRow(row, ev);
            return;
        }
        SetupItemRow(row, ev);
    }

    private GameObject BuildItemRowDynamic(ScheduleEvent ev)
    {
        var rowGO  = new GameObject("ItemRow", typeof(RectTransform));
        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = ev.isCompleted
            ? new Color(1f, 1f, 1f, 0.02f)
            : UITheme_FocusMode.DayCellBG;
        var rowBtn = rowGO.AddComponent<Button>();
        rowBtn.targetGraphic = rowImg;
        var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowHLG.padding  = new RectOffset(7, 7, 5, 5);
        rowHLG.spacing  = 6f;
        rowHLG.childForceExpandWidth  = false;
        rowHLG.childForceExpandHeight = false;
        rowHLG.childAlignment = TextAnchor.MiddleLeft;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 36f;

        // チェックボタン
        var chkGO  = new GameObject("CheckBtn", typeof(RectTransform));
        chkGO.transform.SetParent(rowGO.transform, false);
        var chkImg = chkGO.AddComponent<Image>();
        chkImg.color = ev.isCompleted
            ? new Color(0.31f, 0.78f, 0.47f, 0.35f)
            : new Color(1f, 1f, 1f, 0f);
        var chkBtn = chkGO.AddComponent<Button>();
        chkBtn.targetGraphic = chkImg;
        var chkLE  = chkGO.AddComponent<LayoutElement>();
        chkLE.preferredWidth = 16f; chkLE.preferredHeight = 16f;
        var chkTGO = new GameObject("Text", typeof(RectTransform));
        chkTGO.transform.SetParent(chkGO.transform, false);
        var chkTRT = chkTGO.GetComponent<RectTransform>();
        chkTRT.anchorMin = Vector2.zero; chkTRT.anchorMax = Vector2.one;
        chkTRT.offsetMin = chkTRT.offsetMax = Vector2.zero;
        var chkTxt = chkTGO.AddComponent<TextMeshProUGUI>();
        chkTxt.text      = ev.isCompleted ? "✓" : "";
        chkTxt.fontSize  = UITheme_FocusMode.FontMicro;
        chkTxt.color     = new Color(0.59f, 0.96f, 0.71f, 0.85f);
        chkTxt.alignment = TextAlignmentOptions.Center;

        // タグバー
        var barGO  = new GameObject("TagBar", typeof(RectTransform));
        barGO.transform.SetParent(rowGO.transform, false);
        var barImg = barGO.AddComponent<Image>();
        var tag    = TagConfig.GetById(ev.tagId);
        barImg.color = tag != null ? tag.barColor : UITheme_FocusMode.AccentBlue;
        var barLE  = barGO.AddComponent<LayoutElement>();
        barLE.preferredWidth = 3f; barLE.flexibleHeight = 1f;

        // Body（タイトル＋メタ）
        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(rowGO.transform, false);
        var bodyVLG = bodyGO.AddComponent<VerticalLayoutGroup>();
        bodyVLG.spacing = 1f;
        bodyVLG.childForceExpandWidth  = true;
        bodyVLG.childForceExpandHeight = false;
        bodyGO.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var titleGO  = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(bodyGO.transform, false);
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text      = ev.title;
        titleTxt.fontSize  = UITheme_FocusMode.FontBody;
        titleTxt.color     = ev.isCompleted
            ? UITheme_FocusMode.TextDisabled
            : UITheme_FocusMode.TextBody;
        titleTxt.fontStyle = ev.isCompleted ? FontStyles.Strikethrough : FontStyles.Normal;
        titleTxt.overflowMode = TextOverflowModes.Ellipsis;
        titleGO.AddComponent<LayoutElement>().preferredHeight = 16f;

        var metaGO  = new GameObject("Meta", typeof(RectTransform));
        metaGO.transform.SetParent(bodyGO.transform, false);
        var metaTxt = metaGO.AddComponent<TextMeshProUGUI>();
        metaTxt.text     = string.IsNullOrEmpty(ev.date) ? "日付なし" : FormatDate(ev.date);
        metaTxt.fontSize = UITheme_FocusMode.FontCaption;
        metaTxt.color    = UITheme_FocusMode.TextDisabled;
        metaGO.AddComponent<LayoutElement>().preferredHeight = 13f;

        // バッジ
        var badgeGO  = new GameObject("Badge", typeof(RectTransform));
        badgeGO.transform.SetParent(rowGO.transform, false);
        var badgeImg = badgeGO.AddComponent<Image>();
        badgeImg.color = tag != null ? tag.badgeBG : UITheme_FocusMode.AccentBlueFaint;
        var badgeLE  = badgeGO.AddComponent<LayoutElement>();
        badgeLE.preferredWidth = 36f; badgeLE.preferredHeight = 18f;
        var badgeTGO = new GameObject("Text", typeof(RectTransform));
        badgeTGO.transform.SetParent(badgeGO.transform, false);
        var badgeTRT = badgeTGO.GetComponent<RectTransform>();
        badgeTRT.anchorMin = Vector2.zero; badgeTRT.anchorMax = Vector2.one;
        badgeTRT.offsetMin = badgeTRT.offsetMax = Vector2.zero;
        var badgeTxt = badgeTGO.AddComponent<TextMeshProUGUI>();
        badgeTxt.text      = tag != null ? tag.displayName : "";
        badgeTxt.fontSize  = UITheme_FocusMode.FontMicro;
        badgeTxt.color     = tag != null ? tag.badgeText : UITheme_FocusMode.TextBody;
        badgeTxt.alignment = TextAlignmentOptions.Center;

        return rowGO;
    }

    private void SetupItemRow(GameObject row, ScheduleEvent ev)
    {
        var rowBtn = row.GetComponent<Button>();
        var chkBtn = row.transform.Find("CheckBtn")?.GetComponent<Button>();

        if (rowBtn != null)
        {
            var captured = ev.id;
            rowBtn.onClick.RemoveAllListeners();
            rowBtn.onClick.AddListener(() => SelectItem(captured));
        }
        if (chkBtn != null)
        {
            var captured = ev.id;
            chkBtn.onClick.RemoveAllListeners();
            chkBtn.onClick.AddListener(() =>
            {
                var target = NotebookManager.Instance?.GetAllEvents().Find(e => e.id == captured);
                if (target != null)
                    NotebookManager.Instance?.SetCompleted(captured, !target.isCompleted);
                Refresh();
            });
        }
    }

    // ─── 右パネル ─────────────────────────────────────────
    private void SelectItem(string id)
    {
        selectedId = id;
        var ev = NotebookManager.Instance?.GetAllEvents().Find(e => e.id == id);
        if (ev == null) { ShowEmpty(); return; }

        if (emptyText)  emptyText.gameObject.SetActive(false);
        if (detailTitle) detailTitle.text = ev.title;
        ClearDetailContent();
        BuildDetailForm(ev);
        if (detailFooter) detailFooter.SetActive(true);
    }

    private void OpenNewItem()
    {
        selectedId = null;
        if (emptyText)   emptyText.gameObject.SetActive(false);
        if (detailTitle) detailTitle.text = "新しいアイテム";
        ClearDetailContent();
        BuildDetailForm(null);
        if (detailFooter) detailFooter.SetActive(true);
    }

    private void ShowEmpty()
    {
        selectedId = null;
        if (detailTitle) detailTitle.text = "アイテムを選択";
        if (emptyText)   emptyText.gameObject.SetActive(true);
        if (detailFooter) detailFooter.SetActive(false);
        ClearDetailContent();
    }

    // ─── 詳細フォーム ─────────────────────────────────────
    // 編集用に現在の入力値を保持
    private string editTagId   = "todo";
    private TMP_InputField editTitleField;
    private TMP_InputField editDateField;
    private TMP_InputField editTimeField;
    private TMP_InputField editMemoField;

    private void BuildDetailForm(ScheduleEvent ev)
    {
        if (detailContent == null) return;
        bool isEdit = ev != null;
        editTagId = isEdit ? ev.tagId : "todo";

        // タグ選択
        var tagRowGO = AppendRow("タグ");
        var tagSelGO = new GameObject("TagSel", typeof(RectTransform));
        tagSelGO.transform.SetParent(tagRowGO.transform, false);
        var tagHLG = tagSelGO.AddComponent<HorizontalLayoutGroup>();
        tagHLG.spacing = 4f;
        tagHLG.childForceExpandWidth  = false;
        tagHLG.childForceExpandHeight = false;
        tagSelGO.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var tagBtns = new List<(Button btn, Image img, string id)>();
        foreach (var td in TagConfig.Tags)
        {
            var tbGO  = new GameObject(td.id, typeof(RectTransform));
            tbGO.transform.SetParent(tagSelGO.transform, false);
            var tbImg = tbGO.AddComponent<Image>();
            var tbBtn = tbGO.AddComponent<Button>();
            tbGO.AddComponent<LayoutElement>().preferredHeight = 22f;
            var tbTGO = new GameObject("Text", typeof(RectTransform));
            tbTGO.transform.SetParent(tbGO.transform, false);
            var tbTRT = tbTGO.GetComponent<RectTransform>();
            tbTRT.anchorMin = Vector2.zero; tbTRT.anchorMax = Vector2.one;
            tbTRT.offsetMin = new Vector2(8f,0f); tbTRT.offsetMax = new Vector2(-8f,0f);
            var tbTxt = tbTGO.AddComponent<TextMeshProUGUI>();
            tbTxt.text = td.displayName;
            tbTxt.fontSize = UITheme_FocusMode.FontCaption;
            tbTxt.alignment = TextAlignmentOptions.Center;
            tagBtns.Add((tbBtn, tbImg, td.id));
        }
        void UpdateTagBtns(string selId)
        {
            editTagId = selId;
            foreach (var (b, img, bid) in tagBtns)
            {
                var td2 = TagConfig.GetById(bid);
                if (bid == selId && td2 != null)
                { img.color = td2.selectorBG; b.GetComponentInChildren<TextMeshProUGUI>().color = td2.selectorText; }
                else
                { img.color = UITheme_FocusMode.InputBG; b.GetComponentInChildren<TextMeshProUGUI>().color = UITheme_FocusMode.TextMuted; }
            }
        }
        foreach (var (b, img, id) in tagBtns)
        { var cap = id; b.onClick.AddListener(() => UpdateTagBtns(cap)); }
        UpdateTagBtns(editTagId);

        // タイトル
        editTitleField = AppendInputField("タイトル", isEdit ? ev.title : "タイトルを入力");
        // 日付
        editDateField  = AppendInputField("日付", isEdit ? FormatDate(ev.date) : "日付なし");
        editDateField.interactable = false; // 読み取り専用（将来カレンダーポップアップ対応）
        // 時間
        editTimeField  = AppendInputField("時間（任意）", isEdit ? ev.time ?? "" : "");
        editTimeField.placeholder.GetComponent<TextMeshProUGUI>().text = "例：14:00";
        // 内容
        editMemoField  = AppendInputField("内容", isEdit ? ev.memo ?? "" : "");
    }

    private void SaveItem()
    {
        if (NotebookManager.Instance == null) return;
        var title = editTitleField?.text.Trim();
        if (string.IsNullOrEmpty(title)) return;
        var time = editTimeField?.text.Trim();
        var memo = editMemoField?.text.Trim();

        if (selectedId != null)
        {
            var ev = NotebookManager.Instance.GetAllEvents().Find(e => e.id == selectedId);
            NotebookManager.Instance.UpdateEvent(
                selectedId, editTagId, title,
                ev?.date, string.IsNullOrEmpty(time) ? null : time, null, memo);
        }
        else
        {
            NotebookManager.Instance.AddEvent(editTagId, title, null,
                string.IsNullOrEmpty(time) ? null : time, memo);
        }
        ShowEmpty();
        Refresh();
    }

    private void DeleteItem()
    {
        if (selectedId == null || NotebookManager.Instance == null) return;
        NotebookManager.Instance.DeleteEvent(selectedId);
        ShowEmpty();
        Refresh();
    }

    // ─── UIヘルパー ───────────────────────────────────────
    private void ClearDetailContent()
    {
        if (detailContent == null) return;
        foreach (Transform child in detailContent) Destroy(child.gameObject);
        editTitleField = editDateField = editTimeField = editMemoField = null;
    }

    private GameObject AppendRow(string label)
    {
        var rowGO  = new GameObject(label + "Row", typeof(RectTransform));
        rowGO.transform.SetParent(detailContent, false);
        var rowVLG = rowGO.AddComponent<VerticalLayoutGroup>();
        rowVLG.spacing = 3f;
        rowVLG.childForceExpandWidth  = true;
        rowVLG.childForceExpandHeight = false;
        rowGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var lblGO  = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(rowGO.transform, false);
        var lbl    = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text   = label;
        lbl.fontSize = UITheme_FocusMode.FontCaption;
        lbl.color    = UITheme_FocusMode.TextCaption;
        lblGO.AddComponent<LayoutElement>().preferredHeight = 14f;
        return rowGO;
    }

    private TMP_InputField AppendInputField(string label, string defaultVal)
    {
        var row    = AppendRow(label);
        var fieldGO= new GameObject("Input", typeof(RectTransform));
        fieldGO.transform.SetParent(row.transform, false);
        var fImg   = fieldGO.AddComponent<Image>();
        fImg.color = UITheme_FocusMode.InputBG;
        var field  = fieldGO.AddComponent<TMP_InputField>();
        field.text = defaultVal;
        fieldGO.AddComponent<LayoutElement>().preferredHeight = 28f;

        var taGO   = new GameObject("TextArea", typeof(RectTransform));
        taGO.transform.SetParent(fieldGO.transform, false);
        var taRT   = taGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(6f,0f); taRT.offsetMax = new Vector2(-6f,0f);
        taGO.AddComponent<RectMask2D>();

        var phGO   = new GameObject("Placeholder", typeof(RectTransform));
        phGO.transform.SetParent(taGO.transform, false);
        var phRT   = phGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = phRT.offsetMax = Vector2.zero;
        var ph     = phGO.AddComponent<TextMeshProUGUI>();
        ph.fontSize = UITheme_FocusMode.FontBody;
        ph.color    = UITheme_FocusMode.TextPlaceholder;

        var txtGO  = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(taGO.transform, false);
        var txtRT  = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
        var txt    = txtGO.AddComponent<TextMeshProUGUI>();
        txt.fontSize = UITheme_FocusMode.FontBody;
        txt.color    = UITheme_FocusMode.TextBody;

        field.textViewport  = taRT;
        field.placeholder   = ph;
        field.textComponent = txt;
        UIFonts.FinalizeRuntimeInput(field); // 全選択・ドラッグ選択・コピペを有効化
        return field;
    }

    private static readonly string[] DowLabels = { "日","月","火","水","木","金","土" };

    private static string FormatDate(string dateKey)
    {
        if (string.IsNullOrEmpty(dateKey)) return "日付なし";
        if (!DateTime.TryParse(dateKey, out var dt)) return dateKey;
        return $"{dt.Month}月{dt.Day}日（{DowLabels[(int)dt.DayOfWeek]}）";
    }
}
