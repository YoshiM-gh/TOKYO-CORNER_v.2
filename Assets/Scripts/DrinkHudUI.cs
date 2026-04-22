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

    private void OnEnable()
    {
        if (DrinkInventory.Instance != null)
            DrinkInventory.Instance.OnDrinksChanged += Refresh;
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
}
