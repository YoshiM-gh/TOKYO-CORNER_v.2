using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ウィンドウリサイズ中にCanvasが遅延更新される問題を解消するスクリプト。
/// Canvas と CanvasScaler の更新をフレームごとに強制する。
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public class CanvasResizeForcer : MonoBehaviour
{
    private int _lastWidth;
    private int _lastHeight;
    private CanvasScaler _scaler;

    void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
        _lastWidth  = Screen.width;
        _lastHeight = Screen.height;
    }

    void Update()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
        {
            _lastWidth  = Screen.width;
            _lastHeight = Screen.height;

            // CanvasScaler を一瞬無効化→再有効化してスケール再計算を強制
            _scaler.enabled = false;
            _scaler.enabled = true;

            // 全Canvasのレイアウトを即時更新
            Canvas.ForceUpdateCanvases();
        }
    }
}
