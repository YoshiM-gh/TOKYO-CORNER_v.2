using UnityEngine;
using TMPro;
using System.Text;

/// <summary>
/// スタンプカードの現在状況を表示する。
/// focusUI 内の TextMeshProUGUI に ●●●●○○○○○○ 形式で表示。
/// </summary>
public class StampCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stampText;

    private void OnEnable()
    {
        if (SaveDataManager.Instance != null)
            SaveDataManager.Instance.OnStatsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (SaveDataManager.Instance != null)
            SaveDataManager.Instance.OnStatsChanged -= Refresh;
    }

    private void Refresh()
    {
        if (stampText == null || SaveDataManager.Instance == null) return;

        bool isFocusMode =
            GameModeManager.Instance != null &&
            GameModeManager.Instance.CurrentMode == GameModeManager.GameMode.Focus;
        if (stampText.enabled != !isFocusMode)
            stampText.enabled = !isFocusMode;
        if (isFocusMode) return;

        int current = SaveDataManager.Instance.GetStampCount();
        int total = SaveDataManager.GetStampsPerCard();
        int cards = SaveDataManager.Instance.GetTotalCards();

        var sb = new StringBuilder();
        for (int i = 0; i < total; i++)
            sb.Append(i < current ? "[*]" : "[ ]");

        stampText.text = $"Stamp: {current}/{total}  {sb}  Cards: {cards}";
    }
}
