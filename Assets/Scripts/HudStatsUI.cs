using UnityEngine;
using TMPro;

/// <summary>所持・スタンプ・滞在時間・メニュー回数のテキストHUD（MVP）。Freeモード時のみ表示。</summary>
public class HudStatsUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private float refreshInterval = 0.25f;

    private float _nextRefresh;

    private void Update()
    {
        if (statsText == null || SaveDataManager.Instance == null) return;

        bool isFocusMode =
            GameModeManager.Instance != null &&
            GameModeManager.Instance.CurrentMode == GameModeManager.GameMode.Focus;

        bool showInFreeMode = !isFocusMode;
        if (statsText.enabled != showInFreeMode)
            statsText.enabled = showInFreeMode;

        if (!showInFreeMode) return;
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + refreshInterval;
        statsText.text = SaveDataManager.Instance.BuildHudStatsText();
    }
}
