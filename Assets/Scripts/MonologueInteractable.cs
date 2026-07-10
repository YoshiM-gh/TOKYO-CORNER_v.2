using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// 話しかけると「ちょっといいこと」を1本語って終わるキャラ（ミスターカフェイン用）。
/// 注文は受けない — 本人いわく「俺には俺だけの価値がある」。
/// 話しかけるたびランダム（直前と同じ本は引き直し）。
/// セリフは内蔵データ（量産・差し替えはYoshiM担当。Monologues配列を編集すれば増える）。
/// 同一GameObjectに Collider が必要。PurchaseInteractable と同じクリック様式。
/// </summary>
[RequireComponent(typeof(Collider))]
public class MonologueInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 4f; // カウンター越しでも届く距離
    [SerializeField] private string charName = "ミスターカフェイン";

    public string DisplayName => charName;

    // ── モノローグ10本（1要素=1画面・クリック送り）──
    private static readonly string[][] Monologues =
    {
        new[] { "俺の名前はミスターカフェイン。\nなぜそう呼ばれるかって？", "みんながそう呼ぶからさ！", "ハッハッハッー" },
        new[] { "この世には搾取されるやつと搾取するやつがいる。", "お前はどっちだ？俺は搾取される側だ。", "ハッハッハッー" },
        new[] { "受験勉強だ？大事なプレゼンだ？", "今日のディナーが食えなくなるのと、どっちが大事だ？\nお前、考えすぎじゃないのか？", "ハッハッハッー！" },
        new[] { "ハンバーガーを食いまくってブクブク太ってくだばっちまうのはお前の自由だ。", "だがな、ウマいハンバーガーをたらふく食いまくるのだってお前の自由だ。", "自由を選ぶ権利は、お前にしかない。", "ハッハッハッー！" },
        new[] { "日本のアニメっておもろいよな。\n今度海外のおもろいアニメも教えてくれよ。", "創作活動に国境なんかない。\n世界はそれを愛と呼ぶんだぜ。", "ハッハッハッー！" },
        new[] { "傘は持っているかい？\n傘はお前を雨から守ってくれるんだ。", "では、心にさす傘は持っているかい？\n何を守ってくれるかはお前次第さ。", "ハッハッハッー！" },
        new[] { "そこには将来に悩むガキがいる。\nあっちにはピンチを迎えたビジネスマンがいる。", "俺ならどっちも助けないね。\nなぜかって？", "あいつらは今まさに自分が主役のストーリーを演じているからさ！", "ハッハッハッー！" },
        new[] { "自分が楽しけりゃ、周りは自然とついてくる。\nなに？楽しくても着いてこないって？", "そうだな。自分は楽しんでるだなんて言ってるうちは、\nまだまだ人なんてついて来ないだろうな。", "ハッハッハッー！" },
        new[] { "最後の晩餐、何を選ぶかって？", "そんなの決まってる。俺は卵かけご飯に鰹節をまぶしたやつだ。", "幼少の頃に好きだった食べ物に勝るものなし！", "ハッハッハッー！" },
        new[] { "何？\nお前はカウンターの中でものも売らずに何してるんだって？", "いい質問だ。俺には俺だけの価値がある。", "そう！お前にもお前だけの価値があるようにな。", "ハッハッハッー！" },
    };

    private Transform player;
    private Collider col;
    private int _lastIndex = -1;

    private void Awake() { col = GetComponent<Collider>(); }

    private void Start()
    {
        // 操作キャラ本体を名指しで取得（PurchaseInteractableと同じ流儀）
        GameObject p = GameObject.Find("23_Businessman");
        if (p == null) p = GameObject.FindWithTag("Player");
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

        if (DialogueUI.Instance == null || DialogueUI.Instance.IsOpen) return; // 会話中は重ねない
        if (MenuShopUI.Instance != null && MenuShopUI.Instance.IsOpen) return;  // メニュー表示中も無視

        DialogueUI.Instance.ShowLines(charName, PickMonologue(), null, transform);
    }

    /// <summary>ランダムに1本（直前と同じ本は引き直し）</summary>
    private string[] PickMonologue()
    {
        int i = Random.Range(0, Monologues.Length);
        if (Monologues.Length > 1 && i == _lastIndex)
            i = (i + 1 + Random.Range(0, Monologues.Length - 1)) % Monologues.Length; // 直前以外から等確率
        _lastIndex = i;
        return Monologues[i];
    }

    private static bool WasPrimaryClick()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }
}
