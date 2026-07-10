using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// カフェ↔フォーカスのシーン切替を担うDDOLシングルトン。
/// 切替時は黒の切替オーバーレイ(中央にTOKYO CORNER)を出し、
/// 非同期ロード中ずっと最前面に表示してフェードアウトする(同期ロードの「固まり」を隠す)。
/// 中央ロゴは Resources/TransitionLogo (Sprite) を置けば自動でテキストの代わりに表示される。
/// </summary>
public class SceneRouter : MonoBehaviour
{
    public const string CafeScene = "Cafe";
    public const string FocusScene = "UI_Prototype";

    const float FadeInDur = 0.5f;
    const float FadeOutDur = 0.5f;
    const float MinHold = 1.0f;

    private static SceneRouter _instance;
    public static SceneRouter Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SceneRouter");
                _instance = go.AddComponent<SceneRouter>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private CanvasGroup _overlay;
    private bool _busy;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    /// <summary>初回起動判定: Cafe開始時に未登録ならオープニングへ。
    /// SceneRouterは遅延生成(初回EnterFocusまで不在)のため、シーン配置不要の静的フックで行う。
    /// AfterSceneLoad=全Awake後なのでSaveDataManager.Instanceは確定済み。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CheckOpeningOnBoot()
    {
        if (SceneManager.GetActiveScene().name == CafeScene
            && SaveDataManager.Instance != null
            && !SaveDataManager.Instance.IsOpeningDone)
        {
            SceneManager.LoadScene("Opening");
        }
    }

    public void EnterFocus() { StartTransition(FocusScene); }
    public void ExitFocus() { StartTransition(CafeScene); }

    private void StartTransition(string scene)
    {
        if (_busy) return;
        if (!gameObject.activeInHierarchy) { SceneManager.LoadScene(scene); return; }
        StartCoroutine(TransitionRoutine(scene));
    }

    private IEnumerator TransitionRoutine(string scene)
    {
        _busy = true;
        if (_overlay == null) BuildOverlay();
        _overlay.gameObject.SetActive(true);
        _overlay.blocksRaycasts = true;
        yield return Fade(0f, 1f, FadeInDur);
        yield return null; // 黒幕を1フレーム確実に描画
        float start = Time.realtimeSinceStartup;
        AsyncOperation op = SceneManager.LoadSceneAsync(scene);
        while (op != null && !op.isDone) yield return null;
        float elapsed = Time.realtimeSinceStartup - start;
        if (elapsed < MinHold) yield return new WaitForSecondsRealtime(MinHold - elapsed);
        yield return Fade(1f, 0f, FadeOutDur);
        _overlay.blocksRaycasts = false;
        _overlay.gameObject.SetActive(false);
        _busy = false;
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        if (dur <= 0f) { _overlay.alpha = to; yield break; }
        _overlay.alpha = from;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _overlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        _overlay.alpha = to;
    }

    private void BuildOverlay()
    {
        var canvasGo = new GameObject("TransitionOverlay",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _overlay = canvasGo.GetComponent<CanvasGroup>();
        _overlay.alpha = 0f;
        _overlay.blocksRaycasts = false;

        var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgrt = bg.GetComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = Color.black;

        var logo = Resources.Load<Sprite>("TransitionLogo");
        if (logo != null)
        {
            var logoGo = new GameObject("Logo", typeof(RectTransform), typeof(Image));
            logoGo.transform.SetParent(canvasGo.transform, false);
            var lrt = logoGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 0.5f); lrt.anchorMax = new Vector2(0.5f, 0.5f); lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = new Vector2(0f, 0f);
            lrt.sizeDelta = new Vector2(logo.rect.width, logo.rect.height);
            var li = logoGo.GetComponent<Image>();
            li.sprite = logo; li.preserveAspect = true;
        }
        else
        {
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(canvasGo.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.5f); trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, 0f);
            trt.sizeDelta = new Vector2(1400f, 220f);
            var title = titleGo.AddComponent<TMPro.TextMeshProUGUI>();
            title.text = "トーキョーコーナー";
            title.fontSize = 24f;
            title.alignment = TMPro.TextAlignmentOptions.Center;
            title.color = Color.white;
            title.characterSpacing = 10f;
        }

        canvasGo.SetActive(false);
    }
}
