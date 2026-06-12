using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ルーチンタブ左ペイン(リスト)
/// - セクション: 今日(OccursOn(today)) / 今日以外
/// - 今日の行のみチェック円(=今日のオカレンス完了トグル)。完了は今日セクション内の末尾に沈む
/// - 行末チップ: 繰り返しパターン(毎日/月・水/3日ごと) + 時刻
/// </summary>
public class RoutineListUI : MonoBehaviour
{
    [SerializeField] private Button addButton;
    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private RoutineDetailUI detail;

    private string _selectedId;
    private static readonly string[] DayNames = { "日", "月", "火", "水", "木", "金", "土" };

    private void OnEnable()
    {
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
        if (addButton != null) addButton.onClick.RemoveListener(OnAddClicked);
        if (detail != null)
        {
            detail.OnChanged -= Rebuild;
            detail.OnDeleted -= OnItemDeleted;
        }
        UITheme_FocusMode.OnThemeChanged -= Rebuild;
    }

    private void OnAddClicked()
    {
        var nm = NotebookManager.Instance;
        if (nm == null) return;
        var item = nm.AddRoutine("新しいルーチン");
        Select(item);
        Rebuild();
        if (detail != null) detail.FocusTitle();
    }

    private void OnItemDeleted(string id)
    {
        if (_selectedId == id) _selectedId = null;
        Rebuild();
    }

    private void Select(RoutineItem item)
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

        var all = nm.GetRoutines();
        var today = DateTime.Now.Date;
        var todayKey = today.ToString("yyyy-MM-dd");

        var todays = all.Where(r => r.OccursOn(today))
                        .OrderBy(r => r.IsDoneOn(todayKey) ? 1 : 0)
                        .ThenByDescending(r => r.priorityHigh)
                        .ThenBy(r => r.createdAt)
                        .ToList();
        var others = all.Where(r => !r.OccursOn(today))
                        .OrderByDescending(r => r.priorityHigh)
                        .ThenBy(r => r.createdAt)
                        .ToList();

        if (todays.Count > 0)
        {
            int doneCount = todays.Count(r => r.IsDoneOn(todayKey));
            BuildSectionHeader($"今日 · {doneCount}/{todays.Count}件", UITheme_FocusMode.AccentSatBlue);
            foreach (var item in todays) BuildRow(item, todayKey, true);
        }
        if (others.Count > 0)
        {
            BuildSectionHeader($"今日以外 · {others.Count}件", UITheme_FocusMode.TextCaption);
            foreach (var item in others) BuildRow(item, todayKey, false);
        }
        if (all.Count == 0) BuildEmptyLabel();
    }

    // ── 行の生成 ──────────────────────────────

    private void BuildRow(RoutineItem item, string todayKey, bool isToday)
    {
        bool done = isToday && item.IsDoneOn(todayKey);
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

        // チェック円(今日のみ)/ 今日以外はスペーサーで桁を揃える
        var check = NewUI("Check", row.transform);
        var checkLE = check.AddComponent<LayoutElement>();
        checkLE.minWidth = 26; checkLE.minHeight = 26;
        checkLE.preferredWidth = 26; checkLE.preferredHeight = 26;
        if (isToday)
        {
            var checkImg = check.AddComponent<Image>();
            checkImg.sprite = UISprites.Circle;
            if (done) checkImg.color = UITheme_FocusMode.AccentBlueSolid;
            else
            {
                UIStyleKit.ApplyCircleGhost(checkImg);
                checkImg.color = UITheme_FocusMode.WithAlpha(Color.white, 0.30f);
            }
            var checkBtn = check.AddComponent<Button>();
            checkBtn.targetGraphic = checkImg;
            checkBtn.onClick.AddListener(() =>
            {
                NotebookManager.Instance.SetRoutineDone(captured.id, todayKey, !captured.IsDoneOn(todayKey));
                Rebuild();
            });
        }

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

        // 繰り返しパターンチップ(+時刻)
        var label = RepeatLabel(item);
        if (!string.IsNullOrEmpty(item.time)) label += " " + item.time;
        BuildChip(row.transform, label,
            done ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextMuted,
            UITheme_FocusMode.InputBG);
    }

    private static string RepeatLabel(RoutineItem item)
    {
        switch (item.repeatType)
        {
            case "weekly":
                if (item.weekdays == null || item.weekdays.Count == 0) return "毎週";
                if (item.weekdays.Count == 7) return "毎日";
                var sorted = item.weekdays.Where(d => d >= 0 && d <= 6).Distinct().OrderBy(d => d).ToList();
                return string.Join("・", sorted.Select(d => DayNames[d]));
            case "interval":
                return item.intervalDays <= 1 ? "毎日" : item.intervalDays + "日ごと";
            default:
                return "毎日";
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
        var label = NewText("Label", empty.transform, "ルーチンはありません。「+ 追加」から作成できます",
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
}
