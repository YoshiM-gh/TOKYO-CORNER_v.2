using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// カフェスタッフ（Waiter）をクリック＝話しかけ。会話「ようこそ〜」→ メニューUIの流れ。
/// 3a: 旧「クリック即・固定価格ドリンク購入」から差し替え。実購入は MenuShopUI 側。
/// 同一GameObjectに Collider（推奨: BoxCollider）が必要。Collider.Raycast で他オブジェクトより優先判定。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PurchaseInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 4f; // カウンター越しでも届く距離
    [SerializeField] private string staffName = "店員"; // ネームプレート表示名（キャラ名はシーンで設定）

    public string DisplayName => staffName;

    private Transform player;
    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
    }

    private void Start()
    {
        // 操作キャラ本体を名指しで取得（"Player"タグは原点の親コンテナにも付いているため）。Seat側と同じ流儀。
        GameObject p = GameObject.Find("23_Businessman");
        if (p == null) p = GameObject.FindWithTag("Player");
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

        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen) return;   // 会話中は重ねない
        if (MenuShopUI.Instance != null && MenuShopUI.Instance.IsOpen) return;    // メニュー表示中も無視

        if (DialogueUI.Instance != null)
        {
            var self = transform;
            DialogueUI.Instance.ShowLines(staffName,
                new[] { "ようこそ、TOKYO CORNERへ。ご注文はお決まりですか？" },
                () => { if (MenuShopUI.Instance != null) MenuShopUI.Instance.Open(self); },
                self, true); // 会話を残したままメニューを重ねる
        }
        else if (MenuShopUI.Instance != null) MenuShopUI.Instance.Open(transform); // 会話UI不在時のフォールバック
    }

    private static bool WasPrimaryClick()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }
}
