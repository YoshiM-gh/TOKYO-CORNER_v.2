using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 低DPI/小ウィンドウ環境でUIスケールが下がりすぎてフォントが潰れるのを防ぐ。
/// - CanvasScaler(ScaleWithScreenSize)の実効スケールが MIN_SCALE を下回る場合、
///   ConstantPixelSize に切り替えて下限で固定（フォントの描画ピクセルサイズを保証）
/// - ウィンドウの最小サイズも強制（極端な縮小を防止）
/// GameObject不要（起動時に自動生成）。
/// </summary>
public class UIScaleClamp : MonoBehaviour
{
    private const float MIN_SCALE = 1.0f;   // UIスケールの下限：設計サイズ未満には縮小しない（フォント可読性最優先）
    private const int   MIN_W     = 1100;   // ウィンドウ最小幅（物理px）
    private const int   MIN_H     = 650;    // ウィンドウ最小高さ
    private const float RESIZE_DEBOUNCE = 0.35f; // ドラッグ中に強制リサイズしないための待ち

    private readonly Dictionary<CanvasScaler, (float match, Vector2 refRes)> _orig = new();
    private Vector2Int _lastSize;
    private float _minSizeEnforceAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("[UIScaleClamp]");
        DontDestroyOnLoad(go);
        go.AddComponent<UIScaleClamp>();
    }

    private void Start()
    {
        Apply();
        // 起動直後から最小サイズ未満の場合も強制（前回終了時のウィンドウサイズが復元されるため）
        if (!Screen.fullScreen && (Screen.width < MIN_W || Screen.height < MIN_H))
            _minSizeEnforceAt = Time.unscaledTime + 0.5f;
    }

    private void Update()
    {
        if (Screen.width != _lastSize.x || Screen.height != _lastSize.y)
        {
            Apply();
            // 最小サイズ未満なら、リサイズが落ち着いてから強制的に戻す
            if (!Screen.fullScreen && (Screen.width < MIN_W || Screen.height < MIN_H))
                _minSizeEnforceAt = Time.unscaledTime + RESIZE_DEBOUNCE;
        }

        if (_minSizeEnforceAt > 0f && Time.unscaledTime >= _minSizeEnforceAt)
        {
            _minSizeEnforceAt = -1f;
            if (!Screen.fullScreen && (Screen.width < MIN_W || Screen.height < MIN_H))
                Screen.SetResolution(Mathf.Max(Screen.width, MIN_W),
                                     Mathf.Max(Screen.height, MIN_H),
                                     FullScreenMode.Windowed);
        }
    }

    private void Apply()
    {
        _lastSize = new Vector2Int(Screen.width, Screen.height);

        foreach (var cs in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cs == null) continue;

            // 初回に元設定を記憶（クランプ切替後も復元できるように）
            if (!_orig.TryGetValue(cs, out var o))
            {
                if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                o = (cs.matchWidthOrHeight, cs.referenceResolution);
                _orig[cs] = o;
            }

            // CanvasScaler(ScaleWithScreenSize/MatchWidthOrHeight)と同じ計算式
            float logW  = Mathf.Log(Screen.width  / o.refRes.x, 2f);
            float logH  = Mathf.Log(Screen.height / o.refRes.y, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, o.match));

            if (scale < MIN_SCALE)
            {
                // 下限で固定（フォントが潰れない）。レイアウトは実質「広い仮想解像度」として詰まる
                cs.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                cs.scaleFactor = MIN_SCALE;
            }
            else
            {
                // 十分大きい画面では元の挙動に戻す
                cs.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = o.refRes;
                cs.matchWidthOrHeight  = o.match;
            }
        }
    }
}
