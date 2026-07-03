using System.Collections.Generic;
using UnityEngine;

/// <summary>メニューカテゴリ。数値はセーブ互換のため変更禁止。表示動詞の切替に使う（Drink=飲む / Food=食べる）。</summary>
public enum MenuCategory
{
    Drink = 0,
    Food = 1
}

/// <summary>
/// メニュー1品の定義（読み取り専用マスターデータ）。
/// コーナースペシャル（プレイヤー命名スロット）は isPlayerNamed=true とし、
/// 実際の名前は SaveData 側（playerDrinkName / playerFoodName）に保存する。マスターには書かない。
/// </summary>
[System.Serializable]
public class MenuItemDef
{
    [Tooltip("セーブ・購入履歴用ID（ASCII固定・後から変更禁止）")]
    public string id;

    [Tooltip("表示名。isPlayerNamed の場合は未命名時のフォールバック表示")]
    public string displayName;

    public MenuCategory category;

    [Tooltip("価格（コイン）")]
    public int price;

    [Tooltip("一口数（無くなるまでの飲む/食べる回数）")]
    public int portions;

    [Tooltip("プレイヤー命名スロット（コーナースペシャル）か")]
    public bool isPlayerNamed;

    [Tooltip("ホット/アイスの温度トグルを持つか（コーナースタイルコーヒーのみ）")]
    public bool hasTemperature;
}

/// <summary>
/// 全メニューのマスターデータ（SO 1個方式・Assets/Data/MenuDatabase.asset）。
/// 実行時は読み取り専用。プレイヤー由来の可変データ（命名）は SaveData 側に持つ。
/// </summary>
[CreateAssetMenu(fileName = "MenuDatabase", menuName = "TOKYO CORNER/Menu Database")]
public class MenuDatabase : ScriptableObject
{
    [SerializeField] private List<MenuItemDef> items = new();

    public IReadOnlyList<MenuItemDef> Items => items;

    /// <summary>ID で1品取得。無ければ null。</summary>
    public MenuItemDef GetById(string id)
    {
        foreach (var item in items)
            if (item.id == id) return item;
        return null;
    }

    /// <summary>カテゴリで絞り込み（表示順＝リスト順を維持）。</summary>
    public List<MenuItemDef> GetByCategory(MenuCategory category)
    {
        var result = new List<MenuItemDef>();
        foreach (var item in items)
            if (item.category == category) result.Add(item);
        return result;
    }
}
