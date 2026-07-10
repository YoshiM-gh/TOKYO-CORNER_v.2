using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// セッション限定のフード在庫管理。シーンをまたいで保持するが、アプリ終了で消える。
/// DrinkInventory と対称の設計（3b）。各フードが menuId / 表示名 / 一口数(portions) を持つ。
/// 一皿制（MaxFoods=1）: 食べ切る（または捨てる）まで次は買えない。
/// ※DrinkInventory と同じ GameObject に同居可（シングルトン重複時は Destroy(this) のみ）。
/// </summary>
public class FoodItem
{
    public string menuId;
    public string displayName;
    public System.DateTime purchasedAt;
    public int bitesMax;
    public int bitesRemaining;

    public FoodItem(string menuId, string displayName, int portions)
    {
        this.menuId = menuId;
        this.displayName = displayName;
        purchasedAt = System.DateTime.Now;
        bitesMax = Mathf.Max(1, portions);
        bitesRemaining = bitesMax;
    }

    /// <summary>残り口数を [*][*][ ][ ] 形式で返す</summary>
    public string BiteDots()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < bitesMax; i++)
            sb.Append(i < bitesRemaining ? "[*]" : "[ ]");
        return sb.ToString();
    }

    public string PurchasedTimeLabel() =>
        purchasedAt.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

public class FoodInventory : MonoBehaviour
{
    public static FoodInventory Instance { get; private set; }

    public const int MaxFoods = 1; // 一皿ずつ（食べ切ってから次を購入）

    private readonly List<FoodItem> foods = new();

    public event System.Action OnFoodsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null); // DDOLはルート必須
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit() => ClearAll();

    private void OnApplicationPause(bool pause)
    {
#if !UNITY_EDITOR
        if (pause) ClearAll();
#endif
    }

    public void ClearAll()
    {
        if (foods.Count == 0) return;
        Debug.Log($"[Food] Session ended - discarding {foods.Count} food(s).");
        foods.Clear();
        OnFoodsChanged?.Invoke();
    }

    // ── 購入 ──────────────────────────────────────────

    public bool CanPurchase() => foods.Count < MaxFoods;

    /// <summary>フードを1つ追加（メニュー個体）。満杯なら false。</summary>
    public bool AddFood(string menuId, string displayName, int portions)
    {
        if (!CanPurchase()) return false;
        foods.Add(new FoodItem(menuId, displayName, portions));
        Debug.Log($"[Food] Purchased '{displayName}' ({portions} bites). Inventory: {foods.Count}/{MaxFoods}");
        OnFoodsChanged?.Invoke();
        return true;
    }

    // ── 食べる ────────────────────────────────────────

    public bool HasAnyBite() => TotalBitesRemaining() > 0;

    /// <summary>直近のTakeBiteで選ばれた一言（呼び出し側が会話UIで表示）</summary>
    public string LastBiteLine { get; private set; } = "";

    /// <summary>古いフードから1口消費する。食べ切ったらリストから削除。</summary>
    public bool TakeBite()
    {
        for (int i = 0; i < foods.Count; i++)
        {
            if (foods[i].bitesRemaining <= 0) continue;
            foods[i].bitesRemaining--;
            int left = foods[i].bitesRemaining;
            // 一口=「美味しい」の一言 / 最後の一口=満腹で幸せな一言（オープニングで登録・未設定は既定値）
            string line = SaveDataManager.Instance == null ? "……うまい。"
                : (left == 0 ? SaveDataManager.Instance.FullLine : SaveDataManager.Instance.TastyLine);
            LastBiteLine = line;
            Debug.Log($"[Food] \"{line}\" | {foods[i].displayName}: {left}/{foods[i].bitesMax} bites left.");
            if (left == 0)
            {
                Debug.Log($"[Food] '{foods[i].displayName}' finished and removed.");
                foods.RemoveAt(i);
            }
            OnFoodsChanged?.Invoke();
            return true;
        }
        Debug.Log("[Food] No foods available to bite.");
        return false;
    }

    // ── 捨てる ────────────────────────────────────────

    public List<FoodItem> GetFoods() => foods;

    /// <summary>指定インデックスのフードを捨てる。</summary>
    public bool DiscardFood(int index)
    {
        if (index < 0 || index >= foods.Count) return false;
        var f = foods[index];
        foods.RemoveAt(index);
        Debug.Log($"[Food] Discarded '{f.displayName}' (bought {f.purchasedAt:HH:mm}, {f.bitesRemaining} bites left). Inventory: {foods.Count}/{MaxFoods}");
        OnFoodsChanged?.Invoke();
        return true;
    }

    // ── HUD 用 ────────────────────────────────────────

    public int TotalBitesRemaining()
    {
        int total = 0;
        foreach (var f in foods) total += f.bitesRemaining;
        return total;
    }

    public int TotalBitesMax()
    {
        int total = 0;
        foreach (var f in foods) total += f.bitesMax;
        return total;
    }

    /// <summary>"Food 2/2" 形式の文字列。フードなしは空文字。</summary>
    public string BuildFoodHudText()
    {
        if (foods.Count == 0) return "";
        return $"Food {TotalBitesRemaining()}/{TotalBitesMax()}";
    }
}
