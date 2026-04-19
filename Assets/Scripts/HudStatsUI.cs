using UnityEngine;
using TMPro;

/// <summary>所持・スタンプ・滞在時間・メニュー回数のテキストHUD（MVP）</summary>
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

        if (statsText.enabled != isFocusMode)
            statsText.enabled = isFocusMode;

        if (!isFocusMode) return;
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + refreshInterval;
        statsText.text = SaveDataManager.Instance.BuildHudStatsText();
    }
}
