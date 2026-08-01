using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 表示モード管理 v2（仕様§10・実測ベース）。対象は UI_Prototype シーン。
///   Bar / Timer / Notebook(タブ指定) / Full（TCアイコン=ALL）
/// - 旧ナビ（Window Manager/Buttons 75px行・BackToCafe）はトルツメし、バーに一本化
/// - タブ切替は Michsky WindowManager.OpenWindow() を直接使用
/// - Timer/Notebook/Bar ではキャラ描画カメラ停止（負荷源）
/// - タイマー計測を止めないため、非表示は CanvasGroup（SetActiveしない）
/// 実測値: TimerCard 364×726 / CharacterCard 364×306 / Windows 1508×973(+旧ナビ75)
/// </summary>
public class AppModeManager : MonoBehaviour
{
    public enum Mode { Bar = 0, Timer = 1, Notebook = 2, Full = 3 }

    /// <summary>他所からタブ切替を頼むための入口（カレンダーの日付セルなど）。</summary>
    public static AppModeManager Instance { get; private set; }

    /// <summary>Bar / Timer か（数字と小さなUIしか動かない省エネ向きの状態）。fps制御が参照する。</summary>
    public bool IsCompactMode => _mode == Mode.Bar || _mode == Mode.Timer;

    // ── 定数（実測ベース） ──
    private const float MIN_SCALE  = 0.8f;
    private const int   FULL_MIN_W = 1536, FULL_MIN_H = 864;
    private const int   NB_MIN_W   = 1000, NB_MIN_H   = 700;
    private const int   TIMER_MIN_W = 320, TIMER_MIN_H = 640;
    private const float BAR_UNITS_W = 600f;
    private static readonly Vector2 TimerRef = new Vector2(396f, 806f); // カード364×726+余白+バー48
    private const float BAR_H = 48f;
    private const float RESIZE_DEBOUNCE = 0.4f;
    private const string SIZE_PREFS = "tc_size4"; // 旧初期値の汚染リセット

    private Mode   _mode = Mode.Full;
    private string _tab  = "Monthly";

    // ── UI_Prototype キャッシュ ──
    private readonly Dictionary<CanvasScaler,(float match, Vector2 refRes)> _orig = new();
    private CanvasScaler  _mainScaler;
    private Canvas        _mainCanvas;
    private RectTransform _wmArea;
    private CanvasGroup   _wmAreaGroup;
    private GameObject    _wmButtons;      // 旧ナビ（トルツメ対象）
    private RectTransform _wmWindows;
    private GameObject    _backToCafe;     // 旧「お店に戻る」（トルツメ対象）
    private RectTransform _leftCol;
    private CanvasGroup   _leftColGroup;
    private RectTransform _timerCard;
    private CanvasGroup   _charGroup;
    private Camera        _charCam;
    private bool _origSaved;
    private Vector2 _wmAreaOffMin, _wmAreaOffMax;
    private Vector2 _leftColAnchMin, _leftColAnchMax, _leftColPos;
    private Vector2 _leftColOffMin, _leftColOffMax;
    private Vector2 _cardAnchMin, _cardAnchMax, _cardPivot, _cardPos, _cardSize;
    private Vector2 _windowsOffMax;

    // ── バーUI ──
    private Canvas _barCanvas;
    private TextMeshProUGUI _barTimerLabel;

    private readonly Dictionary<string, Button> _barButtons = new();
    private readonly Dictionary<string, TextMeshProUGUI> _barLabels = new();
    private GameObject _navGroup;             // バーのナビ群（Bar時のみ表示）
    private RectTransform _barRootRT;         // バー本体（Bar時は角丸フローティング形状）
    private GameObject _barHairline;          // 通常モード時の下端ヘアライン
    private Image      _barBaseImg;           // バーの地（Bar時のみ角丸ピル、通常時は直角）
    private GameObject _winMinGO, _winCloseGO, _winCtlSpacer; // タイトルバーが無い場面用の −/×
    private GameObject _barBackdrop;          // 背面ベース
    private Image[] _navChipImgs;             // ナビ2チップの背景（Bar時のみ薄く表示）
    private Camera _bgCam;                    // 透過用：背景クリアを差し替えるカメラ
    private CameraClearFlags _bgCamFlags;
    private Color _bgCamColor;
    private bool _bgCamSaved;
    private Image _barProgress;               // 下端のポモドーロ進捗ライン
    private GameObject _logoBlock;


    private TextMeshProUGUI _barCycleLabel;   // バーの 1/4 表示
    private TextMeshProUGUI _barPlayLabel;    // バーの再生/一時停止グリフ
    private static PomodoroManager PM => PomodoroManager.Instance; // 計測の実体（シーン非依存）
    private float _nextSync;

    private Vector2Int _lastSize;
    private float _enforceAt = -1f;
    private float _reapplyAt = -1f;          // モード切替後の再適用（SetResolution非同期対策）
    private float _nextFontSweep;            // ツール画面のフォント統一スイープ
    private bool _barDragging;               // Barモードのウィンドウドラッグ移動
    private Vector2 _dragMouse0, _dragOrigin0;
    private CanvasScaler _barScaler;

    private static readonly Color BarBG       = new Color(0.078f, 0.094f, 0.125f, 1f);
    private static readonly Color LabelIdle   = new Color(0.62f, 0.67f, 0.74f, 1f);
    private static readonly Color LabelActive = Color.white;
    private static readonly Color TimerWork   = new Color(0.06f, 0.59f, 0.99f, 1f); // 作業中（リングと同色）
    private static readonly Color TimerBreak  = new Color(0.18f, 0.80f, 0.44f, 1f); // 休憩中（リングと同色）
    private static readonly Color ToolIdle = new Color(0.78f, 0.72f, 0.58f, 1f); // リスト系ナビ（TD R M）のウォーム待機色

    // ナビ定義：同じ要素をモードで表示方法だけ変える（通常=フル表記／Bar=短縮形）
    private static readonly (string key, string full, float fullW, string mini, float miniW)[] NavDefs =
    {
        ("Monthly", "Monthly", 74f, "M",  26f),
        ("Weekly",  "Weekly",  66f, "W",  26f),
        ("Daily",   "Daily",   52f, "D",  26f),
        ("Todo",    "Todo",    52f, "TD", 30f),
        ("Routine", "Routine", 68f, "R",  26f),
        ("Memo",    "Memo",    58f, "M",  26f),
    };

    private static ColorBlock IdleBlock()
    {
        var cb = ColorBlock.defaultColorBlock;
        cb.normalColor      = new Color(1f, 1f, 1f, 0.05f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.14f);
        cb.pressedColor     = new Color(0.31f, 0.55f, 0.95f, 0.50f);
        cb.selectedColor    = cb.normalColor;
        cb.colorMultiplier  = 1f; cb.fadeDuration = 0.08f;
        return cb;
    }
    private static ColorBlock ActiveBlock()
    {
        var cb = ColorBlock.defaultColorBlock;
        cb.normalColor      = new Color(0.31f, 0.55f, 0.95f, 0.42f);
        cb.highlightedColor = new Color(0.31f, 0.55f, 0.95f, 0.55f);
        cb.pressedColor     = new Color(0.31f, 0.55f, 0.95f, 0.70f);
        cb.selectedColor    = cb.normalColor;
        cb.colorMultiplier  = 1f; cb.fadeDuration = 0.08f;
        return cb;
    }

    private static ColorBlock BarIdleBlock()
    {
        var cb = ColorBlock.defaultColorBlock;
        cb.normalColor      = Color.clear;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        cb.pressedColor     = new Color(1f, 1f, 1f, 0.16f);
        cb.selectedColor    = Color.clear;
        cb.colorMultiplier  = 1f; cb.fadeDuration = 0.08f;
        return cb;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("[AppModeManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<AppModeManager>();
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Start()
    {
        Instance = this;
        _mode = (Mode)PlayerPrefs.GetInt("tc_mode", (int)Mode.Full);
        _tab  = PlayerPrefs.GetString("tc_tab", "Monthly");
        BuildBar();
        ApplyAll();
        Debug.Log($"[AppModeManager] init mode={_mode} screen={Screen.width}x{Screen.height} dpi={Screen.dpi}");
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _orig.Clear();
        _mainScaler = null; _mainCanvas = null; _wmArea = null; _wmAreaGroup = null;
        _wmButtons = null; _wmWindows = null; _backToCafe = null;
        _leftCol = null; _leftColGroup = null; _timerCard = null;
        _charGroup = null; _charCam = null;

        _bgCam = null; _bgCamSaved = false;
        _origSaved = false; _appliedTab = null;
        ApplyAll();
        if (s.name == SceneRouter.FocusScene) RestoreWindowSize(_mode);
        ApplyWindowAspect();
        Debug.Log($"[AppModeManager] sceneLoaded '{s.name}' → 再適用 (mode={_mode})");
    }

    private void OnApplicationQuit() => SaveWindowSize(_mode);

    private bool InFocusScene => _wmArea != null;

    // ── モード切替 ─────────────────────────────────────────
public void RequestMode(Mode target, string tab = null)
    {
        // タブ＝「表示内容の選択」。モードは変えずに中身だけ切り替える：
        //  - Full中に別タブ → Fullのままウィンドウ切替（ポモドーロは消えない）
        //  - Notebook中に別タブ → Notebookのまま切替
        //  - 同一タブ再クリック → 段階的に畳む（Full→Notebook単独→Bar）
        //  - Timer/Barからのタブ → Notebook単独で開く
        if (target == Mode.Notebook && tab != null && tab != _tab &&
            (_mode == Mode.Full || _mode == Mode.Notebook))
        {
            _tab = tab; _appliedTab = null;
            PlayerPrefs.SetString("tc_tab", tab);
            SwitchWindow(tab);
            UpdateBarHighlight();
            return; // モード維持
        }

        bool tabChange = target == Mode.Notebook && tab != null && (_mode != Mode.Notebook || tab != _tab);
        if (tab != null) { _tab = tab; _appliedTab = null; PlayerPrefs.SetString("tc_tab", tab); }

        Mode next;
        if (tabChange) next = Mode.Notebook;
        else next = (_mode == target && target != Mode.Bar) ? Mode.Bar : target; // 同一ビュー再クリック→Bar
        if (next == _mode && !tabChange) { UpdateBarHighlight(); return; }

        SaveWindowSize(_mode);
        _mode = next;
        PlayerPrefs.SetInt("tc_mode", (int)_mode);
        ApplyScalers();
        BeginWindowResize(_mode); // 引き伸ばし対策：画面を伏せてから次フレームでリサイズ
        ApplyAll();
        _reapplyAt = Time.unscaledTime + 0.7f;
        Debug.Log($"[AppModeManager] mode → {_mode} (tab={_tab})");
    }

    private void RequestShopReturn()
    {
        if (SceneRouter.Instance != null) SceneRouter.Instance.ExitFocus();
    }

/// <summary>
    /// バーのタイマー操作（0=R, 1=再生/一時停止, 2=✓）。
    /// Phase 3以降は PomodoroManager を直接叩く（移行前はTimerCardの実ボタンのonClickを
    /// Invokeしていたため、カードが無い場面では機能しなかった）。分岐はカードと同一。
    /// </summary>
    private void InvokeTimerControl(int idx)
    {
        var m = PM;
        if (m == null) return;
        bool stopped = m.Phase == TimerController.TimerPhase.Stopped;
        switch (idx)
        {
            case 0:
                m.PreparePomodoro();
                break;
            case 1:
                if (stopped) m.StartWithSettings();
                else         m.TogglePause();
                break;
            case 2:
                if (stopped)                    m.StartWithSettings();
                else if (m.IsAwaitingNextPhase) m.AdvanceToNextPhase();
                else                            m.ForceAdvancePhase();
                break;
        }
    }

/// <summary>Barモードの透過：背景カメラのクリアをα=0にして角丸カードの外を突き抜く。
    /// 通常モードに戻ったら元のクリア設定を復元する。</summary>
    private void ApplyBarTransparency()
    {
        bool on = _mode == Mode.Bar && InFocusScene;
        if (on && _bgCam == null)
        {
            foreach (var cam in Camera.allCameras)
                if (cam != null && cam.targetTexture == null) { _bgCam = cam; break; }
        }
        if (_bgCam == null) return;
        if (on)
        {
            if (!_bgCamSaved) { _bgCamFlags = _bgCam.clearFlags; _bgCamColor = _bgCam.backgroundColor; _bgCamSaved = true; }
            var c = new Color(0f, 0f, 0f, 0f);
            if (_bgCam.clearFlags != CameraClearFlags.SolidColor) _bgCam.clearFlags = CameraClearFlags.SolidColor;
            if (_bgCam.backgroundColor != c) _bgCam.backgroundColor = c;
        }
        else if (_bgCamSaved)
        {
            _bgCam.clearFlags = _bgCamFlags;
            _bgCam.backgroundColor = _bgCamColor;
            _bgCamSaved = false;
        }
    }



    // ── 毎フレーム ─────────────────────────────────────────
private void Update()
    {
        TickWindowResize(); // 伏せる→リサイズ→戻す の進行（引き伸ばし対策）

        if (Screen.width != _lastSize.x || Screen.height != _lastSize.y)
        {
            _lastSize = new Vector2Int(Screen.width, Screen.height);
            ApplyScalers();
            _enforceAt = Time.unscaledTime + RESIZE_DEBOUNCE;
        }
        if (_enforceAt > 0f && Time.unscaledTime >= _enforceAt)
        {
            _enforceAt = -1f;
            EnforceMinSize();
        }
        if (_reapplyAt > 0f && Time.unscaledTime >= _reapplyAt)
        {
            _reapplyAt = -1f;
            ApplyAll();
        }
        EnforceLayout();
        SyncBar();
        SweepToolFonts();

        // Barモード: タイトルバー非表示＋バーのドラッグでウィンドウ移動
        // 枠なしの適用範囲はOSで異なる（WindowUtil.BorderlessAllModes）:
        //   Windows = フォーカス画面の全モード（通常モードはリサイズ枠だけ残す）
        //   macOS   = Barモードのみ（信号機ボタンを使うため）
        bool wantBorderless = InFocusScene && (_mode == Mode.Bar || WindowUtil.BorderlessAllModes);
        WindowUtil.TrySetBorderless(wantBorderless, _mode != Mode.Bar);
        // ウィンドウの可視領域をクライアント＋角丸に切り抜く。
        // Barは仕様書の角丸12に合わせ、通常モードは控えめに10px。
        if (wantBorderless)
            WindowUtil.TryClipToClient(_mode == Mode.Bar ? Mathf.RoundToInt(12f * BarScale()) : 10);
        else
            WindowUtil.TryClearClip();
        WindowUtil.TrySetFloating(_mode == Mode.Bar && InFocusScene); // 常に最前面（タスクバーヒーロー）
        // ※ウィンドウ透過はUnityのMetal層が非対応だったため断念→フルブリード角丸で対応
        if (wantBorderless)
        {
            // 縁を掴んだらOSのリサイズへ、そうでなければバーのドラッグ移動へ
            if (Input.GetMouseButtonDown(0) && TryBeginEdgeResize()) _barDragging = false;
            else HandleBarDrag(); // 枠なし中はバーがタイトルバーの代わり
        }
        else _barDragging = false;
    }

    /// <summary>Barモード（枠なし）中はバーのどこを掴んでもウィンドウを移動できる。</summary>
    private const float RESIZE_MARGIN = 6f; // クライアント内側の掴み代（OSの枠は切り抜きで消えている）

    /// <summary>
    /// ウィンドウの縁（内側6px）を掴んだらOS標準のリサイズを開始する。
    /// Barモードは固定サイズなので対象外。Macでは TryBeginResize が false を返すので何も起きない。
    /// </summary>
    private bool TryBeginEdgeResize()
    {
        if (_mode == Mode.Bar) return false;
        var mp = Input.mousePosition;
        int dx = mp.x <= RESIZE_MARGIN ? -1 : mp.x >= Screen.width  - RESIZE_MARGIN ? 1 : 0;
        int dy = mp.y <= RESIZE_MARGIN ? -1 : mp.y >= Screen.height - RESIZE_MARGIN ? 1 : 0;
        if (dx == 0 && dy == 0) return false;
        return WindowUtil.TryBeginResize(dx, dy);
    }

    private readonly List<RaycastResult> _dragHitBuf = new List<RaycastResult>();

    /// <summary>
    /// バーを掴んでのウィンドウ移動を開始してよいか。枠なし運用ではバーがタイトルバーの代役なので
    /// ①ポインタがバーの帯の中 ②ボタン等の操作要素の上ではない、の2条件で判定する。
    /// （Barモードはウィンドウ全体がバーなので①は常に成立する）
    /// </summary>
    private bool CanStartBarDrag()
    {
        if (Input.mousePosition.y < Screen.height - BarPixelHeight()) return false;
        var es = EventSystem.current;
        if (es == null) return true;
        var ped = new PointerEventData(es) { position = Input.mousePosition };
        _dragHitBuf.Clear();
        es.RaycastAll(ped, _dragHitBuf);
        foreach (var hit in _dragHitBuf)
        {
            if (hit.gameObject == null) continue;
            if (hit.gameObject.GetComponentInParent<Selectable>() != null) return false;
        }
        return true;
    }

    private void HandleBarDrag()
    {
        if (Application.isEditor) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (!CanStartBarDrag()) return; // バーの帯の外／ボタンの上では掴まない
            _barDragging = true;
            _dragMouse0  = WindowUtil.GlobalMouse();
            _dragOrigin0 = WindowUtil.GetWindowOrigin();
        }
        else if (!Input.GetMouseButton(0))
        {
            _barDragging = false;
        }
        else if (_barDragging)
        {
            var d = WindowUtil.GlobalMouse() - _dragMouse0;
            if (d.sqrMagnitude > 9f)
                WindowUtil.SetWindowOrigin(_dragOrigin0 + d);
        }
    }

    private void ApplyAll()
    {
        _lastSize = new Vector2Int(Screen.width, Screen.height);
        CacheObjects();
        ApplyScalers();
        EnforceLayout();
        UpdateBarHighlight();
    }

    // ── キャッシュ ─────────────────────────────────────────
    private void CacheObjects()
    {
        if (_wmArea == null)
        {
            var areaGO = GameObject.Find("Canvas/WindowManagerArea");
            if (areaGO != null)
            {
                _wmArea      = areaGO.transform as RectTransform;
                _wmAreaGroup = areaGO.GetComponent<CanvasGroup>();
                if (_wmAreaGroup == null) _wmAreaGroup = areaGO.AddComponent<CanvasGroup>(); // Unityのfake null対策で??は使わない
                var wmT      = areaGO.transform.Find("Window Manager");
                if (wmT != null)
                {
                    var b = wmT.Find("Buttons");   _wmButtons = b != null ? b.gameObject : null;
                    _wmWindows = wmT.Find("Windows") as RectTransform;
                }
                var back = areaGO.transform.Find("BackToCafe");
                _backToCafe = back != null ? back.gameObject : null;

                _mainScaler = areaGO.GetComponentInParent<CanvasScaler>(true);
                _mainCanvas = areaGO.GetComponentInParent<Canvas>(true);
            }
        }
        if (_leftCol == null)
        {
            var colGO = GameObject.Find("Canvas/LeftColumn");
            if (colGO != null)
            {
                _leftCol      = colGO.transform as RectTransform;
                _leftColGroup = colGO.GetComponent<CanvasGroup>();
                if (_leftColGroup == null) _leftColGroup = colGO.AddComponent<CanvasGroup>();
                _timerCard    = colGO.transform.Find("TimerCard") as RectTransform;
                var charCard  = colGO.transform.Find("CharacterCard");
                if (charCard != null)
                {
                    _charGroup = charCard.GetComponent<CanvasGroup>();
                    if (_charGroup == null) _charGroup = charCard.gameObject.AddComponent<CanvasGroup>();
                    // キャラ描画カメラ: CharacterRender の RawImage.texture を描くカメラ
                    var raw = charCard.GetComponentInChildren<RawImage>(true);
                    if (raw != null && raw.texture is RenderTexture rtex)
                        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (cam.targetTexture == rtex) { _charCam = cam; break; }
                }

            }
        }
        // 元レイアウトの保存（1回だけ）
        if (!_origSaved && _wmArea != null && _leftCol != null && _timerCard != null && _wmWindows != null)
        {
            _origSaved     = true;
            _wmAreaOffMin  = _wmArea.offsetMin;  _wmAreaOffMax = _wmArea.offsetMax;
            _leftColAnchMin = _leftCol.anchorMin; _leftColAnchMax = _leftCol.anchorMax; _leftColPos = _leftCol.anchoredPosition;
            _leftColOffMin = _leftCol.offsetMin; _leftColOffMax = _leftCol.offsetMax;
            _cardAnchMin = _timerCard.anchorMin; _cardAnchMax = _timerCard.anchorMax;
            _cardPivot = _timerCard.pivot; _cardPos = _timerCard.anchoredPosition; _cardSize = _timerCard.sizeDelta;
            _windowsOffMax = _wmWindows.offsetMax;
        }
    }

    // ── CanvasScaler ──────────────────────────────────────
    private void ApplyScalers()
    {
        foreach (var cs in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cs == null || _orig.ContainsKey(cs)) continue;
            if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            if (cs.GetComponentInParent<AppModeManager>() != null) continue;
            _orig[cs] = (cs.matchWidthOrHeight, cs.referenceResolution);
        }
        foreach (var kv in _orig)
        {
            var cs = kv.Key;
            if (cs == null) continue;
            // フォーカス画面のメインCanvasは「ゲーム方式」：モード別の固定基準解像度を
            // Expandで等倍拡縮（ウィンドウは同アスペクトに拘束するので常に一枚絵の拡縮になる）
            if (cs == _mainScaler && InFocusScene)
            {
                cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
                switch (_mode)
                {
                    case Mode.Timer:    cs.referenceResolution = TimerRef;                  break;
                    case Mode.Notebook: cs.referenceResolution = new Vector2(1540f, 1080f); break;
                    default:            cs.referenceResolution = new Vector2(1920f, 1080f); break;
                }
                continue;
            }
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

    // ── ウィンドウリサイズ時の『引き伸ばし』対策 ──────────────
    // OSはウィンドウのリサイズ中、アプリが新しいフレームを描き終えるまで
    // 直前のフレームを新しいサイズへ引き伸ばして表示する。
    // Barモード（高さ48px）の絵が縦に伸びると、バーの文字ごと間延びして見えていた。
    // → リサイズの直前に画面を伏せて『無地のフレーム』を1枚描いてからサイズを変える。
    //   無地なら引き伸ばされても分からないので、切替が瞬時に見える。
    private Mode _resizeMode;
    private int  _resizeStep = -1; // -1=何もしない / 0=伏せた / 1=リサイズ済み

    private void BeginWindowResize(Mode m)
    {
        _resizeMode = m;
        _resizeStep = 0;
        if (_barCanvas != null)  _barCanvas.enabled = false;
        if (_mainCanvas != null) _mainCanvas.enabled = false;
    }

    private void TickWindowResize()
    {
        if (_resizeStep < 0) return;

        if (_resizeStep == 0)
        {
            // 無地のフレームが1枚描かれた後にサイズを変える
            RestoreWindowSize(_resizeMode);
            ApplyWindowAspect();
            _resizeStep = 1;
            return;
        }

        // 新しいサイズで描き直せる状態になったので戻す
        if (_barCanvas != null)  _barCanvas.enabled = true;
        if (_mainCanvas != null) _mainCanvas.enabled = true;
        _resizeStep = -1;
        ApplyAll();
    }

    // ── レイアウト強制（毎フレーム・変更時のみ書き込み）──────
    private void SetGroup(CanvasGroup g, bool on)
    {
        if (g == null) return;
        float a = on ? 1f : 0f;
        if (!Mathf.Approximately(g.alpha, a))
        {
            g.alpha = a; g.interactable = on; g.blocksRaycasts = on;
        }
    }

private void EnforceLayout()
    {
        CacheObjects();
        bool has = InFocusScene;
        if (_barCanvas != null && _barCanvas.gameObject.activeSelf != has)
            _barCanvas.gameObject.SetActive(has);
        if (!has || !_origSaved) return;

        // Barモード：メインCanvasは描画停止（負荷削減）。タイマーの計測は継続
        bool renderMain = _mode != Mode.Bar;
        if (_mainCanvas != null && _mainCanvas.enabled != renderMain) _mainCanvas.enabled = renderMain;

        // ── 旧ナビ（Michsky Buttons行）は全モードでトルツメ：ナビはヘッダーバーに常設一本化 ──
        if (_wmButtons != null && _wmButtons.activeSelf) _wmButtons.SetActive(false);
        if (_backToCafe != null && _backToCafe.activeSelf) _backToCafe.SetActive(false);
        if (_wmWindows != null && !Mathf.Approximately(_wmWindows.offsetMax.y, 0f))
            _wmWindows.offsetMax = new Vector2(_wmWindows.offsetMax.x, 0f); // Buttons行の75pxを回収

        bool showWM   = _mode == Mode.Full || _mode == Mode.Notebook;
        bool showLeft = _mode == Mode.Full || _mode == Mode.Timer;
        bool showChar = _mode == Mode.Full;

        SetGroup(_wmAreaGroup, showWM);
        SetGroup(_leftColGroup, showLeft);
        SetGroup(_charGroup, showChar);
        if (_charCam != null && _charCam.enabled != showChar) _charCam.enabled = showChar;

        // バーの高さぶん上に余白（バーとの重なり防止）
        float canvasScale = _mainCanvas != null ? _mainCanvas.scaleFactor : 1f;
        float inset = BAR_H * BarScale() / Mathf.Max(canvasScale, 0.01f);

        // WindowManagerArea: Full=元位置 / Notebook=左に詰める
        if (_wmArea != null)
        {
            float left = _mode == Mode.Notebook ? 16f : _wmAreaOffMin.x;
            var wantMin = new Vector2(left, _wmAreaOffMin.y);
            var wantMax = new Vector2(_wmAreaOffMax.x, _wmAreaOffMax.y - inset);
            if (_wmArea.offsetMin != wantMin) _wmArea.offsetMin = wantMin;
            if (_wmArea.offsetMax != wantMax) _wmArea.offsetMax = wantMax;
        }

        // LeftColumn / TimerCard: Timer=中央固定 / それ以外=元位置
        if (_leftCol != null && _timerCard != null)
        {
            if (_mode == Mode.Timer)
            {
                var cx = new Vector2(0.5f, _leftColAnchMin.y);
                var cx2 = new Vector2(0.5f, _leftColAnchMax.y);
                if (_leftCol.anchorMin != cx)  _leftCol.anchorMin = cx;
                if (_leftCol.anchorMax != cx2) _leftCol.anchorMax = cx2;
                if (_leftCol.anchoredPosition != Vector2.zero) _leftCol.anchoredPosition = Vector2.zero;

                var center = new Vector2(0.5f, 0.5f);
                if (_timerCard.anchorMin != center) _timerCard.anchorMin = center;
                if (_timerCard.anchorMax != center) _timerCard.anchorMax = center;
                if (_timerCard.pivot != center)     _timerCard.pivot = center;
                var wantSize = new Vector2(364f, 726f);
                if (_timerCard.sizeDelta != wantSize) _timerCard.sizeDelta = wantSize;
                var wantPos = new Vector2(0f, -inset * 0.5f);
                if ((_timerCard.anchoredPosition - wantPos).sqrMagnitude > 0.25f)
                    _timerCard.anchoredPosition = wantPos;
            }
            else
            {
                if (_leftCol.anchorMin != _leftColAnchMin) _leftCol.anchorMin = _leftColAnchMin;
                if (_leftCol.anchorMax != _leftColAnchMax) _leftCol.anchorMax = _leftColAnchMax;
                // バー高さぶん上端を詰める：カード上部の「ポモドーロタイマー」タイトルが
                // ヘッダーに隠れて見切れる問題の修正（wmAreaと同じインセットを適用）
                var wantColMin = _leftColOffMin;
                var wantColMax = new Vector2(_leftColOffMax.x, _leftColOffMax.y - inset);
                if (_leftCol.offsetMin != wantColMin) _leftCol.offsetMin = wantColMin;
                if (_leftCol.offsetMax != wantColMax) _leftCol.offsetMax = wantColMax;
                if (_timerCard.anchorMin != _cardAnchMin) _timerCard.anchorMin = _cardAnchMin;
                if (_timerCard.anchorMax != _cardAnchMax) _timerCard.anchorMax = _cardAnchMax;
                if (_timerCard.pivot != _cardPivot)       _timerCard.pivot = _cardPivot;
                if (_timerCard.anchoredPosition != _cardPos) _timerCard.anchoredPosition = _cardPos;
                if (_timerCard.sizeDelta != _cardSize)    _timerCard.sizeDelta = _cardSize;
            }
        }
    }

    // ── ウィンドウサイズ管理 ────────────────────────────────
private float BarScale()
    {
        // Screen.dpiは環境で値が乱れるが、Retina系(≥200)か否かの判別には使える
        // （実測: MacBook Retina=303 / 外部モニタ=109）
        float dpi = Screen.dpi;
        return dpi >= 200f ? 2.0f : 1.2f;
    }

/// <summary>Retina系なら2、それ以外は1（最小サイズの物理px換算用）。</summary>
    private float DpiF() => Screen.dpi >= 200f ? 2f : 1f;
    /// <summary>最小ウィンドウサイズ：可読性優先で「UIスケール≥基準解像度の約75%」を下限にする。
    /// Retina(2x)は同じ見た目サイズでも物理pxが倍あるので小さくできるが、
    /// 1xモニタでは縮めすぎると漢字が7〜8px描画になり潰れる（Noto維持のための措置）。</summary>
    // 最小サイズの思想：可読性の保証ではなく「レイアウトが破綻しない下限」だけを守る。
    // 小さくして文字が潰れるのはプレイヤーの選択（アスペクト固定・等倍ズームは維持）。
    // 1xモニタ: 90%下限の半分まで縮小許可（Full 864 / Nb 693 / TimerH 363）。Retinaは従来どおり。
    // DPIティア: Retina/200%(>=200) / Windowsの125%・150%(>=110) / 等倍96dpi
    // 実測: MacBook 303 / Mac外部 109 / Windows 100%=96・125%=120・150%=144
    // ※Mac外部109は従来どおり等倍ティアのまま（閾値110はその上）＝Mac側の挙動は不変
    private int FullMinW()  => ClampToDisplayW(Screen.dpi >= 200f ? 2048 : Screen.dpi >= 110f ? 1296 : 864);
    private int NbMinW()    => ClampToDisplayW(Screen.dpi >= 200f ? 2048 : Screen.dpi >= 110f ? 1040 : 693);
    private int TimerMinH() => ClampToDisplayH(Screen.dpi >= 200f ? 1040 : Screen.dpi >= 110f ? 545 : 363);

    /// <summary>最小サイズがディスプレイより大きくならないようクランプ（1080p等の小さな画面対策）。</summary>
    private static int ClampToDisplayW(int px)
    {
        try { var di = Screen.mainWindowDisplayInfo; if (di.width  > 0) return Mathf.Min(px, di.width);  } catch { }
        return px;
    }
    private static int ClampToDisplayH(int px)
    {
        try { var di = Screen.mainWindowDisplayInfo; if (di.height > 0) return Mathf.Min(px, di.height); } catch { }
        return px;
    }
    private int TimerMinW() => Mathf.RoundToInt(TimerMinH() * (396f / 806f));

    private int BarPixelHeight()   => Mathf.RoundToInt(BAR_H * BarScale());
    private int BarPixelMinWidth() => Mathf.RoundToInt(BAR_UNITS_W * BarScale());

/// <summary>リサイズ確定後のサイズポリシー：最小サイズ＋アスペクト比へのスナップ。
    /// ネイティブのaspect固定はドラッグ中のみ有効で、最大化・タイル配置は素通しされるため
    /// 確定後にここで必ず正しい比率に戻す（拡大・縮小どちらも維持）。</summary>
    private void EnforceMinSize()
    {
        if (Screen.fullScreen || !InFocusScene) return;
        int w = Screen.width, h = Screen.height;
        var disp = Screen.currentResolution;
        bool hasDisp = disp.width > 100;

        // 全画面相当（画面とほぼ同サイズ）のときはスナップしない
        // （Screen.fullScreenの検知漏れ対策のヒューリスティック）
        if (hasDisp && w >= disp.width - 2 && h >= disp.height - 2) return;

        if (_mode == Mode.Bar)
        {
            int bw = BarPixelMinWidth(), bh = BarPixelHeight();
            // Windows: 枠なし中に Screen.SetResolution を呼ぶとUnityがウィンドウを作り直し、
            // タイトルバーが復活する（剥がす→復活の無限ループ＝点滅）。ネイティブで直接サイズを決める。
            if (!WindowUtil.TrySetClientSize(bw, bh))
            {
                if (w != bw || h != bh) Screen.SetResolution(bw, bh, FullScreenMode.Windowed);
            }
            return;
        }

        float ratio; int minW;
        switch (_mode)
        {
            case Mode.Timer:    ratio = 396f / 806f;   minW = TimerMinW(); break;
            case Mode.Notebook: ratio = 1540f / 1080f; minW = NbMinW();    break;
            default:            ratio = 1920f / 1080f; minW = FullMinW();  break;
        }

        int tw = Mathf.Max(w, minW);
        int th = Mathf.RoundToInt(tw / ratio);
        if (hasDisp && th > disp.height - 40)
        {
            th = disp.height - 40;
            tw = Mathf.Max(Mathf.RoundToInt(th * ratio), minW);
        }
        // 2px以内の誤差はリサイズループ防止のため許容
        if (Mathf.Abs(tw - w) > 2 || Mathf.Abs(th - h) > 2)
            // 通常モードはUnityに任せる。ネイティブ指定だとバックバッファが追従せず
            // 描画がレターボックスされる（右と下に黒帯）。枠なしのBarモードのみネイティブ。
            Screen.SetResolution(tw, th, FullScreenMode.Windowed);
    }

private void SaveWindowSize(Mode m)
    {
        if (!InFocusScene || m == Mode.Bar) return; // Barは常に固定サイズ（保存しない）
        PlayerPrefs.SetInt($"{SIZE_PREFS}_{m}_w", Screen.width);
        PlayerPrefs.SetInt($"{SIZE_PREFS}_{m}_h", Screen.height);
    }

/// <summary>モード別アスペクト固定サイズを適用。初期値はディスプレイ比例（非Retinaでも巨大化しない）。</summary>
/// <summary>モード別アスペクト固定サイズを適用。
    /// Timerの初期値は「Fullで見ていたときのカードと同じ縮尺」（切替で大きさが変わらない）。</summary>
    private void RestoreWindowSize(Mode m)
    {
        if (Screen.fullScreen || !InFocusScene) return;
        var disp = Screen.currentResolution;
        bool hasDisp = disp.width > 100;
        // 枠なしのまま Screen.SetResolution するとUnityがウィンドウを作り直し、
        // 一瞬消えてからタイトルバー付きで再出現する。先に枠を戻しておく。
        // Windowsは枠なしのまま（リサイズ枠だけ戻す）、Macはタイトルバーを復元する
        if (m != Mode.Bar) WindowUtil.TrySetBorderless(WindowUtil.BorderlessAllModes, true);
        int w, h;
        switch (m)
        {
            case Mode.Bar:
                // 先に枠を外してからサイズを確定する。逆順だと「枠ありサイズ→枠を外して
                // 縦に伸びる→再サイズ」の3手になり、切替時に消える/点滅する。
                WindowUtil.TrySetBorderless(true);
                if (!WindowUtil.TrySetClientSize(BarPixelMinWidth(), BarPixelHeight()))
                    Screen.SetResolution(BarPixelMinWidth(), BarPixelHeight(), FullScreenMode.Windowed);
                return;
            case Mode.Timer:
            {
                // Fullのウィンドウ幅から現在のUI縮尺を割り出し、カード部分だけを同縮尺で切り出す
                int fullW = PlayerPrefs.GetInt($"{SIZE_PREFS}_{Mode.Full}_w", 0);
                if (fullW <= 0) fullW = Mathf.Max(Screen.width, FullMinW());
                float uiScale = fullW / 1920f;
                int defH = Mathf.RoundToInt(806f * uiScale);
                h = PlayerPrefs.GetInt($"{SIZE_PREFS}_{m}_h", defH);
                h = Mathf.Max(h, TimerMinH());
                if (hasDisp) h = Mathf.Min(h, disp.height - 80);
                w = Mathf.RoundToInt(h * (396f / 806f));
                break;
            }
            case Mode.Notebook:
            {
                int defW = hasDisp ? Mathf.RoundToInt(disp.width * 0.66f) : Screen.width;
                w = PlayerPrefs.GetInt($"{SIZE_PREFS}_{m}_w", defW);
                w = Mathf.Max(w, NbMinW());
                if (hasDisp) w = Mathf.Min(w, disp.width - 40);
                h = Mathf.RoundToInt(w * (1080f / 1540f));
                break;
            }
            default:
            {
                int defW = hasDisp ? Mathf.RoundToInt(disp.width * 0.80f) : Screen.width;
                w = PlayerPrefs.GetInt($"{SIZE_PREFS}_{m}_w", defW);
                w = Mathf.Max(w, FullMinW());
                if (hasDisp) w = Mathf.Min(w, disp.width - 40);
                h = Mathf.RoundToInt(w * (1080f / 1920f));
                break;
            }
        }
        if (hasDisp && h > disp.height - 60)
        {
            h = disp.height - 60;
            switch (m)
            {
                case Mode.Timer:    w = Mathf.RoundToInt(h * (396f / 806f));   break;
                case Mode.Notebook: w = Mathf.RoundToInt(h * (1540f / 1080f)); break;
                default:            w = Mathf.RoundToInt(h * (1920f / 1080f)); break;
            }
        }
        Screen.SetResolution(w, h, FullScreenMode.Windowed);
    }

/// <summary>モード別にウィンドウのアスペクト比をネイティブ固定（リサイズしても形が崩れない）。</summary>
/// <summary>ネイティブのアスペクト拘束は使わない（macOSの全画面移行をブロックするため）。
    /// 比率の維持は EnforceMinSize の「確定後スナップ」に一本化（Windowsでも同一動作）。</summary>
    private void ApplyWindowAspect()
    {
        WindowUtil.TrySetContentAspect(0f, 0f); // 拘束解除（全画面互換）
    }


    // ── バーUI ────────────────────────────────────────────
private void BuildBar()
    {
        var go = new GameObject("[TCBar]", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _barCanvas = go.AddComponent<Canvas>();
        _barCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _barCanvas.sortingOrder = 5000;
        _barScaler = go.AddComponent<CanvasScaler>();
        _barScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        _barScaler.scaleFactor = BarScale();
        go.AddComponent<GraphicRaycaster>();

        // 背面ベース（Bar時は角丸バーの周囲になる暗色。通常時はバー裏に隠れる）
        var back = new GameObject("Backdrop", typeof(RectTransform));
        back.transform.SetParent(go.transform, false);
        var bkrt = (RectTransform)back.transform;
        bkrt.anchorMin = new Vector2(0f, 1f); bkrt.anchorMax = new Vector2(1f, 1f);
        bkrt.pivot = new Vector2(0.5f, 1f);
        bkrt.sizeDelta = new Vector2(0f, BAR_H);
        var bkImg = back.AddComponent<Image>();
        bkImg.color = new Color(0.028f, 0.034f, 0.048f, 1f);
        _barBackdrop = back;

        var root = new GameObject("BarRoot", typeof(RectTransform));
        root.transform.SetParent(go.transform, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, BAR_H);
        _barRootRT = rt;

        // 角丸ベース＋マスク（Bar時は角丸カード化。中身は縦グラデ）
        var baseImg = root.AddComponent<Image>();
        UIStyleKit.ApplyRounded(baseImg, 12f);
        _barBaseImg = baseImg;
        baseImg.color = new Color(0.055f, 0.066f, 0.088f, 1f);
        root.AddComponent<Mask>().showMaskGraphic = true;

        var grad = new GameObject("BGGrad", typeof(RectTransform));
        grad.transform.SetParent(root.transform, false);
        var grt = (RectTransform)grad.transform;
        grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
        grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
        var bgImg = grad.AddComponent<Image>();
        var gtex = new Texture2D(1, 48, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var cTop = new Color(0.105f, 0.125f, 0.165f, 1f);
        var cBot = new Color(0.055f, 0.066f, 0.088f, 1f);
        for (int y = 0; y < 48; y++) gtex.SetPixel(0, y, Color.Lerp(cBot, cTop, y / 47f));
        gtex.Apply();
        bgImg.sprite = Sprite.Create(gtex, new Rect(0, 0, 1, 48), new Vector2(0.5f, 0.5f));
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;
        grad.AddComponent<LayoutElement>().ignoreLayout = true;

        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 14, 7, 7);
        hlg.spacing = 6;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // 下端ヘアライン（通常モードのみ。Barの角丸カードでは非表示）
        _barHairline = new GameObject("Hairline", typeof(RectTransform));
        _barHairline.transform.SetParent(root.transform, false);
        var lrt = (RectTransform)_barHairline.transform;
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.sizeDelta = new Vector2(0f, 1f);
        var lImg = _barHairline.AddComponent<Image>();
        lImg.color = new Color(1f, 1f, 1f, 0.08f);
        lImg.raycastTarget = false;
        _barHairline.AddComponent<LayoutElement>().ignoreLayout = true;

        // TCアイコン（=ALL）
        var iconTex = Resources.Load<Texture2D>("AppIcon");
        var iconBtn = MakeBarButton(root.transform, "TCIcon", iconTex == null ? "TC" : null, 36f,
            () => RequestMode(Mode.Full));
        if (iconTex != null)
        {
            var img = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(iconBtn.transform, false);
            var irt = (RectTransform)img.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(3f, 3f); irt.offsetMax = new Vector2(-3f, -3f);
            img.sprite = Sprite.Create(iconTex, new Rect(0, 0, iconTex.width, iconTex.height), new Vector2(0.5f, 0.5f));
            img.raycastTarget = false;
        }

        // ロゴ＋タグライン（広いときだけ）
        _logoBlock = new GameObject("Logo", typeof(RectTransform));
        _logoBlock.transform.SetParent(root.transform, false);
        var logoLE = _logoBlock.AddComponent<LayoutElement>();
        logoLE.minWidth = 190f; logoLE.preferredWidth = 190f;
        logoLE.minHeight = 32f; logoLE.preferredHeight = 32f;
        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(_logoBlock.transform, false);
        var nameRT = (RectTransform)nameGO.transform;
        nameRT.anchorMin = new Vector2(0f, 0.42f); nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = new Vector2(6f, 0f); nameRT.offsetMax = Vector2.zero;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        if (UIFonts.Readable != null) nameTMP.font = UIFonts.Readable;
        nameTMP.text = "TOKYO CORNER";
        nameTMP.fontSize = 13f; nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = new Color(0.93f, 0.95f, 0.97f, 1f);
        nameTMP.alignment = TextAlignmentOptions.BottomLeft;
        nameTMP.enableWordWrapping = false; nameTMP.raycastTarget = false;
        var tagGO = new GameObject("Tagline", typeof(RectTransform));
        tagGO.transform.SetParent(_logoBlock.transform, false);
        var tagRT = (RectTransform)tagGO.transform;
        tagRT.anchorMin = new Vector2(0f, 0f); tagRT.anchorMax = new Vector2(1f, 0.42f);
        tagRT.offsetMin = new Vector2(6f, 0f); tagRT.offsetMax = Vector2.zero;
        var tagTMP = tagGO.AddComponent<TextMeshProUGUI>();
        if (UIFonts.Readable != null) tagTMP.font = UIFonts.Readable;
        tagTMP.text = "東京発、あなたの未来へと続くカフェ。";
        tagTMP.fontSize = 8.5f;
        tagTMP.color = new Color(0.55f, 0.60f, 0.66f, 1f);
        tagTMP.alignment = TextAlignmentOptions.TopLeft;
        tagTMP.enableWordWrapping = false; tagTMP.raycastTarget = false;

        MakeSpacer(root.transform, 4f);

        // ── タイマーグループ（全モードで常時表示・一貫性重視） ──
        var tGroup = new GameObject("TimerGroup", typeof(RectTransform));
        tGroup.transform.SetParent(root.transform, false);
        var tgImg = tGroup.AddComponent<Image>();
        UIStyleKit.ApplyRounded(tgImg, 10f);
        tgImg.color = new Color(1f, 1f, 1f, 0.05f);
        var tgl = tGroup.AddComponent<HorizontalLayoutGroup>();
        tgl.padding = new RectOffset(10, 8, 3, 3);
        tgl.spacing = 2;
        tgl.childControlWidth = true; tgl.childControlHeight = true;
        tgl.childForceExpandWidth = false; tgl.childForceExpandHeight = false;
        tgl.childAlignment = TextAnchor.MiddleLeft;

        System.Action<GameObject> shrink = b =>
        {
            var sle = b.GetComponent<LayoutElement>();
            if (sle != null) { sle.minHeight = 26f; sle.preferredHeight = 26f; }
        };

        // タイマー表示（=Timerモード）
        shrink(MakeBarButton(tGroup.transform, "Timer", "--:--", 52f, () => RequestMode(Mode.Timer)));
        _barTimerLabel = _barLabels["Timer"];
        _barTimerLabel.color = LabelIdle;
        _barTimerLabel.fontStyle = FontStyles.Bold;
        _barTimerLabel.fontSize = 15f;

        // サイクル 1/4（表示のみ・稼働中はゴールド）
        var cycGO = new GameObject("Cycle", typeof(RectTransform));
        cycGO.transform.SetParent(tGroup.transform, false);
        var cycLE = cycGO.AddComponent<LayoutElement>();
        cycLE.minWidth = 28f; cycLE.preferredWidth = 28f;
        cycLE.minHeight = 26f; cycLE.preferredHeight = 26f;
        _barCycleLabel = cycGO.AddComponent<TextMeshProUGUI>();
        if (UIFonts.Readable != null) _barCycleLabel.font = UIFonts.Readable;
        _barCycleLabel.text = "-/-";
        _barCycleLabel.fontSize = 12f;
        _barCycleLabel.color = LabelIdle;
        _barCycleLabel.alignment = TextAlignmentOptions.Center;
        _barCycleLabel.raycastTarget = false;

        // タイマー操作（R / 再生 / スキップ）
        shrink(MakeBarButton(tGroup.transform, "CtlR",    "R", 24f, () => InvokeTimerControl(0)));
        shrink(MakeBarButton(tGroup.transform, "CtlPlay", "▶", 24f, () => InvokeTimerControl(1)));
        shrink(MakeBarButton(tGroup.transform, "CtlSkip", "✓", 24f, () => InvokeTimerControl(2)));
        _barPlayLabel = _barLabels["CtlPlay"];

        MakeSpacer(root.transform, 2f);

        // ── ナビ（Barのみ）：カレンダー系／リスト系を2つのチップに分けて情報種を明示 ──
        _navGroup = new GameObject("NavGroup", typeof(RectTransform));
        _navGroup.transform.SetParent(root.transform, false);
        var ngl = _navGroup.AddComponent<HorizontalLayoutGroup>();
        ngl.spacing = 5;
        ngl.childControlWidth = true; ngl.childControlHeight = true;
        ngl.childForceExpandWidth = false; ngl.childForceExpandHeight = false;
        ngl.childAlignment = TextAnchor.MiddleLeft;

        System.Func<string, Transform> navChip = chipName =>
        {
            var cg = new GameObject(chipName, typeof(RectTransform));
            cg.transform.SetParent(_navGroup.transform, false);
            var cImg = cg.AddComponent<Image>();
            UIStyleKit.ApplyRounded(cImg, 9f);
            cImg.color = new Color(1f, 1f, 1f, 0.04f);
            cImg.raycastTarget = false;
            var cl = cg.AddComponent<HorizontalLayoutGroup>();
            cl.padding = new RectOffset(3, 3, 3, 3);
            cl.spacing = 1;
            cl.childControlWidth = true; cl.childControlHeight = true;
            cl.childForceExpandWidth = false; cl.childForceExpandHeight = false;
            cl.childAlignment = TextAnchor.MiddleLeft;
            return cg.transform;
        };
        var calChip = navChip("CalChip");
        shrink(MakeBarButton(calChip, "Monthly", "M",  26f, () => RequestMode(Mode.Notebook, "Monthly")));
        shrink(MakeBarButton(calChip, "Weekly",  "W",  26f, () => RequestMode(Mode.Notebook, "Weekly")));
        shrink(MakeBarButton(calChip, "Daily",   "D",  26f, () => RequestMode(Mode.Notebook, "Daily")));
        var toolChip = navChip("ToolChip");
        shrink(MakeBarButton(toolChip, "Todo",    "TD", 30f, () => RequestMode(Mode.Notebook, "Todo")));
        shrink(MakeBarButton(toolChip, "Routine", "R",  26f, () => RequestMode(Mode.Notebook, "Routine")));
        shrink(MakeBarButton(toolChip, "Memo",    "M",  26f, () => RequestMode(Mode.Notebook, "Memo")));
        _navChipImgs = new[] { calChip.GetComponent<Image>(), toolChip.GetComponent<Image>() };

        var flex = new GameObject("Flex", typeof(RectTransform));
        flex.transform.SetParent(root.transform, false);
        flex.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // お店（アイコン）
        var shopTex = Resources.Load<Texture2D>("ShopIcon");
        var shopBtn = MakeBarButton(root.transform, "Shop", shopTex == null ? "←" : null, 34f, RequestShopReturn);
        if (shopTex != null)
        {
            var simg = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            simg.transform.SetParent(shopBtn.transform, false);
            var srt = (RectTransform)simg.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(8f, 8f); srt.offsetMax = new Vector2(-8f, -8f);
            simg.sprite = Sprite.Create(shopTex, new Rect(0, 0, shopTex.width, shopTex.height), new Vector2(0.5f, 0.5f));
            simg.color = LabelIdle;
            simg.raycastTarget = false;
        }

        // ウィンドウ操作（枠なし運用のWindowsのみ。Macはタイトルバーの信号機を使う）
        if (WindowUtil.IsWindows)
        {
            _winCtlSpacer = MakeSpacer(root.transform, 4f);
            _winMinGO   = MakeBarButton(root.transform, "WinMin",   "−", 26f, () => WindowUtil.TryMinimize());
            _winCloseGO = MakeBarButton(root.transform, "WinClose", "×", 26f, RequestAppQuit);
        }

        // 下端の進捗ライン（ポモドーロ残量。リングと同じ色）
        var pgGO = new GameObject("Progress", typeof(RectTransform));
        pgGO.transform.SetParent(root.transform, false);
        var prt = (RectTransform)pgGO.transform;
        prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(0f, 0f);
        prt.pivot = new Vector2(0f, 0f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(0f, 2f);
        _barProgress = pgGO.AddComponent<Image>();
        _barProgress.color = TimerWork;
        _barProgress.raycastTarget = false;
        _barProgress.enabled = false;
        pgGO.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    /// <summary>枠なし運用ではタイトルバーの「×」が無いため、バーのボタンから終了する。</summary>
    private void RequestAppQuit()
    {
        SaveWindowSize(_mode);
        PlayerPrefs.Save();
        Application.Quit();
    }

    private GameObject MakeSpacer(Transform parent, float width)
    {
        var sp = new GameObject("Spacer", typeof(RectTransform));
        sp.transform.SetParent(parent, false);
        var le = sp.AddComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width;
        return sp;
    }

private GameObject MakeBarButton(Transform parent, string key, string label, float width, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + key, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width;
        le.minHeight = 32f; le.preferredHeight = 32f;

        // 透明ベース＋ホバーでほんのり。アクティブは青のチップ（UpdateBarHighlightで切替）
        var bg = go.AddComponent<Image>();
        UIStyleKit.ApplyRounded(bg, 8f);
        bg.color = Color.white;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        btn.targetGraphic = bg;
        btn.colors = BarIdleBlock();
        btn.onClick.AddListener(onClick);
        _barButtons[key] = btn;

        if (!string.IsNullOrEmpty(label))
        {
            var txtGO = new GameObject("Label", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var trt = (RectTransform)txtGO.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            if (UIFonts.Readable != null) tmp.font = UIFonts.Readable;
            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.color = LabelIdle;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            _barLabels[key] = tmp;
        }
        return go;
    }

private void UpdateBarHighlight()
    {
        // ナビはヘッダーバーに常設（構造は全モード共通）。表示方法だけモードで変える：
        //  Bar=短縮形（M W D / TD R M）＋チップ背景／Full・Notebook=フル表記の素テキスト／Timer=幅がないので非表示
        bool mini = _mode == Mode.Bar;
        bool navShow = _mode != Mode.Timer;
        if (_navGroup != null && _navGroup.activeSelf != navShow) _navGroup.SetActive(navShow);
        foreach (var d in NavDefs)
        {
            if (_barLabels.TryGetValue(d.key, out var nl) && nl != null)
            {
                string wantTxt = mini ? d.mini : d.full;
                if (nl.text != wantTxt) nl.text = wantTxt;
            }
            if (_barButtons.TryGetValue(d.key, out var nb) && nb != null)
            {
                var nle = nb.GetComponent<LayoutElement>();
                float w = mini ? d.miniW : d.fullW;
                if (nle != null && !Mathf.Approximately(nle.preferredWidth, w)) { nle.minWidth = w; nle.preferredWidth = w; }
            }
        }
        if (_navChipImgs != null)
            foreach (var ci in _navChipImgs)
                if (ci != null)
                {
                    var cc = mini ? new Color(1f, 1f, 1f, 0.04f) : Color.clear;
                    if (ci.color != cc) ci.color = cc;
                }

        // バーの形はモードで変える：
        //   Bar    = 独立した浮遊ピル（角丸12・ウィンドウ切り抜きと合わせて四隅が背景に抜ける）
        //   通常   = 直角。下側が角丸だとコンテンツとの間に切れ込みができ、
        //            ヘッダーが浮いたカードに見えてフォーカス画面と分断されるため。
        //            区切りは下端ヘアラインが担う（＝地続きのまま境界だけ示す）
        if (_barBaseImg != null)
        {
            bool rounded = _barBaseImg.sprite != null;
            if (mini && !rounded) UIStyleKit.ApplyRounded(_barBaseImg, 12f);
            else if (!mini && rounded)
            {
                _barBaseImg.sprite = null;
                _barBaseImg.type   = Image.Type.Simple;
            }
        }
        // −/× はタイトルバーが無い場面だけ出す（Barモード、または全モード枠なしを有効化したとき）。
        // 通常モードではOSのタイトルバーのボタンを使うため二重に出さない。
        bool showWinCtl = mini || WindowUtil.BorderlessAllModes;
        if (_winMinGO   != null && _winMinGO.activeSelf   != showWinCtl) _winMinGO.SetActive(showWinCtl);
        if (_winCloseGO != null && _winCloseGO.activeSelf != showWinCtl) _winCloseGO.SetActive(showWinCtl);
        if (_winCtlSpacer != null && _winCtlSpacer.activeSelf != showWinCtl) _winCtlSpacer.SetActive(showWinCtl);
        if (_barHairline != null && _barHairline.activeSelf != !mini) _barHairline.SetActive(!mini);
        if (_barBackdrop != null && !_barBackdrop.activeSelf) _barBackdrop.SetActive(true);

        foreach (var kv in _barButtons)
        {
            if (kv.Value == null) continue;
            bool active =
                (kv.Key == "TCIcon" && _mode == Mode.Full) ||
                (kv.Key == "Timer"  && _mode == Mode.Timer) ||
                ((_mode == Mode.Notebook || _mode == Mode.Full) && kv.Key == _tab); // Full中も現在タブを表示
            var cb = active ? ActiveBlock() : BarIdleBlock();
            if (kv.Value.colors.normalColor != cb.normalColor) kv.Value.colors = cb;
            if (_barLabels.TryGetValue(kv.Key, out var lbl) && lbl != null && kv.Key != "Timer")
            {
                bool warm = kv.Key == "Todo" || kv.Key == "Routine" || kv.Key == "Memo";
                lbl.color = active ? LabelActive : (warm ? ToolIdle : LabelIdle);
            }
        }
    }

private void SyncBar()
    {
        if (_barCanvas == null || !_barCanvas.gameObject.activeSelf) return;
        if (Time.unscaledTime < _nextSync) return;
        _nextSync = Time.unscaledTime + 0.5f;

        // 旧ナビ（Buttons行）は全モードで非表示のため、WindowManager側からタブを読み戻す
        // 処理はPhase 5で撤去した。タブの唯一のソースは _tab（＝ヘッダーバーのナビ）。
        UpdateBarHighlight();

        // ディスプレイ間移動でDPIが変わったらバースケールを追従
        if (_barScaler != null && !Mathf.Approximately(_barScaler.scaleFactor, BarScale()))
            _barScaler.scaleFactor = BarScale();

        // ロゴ＋タグラインは幅に余裕があるときだけ（Bar固定幅では自動で畳む）
        if (_logoBlock != null)
        {
            bool show = (Screen.width / BarScale()) > 860f;
            if (_logoBlock.activeSelf != show) _logoBlock.SetActive(show);
        }

        // ポモドーロ状態（Phase 3以降は PomodoroManager が唯一のソース）
        var m = PM;
        var phase = m != null ? m.Phase : TimerController.TimerPhase.Stopped;
        bool onBreak = phase == TimerController.TimerPhase.ShortBreak || phase == TimerController.TimerPhase.LongBreak;
        bool ticking = onBreak || phase == TimerController.TimerPhase.Work || phase == TimerController.TimerPhase.Stopwatch;
        var stateCol = !ticking ? LabelIdle : (onBreak ? TimerBreak : TimerWork);

        // TimerCard 内の mm:ss をミラー＋状態色（作業=青／休憩=緑／停止=グレー）
        if (_barTimerLabel != null)
        {
            // 停止中は設定値、稼働中は残り時間（カードと同じ規則）
            _barTimerLabel.text = m == null ? "--:--"
                : phase == TimerController.TimerPhase.Stopped
                    ? PomodoroManager.FormatTime(m.WorkMinutes * 60f)
                    : PomodoroManager.FormatTime(m.RemainingSeconds);
            if (_barTimerLabel.color != stateCol) _barTimerLabel.color = stateCol;
        }

        // 下端の進捗ライン（リングと同じ残量・同じ色）
        if (_barProgress != null)
        {
            bool show = ticking && phase != TimerController.TimerPhase.Stopwatch;
            if (_barProgress.enabled != show) _barProgress.enabled = show;
            if (show)
            {
                float frac = Mathf.Clamp01(1f - m.Progress01);
                var prt = (RectTransform)_barProgress.transform;
                float pw = _barRootRT != null ? _barRootRT.rect.width : Screen.width / BarScale();
                var wantSz = new Vector2(pw * frac, 2f);
                if ((prt.sizeDelta - wantSz).sqrMagnitude > 0.25f) prt.sizeDelta = wantSz;
                if (_barProgress.color != stateCol) _barProgress.color = stateCol;
            }
        }

        // サイクル（n/m）ミラー：稼働中はゴールド（カフェの差し色）
        if (_barCycleLabel != null)
        {
            if (m != null)
                _barCycleLabel.text = phase == TimerController.TimerPhase.Stopped
                    ? "1/" + m.CycleCount
                    : m.CurrentRound + "/" + m.TotalRounds;
            var cg = ticking ? new Color(1f, 0.776f, 0.302f, 0.95f) : LabelIdle;
            if (_barCycleLabel.color != cg) _barCycleLabel.color = cg;
        }
        if (_barPlayLabel != null && m != null)
            _barPlayLabel.text = m.IsRunning ? "II" : "\u25B6";

        if (_mode == Mode.Notebook || _mode == Mode.Full) SwitchWindow(_tab);
    }

    private string _appliedTab;

    /// <summary>
    /// タブ切替。Michsky WindowManager.OpenWindow() の置き換え（Phase 5）。
    /// 名前が一致した画面だけを有効にし、他は無効にする。フェードは入れない:
    ///  - MUIPのフェードは Animator + CanvasGroup で行われ、連打すると複数画面が一瞬重なった
    ///  - SetActive で切り替えると各画面の OnEnable が正しく発火し、再描画が自然に走る
    ///    （フェード方式では OnEnable が来ないためポーリングで代用していた）
    /// </summary>
    private void SwitchWindow(string key)
    {
        if (_appliedTab == key || _wmWindows == null) return;

        for (int i = 0; i < _wmWindows.childCount; i++)
        {
            var child = _wmWindows.GetChild(i);
            bool show = child.name == key;
            if (child.gameObject.activeSelf != show) child.gameObject.SetActive(show);
            if (!show) continue;

            // MUIPがフェード途中の値を残していることがあるため、表示側は毎回正規化する
            var cg = child.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
        _appliedTab = key;
    }

    /// <summary>外部（カレンダーの日付セル等）からのタブ切替要求。現在のモードは保つ。</summary>
    public void RequestTab(string tab)
    {
        if (string.IsNullOrEmpty(tab)) return;
        RequestMode(_mode == Mode.Full ? Mode.Full : Mode.Notebook, tab);
    }

/// <summary>トンマナ方針（仕様書§11）：ツール画面（UI_Prototype）のKotonoruをNotoに統一。
    /// リスト行は再構築のたびに新規生成されるため定期スイープで拾う。
    /// Cafeシーン（世界観側）はKotonoruのまま→ InFocusScene でガード。</summary>
    private void SweepToolFonts()
    {
        if (!InFocusScene) return;
        if (Time.unscaledTime < _nextFontSweep) return;
        _nextFontSweep = Time.unscaledTime + 0.7f;
        var noto = UIFonts.Readable;
        if (noto == null) return;
        foreach (var txt in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (txt == null || txt.font == null) continue;
            if (txt.font.name.Contains("Kotonoru")) txt.font = noto;
        }

        // スクロールの弾性バウンスも常時禁止（初回表示のすり抜け対策で全ScrollRectを常時Clampedに）
        foreach (var sr in FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sr != null && sr.movementType == ScrollRect.MovementType.Elastic)
                sr.movementType = ScrollRect.MovementType.Clamped;
    }

}
