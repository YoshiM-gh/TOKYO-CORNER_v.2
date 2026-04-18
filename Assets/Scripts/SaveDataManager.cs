using UnityEngine;
using System.IO;

[System.Serializable]
public class SaveData
{
    public float totalSessionSeconds = 0f;
    public string lastPlayedDate = "";
}

public class SaveDataManager : MonoBehaviour
{
    public static SaveDataManager Instance { get; private set; }

    private SaveData saveData = new SaveData();
    private string savePath;

    private void Awake()
    {
        Instance = this;
        savePath = Path.Combine(Application.persistentDataPath, "savedata.json");
        Debug.Log("Save path: " + savePath);
        Load();
    }

    public void AddSessionSeconds(float seconds)
    {
        saveData.totalSessionSeconds += seconds;
        saveData.lastPlayedDate = System.DateTime.Now.ToString("yyyy-MM-dd");
        Save();
    }

    public float GetTotalSessionSeconds() => saveData.totalSessionSeconds;
    public string GetLastPlayedDate() => saveData.lastPlayedDate;

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
