using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// macOS でゲームウィンドウを常に最前面に表示するスクリプト。
/// Windows ビルドには一切影響しません。
/// </summary>
public class AlwaysOnTop : MonoBehaviour
{
#if UNITY_STANDALONE_OSX
    [DllImport("AlwaysOnTop")]
    private static extern void SetWindowAlwaysOnTop(bool onTop);
#endif

    [Tooltip("ゲーム起動時に自動で最前面に設定するか")]
    public bool enableOnStart = true;

    void Start()
    {
        // バックグラウンドでも描画・更新を継続（タイマーが止まって見えるのを防ぐ）
        Application.runInBackground = true;

#if UNITY_STANDALONE_OSX
        if (enableOnStart)
        {
            StartCoroutine(SetAlwaysOnTopDelayed());
        }
#endif
    }

#if UNITY_STANDALONE_OSX
    private IEnumerator SetAlwaysOnTopDelayed()
    {
        yield return new WaitForSeconds(0.3f);
        SetAlwaysOnTop(true);
    }
#endif

    public void SetAlwaysOnTop(bool onTop)
    {
#if UNITY_STANDALONE_OSX
        SetWindowAlwaysOnTop(onTop);
        Debug.Log("[AlwaysOnTop] " + (onTop ? "有効" : "無効"));
#endif
    }

    void OnApplicationQuit()
    {
#if UNITY_STANDALONE_OSX
        SetWindowAlwaysOnTop(false);
#endif
    }
}
