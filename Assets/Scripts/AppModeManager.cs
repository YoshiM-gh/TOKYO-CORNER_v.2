using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    // ── 定数（実測ベース） ──
    private const float MIN_SCALE  = 0.8f;
    private const int   FULL_MIN_W = 1536, FULL_MIN_H = 864;
    private const int   NB_MIN_W   = 1000, NB_MIN_H   = 700;
    private const int   TIMER_MIN_W = 320, TIMER_MIN_H = 640;
    private const float BAR_UNITS_W = 600f;
    private static readonly Vector2 TimerRef = new Vector2(396f, 806f); // カード364×726+余白+バー48
    private const float BAR_H = 48f;
    private const float RESIZE_DEBOUNCE = 0.4f;
    private const string SIZE_PREFS = "tc_size3";

    private Mode   _mode = Mode.Full;
    private string _tab  = "Monthly";

    // ── UI_Prototype キャッシュ ──
    private readonly Dictionary<CanvasScaler,(float match, Vector2 refRes)> _orig = new();
    private CanvasScaler  _mainScaler;
    private Canvas        _mainCanvas;
    private RectTransform _wmArea;
    private CanvasGroup   _wmAreaGroup;
    private Michsky.MUIP.WindowManager _wm;
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
    private Vector2 _cardAnchMin, _cardAnchMax, _cardPivot, _cardPos, _cardSize;
    private Vector2 _windowsOffMax;

    // ── バーUI ──
    private Canvas _barCanvas;
    private TextMeshProUGUI _barTimerLabel;
    private TMP_Text _cardTimeSrc;
    private readonly Dictionary<string, Button> _barButtons = new();
    private readonly Dictionary<string, TextMeshProUGUI> _barLabels = new();
    private readonly Dictionary<string, Image> _barUnderlines = new();
    private GameObject _logoBlock;
    private float _nextSync;

    private Vector2Int _lastSize;
    private float _enforceAt = -1f;
    private float _reapplyAt = -1f;          // モード切替後の再適用（SetResolution非同期対策）
    private float _nextFontSweep;            // ツール画面のフォント統一スイープ
    private CanvasScaler _barScaler;

    private static readonly Color BarBG       = new Color(0.078f, 0.094f, 0.125f, 1f);
    private static readonly Color LabelIdle   = new Color(0.62f, 0.67f, 0.74f, 1f);
    private static readonly Color LabelActive = Color.white;
    private static readonly Color TimerGreen  = new Color(0.80f, 0.95f, 0.84f, 1f);

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
        _wm = null; _wmButtons = null; _wmWindows = null; _backToCafe = null;
        _leftCol = null; _leftColGroup = null; _timerCard = null;
        _charGroup = null; _charCam = null; _cardTimeSrc = null;
        _origSaved = false; _appliedTab = null;
        ApplyAll();
        if (s.name == SceneRouter.FocusScene) RestoreWindowSize(_mode);
        Debug.Log($"[AppModeManager] sceneLoaded '{s.name}' → 再適用 (mode={_mode})");
    }

    private void OnApplicationQuit() => SaveWindowSize(_mode);

    private bool InFocusScene => _wmArea != null;

    // ── モード切替 ─────────────────────────────────────────
public void RequestMode(Mode target, string tab = null)
    {
        // Notebook内のタブ移動（月→週など）はトグルしない（旧: 同モード判定でBarに畳むバグ）
        bool tabChange = target == Mode.Notebook && tab != null && (_mode != Mode.Notebook || tab != _tab);
        if (tab != null) { _tab = tab; _appliedTab = null; PlayerPrefs.SetString("tc_tab", tab); }

        Mode next;
        if (tabChange) next = Mode.Notebook;
        else next = (_mode == target && target != Mode.Bar) ? Mode.Bar : target; // 同一ビュー再クリック→Bar
        if (next == _mode && !tabChange) { UpdateBarHighlight(); return; }

        SaveWindowSize(_mode);
        _mode = next;
        PlayerPrefs.SetInt("tc_mode", (int)_mode);
        ApplyScalers();               // 先にスケーラを新モードに
        RestoreWindowSize(_mode);     // 次にウィンドウサイズ
        ApplyAll();                   // レイアウト適用
        _reapplyAt = Time.unscaledTime + 0.7f; // リサイズ反映後に再適用（表示崩れ対策）
        Debug.Log($"[AppModeManager] mode → {_mode} (tab={_tab})");
    }

    private void RequestShopReturn()
    {
        if (SceneRouter.Instance != null) SceneRouter.Instance.ExitFocus();
    }

    // ── 毎フレーム ─────────────────────────────────────────
private void Update()
    {
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
                _wm          = areaGO.GetComponentInChildren<Michsky.MUIP.WindowManager>(true);
                var wmT      = _wm != null ? _wm.transform : areaGO.transform.Find("Window Manager");
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
            if (_mode == Mode.Timer && cs == _mainScaler && InFocusScene)
            {
                cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = TimerRef;
                cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
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

        // ── 旧ナビのトルツメ（全モード共通） ──
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
                if (_leftCol.anchoredPosition != _leftColPos) _leftCol.anchoredPosition = _leftColPos;
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
    private int BarPixelHeight()   => Mathf.RoundToInt(BAR_H * BarScale());
    private int BarPixelMinWidth() => Mathf.RoundToInt(BAR_UNITS_W * BarScale());

    private void EnforceMinSize()
    {
        if (Screen.fullScreen || !InFocusScene) return;
        int w = Screen.width, h = Screen.height;
        int minW, minH;
        switch (_mode)
        {
            case Mode.Bar:      minW = BarPixelMinWidth(); minH = BarPixelHeight(); break;
            case Mode.Timer:    minW = TIMER_MIN_W;        minH = TIMER_MIN_H;      break;
            case Mode.Notebook: minW = NB_MIN_W;           minH = NB_MIN_H;         break;
            default:            minW = FULL_MIN_W;         minH = FULL_MIN_H;       break;
        }
        int tw = Mathf.Max(w, minW), th = Mathf.Max(h, minH);
        if (_mode == Mode.Bar) th = BarPixelHeight();
        if (tw != w || th != h) Screen.SetResolution(tw, th, FullScreenMode.Windowed);
    }

private void SaveWindowSize(Mode m)
    {
        if (!InFocusScene || m == Mode.Bar) return; // Barは常に固定サイズ（保存しない）
        PlayerPrefs.SetInt($"{SIZE_PREFS}_{m}_w", Screen.width);
        PlayerPrefs.SetInt($"{SIZE_PREFS}_{m}_h", Screen.height);
    }

private void RestoreWindowSize(Mode m)
    {
        if (Screen.fullScreen || !InFocusScene) return;
        float f = BarScale();
        // Barは常に固定サイズ（ユーザ確認済み：この大きさで固定で良い）
        if (m == Mode.Bar)
        {
            Screen.SetResolution(BarPixelMinWidth(), BarPixelHeight(), FullScreenMode.Windowed);
            return;
        }
        int defW, defH;
        switch (m)
        {
            case Mode.Timer:    defW = Mathf.RoundToInt(340f*f/1.2f*1.2f); defW = Mathf.RoundToInt(340f*f); defH = Mathf.RoundToInt(706f*f); break;
            case Mode.Notebook: defW = Mathf.RoundToInt(1300f*f); defH = Mathf.RoundToInt(900f*f); break;
            default:            defW = Mathf.RoundToInt(1500f*f); defH = Mathf.RoundToInt(980f*f); break;
        }
        int w = PlayerPrefs.GetInt($"{SIZE_PREFS}_{m}_w", defW);
        int h = PlayerPrefs.GetInt($"{SIZE_PREFS}_{m}_h", defH);

        var disp = Screen.currentResolution;
        if (disp.width > 100)
        {
            w = Mathf.Min(w, disp.width - 40);
            h = Mathf.Min(h, Mathf.Max(disp.height - 80, BarPixelHeight()));
        }
        switch (m)
        {
            case Mode.Timer:    w = Mathf.Max(w, TIMER_MIN_W); h = Mathf.Max(h, TIMER_MIN_H); break;
            case Mode.Notebook: w = Mathf.Max(w, NB_MIN_W);    h = Mathf.Max(h, NB_MIN_H);    break;
            default:            w = Mathf.Max(w, FULL_MIN_W);  h = Mathf.Max(h, FULL_MIN_H);  break;
        }
        Screen.SetResolution(w, h, FullScreenMode.Windowed);
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

        var root = new GameObject("BarRoot", typeof(RectTransform));
        root.transform.SetParent(go.transform, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, BAR_H);
        root.AddComponent<Image>().color = BarBG;
        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 8, 8);
        hlg.spacing = 4;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var line = new GameObject("Hairline", typeof(RectTransform));
        line.transform.SetParent(root.transform, false);
        var lrt = (RectTransform)line.transform;
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.sizeDelta = new Vector2(0f, 1f);
        var lImg = line.AddComponent<Image>();
        lImg.color = new Color(1f, 1f, 1f, 0.08f);
        lImg.raycastTarget = false;
        line.AddComponent<LayoutElement>().ignoreLayout = true;

        // TCアイコン（=ALL / フル表示）
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

        // ロゴ＋タグライン（幅に余裕があるときだけ表示）
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

        MakeSpacer(root.transform, 10f);
        var timerBtn = MakeBarButton(root.transform, "Timer", "--:--", 56f, () => RequestMode(Mode.Timer));
        _barTimerLabel = _barLabels["Timer"];
        _barTimerLabel.color = TimerGreen;
        _barTimerLabel.fontStyle = FontStyles.Bold;

        MakeSpacer(root.transform, 12f);
        MakeBarButton(root.transform, "Monthly", "Monthly", 66f, () => RequestMode(Mode.Notebook, "Monthly"));
        MakeBarButton(root.transform, "Weekly",  "Weekly",  58f, () => RequestMode(Mode.Notebook, "Weekly"));
        MakeBarButton(root.transform, "Daily",   "Daily",   46f, () => RequestMode(Mode.Notebook, "Daily"));
        MakeBarButton(root.transform, "Todo",    "Todo",    44f, () => RequestMode(Mode.Notebook, "Todo"));
        MakeBarButton(root.transform, "Routine", "Routine", 60f, () => RequestMode(Mode.Notebook, "Routine"));
        MakeBarButton(root.transform, "Memo",    "Memo",    48f, () => RequestMode(Mode.Notebook, "Memo"));

        var flex = new GameObject("Flex", typeof(RectTransform));
        flex.transform.SetParent(root.transform, false);
        flex.AddComponent<LayoutElement>().flexibleWidth = 1f;

        MakeBarButton(root.transform, "Shop", "← お店", 60f, RequestShopReturn);
    }

    private void MakeSpacer(Transform parent, float width)
    {
        var sp = new GameObject("Spacer", typeof(RectTransform));
        sp.transform.SetParent(parent, false);
        var le = sp.AddComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width;
    }

private GameObject MakeBarButton(Transform parent, string key, string label, float width, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + key, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width;
        le.minHeight = 32f; le.preferredHeight = 32f;

        // 箱は描かない：透明ベース＋ホバーでほんのりだけ（テキスト主体のナビ）
        var bg = go.AddComponent<Image>();
        UIStyleKit.ApplyRounded(bg, 6f);
        bg.color = Color.white;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        btn.targetGraphic = bg;
        var cb = ColorBlock.defaultColorBlock;
        cb.normalColor      = Color.clear;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.07f);
        cb.pressedColor     = new Color(1f, 1f, 1f, 0.14f);
        cb.selectedColor    = Color.clear;
        cb.colorMultiplier  = 1f; cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
        _barButtons[key] = btn;

        // アクティブ下線（参考デザインの繊細なアクティブ表現）
        var ulGO = new GameObject("Underline", typeof(RectTransform));
        ulGO.transform.SetParent(go.transform, false);
        var ulRT = (RectTransform)ulGO.transform;
        ulRT.anchorMin = new Vector2(0f, 0f); ulRT.anchorMax = new Vector2(1f, 0f);
        ulRT.pivot = new Vector2(0.5f, 0f);
        ulRT.sizeDelta = new Vector2(-14f, 2f);
        ulRT.anchoredPosition = new Vector2(0f, 1f);
        var ul = ulGO.AddComponent<Image>();
        ul.color = new Color(0.31f, 0.55f, 0.95f, 1f);
        ul.raycastTarget = false;
        ul.enabled = false;
        _barUnderlines[key] = ul;

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
        foreach (var kv in _barButtons)
        {
            bool active =
                (kv.Key == "TCIcon" && _mode == Mode.Full) ||
                (kv.Key == "Timer"  && _mode == Mode.Timer) ||
                (_mode == Mode.Notebook && kv.Key == _tab);
            if (_barUnderlines.TryGetValue(kv.Key, out var ul) && ul != null && ul.enabled != active)
                ul.enabled = active;
            if (_barLabels.TryGetValue(kv.Key, out var lbl) && lbl != null && kv.Key != "Timer")
                lbl.color = active ? LabelActive : LabelIdle;
        }
    }

    private void SyncBar()
    {
        if (_barCanvas == null || !_barCanvas.gameObject.activeSelf) return;
        if (Time.unscaledTime < _nextSync) return;
        _nextSync = Time.unscaledTime + 0.5f;
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

        // TimerCard 内の mm:ss をミラー（Phase 3 で PomodoroManager に置換予定）
        if (_barTimerLabel != null)
        {
            if (_cardTimeSrc == null && _timerCard != null)
            {
                float best = 0f;
                foreach (var t in _timerCard.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t == null || string.IsNullOrEmpty(t.text)) continue;
                    if (System.Text.RegularExpressions.Regex.IsMatch(t.text.Trim(), @"^\d{1,3}:\d{2}$") && t.fontSize > best)
                    {
                        best = t.fontSize;
                        _cardTimeSrc = t;
                    }
                }
            }
            _barTimerLabel.text = _cardTimeSrc != null ? _cardTimeSrc.text.Trim() : "--:--";
        }

        if (_mode == Mode.Notebook || _mode == Mode.Full) SwitchWindow(_tab);
    }

    private string _appliedTab;
    private void SwitchWindow(string key)
    {
        if (_appliedTab == key || _wm == null) return;
        try { _wm.OpenWindow(key); _appliedTab = key; }
        catch (System.Exception e) { Debug.LogWarning("[AppModeManager] OpenWindow失敗: " + e.Message); _appliedTab = key; }
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
    }

}
