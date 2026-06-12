using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Monthly/Weekly の日セルに「Todoあり(T) / Routineあり(R)」インジケータを生成する共通ヘルパー。
/// その日の項目が全て完了済みなら「T✓」「R✓」+ 減光表示。
/// クリックで該当タブ（Todo/Routine）へ移動する。
/// </summary>
public static class CalendarDayIndicators
{
    public const string ContainerName = "DayIndicators";
    private const float FONT_SIZE = 22f;   // 日付番号と同サイズ
    private const float ICON_H    = 26f;
    private const float ICON_GAP  = 4f;

    private static Michsky.MUIP.WindowManager _wm;

    /// <summary>既存インジケータの除去（セル再利用時のクリア用）</summary>
    public static void Clear(Transform cell)
    {
        var old = cell.Find(ContainerName);
        if (old != null) UnityEngine.Object.Destroy(old.gameObject);
    }

    /// <summary>
    /// 指定日の Todo/Routine 有無を判定してインジケータを生成。
    /// デフォルト配置はセル右上。戻り値の RectTransform で呼び出し側が配置を上書き可（生成なしなら null）。
    /// </summary>
    public static RectTransform Build(Transform cell, DateTime date)
    {
        Clear(cell);

        var nm = NotebookManager.Instance;
        if (nm == null) return null;

        string dk    = NotebookManager.DateKey(date);
        var todos    = nm.GetTodosOn(dk);
        var routines = nm.GetRoutinesOn(date);
        bool hasTodo    = todos.Count > 0;
        bool hasRoutine = routines.Count > 0;
        if (!hasTodo && !hasRoutine) return null;

        var go = new GameObject(ContainerName, typeof(RectTransform));
        go.transform.SetParent(cell, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(0f, ICON_H);
        rt.anchoredPosition = new Vector2(-6f, -4f);

        // 親セルの LayoutGroup から除外（自由配置を維持）
        var ignore = go.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = ICON_GAP;
        hlg.childAlignment         = TextAnchor.MiddleRight;
        hlg.childControlWidth      = true;   // TMP の preferredWidth で幅決定
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

        if (hasTodo)
        {
            bool allDone = todos.All(t => t.isCompleted);
            AddIndicator(go.transform, "TodoIcon", "T", allDone, "Todo");
        }
        if (hasRoutine)
        {
            bool allDone = routines.All(r => r.IsDoneOn(dk));
            AddIndicator(go.transform, "RoutineIcon", "R", allDone, "Routine");
        }
        return rt;
    }

    private static void AddIndicator(Transform parent, string name, string letter,
        bool allDone, string windowName)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text      = allDone ? letter + "\u2713" : letter;  // 全完了で ✓ を付加
        txt.fontSize  = FONT_SIZE;
        txt.color     = allDone ? UITheme_FocusMode.TextMuted : UITheme_FocusMode.TextBody;
        txt.alignment = TextAlignmentOptions.Midline;
        txt.enableWordWrapping = false;
        txt.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = txt;
        string cap = windowName;
        btn.onClick.AddListener(() => OpenTab(cap));
    }

    private static void OpenTab(string windowName)
    {
        if (_wm == null)
            _wm = UnityEngine.Object.FindFirstObjectByType<Michsky.MUIP.WindowManager>(FindObjectsInactive.Include);
        if (_wm != null) _wm.OpenWindow(windowName);
    }
}
