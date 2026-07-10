using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// セッション限定のドリンク在庫管理。シーンをまたいで保持するが、アプリ終了で消える。
/// 3a: メニュー個体化。各ドリンクが menuId / 表示名 / 一口数(portions) を持つ。
/// 購入時刻と残り口数を持ち、古い順に消費する。
/// ※フォーカス中のFキー一口は廃止（ROADMAP確定）。飲むのは着席メニューから。
/// </summary>
public class DrinkItem
{
    public string menuId;
    public string displayName;
    public System.DateTime purchasedAt;
    public int sipsMax;
    public int sipsRemaining;

    public DrinkItem(string menuId, string displayName, int portions)
    {
        this.menuId = menuId;
        this.displayName = displayName;
        purchasedAt = System.DateTime.Now;
        sipsMax = Mathf.Max(1, portions);
        sipsRemaining = sipsMax;
    }

    /// <summary>残り口数を [*][*][ ][ ] 形式で返す</summary>
    public string SipDots()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < sipsMax; i++)
            sb.Append(i < sipsRemaining ? "[*]" : "[ ]");
        return sb.ToString();
    }

    public string PurchasedTimeLabel() =>
        purchasedAt.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

public class DrinkInventory : MonoBehaviour
{
    public static DrinkInventory Instance { get; private set; }

    public const int MaxDrinks = 1; // 1度に1杯まで（飲み切ってから次を購入）

    private readonly List<DrinkItem> drinks = new();

    public event System.Action OnDrinksChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null); // DDOLはルート必須。子オブジェクトだと失敗するので親から切り離す
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
        if (drinks.Count == 0) return;
        Debug.Log($"[Drink] Session ended - discarding {drinks.Count} drink(s).");
        drinks.Clear();
        OnDrinksChanged?.Invoke();
    }

    // ── 購入 ──────────────────────────────────────────

    public bool CanPurchase() => drinks.Count < MaxDrinks;

    /// <summary>ドリンクを1つ追加（メニュー個体）。満杯なら false。</summary>
    public bool AddDrink(string menuId, string displayName, int portions)
    {
        if (!CanPurchase()) return false;
        drinks.Add(new DrinkItem(menuId, displayName, portions));
        Debug.Log($"[Drink] Purchased '{displayName}' ({portions} sips). Inventory: {drinks.Count}/{MaxDrinks}");
        OnDrinksChanged?.Invoke();
        return true;
    }

    // ── 飲む ──────────────────────────────────────────

    public bool HasAnySip() => TotalSipsRemaining() > 0;

    /// <summary>直近のTakeSipで選ばれた一言（呼び出し側が会話UIで表示）</summary>
    public string LastSipLine { get; private set; } = "";

    /// <summary>古いドリンクから1口消費する。飲み切ったらリストから削除。</summary>
    public bool TakeSip()
    {
        for (int i = 0; i < drinks.Count; i++)
        {
            if (drinks[i].sipsRemaining <= 0) continue;
            drinks[i].sipsRemaining--;
            int left = drinks[i].sipsRemaining;
            // 一口=「美味しい」の一言 / 飲み切り=満腹で幸せな一言（オープニングで登録・未設定は既定値）
            string line = SaveDataManager.Instance == null ? "……うまい。"
                : (left == 0 ? SaveDataManager.Instance.FullLine : SaveDataManager.Instance.TastyLine);
            LastSipLine = line;
            Debug.Log($"[Drink] \"{line}\" | {drinks[i].displayName}: {left}/{drinks[i].sipsMax} sips left.");
            if (left == 0)
            {
                Debug.Log($"[Drink] '{drinks[i].displayName}' finished and removed.");
                drinks.RemoveAt(i);
            }
            OnDrinksChanged?.Invoke();
            return true;
        }
        Debug.Log("[Drink] No drinks available to sip.");
        return false;
    }

    // ── 捨てる ────────────────────────────────────────

    public List<DrinkItem> GetDrinks() => drinks;

    /// <summary>指定インデックスのドリンクを捨てる。</summary>
    public bool DiscardDrink(int index)
    {
        if (index < 0 || index >= drinks.Count) return false;
        var d = drinks[index];
        drinks.RemoveAt(index);
        Debug.Log($"[Drink] Discarded '{d.displayName}' (bought {d.purchasedAt:HH:mm}, {d.sipsRemaining} sips left). Inventory: {drinks.Count}/{MaxDrinks}");
        OnDrinksChanged?.Invoke();
        return true;
    }

    // ── HUD 用 ────────────────────────────────────────

    public int TotalSipsRemaining()
    {
        int total = 0;
        foreach (var d in drinks) total += d.sipsRemaining;
        return total;
    }

    public int TotalSipsMax()
    {
        int total = 0;
        foreach (var d in drinks) total += d.sipsMax;
        return total;
    }

    /// <summary>"Drink 3/4" 形式の文字列。ドリンクなしは空文字。</summary>
    public string BuildDrinkHudText()
    {
        if (drinks.Count == 0) return "";
        return $"Drink {TotalSipsRemaining()}/{TotalSipsMax()}";
    }
}
