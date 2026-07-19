using UnityEngine;

/// <summary>
/// アプリ起動時に自動実行されるパフォーマンス設定。
/// GameObjectに貼る必要なし。ビルド版でもEditor版でも動作。
/// </summary>
public static class PerformanceSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // ★ フレームレートを630に制限（無制限だとCPU/GPUが爆走してファン全開になる）
        Application.targetFrameRate = 30;

        // ★ VSync無効（vSync有効だと targetFrameRate が無視され、
        //    120Hzディスプレイでは120fpsで回ってしまうため）
        QualitySettings.vSyncCount = 0;

        // ★ シャドウ距離を短縮（40 → 20）
        QualitySettings.shadowDistance = 20f;

        // ★ シャドウ解像度をMediumに（HighやVery Highより軽い）
        QualitySettings.shadowResolution = ShadowResolution.Medium;

        Debug.Log($"[PerformanceSettings] Applied: targetFR={Application.targetFrameRate}, " +
                  $"vSync={QualitySettings.vSyncCount}, " +
                  $"shadowDist={QualitySettings.shadowDistance}");
    }
}
