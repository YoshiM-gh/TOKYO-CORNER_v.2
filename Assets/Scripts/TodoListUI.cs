using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// タスクタブUI
/// - 上部バー: 完了済み表示トグル + 追加ボタン
/// - リスト: 未完了(優先度高→sortOrder→作成順) / 完了済みセクション(トグルON時のみ)
/// - 行: チェック円 / タイトル / 「高」チップ / 日付時刻チップ
/// - 行クリックで編集モーダル(モーダル実装後に接続)
/// </summary>
public class TodoListUI : MonoBehaviour
{
    [SerializeField] private Toggle showDoneToggle;
    [SerializeField] private Button addButton;
    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_FontAsset font;

    private void OnEnable()
    {
        if (showDoneToggle != null) showDoneToggle.onValueChanged.AddListener(OnShowDoneChanged);
        if (addButton != null) addButton.onClick.AddListener(OnAddClicked);
        UITheme_FocusMode.OnThemeChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        if (showDoneToggle != null) showDoneToggle.onValueChanged.RemoveListener(OnShowDoneChanged);
        if (addButton != null) addButton.onClick.RemoveListener(OnAddClicked);
        UITheme_FocusMode.OnThemeChanged -= Rebuild;
    }

    private void OnShowDoneChanged(bool _) => Rebuild();

    private void OnAddClicked()
    {
        // TODO: 編集モーダル(新規)を開く。モーダル実装までの暫定動作
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        nm.AddTodo("新しいタスク");
        Rebuild();
    }

    private void OpenEditModal(TodoItem item)
    {
        // TODO: モーダル実装後に接続
        Debug.Log($"[TodoListUI] OpenEditModal: {item.title}");
    }

    public void Rebuild()
    {
        if (listContent == null) return;
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        var nm = NotebookManager.Instance;
        if (nm == null) return;

        var all = nm.GetTodos(true);
        var open = all.Where(t => !t.isCompleted)
                      .OrderByDescending(t => t.priorityHigh)
                      .ThenBy(t => t.sortOrder)
                      .ThenBy(t => t.createdAt)
                      .ToList();
        foreach (var item in open) BuildRow(item);

        bool showDone = showDoneToggle != null && showDoneToggle.isOn;
        if (showDone)
        {
            var done = all.Where(t => t.isCompleted)
                          .OrderByDescending(t => t.completedAt)
                          .ToList();
            if (done.Count > 0)
            {
                BuildSectionHeader($"完了済み · {done.Count}件");
                foreach (var item in done) BuildRow(item);
            }
        }

        if (open.Count == 0 && !showDone) BuildEmptyLabel();
    }

    // ── 行の生成 ──────────────────────────────

    private void BuildRow(TodoItem item)
    {
        bool done = item.isCompleted;

        var row = NewUI("Row_" + item.id, listContent);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = done ? UITheme_FocusMode.DoneBG : UITheme_FocusMode.PanelBG;
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
        rowBtn.onClick.AddListener(() => OpenEditModal(item));

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
        var captured = item;
        checkBtn.onClick.AddListener(() =>
        {
            NotebookManager.Instance.SetTodoCompleted(captured.id, !captured.isCompleted);
            Rebuild();
        });

        // タイトル(クリップ枠 + Overflow、Ellipsis不使用)
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

        // 日付時刻チップ
        var dateLabel = FormatDateChip(item);
        if (dateLabel != null)
            BuildChip(row.transform, dateLabel, UITheme_FocusMode.TextMuted, UITheme_FocusMode.InputBG);
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

    private void BuildSectionHeader(string text)
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
        NewText("Label", sec.transform, text, UITheme_FocusMode.FontCaption, UITheme_FocusMode.TextCaption);
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
        if (string.IsNullOrEmpty(item.dateKey)) return null;
        if (!DateTime.TryParseExact(item.dateKey, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d)) return null;
        var s = $"{d.Month}/{d.Day}";
        if (!string.IsNullOrEmpty(item.time)) s += " " + item.time;
        return s;
    }
}
