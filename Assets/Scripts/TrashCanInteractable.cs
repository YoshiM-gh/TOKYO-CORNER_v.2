using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// ゴミ箱オブジェクトにアタッチ。Collider 必須。
/// クリックで DrinkDiscardUI を開き、捨てるドリンクを選ばせる。
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrashCanInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private DrinkDiscardUI discardUI;

    private Transform player;
    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
    }

    private void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (!WasPrimaryClick()) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (player == null || Camera.main == null) return;
        if (Vector3.Distance(player.position, transform.position) > interactRange) return;

        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        if (!col.Raycast(ray, out RaycastHit _, 100f)) return;

        if (DrinkInventory.Instance == null || DrinkInventory.Instance.GetDrinks().Count == 0)
        {
            Debug.Log("[TrashCan] Clicked - no drinks to discard.");
            return;
        }

        Debug.Log("[TrashCan] Clicked - opening discard UI.");
        if (discardUI != null) discardUI.Open();
    }

    private static bool WasPrimaryClick()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
