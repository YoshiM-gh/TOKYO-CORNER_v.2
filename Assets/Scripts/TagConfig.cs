using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// タグの定義を一元管理。
/// Configから書き換えることで全UI に反映される。
/// 最大 MAX_TAGS 個まで登録可能。
/// </summary>
[System.Serializable]
public class TagDefinition
{
    public string id;           // 内部識別子（変更しない）
    public string displayName;  // 表示名（Configで変更可）

    // チップ色（カレンダーの予定ブロック・週タイムライン）
    public Color chipBG;        // 例: rgba(80,140,255,0.45)
    public Color chipBorder;    // 例: rgba(80,140,255,0.90)

    // タグバッジ色（管理リストの左ライン・バッジ）
    public Color barColor;      // 例: rgba(80,140,255,0.80)
    public Color badgeBG;       // 例: rgba(80,140,255,0.18)
    public Color badgeText;     // 例: rgba(160,200,255,0.90)

    // タグ選択ボタン色（フォーム内のタグ選択UI）
    public Color selectorBG;    // 例: rgba(80,140,255,0.20)
    public Color selectorBorder;// 例: rgba(80,140,255,0.50)
    public Color selectorText;  // 例: rgba(160,200,255,0.90)
}

public static class TagConfig
{
    public const int MAX_TAGS = 10;

    /// <summary>デフォルトのタグ定義（4種）</summary>
    public static List<TagDefinition> Tags = new List<TagDefinition>
    {
        new TagDefinition
        {
            id          = "habit",
            displayName = "習慣",
            chipBG      = new Color(0.627f, 0.392f, 1.000f, 0.40f),
            chipBorder  = new Color(0.627f, 0.392f, 1.000f, 0.90f),
            barColor    = new Color(0.627f, 0.392f, 1.000f, 0.80f),
            badgeBG     = new Color(0.627f, 0.392f, 1.000f, 0.18f),
            badgeText   = new Color(0.784f, 0.627f, 1.000f, 0.90f),
            selectorBG     = new Color(0.627f, 0.392f, 1.000f, 0.20f),
            selectorBorder = new Color(0.627f, 0.392f, 1.000f, 0.50f),
            selectorText   = new Color(0.784f, 0.627f, 1.000f, 0.90f),
        },
        new TagDefinition
        {
            id          = "yotei",
            displayName = "予定",
            chipBG      = new Color(0.314f, 0.549f, 1.000f, 0.45f),
            chipBorder  = new Color(0.314f, 0.549f, 1.000f, 0.90f),
            barColor    = new Color(0.314f, 0.549f, 1.000f, 0.80f),
            badgeBG     = new Color(0.314f, 0.549f, 1.000f, 0.18f),
            badgeText   = new Color(0.627f, 0.784f, 1.000f, 0.90f),
            selectorBG     = new Color(0.314f, 0.549f, 1.000f, 0.20f),
            selectorBorder = new Color(0.314f, 0.549f, 1.000f, 0.50f),
            selectorText   = new Color(0.627f, 0.784f, 1.000f, 0.90f),
        },
        new TagDefinition
        {
            id          = "mokuhyo",
            displayName = "目標",
            chipBG      = new Color(0.863f, 0.627f, 0.235f, 0.40f),
            chipBorder  = new Color(0.863f, 0.627f, 0.235f, 0.90f),
            barColor    = new Color(0.863f, 0.627f, 0.235f, 0.80f),
            badgeBG     = new Color(0.863f, 0.627f, 0.235f, 0.18f),
            badgeText   = new Color(1.000f, 0.824f, 0.510f, 0.90f),
            selectorBG     = new Color(0.863f, 0.627f, 0.235f, 0.20f),
            selectorBorder = new Color(0.863f, 0.627f, 0.235f, 0.50f),
            selectorText   = new Color(1.000f, 0.824f, 0.510f, 0.90f),
        },
        new TagDefinition
        {
            id          = "todo",
            displayName = "Todo",
            chipBG      = new Color(0.235f, 0.706f, 0.471f, 0.40f),
            chipBorder  = new Color(0.235f, 0.706f, 0.471f, 0.90f),
            barColor    = new Color(0.235f, 0.706f, 0.471f, 0.80f),
            badgeBG     = new Color(0.235f, 0.706f, 0.471f, 0.18f),
            badgeText   = new Color(0.510f, 0.902f, 0.667f, 0.90f),
            selectorBG     = new Color(0.235f, 0.706f, 0.471f, 0.20f),
            selectorBorder = new Color(0.235f, 0.706f, 0.471f, 0.50f),
            selectorText   = new Color(0.510f, 0.902f, 0.667f, 0.90f),
        },
    };

    /// <summary>idからTagDefinitionを取得</summary>
    public static TagDefinition GetById(string id)
        => Tags.Find(t => t.id == id);

    /// <summary>displayNameからTagDefinitionを取得</summary>
    public static TagDefinition GetByName(string name)
        => Tags.Find(t => t.displayName == name);

    /// <summary>タグを追加（MAX_TAGS未満の場合のみ）</summary>
    public static bool AddTag(TagDefinition tag)
    {
        if (Tags.Count >= MAX_TAGS) return false;
        Tags.Add(tag);
        return true;
    }

    /// <summary>タグを削除（idで指定）</summary>
    public static bool RemoveTag(string id)
    {
        var tag = GetById(id);
        if (tag == null) return false;
        Tags.Remove(tag);
        return true;
    }
}
