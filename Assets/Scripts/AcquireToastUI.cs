using UnityEngine;

/// <summary>
/// 「○○ を手に入れた！」トースト。プレイヤー頭上（worldOffset）に追従表示する。
/// ScreenSpaceOverlayキャンバス内に置き、表示中のみ WorldToScreenPoint で位置更新。
/// Show(itemName) でポップイン→保持→フェードアウト。raycastは塞がない設定にすること。
/// </summary>
public class AcquireToastUI : MonoBehaviour
{
    public static AcquireToastUI Instance { get; private set; }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMPro.TMP_Text label;
    [SerializeField] private Transform followTarget;                       // プレイヤー
    [SerializeField] private float screenOffsetY = 160f; // 足元投影からの上方向オフセット(px)
    [SerializeField] private float holdSeconds = 1.6f;
    [SerializeField] private float fadeSeconds = 0.4f;

    private float _t = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (group != null) group.alpha = 0f;
    }

    private void Start()
    {
        if (followTarget == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) followTarget = p.transform;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string itemName)
    {
        if (label != null) label.text = itemName + " を手に入れた！";
        _t = 0f;
        if (group != null) group.alpha = 0f;
    }

    private void Update()
    {
        if (_t < 0f || group == null) return;
        _t += Time.deltaTime;
        const float inSec = 0.15f;
        if (_t < inSec)
        {
            group.alpha = _t / inSec;
            transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, _t / inSec);
        }
        else if (_t < inSec + holdSeconds)
        {
            group.alpha = 1f;
            transform.localScale = Vector3.one;
        }
        else
        {
            float k = (_t - inSec - holdSeconds) / fadeSeconds;
            group.alpha = 1f - Mathf.Clamp01(k);
            if (k >= 1f) _t = -1f;
        }
    }

    private void LateUpdate()
    {
        if (_t < 0f || followTarget == null || Camera.main == null) return;
        var sp = Camera.main.WorldToScreenPoint(followTarget.position);
        if (sp.z <= 0f) return; // カメラ背後では更新しない
        // 見下ろしカメラではワールド頭上の透視投影が発散するため、
        // 足元を投影してスクリーン空間で持ち上げる（+画面内クランプ）
        float x = Mathf.Clamp(sp.x, 270f, Screen.width - 270f);
        float y = Mathf.Clamp(sp.y + screenOffsetY, 40f, Screen.height - 40f);
        transform.position = new Vector3(x, y, 0f);
    }
}
