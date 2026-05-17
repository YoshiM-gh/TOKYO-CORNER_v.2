using System.Collections;
using UnityEngine;

/// <summary>
/// ウィンドウの最小サイズを Screen.SetResolution で強制するスクリプト。
/// macOS / Windows 両対応。
/// </summary>
public class MinWindowSize : MonoBehaviour
{
    private const int MIN_WIDTH  = 150;
    private const int MIN_HEIGHT = 310;

    void Start()
    {
        StartCoroutine(EnforceMinSize());
    }

    private IEnumerator EnforceMinSize()
    {
        bool running = true;
        while (running)
        {
            if (Screen.width < MIN_WIDTH || Screen.height < MIN_HEIGHT)
            {
                int w = Mathf.Max(Screen.width, MIN_WIDTH);
                int h = Mathf.Max(Screen.height, MIN_HEIGHT);
                Screen.SetResolution(w, h, false);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
}
