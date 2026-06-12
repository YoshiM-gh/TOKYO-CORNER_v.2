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

        var rowBtn = row.AddComponent<Button>();
        rowBtn.targetGraphic = rowImg;
        var captured = item;
        rowBtn.onClick.AddListener(() => { Select(captured); Rebuild(); });

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

        // タイトル(クリップ枠 + Overflow)
        var titleClip = NewUI("TitleClip", row.transform);
        titleClip.AddComponent<RectMask2D>();
        var clipLE = titleClip.AddComponent<LayoutElement>();
        clipLE.flexibleWidth = 1; clipLE.minHeight = 30;
        var title = NewText("Title", titleClip.transform, item.title, UITheme_FocusMode.FontChipTitle,
            done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextSecondary);
        if (done) title.fontStyle = FontStyles.Strikethrough;
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

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
