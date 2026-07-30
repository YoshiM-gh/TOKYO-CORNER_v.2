using UnityEngine;
using System.IO;
using System.Globalization;
using System.Text;
using System.Collections.Generic;

[System.Serializable]
public class MenuCountEntry
{
    public string menuId;
    public int count;
}

[System.Serializable]
public class SaveData
{
    /// <summary>旧データ移行用。以前はフォーカス時間のみ加算。</summary>
    public float totalSessionSeconds = 0f;
    public string lastPlayedDate = "";
    public int coins = 0;
    public string lastCoinDate = "";
    public int stampCount = 0;
    public int totalCards = 0;

    // ── オープニング（ナギの登録面接）──
    public bool openingDone = false;
    public string playerName = "";        // ローカルのみ・いつでも変更可
    public bool specialDrinkHot = true;    // コーナースペシャル〈ドリンク〉の温度
    public string drinkLine = "";          // 「美味しい」の一言（空=既定値）
    public string foodLine = "";           // 満腹で幸せな一言（空=既定値）
    public int guestNumber = 0;            // 仮ID（プロト:ローカル採番・初期化で振り直し）
    public string avatarId = "";          // 選んだ見た目（ithappyプレハブ名・空=既定の23）

    public float cumulativeRoamingSeconds = 0f;
    public float cumulativeFocusSeconds = 0f;
    public string todayStatsDate = "";
    public float todayRoamingSeconds = 0f;
    public float todayFocusSeconds = 0f;

    public MenuCountEntry[] menuPurchases = new MenuCountEntry[0];

    /// <summary>コーナースペシャル〈ドリンク〉のプレイヤー命名（空=未命名）。オープニングで設定。</summary>
    public string playerDrinkName = "";
    /// <summary>コーナースペシャル〈フード〉のプレイヤー命名（空=未命名）。オープニングで設定。</summary>
    public string playerFoodName = "";
}

public class SaveDataManager : MonoBehaviour
{
    public static SaveDataManager Instance { get; private set; }

    public event System.Action OnStatsChanged;

    public const int DailyLoginCoins = 100;
    /// <summary>コイン所持上限。加算処理は必ずこれでクランプすること。</summary>
    public const int MaxCoins = 9999;
    public const int MvpDrinkPrice = 100;
    /// <summary>セーブ用ID（ASCII）。表示は <see cref="FormatMenuLine"/> で英語ラベルに。</summary>
    public const string MenuIdMvpDrink = "mvp_drink";
    private const int StampsPerCard = 10;

    private SaveData saveData = new SaveData();
    private string savePath;
    private float playtimeSaveTimer;
    private System.DateTime lastRealtimeUtc;
    private bool hasRealtimeSample;
    private GameModeManager.GameMode modeAtLastSample = GameModeManager.GameMode.Roaming;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; } // 重複ガード（シーン切替で生き残る本体を1つに）
        Instance = this;
        if (transform.parent != null) transform.SetParent(null); // DDOLはルート必須。子オブジェクトだと失敗するので親から切り離す
        DontDestroyOnLoad(gameObject); // ★ シーン切替（Cafe↔UI_Prototype）をまたいでコイン/セーブを保持
        savePath = Path.Combine(Application.persistentDataPath, "savedata.json");
        Load();
        MigrateLegacyPlaytime();
        MigrateLegacyMenuIds();
        NormalizeTodayStatsDate();
        TryGrantDailyCoins();
        Debug.Log($"[SaveData] Coins: {saveData.coins}C | Stamps: {saveData.stampCount}/{StampsPerCard} | Cards: {saveData.totalCards} | Daily: {saveData.lastCoinDate}");
    }

    private void Update()
    {
        TickPlaytime();
    }

    private void OnApplicationPause(bool pause)
    {
        // 一時停止前後で実時間差分を反映することで、バックグラウンド滞在も計測する
        TickPlaytime();
        if (pause) Save();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        TickPlaytime();
        if (!hasFocus) Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void MigrateLegacyPlaytime()
    {
        if (saveData.cumulativeFocusSeconds <= 0f && saveData.totalSessionSeconds > 0f)
            saveData.cumulativeFocusSeconds = saveData.totalSessionSeconds;
        if (string.IsNullOrEmpty(saveData.todayStatsDate))
            saveData.todayStatsDate = TodayString();
    }

    /// <summary>旧ID「ドリンク」を mvp_drink に統合（日本語非対応フォントのHUD対策と併せた整理）。</summary>
    private void MigrateLegacyMenuIds()
    {
        if (saveData.menuPurchases == null || saveData.menuPurchases.Length == 0) return;

        int legacy = 0;
        var kept = new List<MenuCountEntry>();
        foreach (var e in saveData.menuPurchases)
        {
            if (e == null) continue;
            if (e.menuId == "ドリンク")
                legacy += e.count;
            else
                kept.Add(new MenuCountEntry { menuId = e.menuId, count = e.count });
        }

        if (legacy <= 0) return;

        bool merged = false;
        for (int i = 0; i < kept.Count; i++)
        {
            if (kept[i].menuId != MenuIdMvpDrink) continue;
            kept[i] = new MenuCountEntry { menuId = MenuIdMvpDrink, count = kept[i].count + legacy };
            merged = true;
            break;
        }

        if (!merged)
            kept.Add(new MenuCountEntry { menuId = MenuIdMvpDrink, count = legacy });

        saveData.menuPurchases = kept.ToArray();
        Save();
    }

    private void NormalizeTodayStatsDate()
    {
        string t = TodayString();
        if (saveData.todayStatsDate == t) return;
        saveData.todayRoamingSeconds = 0f;
        saveData.todayFocusSeconds = 0f;
        saveData.todayStatsDate = t;
        Save();
    }

    private void TickPlaytime()
    {
        NormalizeTodayStatsDate();
        var nowUtc = System.DateTime.UtcNow;
        var currentMode = GetCurrentMode();
        if (!hasRealtimeSample)
        {
            lastRealtimeUtc = nowUtc;
            modeAtLastSample = currentMode;
            hasRealtimeSample = true;
            return;
        }

        float elapsedSeconds = (float)(nowUtc - lastRealtimeUtc).TotalSeconds;
        if (elapsedSeconds < 0f) elapsedSeconds = 0f;

        if (elapsedSeconds > 0f)
        {
            if (modeAtLastSample == GameModeManager.GameMode.Roaming)
            {
                saveData.todayRoamingSeconds += elapsedSeconds;
                saveData.cumulativeRoamingSeconds += elapsedSeconds;
            }
            else
            {
                saveData.todayFocusSeconds += elapsedSeconds;
                saveData.cumulativeFocusSeconds += elapsedSeconds;
            }

            saveData.lastPlayedDate = TodayString();
            playtimeSaveTimer += elapsedSeconds;
            if (playtimeSaveTimer >= 4f)
            {
                playtimeSaveTimer = 0f;
                Save();
            }
        }

        lastRealtimeUtc = nowUtc;
        modeAtLastSample = currentMode;
    }

    /// <summary>
    /// 現在のモード。**シーンで判定する**（2026-07-27修正）。
    /// 旧実装は GameModeManager.Instance を見ていたが、フォーカスは別シーンになり
    /// そこには GameModeManager が居ないため Instance が null → 直前の値(Roaming)を返し続け、
    /// **集中時間がすべて散策時間に計上されていた**。
    /// </summary>
    private GameModeManager.GameMode GetCurrentMode()
    {
        return SceneRouter.IsFocusScene
            ? GameModeManager.GameMode.Focus
            : GameModeManager.GameMode.Roaming;
    }

    public bool TryPurchaseMvpDrink()
    {
        if (!PurchaseItem(MvpDrinkPrice)) return false;
        AddMenuPurchase(MenuIdMvpDrink);
        return true;
    }

    /// <summary>メニュー定義から購入（コイン減算＋スタンプ＋購入履歴）。</summary>
    public bool TryPurchase(MenuItemDef def)
    {
        if (def == null) return false;
        if (!PurchaseItem(def.price)) return false;
        AddMenuPurchase(def.id);
        return true;
    }

    public bool PurchaseItem(int cost)
    {
        if (saveData.coins < cost) return false;

        saveData.coins -= cost;
        saveData.stampCount++;

        if (saveData.stampCount >= StampsPerCard)
        {
            saveData.stampCount -= StampsPerCard;
            saveData.totalCards++;
            Debug.Log($"[Stamp] Card complete! Total cards: {saveData.totalCards} | Coins left: {saveData.coins}C");
        }
        else
        {
            Debug.Log($"[Stamp] Stamp added: {saveData.stampCount}/{StampsPerCard} | Coins left: {saveData.coins}C");
        }

        Save();
        OnStatsChanged?.Invoke();
        return true;
    }

    private void AddMenuPurchase(string menuId)
    {
        var list = new List<MenuCountEntry>();
        if (saveData.menuPurchases != null)
        {
            foreach (var e in saveData.menuPurchases)
                list.Add(new MenuCountEntry { menuId = e.menuId, count = e.count });
        }

        bool found = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].menuId != menuId) continue;
            list[i] = new MenuCountEntry { menuId = menuId, count = list[i].count + 1 };
            found = true;
            break;
        }

        if (!found)
            list.Add(new MenuCountEntry { menuId = menuId, count = 1 });

        saveData.menuPurchases = list.ToArray();
        Save();
    }

    // ── メニュー命名（コーナースペシャル） ──────────────

    /// <summary>プレイヤー命名スロットの名前（未命名は空文字）。</summary>
    public string GetPlayerMenuName(MenuCategory category) =>
        category == MenuCategory.Drink ? saveData.playerDrinkName : saveData.playerFoodName;

    public void SetPlayerMenuName(MenuCategory category, string name)
    {
        if (category == MenuCategory.Drink) saveData.playerDrinkName = name ?? "";
        else saveData.playerFoodName = name ?? "";
        Save();
    }

    /// <summary>表示名解決。命名スロットで命名済みならその名前、それ以外はマスターの displayName。</summary>
    public string ResolveDisplayName(MenuItemDef def)
    {
        if (def == null) return "";
        if (def.isPlayerNamed)
        {
            string custom = GetPlayerMenuName(def.category);
            if (!string.IsNullOrEmpty(custom)) return custom;
        }
        return def.displayName;
    }

    public int GetCoins() => saveData.coins;
    public int GetStampCount() => saveData.stampCount;
    public int GetTotalCards() => saveData.totalCards;
    public static int GetStampsPerCard() => StampsPerCard;
    public float GetTotalSessionSeconds() =>
        saveData.cumulativeRoamingSeconds + saveData.cumulativeFocusSeconds;

    public bool HasClaimedDailyBonusToday() => saveData.lastCoinDate == TodayString();
    public string GetLastDailyBonusDate() => saveData.lastCoinDate;

    /// <summary>
    /// HUD用。デフォルトTMPフォント（LiberationSans）は日本語非対応のため、ラベルはASCIIのみ。
    /// 日本語表示する場合は StatsHud の Font Asset を Noto Sans JP 等に差し替え、このメソッドを書き換え。
    /// </summary>
    public string BuildHudStatsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Free / Focus]");
        sb.AppendLine($"Coins: {saveData.coins}C");
        sb.AppendLine();
        sb.AppendLine("[Stay time]");
        sb.AppendLine(
            $"Today  All: {FormatDuration(saveData.todayRoamingSeconds + saveData.todayFocusSeconds)}  Focus: {FormatDuration(saveData.todayFocusSeconds)}");
        sb.AppendLine(
            $"Total  All: {FormatDuration(saveData.cumulativeRoamingSeconds + saveData.cumulativeFocusSeconds)}  Focus: {FormatDuration(saveData.cumulativeFocusSeconds)}");
        sb.AppendLine();
        sb.AppendLine("[Focus only]");
        sb.AppendLine($"Stamps: {saveData.stampCount}/{StampsPerCard}  Cards: {saveData.totalCards}  (10 stamps -> +1 card)");
        sb.AppendLine("Menu:");
        if (saveData.menuPurchases == null || saveData.menuPurchases.Length == 0)
            sb.AppendLine("  (none)");
        else
        {
            foreach (var e in saveData.menuPurchases)
            {
                if (e == null) continue;
                sb.AppendLine($"  {FormatMenuLine(e.menuId, e.count)}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatMenuLine(string menuId, int count)
    {
        string label = menuId switch
        {
            MenuIdMvpDrink => "Drink",
            _ => menuId
        };
        return $"{label} x{count}";
    }

    private static string FormatDuration(float seconds)
    {
        int s = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        int h = s / 3600;
        int m = (s % 3600) / 60;
        int sec = s % 60;
        if (h > 0)
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", h, m, sec);
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", m, sec);
    }

    private void TryGrantDailyCoins()
    {
        if (saveData.lastCoinDate == TodayString())
        {
            Debug.Log($"[Login] Daily bonus already claimed today ({saveData.lastCoinDate}). Coins: {saveData.coins}C");
            return;
        }
        saveData.coins = Mathf.Min(saveData.coins + DailyLoginCoins, MaxCoins);
        saveData.lastCoinDate = TodayString();
        Save();
        OnStatsChanged?.Invoke();
        Debug.Log($"[Login] Daily bonus granted: +{DailyLoginCoins}C → Total: {saveData.coins}C");
    }

    private static string TodayString() =>
        System.DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ── オープニング ─────────────────────────────
    public bool IsOpeningDone => saveData.openingDone;
    public string PlayerName => saveData.playerName;
    /// <summary>「美味しい」の一言（未設定なら既定値）</summary>
    public string DrinkLine => string.IsNullOrEmpty(saveData.drinkLine) ? "……うまい。" : saveData.drinkLine;
    /// <summary>満腹で幸せな一言（未設定なら既定値）</summary>
    public string FoodLine => string.IsNullOrEmpty(saveData.foodLine) ? "……うまい。" : saveData.foodLine;

    public string AvatarId => saveData.avatarId;

    /// <summary>見た目（アバター）を保存。オープニング/将来の設定画面から呼ぶ</summary>
    public void SetAvatar(string prefabName)
    {
        saveData.avatarId = prefabName ?? "";
        Save();
    }

    /// <summary>オープニング完了を保存し、仮ID（会員番号）を返す</summary>
    public int CompleteOpening(string playerName, bool drinkHot, string drinkLine, string foodLine)
    {
        saveData.playerName = playerName ?? "";
        saveData.specialDrinkHot = drinkHot;
        saveData.drinkLine = drinkLine ?? "";
        saveData.foodLine = foodLine ?? "";
        if (saveData.guestNumber <= 0) saveData.guestNumber = UnityEngine.Random.Range(1000, 10000); // 仮ID採番
        saveData.openingDone = true;
        Save();
        OnStatsChanged?.Invoke();
        return saveData.guestNumber;
    }

    private void Save()
    {
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(savePath, json);
    }

    private void Load()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            saveData = JsonUtility.FromJson<SaveData>(json);
        }
    }
}
