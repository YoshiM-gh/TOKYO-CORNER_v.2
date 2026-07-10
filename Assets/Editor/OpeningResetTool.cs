using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 開発用: オープニング（ナギの登録面接）のやり直しリセット。
/// Tools > TOKYO CORNER > オープニングをリセット
/// openingDone/名前/一言2つ/仮ID/コーナースペシャル命名をクリアする。
/// コイン・スタンプ・購入履歴などは温存（全消去ではない）。
/// </summary>
public static class OpeningResetTool
{
    [MenuItem("Tools/TOKYO CORNER/オープニングをリセット")]
    private static void ResetOpening()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[OpeningReset] Play中は実行できません。停止してから実行してください。");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, "savedata.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[OpeningReset] savedata.json がありません: " + path);
            return;
        }
        string json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<SaveData>(json);
        data.openingDone = false;
        data.playerName = "";
        data.tastyLine = "";
        data.fullLine = "";
        data.guestNumber = 0;        // 再採番させる
        data.specialDrinkHot = true; // 初期値へ
        data.playerDrinkName = "";  // コーナースペシャル命名クリア
        data.playerFoodName = "";
        File.WriteAllText(path, JsonUtility.ToJson(data, true), new System.Text.UTF8Encoding(false));
        Debug.Log("[OpeningReset] 完了。次のPlay(Cafe)でオープニングへ飛びます。");
    }
}
