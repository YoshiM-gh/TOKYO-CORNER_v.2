using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TagDefinition
{
    public string id, displayName;
    public Color chipBG, chipBorder, barColor, badgeBG, badgeText;
    public Color selectorBG, selectorBorder, selectorText;
}

public static class TagConfig
{
    public const int MAX_TAGS = 10;

    // 順番: 目標(赤) → 予定(黄) → Todo(青) → 習慣(緑)
    public static List<TagDefinition> Tags = new List<TagDefinition>
    {
        new TagDefinition { id="mokuhyo", displayName="目標",
            chipBG=new Color(0.86f,0.22f,0.22f,0.45f), chipBorder=new Color(0.86f,0.22f,0.22f,0.90f),
            barColor=new Color(0.86f,0.22f,0.22f,0.80f), badgeBG=new Color(0.86f,0.22f,0.22f,0.18f),
            badgeText=new Color(1f,0.72f,0.72f,0.92f),
            selectorBG=new Color(0.86f,0.22f,0.22f,0.35f), selectorBorder=new Color(0.86f,0.22f,0.22f,0.65f),
            selectorText=new Color(1f,0.82f,0.82f,0.96f) },
        new TagDefinition { id="yotei", displayName="予定",
            chipBG=new Color(0.90f,0.72f,0.10f,0.55f), chipBorder=new Color(0.90f,0.72f,0.10f,0.90f),
            barColor=new Color(0.90f,0.72f,0.10f,0.80f), badgeBG=new Color(0.90f,0.72f,0.10f,0.18f),
            badgeText=new Color(1f,0.94f,0.60f,0.92f),
            selectorBG=new Color(0.90f,0.72f,0.10f,0.35f), selectorBorder=new Color(0.90f,0.72f,0.10f,0.65f),
            selectorText=new Color(0.15f,0.10f,0f,0.96f) },
        new TagDefinition { id="todo", displayName="Todo",
            chipBG=new Color(0.18f,0.52f,0.95f,0.45f), chipBorder=new Color(0.18f,0.52f,0.95f,0.90f),
            barColor=new Color(0.18f,0.52f,0.95f,0.80f), badgeBG=new Color(0.18f,0.52f,0.95f,0.18f),
            badgeText=new Color(0.70f,0.85f,1f,0.92f),
            selectorBG=new Color(0.18f,0.52f,0.95f,0.35f), selectorBorder=new Color(0.18f,0.52f,0.95f,0.65f),
            selectorText=new Color(0.82f,0.92f,1f,0.96f) },
        new TagDefinition { id="habit", displayName="習慣",
            chipBG=new Color(0.12f,0.72f,0.42f,0.45f), chipBorder=new Color(0.12f,0.72f,0.42f,0.90f),
            barColor=new Color(0.12f,0.72f,0.42f,0.80f), badgeBG=new Color(0.12f,0.72f,0.42f,0.18f),
            badgeText=new Color(0.68f,1f,0.82f,0.92f),
            selectorBG=new Color(0.12f,0.72f,0.42f,0.35f), selectorBorder=new Color(0.12f,0.72f,0.42f,0.65f),
            selectorText=new Color(0.82f,1f,0.90f,0.96f) },
        new TagDefinition { id="hobby", displayName="趣味・遊び",
            chipBG=new Color(0.55f,0.20f,0.85f,0.45f), chipBorder=new Color(0.55f,0.20f,0.85f,0.90f),
            barColor=new Color(0.55f,0.20f,0.85f,0.80f), badgeBG=new Color(0.55f,0.20f,0.85f,0.18f),
            badgeText=new Color(0.85f,0.70f,1f,0.92f),
            selectorBG=new Color(0.55f,0.20f,0.85f,0.35f), selectorBorder=new Color(0.55f,0.20f,0.85f,0.65f),
            selectorText=new Color(0.90f,0.80f,1f,0.96f) },
    };

    public static TagDefinition GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Tags.Find(t => t.id == id);
    }
}
