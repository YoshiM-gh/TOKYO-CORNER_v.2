using UnityEngine;
using System.IO;

[System.Serializable]
public class SaveData
{
    public float totalSessionSeconds = 0f;
    public string lastPlayedDate = "";
    public int coins = 100;
    public string lastCoinDate = "";
    public int stampCount = 0;
    public int totalCards = 0;
}

public class SaveDataManager : MonoBehaviour
{
    public static SaveDataManager Instance { get; private set; }

    private const int DailyCoins = 100;
    private const int StampsPerCard = 10;

    private SaveData saveData = new SaveData();
    private string savePath;

    private void Awake()
    {
        Instance = this;
        savePath = Path.Combine(Application.persistentDataPath, "savedata.json");
        Load();
        TryGrantDailyCoins();
        Debug.Log($"[SaveData] Coins: {saveData.coins}C | Stamps: {saveData.stampCount}/{StampsPerCard} | Cards: {saveData.totalCards} | Last played: {saveData.lastPlayedDate}");
    }

    // セッション時間を記録
    public void AddSessionSeconds(float seconds)
    {
        saveData.totalSessionSeconds += seconds;
        saveData.lastPlayedDate = Today();
        Save();
    }

    // フード購入：コインを消費してスタンプを付与
    public bool PurchaseItem(int cost)
    {
        if (saveData.coins < cost) return false;

        saveData.coins -= cost;
        saveData.stampCount++;

        if (saveData.stampCount >= StampsPerCard)
        {
            saveData.stampCount -= StampsPerCard;
            saveData.totalCards++;
        }

        Save();
        return true;
    }

    public int GetCoins() => saveData.coins;
    public int GetStampCount() => saveData.stampCount;
    public int GetTotalCards() => saveData.totalCards;
    public float GetTotalSessionSeconds() => saveData.totalSessionSeconds;

    // 毎日100C付与
    private void TryGrantDailyCoins()
    {
        if (saveData.lastCoinDate == Today()) return;
        saveData.coins += DailyCoins;
        saveData.lastCoinDate = Today();
        Save();
    }

    private string Today() => System.DateTime.Now.ToString("yyyy-MM-dd");

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
