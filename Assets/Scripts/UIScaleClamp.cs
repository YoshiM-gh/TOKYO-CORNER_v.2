using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ウィンドウサイズ対応の統合マネージャ。
/// 1) 通常モード: UIスケール下限クランプ（低DPI環境でのフォント潰れ防止）
/// 2) ミニモード: ウィンドウを一定以下に縮めるとポモドーロタイマーだけを中央表示。
///    タイマーカード全体が必ず収まるスケーリング（Expand）なので潰れ・はみ出しなし。
///    ヒステリシス付き（境目でパタパタしない）。
/// GameObject不要（起動時に自動生成）。
/// </summary>
public class UIScaleClamp : MonoBehaviour
{
    // ── 通常モード：スケール下限（フォント物理pxが設計の80%を下回らない）──
    private const float MIN_SCALE = 0.8f;

    // ── モード境界（物理px・ヒステリシス）──
    private const int FULL_W = 1536;          // これ以上でフルUI復帰（=1920×0.8）
    private const int FULL_H = 864;           // （=1080×0.8）
    private const int ENTER_COMPACT_W = 1500; // これ未満でミニモードへ
    private const int ENTER_COMPACT_H = 840;

    // ── ミニモード ──
    private const int COMPACT_MIN_W = 400;    // ミニモードの最小ウィンドウ（物理px）
    private const int COMPACT_MIN_H = 520;
    private static readonly Vector2 CompactRef = new Vector2(560f, 1000f); // カード460×886＋余白
    private const float RESIZE_DEBOUNCE = 0.35f;

    private readonly Dictionary<CanvasScaler, (float match, Vector2 refRes)> _orig = new();
    private Vector2Int _lastSize;
    private float _minSizeEnforceAt = -1f;
    private bool _compact;

    // ミニモード対象のキャッシュ
    private CanvasScaler _mainScaler;   // FocusUIを含むメインCanvas
    private RectTransform _card;        // ポモドーロタイマーカード
    private GameObject _notebook;       // カレンダー/Todo等のパネル
    private Camera[] _overlayCams;      // AlwaysOnTop のキャラ表示カメラ
    private GameObject _compactBG;      // ミニモード用の不透明背景（3Dの見える穴を覆う）
    private bool _cardSaved;
    private Vector2 _cardAnchMin, _cardAnchMax, _cardPivot, _cardPos, _cardSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("[UIScaleClamp]");
        DontDestroyOnLoad(go);
        go.AddComponent<UIScaleClamp>();
    }

private void Start()
    {
        Refresh();
        Debug.Log($"[UIScaleClamp] init screen={Screen.width}x{Screen.height} dpi={Screen.dpi} " +
                  $"compact={_compact} main={_mainScaler != null} card={_card != null} " +
                  $"notebook={_notebook != null} cams={(_overlayCams != null ? _overlayCams.Length : 0)}");
    }

private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>シーン切替（オープニング→メインなど）でUIが入れ替わるため、
    /// キャッシュを破棄して再探索・再適用する。これがないと起動時の
    /// オープニングシーンしか見えず、ミニモードが機能しない。</summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        _mainScaler  = null;
        _card        = null;
        _notebook    = null;
        _overlayCams = null;
        _cardSaved   = false;
        Debug.Log($"[UIScaleClamp] sceneLoaded '{scene.name}' → キャッシュ再構築");
        Refresh();
    }


private void Update()
    {
        if (Screen.width != _lastSize.x || Screen.height != _lastSize.y)
        {
            Refresh();
            if (!Screen.fullScreen && (Screen.width < COMPACT_MIN_W || Screen.height < COMPACT_MIN_H))
                _minSizeEnforceAt = Time.unscaledTime + RESIZE_DEBOUNCE;
        }
        if (_minSizeEnforceAt > 0f && Time.unscaledTime >= _minSizeEnforceAt)
        {
            _minSizeEnforceAt = -1f;
            if (!Screen.fullScreen && (Screen.width < COMPACT_MIN_W || Screen.height < COMPACT_MIN_H))
                Screen.SetResolution(Mathf.Max(Screen.width, COMPACT_MIN_W),
                                     Mathf.Max(Screen.height, COMPACT_MIN_H),
                                     FullScreenMode.Windowed);
        }

        // ミニモード中は、アプリ側のモード遷移が再表示しても毎フレーム抑え込む
        if (_compact) EnforceCompact();
    }

    private void Refresh()
    {
        _lastSize = new Vector2Int(Screen.width, Screen.height);
        RegisterScalers();

        // ヒステリシス付きモード判定
        bool enter = Screen.width < ENTER_COMPACT_W || Screen.height < ENTER_COMPACT_H;
        bool exit  = Screen.width >= FULL_W && Screen.height >= FULL_H;
        if (!_compact && enter)      SetCompact(true);
        else if (_compact && exit)   SetCompact(false);

        ApplyScalers();
    }

    private void RegisterScalers()
    {
        foreach (var cs in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cs == null || _orig.ContainsKey(cs)) continue;
            if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            _orig[cs] = (cs.matchWidthOrHeight, cs.referenceResolution);
            if (_mainScaler == null && cs.transform.Find("FocusUI") != null)
                _mainScaler = cs;
        }
    }

    private void ApplyScalers()
    {
        foreach (var kv in _orig)
        {
            var cs = kv.Key;
            if (cs == null) continue;

            // ミニモード中のメインCanvas: タイマーカードが必ず収まるExpandスケーリング
            if (_compact && cs == _mainScaler)
            {
                cs.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = CompactRef;
                cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
                continue;
            }

            // 通常ロジック: 比例スケール＋下限クランプ
            var o = kv.Value;
            float logW  = Mathf.Log(Screen.width  / o.refRes.x, 2f);
            float logH  = Mathf.Log(Screen.height / o.refRes.y, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, o.match));

            if (scale < MIN_SCALE)
            {
                cs.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                cs.scaleFactor = MIN_SCALE;
            }
            else
            {
                cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = o.refRes;
                cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                cs.matchWidthOrHeight  = o.match;
            }
        }
    }

    private void SetCompact(bool on)
    {
        _compact = on;
        CacheFocusObjects();
        Debug.Log($"[UIScaleClamp] compact={on} screen={Screen.width}x{Screen.height} " +
                  $"main={_mainScaler != null} card={_card != null} notebook={_notebook != null} " +
                  $"cams={(_overlayCams != null ? _overlayCams.Length : 0)}");


        // カレンダー等を隠す / 戻す
        if (_notebook != null) _notebook.SetActive(!on);

        // タイマーカードを中央固定 / 元に戻す
        if (_card != null)
        {
            if (on)
            {
                if (!_cardSaved)
                {
                    _cardSaved   = true;
                    _cardAnchMin = _card.anchorMin; _cardAnchMax = _card.anchorMax;
                    _cardPivot   = _card.pivot;
                    _cardPos     = _card.anchoredPosition;
                    _cardSize    = _card.sizeDelta;
                }
                _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
                _card.sizeDelta = new Vector2(460f, 886f);
                _card.anchoredPosition = Vector2.zero;
            }
            else if (_cardSaved)
            {
                _card.anchorMin = _cardAnchMin; _card.anchorMax = _cardAnchMax;
                _card.pivot     = _cardPivot;
                _card.anchoredPosition = _cardPos;
                _card.sizeDelta = _cardSize;
            }
        }

        // キャラ表示カメラ（AlwaysOnTop）はミニモードでは切る
        if (_overlayCams != null)
            foreach (var c in _overlayCams)
                if (c != null) c.enabled = !on;

        // 3Dの見える穴を覆う不透明背景
        EnsureCompactBG(on);
    }

/// <summary>ミニモードの状態を毎フレーム強制する。
    /// アプリ本体のモード遷移（フォーカス入座など）が NotebookPanel や
    /// キャラ表示カメラを再有効化するため、切替時の1回だけでは不十分。</summary>
    private void EnforceCompact()
    {
        if (_notebook == null || _card == null || _overlayCams == null) CacheFocusObjects();

        if (_notebook != null && _notebook.activeSelf) _notebook.SetActive(false);

        if (_overlayCams != null)
            foreach (var c in _overlayCams)
                if (c != null && c.enabled) c.enabled = false;

        // 不透明背景の維持（アプリ側の遷移で消されても復元）
        EnsureCompactBG(true);

        // カードの中央固定も再確認（アプリ側が位置を戻した場合に再適用）
        var center = new Vector2(0.5f, 0.5f);
        if (_card != null && (_card.anchorMin != center || _card.anchorMax != center))
        {
            if (!_cardSaved)
            {
                _cardSaved   = true;
                _cardAnchMin = _card.anchorMin; _cardAnchMax = _card.anchorMax;
                _cardPivot   = _card.pivot;
                _cardPos     = _card.anchoredPosition;
                _cardSize    = _card.sizeDelta;
            }
            _card.anchorMin = _card.anchorMax = _card.pivot = center;
            _card.sizeDelta = new Vector2(460f, 886f);
            _card.anchoredPosition = Vector2.zero;
        }
    }

/// <summary>ミニモード用の不透明背景をCardの背面に用意する。
    /// キャラ表示は専用カメラではなくUIの「穴」から3D世界が見えている構造のため、
    /// 全面を覆う板で隠す（背後のUIへのクリックも遮断）。</summary>
    private void EnsureCompactBG(bool on)
    {
        if (_card == null) return;
        var fp = _card.parent;
        if (fp == null) return;

        if (_compactBG == null)
        {
            var existing = fp.Find("[CompactBG]");
            _compactBG = existing != null ? existing.gameObject : null;
        }
        if (_compactBG == null && on)
        {
            _compactBG = new GameObject("[CompactBG]", typeof(RectTransform));
            _compactBG.transform.SetParent(fp, false);
            var rt = (RectTransform)_compactBG.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-2000f, -2000f);
            rt.offsetMax = new Vector2( 2000f,  2000f);
            var img = _compactBG.AddComponent<UnityEngine.UI.Image>();
            var bg  = new Color(0.09f, 0.11f, 0.15f, 1f);
            var fsbg = fp.parent != null ? fp.parent.Find("FullscreenBG") : null;
            if (fsbg != null)
            {
                var fi = fsbg.GetComponent<UnityEngine.UI.Image>();
                if (fi != null) bg = new Color(fi.color.r, fi.color.g, fi.color.b, 1f);
            }
            img.color = bg;
            img.raycastTarget = true;
            _compactBG.transform.SetSiblingIndex(Mathf.Max(0, _card.GetSiblingIndex())); // Cardの直前（背面）
        }
        if (_compactBG != null && _compactBG.activeSelf != on)
            _compactBG.SetActive(on);
    }



private void CacheFocusObjects()
    {
        // NotebookPanel / Card / メインCanvasを「型ベース」で確実に取得する。
        // WeeklyCalendarUI は NotebookPanel 配下に必ず存在する（非アクティブでも探索可）。
        if (_notebook == null || _card == null || _mainScaler == null)
        {
            var weekly = FindFirstObjectByType<WeeklyCalendarUI>(FindObjectsInactive.Include);
            if (weekly != null)
            {
                var panel = weekly.transform;
                while (panel != null && panel.name != "NotebookPanel") panel = panel.parent;
                if (panel != null)
                {
                    _notebook = panel.gameObject;
                    var fp = panel.parent; // FocusPanel
                    if (fp != null) _card = fp.Find("Card") as RectTransform;
                }
                if (_mainScaler == null)
                    _mainScaler = weekly.GetComponentInParent<CanvasScaler>(true);
            }
        }
        if (_overlayCams == null)
        {
            var top = GameObject.Find("AlwaysOnTop");
            if (top != null) _overlayCams = top.GetComponentsInChildren<Camera>(true);
        }
    }
}
