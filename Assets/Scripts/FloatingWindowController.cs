using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// フローティングウィンドウ共通コントローラー。
/// 月・週・日タブから呼び出されて以下の3モードで表示する：
///   1. OpenDayList   — 選択日の予定一覧
///   2. OpenEventDetail — 予定の詳細
///   3. OpenAddForm   — 予定の追加フォーム
/// </summary>
public class FloatingWindowController : MonoBehaviour
{
    [Header("オーバーレイ")]
    [SerializeField] private GameObject overlay;      // 半透明の背景
    [SerializeField] private GameObject windowPanel;  // ウィンドウ本体

    [Header("共通ヘッダー")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button closeBtn;

    [Header("コンテンツエリア")]
    [SerializeField] private Transform contentParent; // ここに動的UIを追加

    [Header("フッター")]
    [SerializeField] private GameObject footer;
    [SerializeField] private Button primaryBtn;
    [SerializeField] private Button secondaryBtn;
    [SerializeField] private TextMeshProUGUI primaryBtnText;
    [SerializeField] private TextMeshProUGUI secondaryBtnText;

    [Header("Prefabs")]
    [SerializeField] private GameObject eventListItemPrefab; // 予定リストアイテム

    // コールバック
    private Action onRefresh;
    private ScheduleEvent currentEvent;

    private void Awake()
    {
        closeBtn?.onClick.AddListener(Close);
        overlay?.GetComponent<Button>()?.onClick.AddListener(Close);
    }

    // ─── 1. 日の予定一覧 ──────────────────────────────────
    public void OpenDayList(string dateKey, List<ScheduleEvent> events, Action onRefreshCallback)
    {
        onRefresh = onRefreshCallback;
        titleText.text = FormatDate(dateKey);
        ClearContent();
        footer.SetActive(false);

        // 予定リスト
        foreach (var ev in events)
        {
            var item = Instantiate(eventListItemPrefab, contentParent);
            SetupListItem(item, ev, dateKey);
        }

        // 「+ この日に予定を追加」ボタン
        AddDivider();
        var addBtn = CreateTextButton("+ この日に予定を追加");
        addBtn.onClick.AddListener(() =>
        {
            Close();
            // 少し遅延してから追加フォームを開く
            OpenAddForm(dateKey, null);
        });

        Show();
    }

    private void SetupListItem(GameObject item, ScheduleEvent ev, string dateKey)
    {
        var tag     = TagConfig.GetById(ev.tagId);
        var dotImg  = item.transform.Find("Dot")?.GetComponent<Image>();
        var titleTxt= item.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        var timeTxt = item.transform.Find("Time")?.GetComponent<TextMeshProUGUI>();
        var btn     = item.GetComponent<Button>();

        if (dotImg  != null && tag != null) dotImg.color = tag.barColor;
        if (titleTxt!= null) titleTxt.text  = ev.title;
        if (timeTxt != null) timeTxt.text   = ev.time ?? "時間未設定";

        if (btn != null)
        {
            var captured = ev;
            btn.onClick.AddListener(() =>
            {
                Close();
                OpenEventDetail(captured, onRefresh);
            });
        }
    }

    // ─── 2. 予定詳細 ──────────────────────────────────────
    public void OpenEventDetail(ScheduleEvent ev, Action onRefreshCallback)
    {
        onRefresh    = onRefreshCallback;
        currentEvent = ev;
        titleText.text = ev.title;
        ClearContent();

        var tag = TagConfig.GetById(ev.tagId);

        AddRow("🗓", FormatDate(ev.date));
        if (!string.IsNullOrEmpty(ev.time))
            AddRow("🕐", ev.time + (string.IsNullOrEmpty(ev.endTime) ? "" : $" 〜 {ev.endTime}"));
        if (tag != null)
            AddTagBadge(tag);
        if (!string.IsNullOrEmpty(ev.memo))
        {
            AddDivider();
            AddMemoText(ev.memo);
        }

        // フッター：編集・削除
        footer.SetActive(true);
        primaryBtnText.text   = "編集";
        secondaryBtnText.text = "削除";
        primaryBtn.onClick.RemoveAllListeners();
        secondaryBtn.onClick.RemoveAllListeners();
        primaryBtn.onClick.AddListener(() =>
        {
            Close();
            OpenAddForm(ev.date, ev); // 既存データを渡して編集モード
        });
        secondaryBtn.onClick.AddListener(() =>
        {
            NotebookManager.Instance?.DeleteEvent(ev.id);
            onRefresh?.Invoke();
            Close();
        });

        Show();
    }

    // ─── 3. 追加・編集フォーム ────────────────────────────
public void OpenAddForm(string defaultDate, ScheduleEvent editTarget, string defaultTime = null)
    {
        bool isEdit = editTarget != null;
        titleText.text = isEdit ? "予定を編集" : "予定を追加";
        ClearContent();

        // タグ選択
        var tagRow     = CreateRow("タグ");
        var tagSelGO   = new GameObject("TagSelector");
        tagSelGO.transform.SetParent(tagRow.transform, false);
        var tagSelHL   = tagSelGO.AddComponent<HorizontalLayoutGroup>();
        tagSelHL.spacing = 4f;
        tagSelHL.childForceExpandWidth  = false;
        tagSelHL.childForceExpandHeight = false;
        tagSelGO.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        string selectedTagId = isEdit ? editTarget.tagId : "todo";
        var tagBtns = new List<(Button btn, Image img, TextMeshProUGUI txt, string id)>();
        foreach (var tagDef in TagConfig.Tags)
        {
            var tBtnGO  = new GameObject(tagDef.id);
            tBtnGO.transform.SetParent(tagSelGO.transform, false);
            var tImg    = tBtnGO.AddComponent<Image>();
            var tBtn    = tBtnGO.AddComponent<Button>();
            var tLE     = tBtnGO.AddComponent<LayoutElement>();
            tLE.preferredHeight = 22f;
            var tTxtGO  = new GameObject("Label");
            tTxtGO.transform.SetParent(tBtnGO.transform, false);
            var tTxtRT  = tTxtGO.AddComponent<RectTransform>();
            tTxtRT.anchorMin = Vector2.zero; tTxtRT.anchorMax = Vector2.one;
            tTxtRT.offsetMin = new Vector2(8f,0f); tTxtRT.offsetMax = new Vector2(-8f,0f);
            var tTxt    = tTxtGO.AddComponent<TextMeshProUGUI>();
            tTxt.text   = tagDef.displayName;
            tTxt.fontSize = UITheme_FocusMode.FontCaption;
            tTxt.alignment = TextAlignmentOptions.Center;
            tagBtns.Add((tBtn, tImg, tTxt, tagDef.id));
        }
        void UpdateTagBtns(string selId)
        {
            selectedTagId = selId;
            foreach (var (btn, img, txt, id) in tagBtns)
            {
                var td = TagConfig.GetById(id);
                if (id == selId)
                { img.color = td.selectorBG; txt.color = td.selectorText; }
                else
                { img.color = UITheme_FocusMode.InputBG; txt.color = UITheme_FocusMode.TextMuted; }
            }
        }
        foreach (var (btn, img, txt, id) in tagBtns)
        {
            var capId = id;
            btn.onClick.AddListener(() => UpdateTagBtns(capId));
        }
        UpdateTagBtns(selectedTagId);

        var titleInput = CreateInputField("タイトル", isEdit ? editTarget.title : "");
        var dateRow    = CreateRow("日付");
        var dateTxt    = CreateValueText(dateRow, FormatDate(defaultDate));
        var timeInput  = CreateInputField("時間（任意）", isEdit ? editTarget.time ?? "" : defaultTime ?? "");
        timeInput.placeholder.GetComponent<TextMeshProUGUI>().text = "例：14:00";
        var memoInput  = CreateInputField("内容", isEdit ? editTarget.memo ?? "" : "");

        footer.SetActive(true);
        primaryBtnText.text   = "保存";
        secondaryBtnText.text = "キャンセル";
        primaryBtn.onClick.RemoveAllListeners();
        secondaryBtn.onClick.RemoveAllListeners();
        primaryBtn.onClick.AddListener(() =>
        {
            var t  = titleInput.text.Trim();
            if (string.IsNullOrEmpty(t)) return;
            var ti = timeInput.text.Trim();
            var m  = memoInput.text.Trim();

            if (isEdit)
                NotebookManager.Instance?.UpdateEvent(editTarget.id, selectedTagId, t, defaultDate, ti, null, m);
            else
                NotebookManager.Instance?.AddEvent(selectedTagId, t, defaultDate, string.IsNullOrEmpty(ti) ? null : ti, m);

            onRefresh?.Invoke();
            Close();
        });
        secondaryBtn.onClick.AddListener(Close);

        Show();
    }

    // ─── UIヘルパー ───────────────────────────────────────
    private void ClearContent()
    {
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);
    }

    private GameObject CreateRow(string label)
    {
        var rowGO  = new GameObject(label + "Row");
        rowGO.transform.SetParent(contentParent, false);
        var rowVL  = rowGO.AddComponent<VerticalLayoutGroup>();
        rowVL.spacing = 3f;
        rowVL.childForceExpandWidth = true;
        rowVL.childForceExpandHeight = false;
        rowGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var lblGO  = new GameObject("Label");
        lblGO.transform.SetParent(rowGO.transform, false);
        var lbl    = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text   = label;
        lbl.fontSize  = UITheme_FocusMode.FontCaption;
        lbl.color     = UITheme_FocusMode.TextCaption;
        lblGO.AddComponent<LayoutElement>().preferredHeight = 14f;
        return rowGO;
    }

    private TMP_InputField CreateInputField(string label, string defaultValue)
    {
        var row    = CreateRow(label);
        var fieldGO= new GameObject("InputField");
        fieldGO.transform.SetParent(row.transform, false);
        var img    = fieldGO.AddComponent<Image>();
        img.color  = UITheme_FocusMode.InputBG;
        var field  = fieldGO.AddComponent<TMP_InputField>();
        field.text = defaultValue;
        var fieldLE= fieldGO.AddComponent<LayoutElement>();
        fieldLE.preferredHeight = 28f;

        // TextArea
        var textAreaGO = new GameObject("TextArea");
        textAreaGO.transform.SetParent(fieldGO.transform, false);
        var taRT   = textAreaGO.AddComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(6f,0f); taRT.offsetMax = new Vector2(-6f,0f);
        textAreaGO.AddComponent<RectMask2D>();

        // Placeholder
        var phGO   = new GameObject("Placeholder");
        phGO.transform.SetParent(textAreaGO.transform, false);
        var phRT   = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var ph     = phGO.AddComponent<TextMeshProUGUI>();
        ph.color   = UITheme_FocusMode.TextPlaceholder;
        ph.fontSize= UITheme_FocusMode.FontBody;

        // Text
        var txtGO  = new GameObject("Text");
        txtGO.transform.SetParent(textAreaGO.transform, false);
        var txtRT  = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var txt    = txtGO.AddComponent<TextMeshProUGUI>();
        txt.color  = UITheme_FocusMode.TextBody;
        txt.fontSize = UITheme_FocusMode.FontBody;

        field.textViewport  = taRT;
        field.placeholder   = ph;
        field.textComponent = txt;
        return field;
    }

    private void AddRow(string icon, string value)
    {
        var rowGO  = new GameObject("InfoRow");
        rowGO.transform.SetParent(contentParent, false);
        var rowHL  = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowHL.spacing = 8f;
        rowHL.childForceExpandWidth  = false;
        rowHL.childForceExpandHeight = false;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 20f;

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(rowGO.transform, false);
        var iconTxt= iconGO.AddComponent<TextMeshProUGUI>();
        iconTxt.text = icon; iconTxt.fontSize = 12f;
        iconTxt.color = UITheme_FocusMode.TextMuted;
        iconGO.AddComponent<LayoutElement>().preferredWidth = 16f;

        var valGO  = new GameObject("Value");
        valGO.transform.SetParent(rowGO.transform, false);
        var valTxt = valGO.AddComponent<TextMeshProUGUI>();
        valTxt.text = value;
        valTxt.fontSize = UITheme_FocusMode.FontBody;
        valTxt.color = UITheme_FocusMode.TextBody;
    }

    private void AddTagBadge(TagDefinition tag)
    {
        var badgeGO = new GameObject("TagBadge");
        badgeGO.transform.SetParent(contentParent, false);
        var badgeImg= badgeGO.AddComponent<Image>();
        badgeImg.color = tag.badgeBG;
        var badgeLE = badgeGO.AddComponent<LayoutElement>();
        badgeLE.preferredHeight = 20f;
        badgeLE.preferredWidth  = 60f;
        var badgeTxt= new GameObject("Text");
        badgeTxt.transform.SetParent(badgeGO.transform, false);
        var tRT = badgeTxt.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(6f,0f); tRT.offsetMax = new Vector2(-6f,0f);
        var t = badgeTxt.AddComponent<TextMeshProUGUI>();
        t.text = tag.displayName;
        t.fontSize = UITheme_FocusMode.FontCaption;
        t.color = tag.badgeText;
        t.alignment = TextAlignmentOptions.Center;
    }

    private void AddDivider()
    {
        var divGO  = new GameObject("Divider");
        divGO.transform.SetParent(contentParent, false);
        var divImg = divGO.AddComponent<Image>();
        divImg.color = UITheme_FocusMode.BorderDivider;
        divGO.AddComponent<LayoutElement>().preferredHeight = 1f;
    }

    private void AddMemoText(string memo)
    {
        var memoGO = new GameObject("Memo");
        memoGO.transform.SetParent(contentParent, false);
        var memoTxt= memoGO.AddComponent<TextMeshProUGUI>();
        memoTxt.text = memo;
        memoTxt.fontSize = UITheme_FocusMode.FontBody;
        memoTxt.color = UITheme_FocusMode.TextMuted;
        memoTxt.enableWordWrapping = true;
    }

    private Button CreateTextButton(string label)
    {
        var btnGO  = new GameObject("TextButton");
        btnGO.transform.SetParent(contentParent, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = UITheme_FocusMode.AccentBlueFaint;
        var btn    = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btnGO.AddComponent<LayoutElement>().preferredHeight = 32f;

        var lblGO  = new GameObject("Label");
        lblGO.transform.SetParent(btnGO.transform, false);
        var lblRT  = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lbl    = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text   = label;
        lbl.fontSize = UITheme_FocusMode.FontBody;
        lbl.color  = UITheme_FocusMode.TextSecondary;
        lbl.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    private TextMeshProUGUI CreateValueText(GameObject parent, string value)
    {
        var valGO  = new GameObject("Value");
        valGO.transform.SetParent(parent.transform, false);
        var valImg = valGO.AddComponent<Image>();
        valImg.color = UITheme_FocusMode.InputBG;
        valGO.AddComponent<LayoutElement>().preferredHeight = 28f;
        var txtGO  = new GameObject("Text");
        txtGO.transform.SetParent(valGO.transform, false);
        var txtRT  = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(8f,0f); txtRT.offsetMax = new Vector2(-8f,0f);
        var txt    = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text   = value;
        txt.fontSize = UITheme_FocusMode.FontBody;
        txt.color  = UITheme_FocusMode.TextBody;
        return txt;
    }

    // ─── 表示・非表示 ─────────────────────────────────────
    private void Show()
    {
        overlay?.SetActive(true);
        windowPanel?.SetActive(true);
    }

    public void Close()
    {
        overlay?.SetActive(false);
        windowPanel?.SetActive(false);
        ClearContent();
        footer?.SetActive(false);
        currentEvent = null;
    }

    // ─── ユーティリティ ───────────────────────────────────
    private static readonly string[] DowLabels = {"日","月","火","水","木","金","土"};

    private static string FormatDate(string dateKey)
    {
        if (string.IsNullOrEmpty(dateKey)) return "日付なし";
        if (!DateTime.TryParse(dateKey, out var dt)) return dateKey;
        return $"{dt.Year}年{dt.Month}月{dt.Day}日（{DowLabels[(int)dt.DayOfWeek]}）";
    }
}
