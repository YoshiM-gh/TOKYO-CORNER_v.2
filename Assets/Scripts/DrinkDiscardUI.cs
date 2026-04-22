using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゴミ箱クリック時に表示するドリンク選択ポップアップ。
/// プレハブ不要。ボタンはコードで動的生成する。
/// </summary>
public class DrinkDiscardUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private Button cancelButton;
    [SerializeField] private GameObject noItemsText;

    private readonly List<GameObject> spawnedRows = new();

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        if (DrinkInventory.Instance == null) return;

        var drinks = DrinkInventory.Instance.GetDrinks();
        bool hasDrinks = drinks.Count > 0;

        if (noItemsText != null) noItemsText.SetActive(!hasDrinks);
        if (rowContainer != null) rowContainer.gameObject.SetActive(hasDrinks);

        if (hasDrinks) Rebuild(drinks);
        if (panel != null) panel.SetActive(true);
        Debug.Log(hasDrinks ? "[TrashCan] Discard UI opened." : "[TrashCan] No drinks to discard.");
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Rebuild(IReadOnlyList<DrinkItem> drinks)
    {
        foreach (var row in spawnedRows) Destroy(row);
        spawnedRows.Clear();

        for (int i = 0; i < drinks.Count; i++)
        {
            int index = i;

            // ボタン本体
            var btnGo = new GameObject($"DrinkBtn_{i}");
            btnGo.transform.SetParent(rowContainer, false);

            var rect = btnGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 50f);

            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.2f, 0.1f, 1f);
            colors.pressedColor = new Color(0.6f, 0.3f, 0.1f, 1f);
            btn.colors = colors;

            // ラベル
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = $"Drink #{i + 1}   {drinks[i].SipDots()}   {drinks[i].PurchasedTimeLabel()}";
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;

            btn.onClick.AddListener(() => OnDiscardSelected(index));
            spawnedRows.Add(btnGo);
        }
    }

    private void OnDiscardSelected(int index)
    {
        if (DrinkInventory.Instance == null) return;
        DrinkInventory.Instance.DiscardDrink(index);
        Close();
    }
}
