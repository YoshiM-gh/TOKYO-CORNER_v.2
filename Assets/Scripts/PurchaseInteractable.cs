using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// 購入エリア（カウンター）をクリックでメニューUIを開く。
/// 3a: 旧「クリック即・固定価格ドリンク購入」から差し替え。実購入は MenuShopUI 側。
/// 同一GameObjectに Collider（推奨: BoxCollider）が必要。Collider.Raycast で他オブジェクトより優先判定。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PurchaseInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 2.5f;

    private Transform player;
    private Collider col;

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

        if (MenuShopUI.Instance != null) MenuShopUI.Instance.Open();
    }

    private static bool WasPrimaryClick()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }
}
