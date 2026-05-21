using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 日カレンダーUI
/// - 左：時間軸タイムライン（縦スクロール）
/// - 右：選択した予定の詳細 or 追加フォーム（並列表示）
/// - 時間帯クリック → 右カラムに追加フォーム（時間自動入力）
/// - 予定クリック  → 右カラムに詳細
/// </summary>
public class DailyCalendarUI : MonoBehaviour
{
    [Header("ナビゲーション")]
    [SerializeField] private Button prevDayBtn;
    [SerializeField] private Button nextDayBtn;
    [SerializeField] private TextMeshProUGUI dayLabel;

    [Header("左：タイムライン")]
    [SerializeField] private ScrollRect timelineScroll;
    [SerializeField] private Transform  timelineParent;

    [Header("右：詳細パネル")]
    [SerializeField] private TextMeshProUGUI detailTitle;
    [SerializeField] private Transform       detailContent;  // 動的UIの親
    [SerializeField] private GameObject      detailFooter;
    [SerializeField] private Button          detailSaveBtn;
    [SerializeField] private Button          detailDeleteBtn;
    [SerializeField] private TextMeshProUGUI detailEmptyText;

    private static readonly string[] DowLabels = { "日", "月", "火", "水", "木", "金", "土" };
    private const int START_HOUR = 5;
    private const int END_HOUR   = 23;
    private const float ROW_HEIGHT = 36f;

    private DateTime currentDate;
    private ScheduleEvent selectedEvent;
    private string pendingTime; // 追加フォームの時間

    private void OnEnable()
    {
        currentDate = DateTime.Now.Date;
        SetupButtons();
        Refresh();
    }

    private void SetupButtons()
    {
        prevDayBtn?.onClick.RemoveAllListeners();
        nextDayBtn?.onClick.RemoveAllListeners();
        prevDayBtn?.onClick.AddListener(() => { currentDate = currentDate.AddDays(-1); Refresh(); });
        nextDayBtn?.onClick.AddListener(() => { currentDate = currentDate.AddDays(1);  Refresh(); });
    }

    public void Refresh()
    {
        UpdateDayLabel();
        BuildTimeline();
        // 右パネルを空状態に戻す
        ShowEmpty();
    }

    // ─── 日付ラベル ───────────────────────────────────────
    private void UpdateDayLabel()
    {
        if (dayLabel == null) return;
        var dow = (int)currentDate.DayOfWeek;
        dayLabel.text = $"{currentDate.Year}年{currentDate.Month}月{currentDate.Day}日（{DowLabels[dow]}）";
    }

    // ─── タイムライン ─────────────────────────────────────
    private void BuildTimeline()
    {
        if (timelineParent == null) return;
        foreach (Transform child in timelineParent)
            Destroy(child.gameObject);

        var events = NotebookManager.Instance != null
            ? NotebookManager.Instance.GetEventsByDate(currentDate)
            : new List<ScheduleEvent>();

        for (int h = START_HOUR; h <= END_HOUR; h++)
        {
            var rowGO  = new GameObject($"Row_{h:D2}", typeof(RectTransform));
            rowGO.transform.SetParent(timelineParent, false);
            var rowRT  = rowGO.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0f, ROW_HEIGHT);

            var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowHLG.childForceExpandWidth  = true;
            rowHLG.childForceExpandHeight = true;
            rowHLG.spacing = 0f;

            // 時間ラベル
            var lblGO  = new GameObject("Label", typeof(RectTransform));
            lblGO.transform.SetParent(rowGO.transform, false);
            var lblTxt = lblGO.AddComponent<TextMeshProUGUI>();
            lblTxt.text      = $"{h:D2}";
            lblTxt.fontSize  = UITheme_FocusMode.FontMicro;
            lblTxt.color     = UITheme_FocusMode.TextDisabled;
            lblTxt.alignment = TextAlignmentOptions.TopRight;
            var lblLE  = lblGO.AddComponent<LayoutElement>();
            lblLE.preferredWidth = 28f;
            lblLE.flexibleWidth  = 0f;

            // スロット
            var slotGO  = new GameObject("Slot", typeof(RectTransform));
            slotGO.transform.SetParent(rowGO.transform, false);
            var slotImg = slotGO.AddComponent<Image>();
            slotImg.color = new Color(1f, 1f, 1f, 0.01f);

            // 上ボーダーライン
            var lineGO  = new GameObject("Line", typeof(RectTransform));
            lineGO.transform.SetParent(slotGO.transform, false);
            var lineRT  = lineGO.GetComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(0f, 1f);
            lineRT.anchorMax = new Vector2(1f, 1f);
            lineRT.sizeDelta = new Vector2(0f, 0.5f);
            lineRT.anchoredPosition = Vector2.zero;
            var lineImg = lineGO.AddComponent<Image>();
            lineImg.color = UITheme_FocusMode.BorderSubtle;

            var slotVLG = slotGO.AddComponent<VerticalLayoutGroup>();
            slotVLG.padding = new RectOffset(4, 4, 2, 2);
            slotVLG.spacing = 2f;
            slotVLG.childForceExpandWidth  = true;
            slotVLG.childForceExpandHeight = false;

            // この時間帯の予定
            var hourEvs = events.Where(e =>
            {
                if (string.IsNullOrEmpty(e.time)) return false;
                if (TimeSpan.TryParse(e.time, out var t))
                    return t.Hours == h;
                return false;
            }).ToList();

            if (hourEvs.Count > 0)
            {
                foreach (var ev in hourEvs)
                    BuildTimelineEventBlock(slotGO.transform, ev);
            }
            else
            {
                // 空きスロット → クリックで追加フォーム
                var btn = slotGO.AddComponent<Button>();
                btn.targetGraphic = slotImg;
                int capturedH = h;
                btn.onClick.AddListener(() => ShowAddForm($"{capturedH:D2}:00"));
            }
        }
    }

    private void BuildTimelineEventBlock(Transform parent, ScheduleEvent ev)
    {
        var tag     = TagConfig.GetById(ev.tagId);
        var blockGO = new GameObject("EventBlock", typeof(RectTransform));
        blockGO.transform.SetParent(parent, false);

        var blockImg = blockGO.AddComponent<Image>();
        blockImg.color = tag != null ? tag.chipBG : UITheme_FocusMode.AccentBlueFaint;

        // 左ボーダー
        var bdrGO  = new GameObject("Border", typeof(RectTransform));
        bdrGO.transform.SetParent(blockGO.transform, false);
        var bdrRT  = bdrGO.GetComponent<RectTransform>();
        bdrRT.anchorMin = new Vector2(0f, 0f);
        bdrRT.anchorMax = new Vector2(0f, 1f);
        bdrRT.sizeDelta = new Vector2(2f, 0f);
        var bdrImg = bdrGO.AddComponent<Image>();
        bdrImg.color = tag != null ? tag.chipBorder : UITheme_FocusMode.AccentBlue;

        // コンテンツ
        var ctnGO  = new GameObject("Content", typeof(RectTransform));
        ctnGO.transform.SetParent(blockGO.transform, false);
        var ctnRT  = ctnGO.GetComponent<RectTransform>();
        ctnRT.anchorMin = new Vector2(0f, 0f);
        ctnRT.anchorMax = new Vector2(1f, 1f);
        ctnRT.offsetMin = new Vector2(6f, 2f);
        ctnRT.offsetMax = new Vector2(-2f, -2f);
        var ctnVLG = ctnGO.AddComponent<VerticalLayoutGroup>();
        ctnVLG.childForceExpandWidth  = true;
        ctnVLG.childForceExpandHeight = false;
        ctnVLG.spacing = 1f;

        var titleGO  = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(ctnGO.transform, false);
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text      = ev.title;
        titleTxt.fontSize  = UITheme_FocusMode.FontBody;
        titleTxt.color     = Color.white;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.overflowMode = TextOverflowModes.Ellipsis;
        titleGO.AddComponent<LayoutElement>().preferredHeight = 16f;

        var timeGO  = new GameObject("Time", typeof(RectTransform));
        timeGO.transform.SetParent(ctnGO.transform, false);
        var timeTxt = timeGO.AddComponent<TextMeshProUGUI>();
        timeTxt.text     = ev.time ?? "";
        timeTxt.fontSize = UITheme_FocusMode.FontMicro;
        timeTxt.color    = UITheme_FocusMode.WithAlpha(Color.white, 0.6f);
        timeGO.AddComponent<LayoutElement>().preferredHeight = 13f;

        var btn = blockGO.AddComponent<Button>();
        btn.targetGraphic = blockImg;
        var captured = ev;
        btn.onClick.AddListener(() => ShowEventDetail(captured));

        blockGO.AddComponent<LayoutElement>().preferredHeight = ROW_HEIGHT - 6f;
    }

    // ─── 右パネル：空状態 ─────────────────────────────────
    private void ShowEmpty()
    {
        selectedEvent = null;
        pendingTime   = null;
        if (detailTitle     != null) detailTitle.text = "予定を選択";
        if (detailEmptyText != null) detailEmptyText.gameObject.SetActive(true);
        if (detailFooter    != null) detailFooter.SetActive(false);
        ClearDetailContent();
    }

    // ─── 右パネル：予定詳細 ───────────────────────────────
    private void ShowEventDetail(ScheduleEvent ev)
    {
        selectedEvent = ev;
        pendingTime   = null;
        if (detailEmptyText != null) detailEmptyText.gameObject.SetActive(false);
        if (detailTitle     != null) detailTitle.text = ev.title;
        ClearDetailContent();
        BuildDetailFields(ev);
        SetupDetailFooter(isEdit: false);
    }

    private void BuildDetailFields(ScheduleEvent ev)
    {
        if (detailContent == null) return;
        var tag = TagConfig.GetById(ev.tagId);

        // タグバッジ
        if (tag != null) AppendBadge(tag);

        // 時間
        if (!string.IsNullOrEmpty(ev.time))
            AppendField("時間", ev.time + (string.IsNullOrEmpty(ev.endTime) ? "" : $" 〜 {ev.endTime}"));

        // 内容
        if (!string.IsNullOrEmpty(ev.memo))
            AppendField("内容", ev.memo);
    }

    // ─── 右パネル：追加フォーム ───────────────────────────
    private void ShowAddForm(string time)
    {
        selectedEvent = null;
        pendingTime   = time;
        if (detailEmptyText != null) detailEmptyText.gameObject.SetActive(false);
        if (detailTitle     != null) detailTitle.text = $"{time} に予定を追加";
        ClearDetailContent();
        BuildAddFormFields(time);
        SetupDetailFooter(isEdit: true);
    }

    private void BuildAddFormFields(string time)
    {
        if (detailContent == null) return;

        // タグ選択
        var tagRow = AppendLabelRow("タグ");
        var tagSelGO = new GameObject("TagSel", typeof(RectTransform));
        tagSelGO.transform.SetParent(tagRow.transform, false);
        var tagHLG = tagSelGO.AddComponent<HorizontalLayoutGroup>();
        tagHLG.spacing = 4f;
        tagHLG.childForceExpandWidth  = false;
        tagHLG.childForceExpandHeight = false;
        tagSelGO.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        string selTagId = "todo";
        var tagBtns = new List<(Button b, Image img, string id)>();
        foreach (var td in TagConfig.Tags)
        {
            var tbGO  = new GameObject(td.id, typeof(RectTransform));
            tbGO.transform.SetParent(tagSelGO.transform, false);
            var tbImg = tbGO.AddComponent<Image>();
            tbImg.color = UITheme_FocusMode.InputBG;
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
        void UpdateTagBtns(string id)
        {
            selTagId = id;
            foreach (var (b, img, bid) in tagBtns)
            {
                var td2 = TagConfig.GetById(bid);
                img.color = bid == id ? td2.selectorBG : UITheme_FocusMode.InputBG;
            }
        }
        foreach (var (b, img, id) in tagBtns)
        { var cap = id; b.onClick.AddListener(() => UpdateTagBtns(cap)); }
        UpdateTagBtns(selTagId);

        // タイトル
        var titleInput = AppendInputField("タイトル", "");
        // 時間（自動入力済み）
        var timeInput  = AppendInputField("時間", time);
        // 内容
        var memoInput  = AppendInputField("内容", "");

        // フッターに保存処理を設定
        detailSaveBtn?.onClick.RemoveAllListeners();
        detailSaveBtn?.onClick.AddListener(() =>
        {
            var t = titleInput.text.Trim();
            if (string.IsNullOrEmpty(t)) return;
            var dateKey = NotebookManager.DateKey(currentDate);
            NotebookManager.Instance?.AddEvent(selTagId, t, dateKey, timeInput.text.Trim(), memoInput.text.Trim());
            Refresh();
        });
    }

    // ─── フッター設定 ─────────────────────────────────────
    private void SetupDetailFooter(bool isEdit)
    {
        if (detailFooter != null) detailFooter.SetActive(true);
        if (!isEdit && selectedEvent != null)
        {
            detailSaveBtn?.onClick.RemoveAllListeners();
            detailDeleteBtn?.onClick.RemoveAllListeners();
            detailSaveBtn?.onClick.AddListener(() => { /* 編集は FloatingWindow で */ });
            detailDeleteBtn?.onClick.AddListener(() =>
            {
                if (selectedEvent != null)
                    NotebookManager.Instance?.DeleteEvent(selectedEvent.id);
                Refresh();
            });
        }
        else if (!isEdit)
        {
            if (detailFooter != null) detailFooter.SetActive(false);
        }
    }

    // ─── 右パネルUIヘルパー ───────────────────────────────
    private void ClearDetailContent()
    {
        if (detailContent == null) return;
        foreach (Transform child in detailContent)
            Destroy(child.gameObject);
    }

    private void AppendBadge(TagDefinition tag)
    {
        var go  = new GameObject("Badge", typeof(RectTransform));
        go.transform.SetParent(detailContent, false);
        var img = go.AddComponent<Image>();
        img.color = tag.badgeBG;
        var le  = go.AddComponent<LayoutElement>();
        le.preferredHeight = 22f; le.preferredWidth = 60f;
        var tGO = new GameObject("Text", typeof(RectTransform));
        tGO.transform.SetParent(go.transform, false);
        var tRT = tGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(8f,0f); tRT.offsetMax = new Vector2(-8f,0f);
        var t   = tGO.AddComponent<TextMeshProUGUI>();
        t.text  = tag.displayName;
        t.fontSize = UITheme_FocusMode.FontCaption;
        t.color = tag.badgeText;
        t.alignment = TextAlignmentOptions.Center;
    }

    private void AppendField(string label, string value)
    {
        var row = AppendLabelRow(label);
        var valGO = new GameObject("Value", typeof(RectTransform));
        valGO.transform.SetParent(row.transform, false);
        var valImg = valGO.AddComponent<Image>();
        valImg.color = UITheme_FocusMode.InputBG;
        valGO.AddComponent<LayoutElement>().preferredHeight = 28f;
        var tGO = new GameObject("Text", typeof(RectTransform));
        tGO.transform.SetParent(valGO.transform, false);
        var tRT = tGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(8f,0f); tRT.offsetMax = new Vector2(-8f,0f);
        var t   = tGO.AddComponent<TextMeshProUGUI>();
        t.text  = value;
        t.fontSize = UITheme_FocusMode.FontBody;
        t.color = UITheme_FocusMode.TextBody;
        t.enableWordWrapping = true;
    }

    private TMP_InputField AppendInputField(string label, string defaultVal)
    {
        var row    = AppendLabelRow(label);
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
        return field;
    }

    private GameObject AppendLabelRow(string label)
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
}
