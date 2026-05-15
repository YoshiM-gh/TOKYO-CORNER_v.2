using TranslucentUIFX;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public class FocusOverlayController : MonoBehaviour
{
    public static FocusOverlayController Instance { get; private set; }

    [Header("パネル参照")]
    [SerializeField] private GameObject runtimePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Translucent 背景")]
    [SerializeField] private TranslucentImageFX translucentBackground;
    [SerializeField] private float fadeInDuration  = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("Canvas スケール")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private Canvas       _canvas;
    private CanvasScaler _scaler;
    private bool         _isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _canvas = GetComponent<Canvas>();
        _scaler = GetComponent<CanvasScaler>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        _scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.referenceResolution = referenceResolution;
        _scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        _scaler.matchWidthOrHeight  = 1f;

        // FullscreenBG を自動検索
        if (translucentBackground == null)
        {
            var bgT = transform.Find("FullscreenBG");
            if (bgT != null) translucentBackground = bgT.GetComponent<TranslucentUIFX.TranslucentImageFX>();
        }

        gameObject.SetActive(false);
    }

    public void OpenSettings() { SetPanel(false); Show(); }
    public void ShowRuntime()  { if (!_isOpen) Show(); SetPanel(true); }
    public void ShowSettings() { SetPanel(false); }
    public bool IsOpen => _isOpen;

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        CancelInvoke(nameof(HideCanvas));
        if (translucentBackground != null) translucentBackground.FadeOut(fadeOutDuration);
        Invoke(nameof(HideCanvas), fadeOutDuration + 0.05f);
    }

    private void Show()
    {
        _isOpen = true;
        CancelInvoke(nameof(HideCanvas));
        gameObject.SetActive(true);
        if (translucentBackground != null) translucentBackground.FadeIn(fadeInDuration);
    }

    private void HideCanvas() => gameObject.SetActive(false);

    private void SetPanel(bool showRuntime)
    {
        if (runtimePanel  != null) runtimePanel .SetActive(showRuntime);
        if (settingsPanel != null) settingsPanel.SetActive(!showRuntime);
    }
}
