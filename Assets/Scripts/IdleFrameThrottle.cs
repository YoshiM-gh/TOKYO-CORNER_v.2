using UnityEngine;

/// <summary>
/// フレームレートの動的制御（省電力）。GameObject不要（起動時に自動生成）。
///
/// 決め方は「今どれだけの絵が動いているか」で、2つの軸を掛け合わせる:
///   1. 表示モード … Bar/Timer は数字と小さなUIしか動かないので操作中でも低くてよい
///   2. 無操作かどうか … 3秒以上さわっていなければさらに落とす
///
/// 2026-07-27: 全シーン一律5fpsだったのをシーン別にした（3D世界で5fpsは動きが破綻して見える）。
/// 2026-07-31: 表示モードの軸を追加（Phase 4）。
/// </summary>
public class IdleFrameThrottle : MonoBehaviour
{
    private const int ACTIVE_WORLD  = 30; // 操作中・3Dのカフェ
    private const int ACTIVE_TOOL   = 30; // 操作中・道具画面（スクロールの追従があるので落とさない）
    private const int ACTIVE_COMPACT= 15; // 操作中・Bar/Timer（数字と小さなUIだけ）
    private const int IDLE_WORLD    = 20; // 無操作・3Dのカフェ（キャラが動くので落としすぎない）
    private const int IDLE_TOOL     = 5;  // 無操作・道具画面（ほぼ静止画）
    private const int IDLE_COMPACT  = 5;  // 無操作・Bar/Timer
    private const float IDLE_AFTER  = 3f;

    private float   _lastActiveTime;
    private Vector3 _lastMousePos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("[IdleFrameThrottle]");
        DontDestroyOnLoad(go);
        go.AddComponent<IdleFrameThrottle>();
    }

    private void Awake()
    {
        _lastActiveTime = Time.unscaledTime;
        _lastMousePos   = Input.mousePosition;
    }

    private void Update()
    {
        bool active =
            Input.anyKey ||
            Input.mouseScrollDelta.sqrMagnitude > 0.0001f ||
            (Input.mousePosition - _lastMousePos).sqrMagnitude > 4f;
        _lastMousePos = Input.mousePosition;
        if (active) _lastActiveTime = Time.unscaledTime;

        bool idle = Time.unscaledTime - _lastActiveTime > IDLE_AFTER;
        int target;

        if (!SceneRouter.IsFocusScene)
        {
            target = idle ? IDLE_WORLD : ACTIVE_WORLD; // カフェ・オープニング
        }
        else
        {
            var am = AppModeManager.Instance;
            bool compact = am != null && am.IsCompactMode; // Bar / Timer
            if (compact) target = idle ? IDLE_COMPACT : ACTIVE_COMPACT;
            else         target = idle ? IDLE_TOOL    : ACTIVE_TOOL;
        }

        if (Application.targetFrameRate != target)
            Application.targetFrameRate = target;
    }
}
