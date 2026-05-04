using UnityEngine;
using TMPro;
using System.Text;

/// <summary>
/// フォーカスモードUIに配置。ドリンク所持状況を Drink(n/4) Drink(n/4)... の形式で表示する。
/// focusUI の子オブジェクトに TextMeshProUGUI と一緒に置き、drinkText を割り当てること。
/// </summary>
public class DrinkHudUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI drinkText;
    [SerializeField] private bool showInFocusMode = false;
    [Header("Temporary Layout Offset")]
    [SerializeField] private bool forceTopLeftLayout = true;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -24f);
    [SerializeField] private Vector2 preferredSize = new Vector2(280f, 40f);

    private RectTransform drinkRect;

    private void Awake()
    {
        if (drinkText != null)
            drinkRect = drinkText.rectTransform;
    }

    private void OnEnable()
    {
        if (DrinkInventory.Instance != null)
            DrinkInventory.Instance.OnDrinksChanged += Refresh;
        ApplyTemporaryLayout();
        Refresh();
    }

    private void OnDisable()
    {
        if (DrinkInventory.Instance != null)
            DrinkInventory.Instance.OnDrinksChanged -= Refresh;
    }

    private void Refresh()
    {
        if (drinkText == null) return;

        bool isFocusMode =
            GameModeManager.Instance != null &&
            GameModeManager.Instance.CurrentMode == GameModeManager.GameMode.Focus;
        if (drinkText.enabled != (showInFocusMode && isFocusMode))
            drinkText.enabled = showInFocusMode && isFocusMode;
        if (!drinkText.enabled) return;

        if (DrinkInventory.Instance == null)
        {
            drinkText.text = "Drink(0)";
            return;
        }

        var drinks = DrinkInventory.Instance.GetDrinks();
        if (drinks.Count == 0)
        {
            drinkText.text = "Drink(0)";
            return;
        }

        var sb = new StringBuilder();
        foreach (var d in drinks)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append($"Drink({d.sipsRemaining}/{DrinkInventory.SipsPerDrink})");
        }
        drinkText.text = sb.ToString();
    }

    private void ApplyTemporaryLayout()
    {
        if (!forceTopLeftLayout || drinkRect == null) return;

        drinkRect.anchorMin = new Vector2(0f, 1f);
        drinkRect.anchorMax = new Vector2(0f, 1f);
        drinkRect.pivot = new Vector2(0f, 1f);
        drinkRect.anchoredPosition = anchoredPosition;
        drinkRect.sizeDelta = preferredSize;
        drinkText.alignment = TextAlignmentOptions.TopLeft;
    }
}
