using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// フォーカスモードUIにデザイントークンを一括適用するエディターツール。
/// メニュー: TOKYO CORNER > Apply Focus Mode Design Tokens
/// </summary>
public static class FocusModeDesignApplier
{
    [MenuItem("TOKYO CORNER/Apply Focus Mode Design Tokens")]
    public static void Apply()
    {
        Debug.Log("[DesignApplier] 開始...");
        ApplyFocusPanel();
        ApplyCard();
        ApplyNotebookPanel();
        ApplyTabBar();
        ApplyMonthlyPanel();
        ApplyWeeklyPanel();
        ApplyDailyPanel();
        ApplyKanriPanel();
        ApplyMemoPanel();
        ApplyFloatingWindow();

        EditorUtility.SetDirty(GameObject.Find("Systems/Canvas/FocusUI"));
        Debug.Log("[DesignApplier] 完了！");
    }

    // ─── ユーティリティ ───────────────────────────────────
    static GameObject Find(string path) => GameObject.Find(path);

    static void SetImg(string path, Color c)
    {
        var go = Find(path);
        var img = go?.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    static void SetTxt(string path, Color c, float? size = null)
    {
        var go = Find(path);
        var txt = go?.GetComponent<TextMeshProUGUI>();
        if (txt == null) return;
        txt.color = c;
        if (size.HasValue) txt.fontSize = size.Value;
    }

    // ─── FocusPanel 背景 ──────────────────────────────────
    static void ApplyFocusPanel()
    {
        var fp = Find("Systems/Canvas/FocusUI/FocusPanel");
        if (fp == null) return;
        var img = fp.GetComponent<Image>() ?? fp.AddComponent<Image>();
        img.color = UITheme_FocusMode.DashboardBG;
        img.raycastTarget = false;
    }

    // ─── Card（ポモドーロ） ────────────────────────────────
    static void ApplyCard()
    {
        var card = Find("Systems/Canvas/FocusUI/FocusPanel/Card");
        if (card == null) return;

        // Card 背景
        var cardImg = card.GetComponent<Image>();
        if (cardImg != null) cardImg.color = UITheme_FocusMode.PanelBG;

        // 全TextMeshProUGUI
        foreach (var txt in card.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var n = txt.gameObject.name;
            var p = txt.transform.parent?.name ?? "";

            if (n == "TimerText")
            { txt.color = UITheme_FocusMode.TextPrimary; txt.fontSize = UITheme_FocusMode.FontTimerDisplay; }
            else if (n == "CycleText")
            { txt.color = UITheme_FocusMode.TextMuted; txt.fontSize = UITheme_FocusMode.FontCaption; }
            else if (n == "PomodoroLabel")
            { txt.color = UITheme_FocusMode.TextMuted; txt.fontSize = UITheme_FocusMode.FontCaption; }
            else if (n == "Label")
            { txt.color = UITheme_FocusMode.TextMuted; txt.fontSize = UITheme_FocusMode.FontBody; }
            else if (n == "Value")
            { txt.color = UITheme_FocusMode.TextPrimary; txt.fontSize = UITheme_FocusMode.FontBody; }
            else
            { txt.color = UITheme_FocusMode.TextMuted; txt.fontSize = UITheme_FocusMode.FontBody; }
        }

        // 全ボタン
        foreach (var btn in card.GetComponentsInChildren<Button>(true))
        {
            var img = btn.GetComponent<Image>();
            if (img == null) continue;
            var n = btn.gameObject.name;
            bool isPlay = n.ToLower().Contains("play");
            img.color = isPlay
                ? UITheme_FocusMode.AccentBlue
                : new Color(1f, 1f, 1f, 0.06f);
            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.color = isPlay ? Color.white : UITheme_FocusMode.TextMuted;
        }
    }

    // ─── NotebookPanel ────────────────────────────────────
    static void ApplyNotebookPanel()
    {
        var nb = Find("Systems/Canvas/FocusUI/FocusPanel/NotebookPanel");
        if (nb == null) return;
        var img = nb.GetComponent<Image>();
        if (img != null) img.color = UITheme_FocusMode.PanelBG;
    }

    // ─── TabBar ───────────────────────────────────────────
    static void ApplyTabBar()
    {
        var tabBar = Find("Systems/Canvas/FocusUI/FocusPanel/NotebookPanel/TabBar");
        if (tabBar == null) return;

        // TabBar 背景
        var tbImg = tabBar.GetComponent<Image>();
        if (tbImg != null) tbImg.color = new Color(1f, 1f, 1f, 0.03f);

        // 各タブボタン
        string[] tabNames = { "Tab_月", "Tab_週", "Tab_日", "Tab_Todo", "Tab_Memo" };
        bool first = true;
        foreach (var tabName in tabNames)
        {
            var tab = tabBar.transform.Find(tabName)?.gameObject;
            if (tab == null) continue;
            var img = tab.GetComponent<Image>();
            var txt = tab.GetComponentInChildren<TextMeshProUGUI>();
            if (img != null) img.color = first
                ? new Color(1f, 1f, 1f, 0.08f)   // アクティブ（月）
                : new Color(1f, 1f, 1f, 0.03f);   // 非アクティブ
            if (txt != null)
            {
                txt.color = first ? UITheme_FocusMode.TextPrimary : UITheme_FocusMode.TextMuted;
                txt.fontSize = UITheme_FocusMode.FontTabLabel;
            }
            first = false;
        }
    }

    // ─── MonthlyPanel ─────────────────────────────────────
    static void ApplyMonthlyPanel()
    {
        var mp = Find("Systems/Canvas/FocusUI/FocusPanel/NotebookPanel/MonthlyPanel");
        if (mp == null) return;

        // NavRow
        ApplyNavRow(mp, "NavRow");

        // 曜日ヘッダー
        var dowHeader = mp.transform.Find("DowHeader");
        if (dowHeader != null)
        {
            var txts = dowHeader.GetComponentsInChildren<TextMeshProUGUI>();
            string[] dowLabels = {"日","月","火","水","木","金","土"};
            for (int i = 0; i < txts.Length && i < 7; i++)
            {
                txts[i].fontSize = UITheme_FocusMode.FontCaption;
                txts[i].color = i == 0
                    ? UITheme_FocusMode.AccentRed
                    : i == 6
                        ? UITheme_FocusMode.AccentSatBlue
                        : UITheme_FocusMode.TextMuted;
            }
        }

        // CalGrid セル
        var calGrid = mp.transform.Find("CalGrid");
        if (calGrid != null)
        {
            foreach (Transform cell in calGrid)
            {
                var img = cell.GetComponent<Image>();
                if (img != null) img.color = UITheme_FocusMode.DayCellBG;
                // DayNumber テキスト
                var numTxt = cell.Find("DayNumber")?.GetComponent<TextMeshProUGUI>();
                if (numTxt != null)
                {
                    numTxt.color = UITheme_FocusMode.TextBody;
                    numTxt.fontSize = UITheme_FocusMode.FontBody;
                }
            }
        }
    }

    // ─── WeeklyPanel ──────────────────────────────────────
    static void ApplyWeeklyPanel()
    {
        var wp = Find("Systems/Canvas/FocusUI/FocusPanel/NotebookPanel/WeeklyPanel");
        if (wp == null) return;
        ApplyNavRow(wp, "NavRow");

        // HeaderRow のセル
        var headerRow = wp.transform.Find("HeaderRow");
        if (headerRow != null)
        {
            foreach (var cell in headerRow.GetComponentsInChildren<Image>())
                if (cell.gameObject != headerRow.gameObject)
                    cell.color = Color.clear;
            foreach (var txt in headerRow.GetComponentsInChildren<TextMeshProUGUI>())
            {
                txt.fontSize = UITheme_FocusMode.FontCaption;
                txt.color = UITheme_FocusMode.TextMuted;
            }
        }
    }

    // ─── DailyPanel ───────────────────────────────────────
    static void ApplyDailyPanel()
    {
        var dp = Find("Systems/Canvas/FocusUI/FocusPanel/NotebookPanel/DailyPanel");
        if (dp == null) return;

        // TimelineArea NavRow
        var tlArea = dp.transform.Find("TimelineArea");
        if (tlArea != null) ApplyNavRow(tlArea.gameObject, "NavRow");

        // DetailPanel
        var detail = dp.transform.Find("DetailPanel");
        if (detail != null)
        {
            var dImg = detail.GetComponent<Image>();
            if (dImg != null) dImg.color = UITheme_FocusMode.SubPanelBG;

            var dhTxt = detail.Find("DetailHeader/Title")?.GetComponent<TextMeshProUGUI>();
            if (dhTxt != null) { dhTxt.color = UITheme_FocusMode.TextPrimary; dhTxt.fontSize = UITheme_FocusMode.FontSectionTitle; }

            var emptyTxt = detail.Find("EmptyText")?.GetComponent<TextMeshProUGUI>();
            if (emptyTxt != null) { emptyTxt.color = UITheme_FocusMode.TextDisabled; emptyTxt.fontSize = UITheme_FocusMode.FontBody; }

            ApplyFooter(detail.Find("DetailFooter")?.gameObject);
        }
    }

    // ─── KanriPanel（TodoPanel） ───────────────────────────
    static void ApplyKanriPanel()
    {
        var kp = Find("Systems/Canvas/FocusUI/FocusPanel/NotebookPanel/TodoPanel");
        if (kp == null) return;

        // 右詳細パネル
        var right = kp.transform.Find("RightColumn");
        if (right != null)
        {
            var rImg = right.GetComponent<Image>();
            if (rImg != null) rImg.color = UITheme_FocusMode.SubPanelBG;

            var dhTxt = right.Find("DetailHeader/Title")?.GetComponent<TextMeshProUGUI>();
            if (dhTxt != null) { dhTxt.color = UITheme_FocusMode.TextPrimary; dhTxt.fontSize = UITheme_FocusMode.FontSectionTitle; }

            var emptyTxt = right.Find("EmptyText")?.GetComponent<TextMeshProUGUI>();
            if (emptyTxt != null) { emptyTxt.color = UITheme_FocusMode.TextDisabled; emptyTxt.fontSize = UITheme_FocusMode.FontBody; }

            ApplyFooter(right.Find("DetailFooter")?.gameObject);
        }

        // 左フィルターボタン全体のラベル
        var leftCol = kp.transform.Find("LeftColumn");
        if (leftCol != null)
        {
            foreach (var txt in leftCol.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (txt.fontSize <= 0) txt.fontSize = UITheme_FocusMode.FontCaption;
            }
        }
    }

    // ─── MemoPanel ────────────────────────────────────────
    static void ApplyMemoPanel()
    {
        var memo = Find("Systems/Canvas/FocusUI/FocusPanel/NotebookPanel/MemoPanel");
        if (memo == null) return;

        var right = memo.transform.Find("RightColumn");
        if (right != null)
        {
            var rImg = right.GetComponent<Image>();
            if (rImg != null) rImg.color = UITheme_FocusMode.SubPanelBG;

            var metaTxt = right.Find("MetaText")?.GetComponent<TextMeshProUGUI>();
            if (metaTxt != null) { metaTxt.color = UITheme_FocusMode.TextDisabled; metaTxt.fontSize = UITheme_FocusMode.FontCaption; }

            var emptyTxt = right.Find("EmptyText")?.GetComponent<TextMeshProUGUI>();
            if (emptyTxt != null) { emptyTxt.color = UITheme_FocusMode.TextDisabled; emptyTxt.fontSize = UITheme_FocusMode.FontBody; }
        }

        var left = memo.transform.Find("LeftColumn");
        if (left != null)
        {
            var headerLbl = left.Find("ListHeader/Label")?.GetComponent<TextMeshProUGUI>();
            if (headerLbl != null) { headerLbl.color = UITheme_FocusMode.TextMuted; headerLbl.fontSize = UITheme_FocusMode.FontBody; }

            var addBtn = left.Find("ListHeader/AddMemoBtn")?.GetComponent<Image>();
            if (addBtn != null) addBtn.color = UITheme_FocusMode.AccentBlue;
        }
    }

    // ─── FloatingWindow ───────────────────────────────────
    static void ApplyFloatingWindow()
    {
        var overlay = Find("Systems/Canvas/FocusUI/FocusPanel/FloatingOverlay");
        if (overlay == null) return;

        var ovImg = overlay.GetComponent<Image>();
        if (ovImg != null) ovImg.color = new Color(0f, 0f, 0f, 0.5f);

        var win = overlay.transform.Find("FloatingWindow");
        if (win == null) return;

        var winImg = win.GetComponent<Image>();
        if (winImg != null) winImg.color = UITheme_FocusMode.FloatBG;

        // ヘッダータイトル
        var titleTxt = win.Find("Header/TitleText")?.GetComponent<TextMeshProUGUI>();
        if (titleTxt != null) { titleTxt.color = UITheme_FocusMode.TextPrimary; titleTxt.fontSize = UITheme_FocusMode.FontSectionTitle; }

        // 閉じるボタン
        var closeImg = win.Find("Header/CloseBtn")?.GetComponent<Image>();
        if (closeImg != null) closeImg.color = new Color(1f, 1f, 1f, 0.08f);

        // フッターボタン
        ApplyFooter(win.Find("Footer")?.gameObject);
    }

    // ─── 共通：ナビゲーション行 ───────────────────────────
    static void ApplyNavRow(GameObject parent, string rowName)
    {
        var navRow = parent.transform.Find(rowName);
        if (navRow == null) return;

        // ◀▶ボタン
        foreach (var btn in navRow.GetComponentsInChildren<Button>())
        {
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, 0.06f);
            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) { txt.color = UITheme_FocusMode.TextMuted; txt.fontSize = UITheme_FocusMode.FontCaption; }
        }

        // ラベルテキスト
        foreach (var txt in navRow.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (txt.GetComponent<Button>() == null && txt.GetComponentInParent<Button>() == null)
            {
                txt.color = UITheme_FocusMode.TextPrimary;
                txt.fontSize = UITheme_FocusMode.FontSectionTitle;
            }
        }
    }

    // ─── 共通：フッターボタン ─────────────────────────────
    static void ApplyFooter(GameObject footer)
    {
        if (footer == null) return;
        var btns = footer.GetComponentsInChildren<Button>();
        if (btns.Length >= 1)
        {
            var img = btns[0].GetComponent<Image>();
            if (img != null) img.color = UITheme_FocusMode.AccentBlue;
            var txt = btns[0].GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) { txt.color = UITheme_FocusMode.TextSecondary; txt.fontSize = UITheme_FocusMode.FontBody; }
        }
        if (btns.Length >= 2)
        {
            var img = btns[1].GetComponent<Image>();
            if (img != null) img.color = new Color(0.627f, 0.216f, 0.216f, 0.28f);
            var txt = btns[1].GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) { txt.color = UITheme_FocusMode.TextSecondary; txt.fontSize = UITheme_FocusMode.FontBody; }
        }
    }
}
