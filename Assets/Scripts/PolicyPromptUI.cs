using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 着席→フォーカス入場時の方針プロンプト（儀式ループ）。
/// 「今日はどんな一日にしたいですか？」— その日の方針(NotebookManager.WeeklyMemo)が
/// 未設定のときだけ表示し、設定済みなら黙って素通しで入場する。
/// 行クリックで確定（キー操作なし・ワールドと同じマウス文法）:
///   方針 → 保存して入場 / きめずに入る → 保存せず入場（白紙を赦す） / やめる → 着席メニューへ戻る
/// 子を「名前」で探して結線: Panel > Box > TitleLabel / ListContent > RowTemplate(CursorLabel/NameLabel)
/// </summary>
public class PolicyPromptUI : MonoBehaviour
{
    public static PolicyPromptUI Instance { get; private set; }

    private GameObject _panel;
    private Transform _listContent;
    private GameObject _rowTemplate;
    private Action _onEnter;
    private Action _onCancel;
    private readonly List<GameObject> _rows = new();
    private MenuRowHighlight _cursorRow; // ▶の現在行（デフォルト=最上行・ホバーで移動・常駐）

    private static readonly Color ColBody = new Color32(232, 237, 242, 255);
    private static readonly Color ColMuted = new Color32(139, 152, 165, 255);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Bind();
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>方針が未設定なら選択を挟み、設定済みなら即 onEnter を実行する（儀式の入口）。</summary>
    public void OpenOrPass(Action onEnter, Action onCancel)
    {
        var nm = NotebookManager.Instance;
        string todayKey = NotebookManager.DateKey(DateTime.Now);
        string cur = nm != null ? nm.GetWeeklyMemo(todayKey) : "";
        if (!string.IsNullOrEmpty(cur) || _panel == null)
        {
            onEnter?.Invoke(); // 設定済み: 黙って引き継ぐ
            return;
        }
        _onEnter = onEnter;
        _onCancel = onCancel;
        _panel.SetActive(true);
        RebuildRows(todayKey);
    }

    private void RebuildRows(string todayKey)
    {
        foreach (var r in _rows) if (r != null) { r.SetActive(false); Destroy(r); } // Destroyは同フレーム残留するため先に非表示化
        _rows.Clear();
        if (_rowTemplate == null || _listContent == null) return;

        foreach (var opt in PolicyOptions.All)
        {
            if (string.IsNullOrEmpty(opt)) continue; // 空(未設定)は選択肢に出さない
            var captured = opt;
            AddRow(captured, ColBody, () =>
            {
                NotebookManager.Instance?.SetWeeklyMemo(todayKey, captured);
                FinishWith(_onEnter);
            });
        }
        AddRow("決めずに入る", ColMuted, () => FinishWith(_onEnter));      // 白紙を赦す
        AddRow("やっぱりやめる", ColMuted, () => FinishWith(_onCancel));   // 着席メニューへ戻る

        // 矢印上下の明示チェーン（動的生成＝レイアウト確定前でも確実に繋がるExplicit方式）
        for (int i = 0; i < _rows.Count; i++)
        {
            var b = _rows[i].GetComponent<Button>();
            if (b == null) continue;
            var nav = b.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? _rows[i - 1].GetComponent<Button>() : null;
            nav.selectOnDown = i < _rows.Count - 1 ? _rows[i + 1].GetComponent<Button>() : null;
            b.navigation = nav;
        }

        // ▶はデフォルトで最上行（キーボード選択と同期・ホバー/矢印で移動）
        _cursorRow = null;
        if (_rows.Count > 0)
        {
            var first = _rows[0].GetComponent<MenuRowHighlight>();
            if (first != null) { first.SetHover(true); _cursorRow = first; }
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_rows[0]); // 矢印キーの起点
        }
    }

    private void AddRow(string label, Color color, Action onClick)
    {
        var row = Instantiate(_rowTemplate, _listContent);
        row.name = "Row_" + label;
        row.SetActive(true);
        var lbl = FindDeep(row.transform, "NameLabel");
        var tmp = lbl != null ? lbl.GetComponent<TMPro.TMP_Text>() : null;
        if (tmp != null) { tmp.text = label; tmp.color = color; }
        var btn = row.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick());
        }
        var rh = row.GetComponent<MenuRowHighlight>();
        if (rh != null)
        {
            rh.stickyCursor = true;              // Exitで消さない（カーソル常駐）
            rh.HoverEntered += OnRowHover;       // ホバーでカーソル移動
        }
        _rows.Add(row);
    }

    private void OnRowHover(MenuRowHighlight rh)
    {
        if (_cursorRow != null && _cursorRow != rh) _cursorRow.SetHover(false);
        _cursorRow = rh;
    }

    private void FinishWith(Action cb)
    {
        _onEnter = null;
        _onCancel = null;
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null); // 選択の残存を掃除
        if (_panel != null) _panel.SetActive(false);
        cb?.Invoke();
    }

    private Transform FindDeep(Transform root, string goName)
    {
        if (root.name == goName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), goName);
            if (r != null) return r;
        }
        return null;
    }

    private void Bind()
    {
        var p = FindDeep(transform, "Panel");
        _panel = p != null ? p.gameObject : null;
        _listContent = FindDeep(transform, "ListContent");
        var rt = FindDeep(transform, "RowTemplate");
        _rowTemplate = rt != null ? rt.gameObject : null;
        if (_rowTemplate != null) _rowTemplate.SetActive(false);
    }
}
