using UnityEngine;

/// <summary>
/// プレイヤー頭上の喜びエモート（♪など）。Player配下のワールド空間キャンバスに付与し、常時カメラへ正対。
/// Show(symbol) でポップ→上昇→フェードの一回演出。キャラ本体のAnimatorには触らない。
/// </summary>
public class PlayerEmote : MonoBehaviour
{
    public static PlayerEmote Instance { get; private set; }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMPro.TMP_Text label;
    [SerializeField] private float canvasScale = 0.01f; // WorldSpaceキャンバスの実寸(100units×0.01=1m)。シーン値事故への防御でAwakeで強制
    [SerializeField] private float riseDistance = 0.35f;
    [SerializeField] private float duration = 1.2f;

    private Vector3 _basePos;
    private float _t = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _basePos = transform.localPosition;
        transform.localScale = Vector3.one * canvasScale; // scaleが1に戻る事故があったため毎回保証
        if (group != null) group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string symbol)
    {
        if (label != null) label.text = symbol;
        _t = 0f;
        if (group != null) group.alpha = 1f;
        transform.localPosition = _basePos;
        transform.localScale = Vector3.one * (canvasScale * 0.6f); // ポップはcanvasScale基準（1.0基準だと100倍に膨れるバグの修正）
    }

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        if (_t < 0f) return;
        _t += Time.deltaTime;
        float k = Mathf.Clamp01(_t / duration);
        float pop = Mathf.Clamp01(_t / 0.15f);
        transform.localScale = Vector3.one * (canvasScale * Mathf.Lerp(0.6f, 1f, pop));
        transform.localPosition = _basePos + Vector3.up * (riseDistance * k);
        if (group != null) group.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
        if (k >= 1f)
        {
            _t = -1f;
            if (group != null) group.alpha = 0f;
            transform.localPosition = _basePos;
        }
    }
}
