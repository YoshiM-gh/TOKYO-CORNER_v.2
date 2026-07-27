using UnityEngine;

/// <summary>
/// 無操作時にフレームレートを自動で落とし、入力があれば即復帰する省電力機構。
/// GameObject不要（起動時に自動生成）。
///
/// 2026-07-27: 下限をシーンで分けた。
/// 元は全シーン一律5fpsだったが、カフェのように**キャラが動いている3D画面**では
/// 1秒5コマの描画が「小刻みに震える」「その場で足踏みする」ように見えてしまう
/// （実測では位置・接地は正常で、カクつきによる見え方だけの問題）。
/// 道具画面（カレンダー/Todo/メモ）は静止画に近いので5fpsで問題ない。
/// </summary>
public class IdleFrameThrottle : MonoBehaviour
{
    private const int   ACTIVE_FPS     = 30; // 操作中
    private const int   IDLE_FPS_TOOL  = 5;  // 無操作時・道具画面（ほぼ静止画）
    private const int   IDLE_FPS_WORLD = 20; // 無操作時・3D世界（キャラが動くので下げすぎない）
    private const float IDLE_AFTER     = 3f; // この秒数無操作でアイドル扱い

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

        bool idle = Time.unscaledTime - _lastActiveTime > IDLE_AFTER;
        int target = !idle ? ACTIVE_FPS
                   : SceneRouter.IsFocusScene ? IDLE_FPS_TOOL   // 道具画面
                                              : IDLE_FPS_WORLD; // カフェ・オープニング

        if (Application.targetFrameRate != target)
            Application.targetFrameRate = target;
    }
}
