using UnityEngine;

/// <summary>
/// 無操作時にフレームレートを自動で落とし、入力があれば即復帰する。
/// カレンダー/Todo系の「眺めている時間が長い」アプリ向けの省電力機構。
/// GameObject不要（起動時に自動生成）。
/// </summary>
public class IdleFrameThrottle : MonoBehaviour
{
    private const int   ACTIVE_FPS = 30;  // 操作中
    private const int   IDLE_FPS   = 5;   // 無操作時（NowLine等の見た目更新には十分）
    private const float IDLE_AFTER = 3f;  // この秒数無操作でアイドル扱い

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
        // キー・クリック・スクロール・マウス移動のいずれかで「操作中」とみなす
        bool active =
            Input.anyKey ||
            Input.mouseScrollDelta.sqrMagnitude > 0.0001f ||
            (Input.mousePosition - _lastMousePos).sqrMagnitude > 4f;
        _lastMousePos = Input.mousePosition;

        if (active) _lastActiveTime = Time.unscaledTime;

        int target = (Time.unscaledTime - _lastActiveTime > IDLE_AFTER)
            ? IDLE_FPS
            : ACTIVE_FPS;

        if (Application.targetFrameRate != target)
            Application.targetFrameRate = target;
    }
}
