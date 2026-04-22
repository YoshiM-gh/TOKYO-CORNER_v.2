using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// セッション限定のドリンク在庫管理。シーンをまたいで保持するが、アプリ終了で消える。
/// 購入時刻と残り口数を持ち、古い順に消費する。
/// </summary>
public class DrinkItem
{
    public System.DateTime purchasedAt;
    public int sipsRemaining;

    public DrinkItem()
    {
        purchasedAt = System.DateTime.Now;
        sipsRemaining = DrinkInventory.SipsPerDrink;
    }

    /// <summary>残り口数を [*][*][_][_] 形式で返す</summary>
    public string SipDots()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < DrinkInventory.SipsPerDrink; i++)
            sb.Append(i < sipsRemaining ? "[*]" : "[ ]");
        return sb.ToString();
    }

    public string PurchasedTimeLabel() =>
        purchasedAt.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

public class DrinkInventory : MonoBehaviour
{
    public static DrinkInventory Instance { get; private set; }

    public const int MaxDrinks = 4;
    public const int SipsPerDrink = 4;

    private readonly List<DrinkItem> drinks = new();

    public event System.Action OnDrinksChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (GameModeManager.Instance == null) return;
        if (GameModeManager.Instance.CurrentMode != GameModeManager.GameMode.Focus) return;

        bool fPressed = Input.GetKeyDown(KeyCode.F) ||
                        (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
        if (fPressed) TakeSip();
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

    /// <summary>ドリンクを1つ追加。満杯なら false。</summary>
    public bool AddDrink()
    {
        if (!CanPurchase()) return false;
        drinks.Add(new DrinkItem());
        Debug.Log($"[Drink] Purchased. Inventory: {drinks.Count}/{MaxDrinks}");
        OnDrinksChanged?.Invoke();
        return true;
    }

    // ── 飲む ──────────────────────────────────────────

    public bool HasAnySip() => TotalSipsRemaining() > 0;

    private const string SipMessage = "この一口がいつだって俺を奮い立たせてくれるんだ";

    /// <summary>古いドリンクから1口消費する。飲み切ったらリストから削除。</summary>
    public bool TakeSip()
    {
        for (int i = 0; i < drinks.Count; i++)
        {
            if (drinks[i].sipsRemaining <= 0) continue;
            drinks[i].sipsRemaining--;
            int left = drinks[i].sipsRemaining;
            Debug.Log($"[Drink] \"{SipMessage}\" | Drink #{i + 1}: {left}/{SipsPerDrink} sips left.");
            if (left == 0)
            {
                drinks.RemoveAt(i);
                Debug.Log($"[Drink] Drink finished and removed. Inventory: {drinks.Count}/{MaxDrinks}");
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
        Debug.Log($"[Drink] Discarded drink #{index + 1} (bought {d.purchasedAt:HH:mm}, {d.sipsRemaining} sips left). Inventory: {drinks.Count}/{MaxDrinks}");
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

    public int TotalSipsMax() => drinks.Count * SipsPerDrink;

    /// <summary>"Drink 3/4" 形式の文字列。ドリンクなしは空文字。</summary>
    public string BuildDrinkHudText()
    {
        if (drinks.Count == 0) return "";
        return $"Drink {TotalSipsRemaining()}/{TotalSipsMax()}";
    }
}
