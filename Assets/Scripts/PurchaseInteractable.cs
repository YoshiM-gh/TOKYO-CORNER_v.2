using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// MVP: 購入エリアをクリックでドリンク1つ（固定価格）。成功時にスタンプ1つ。
/// 同一GameObjectに Collider（推奨: BoxCollider）が必要。Collider.Raycast で他オブジェクトより優先判定。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PurchaseInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private float feedbackDuration = 2.5f;

    private Transform player;
    private Collider col;
    private float clearFeedbackAt = -1f;

    private void Awake()
    {
        col = GetComponent<Collider>();
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (feedbackText != null && clearFeedbackAt > 0f && Time.unscaledTime >= clearFeedbackAt)
        {
            feedbackText.text = "";
            clearFeedbackAt = -1f;
        }

        if (!WasPrimaryClick()) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        if (player == null || Camera.main == null) return;
        if (Vector3.Distance(player.position, transform.position) > interactRange) return;

        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        if (!col.Raycast(ray, out RaycastHit _, 100f)) return;

        TryPurchase();
    }

    private static bool WasPrimaryClick()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private void TryPurchase()
    {
        if (SaveDataManager.Instance == null) return;

        if (DrinkInventory.Instance != null && !DrinkInventory.Instance.CanPurchase())
        {
            Show($"Can't carry more drinks (max {DrinkInventory.MaxDrinks})");
            Debug.Log($"[Purchase] Failed - drink inventory full ({DrinkInventory.MaxDrinks}/{DrinkInventory.MaxDrinks})");
            return;
        }

        bool ok = SaveDataManager.Instance.TryPurchaseMvpDrink();
        if (ok)
        {
            DrinkInventory.Instance?.AddDrink();
            Show($"Drink purchased! Coins left: {SaveDataManager.Instance.GetCoins()}C");
            Debug.Log($"[Purchase] MVP drink purchased. Coins: {SaveDataManager.Instance.GetCoins()}C | Stamps: {SaveDataManager.Instance.GetStampCount()}");
        }
        else
        {
            Show($"Not enough coins (need {SaveDataManager.MvpDrinkPrice}C, have {SaveDataManager.Instance.GetCoins()}C)");
            Debug.Log($"[Purchase] Failed - not enough coins. Need: {SaveDataManager.MvpDrinkPrice}C, Have: {SaveDataManager.Instance.GetCoins()}C");
        }
    }

    private void Show(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            clearFeedbackAt = Time.unscaledTime + feedbackDuration;
        }
    }
}
