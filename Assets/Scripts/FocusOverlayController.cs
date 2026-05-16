using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public class FocusOverlayController : MonoBehaviour
{
    public static FocusOverlayController Instance { get; private set; }

    [Header("単一パネル")]
    [SerializeField] private GameObject focusPanel;

    [Header("背景")]
    [SerializeField] private Image background;
    [SerializeField] private float fadeInDuration  = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private float backgroundAlpha = 1f;

    [Header("Canvas スケール")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private Canvas       _canvas;
    private CanvasScaler _scaler;
    private bool         _isOpen;
    private Coroutine    _fadeCoroutine;

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

        if (background == null)
        {
            var bgT = transform.Find("FullscreenBG");
            if (bgT != null) background = bgT.GetComponent<Image>();
        }

        if (background != null)
            background.color = new Color(0f, 0f, 0f, 0f);

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
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        if (background != null) _fadeCoroutine = StartCoroutine(FadeBackground(0f, backgroundAlpha, fadeInDuration));
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        CancelInvoke(nameof(HideCanvas));
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        if (background != null) _fadeCoroutine = StartCoroutine(FadeBackground(backgroundAlpha, 0f, fadeOutDuration));
        Invoke(nameof(HideCanvas), fadeOutDuration + 0.05f);
    }

    private IEnumerator FadeBackground(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = UnityEngine.Mathf.Lerp(from, to, elapsed / duration);
            background.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        background.color = new Color(0f, 0f, 0f, to);
    }

    // 旧API互換
    public void OpenSettings() => Open();
    public void ShowRuntime()  => Open();
    public void ShowSettings() { }

    private void HideCanvas() => gameObject.SetActive(false);
}
