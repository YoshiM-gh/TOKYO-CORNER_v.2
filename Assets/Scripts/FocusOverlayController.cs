using TranslucentUIFX;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public class FocusOverlayController : MonoBehaviour
{
    public static FocusOverlayController Instance { get; private set; }

    [Header("単一パネル")]
    [SerializeField] private GameObject focusPanel;

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
        _scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.referenceResolution  = referenceResolution;
        _scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        _scaler.matchWidthOrHeight   = 1f;

        if (translucentBackground == null)
        {
            var bgT = transform.Find("FullscreenBG");
            if (bgT != null) translucentBackground = bgT.GetComponent<TranslucentImageFX>();
        }

        gameObject.SetActive(false);
    }

    public bool IsOpen => _isOpen;

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        CancelInvoke(nameof(HideCanvas));
        gameObject.SetActive(true);
        if (focusPanel != null) focusPanel.SetActive(true);
        if (translucentBackground != null) translucentBackground.FadeIn(fadeInDuration);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        CancelInvoke(nameof(HideCanvas));
        if (translucentBackground != null) translucentBackground.FadeOut(fadeOutDuration);
        Invoke(nameof(HideCanvas), fadeOutDuration + 0.05f);
    }

    // 旧API互換
    public void OpenSettings() => Open();
    public void ShowRuntime()  => Open();
    public void ShowSettings() { }

    private void HideCanvas() => gameObject.SetActive(false);
}