using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プリセットテーマ1つ分のデザイントークン（ScriptableObject）。
/// UITheme_FocusMode の色は全てここから供給される（ファサード経由）。
/// アセットは Assets/Resources/Themes/ に置き、ThemeManager がロードする。
/// </summary>
[CreateAssetMenu(fileName = "NewTheme", menuName = "TOKYO CORNER/Theme Asset")]
public class ThemeAsset : ScriptableObject
{
    [Header("識別")]
    public string themeId      = "classic_dark";
    public string displayName  = "クラシック";
    public bool   isDark       = true;

    [Header("背景")]
    public Color dashboardBG;
    public Color panelBG;
    public Color subPanelBG;
    public Color inputBG;
    public Color floatBG;
    public Color dayCellBG;

    [Header("テキスト")]
    public Color textPrimary;
    public Color textSecondary;
    public Color textBody;
    public Color textMuted;
    public Color textCaption;
    public Color textPlaceholder;
    public Color textDisabled;

    [Header("ボーダー")]
    public Color borderPanel;
    public Color borderInput;
    public Color borderFloat;
    public Color borderDivider;
    public Color borderSubtle;

    [Header("アクセント")]
    public Color accentMain;     // 旧 AccentBlue
    public Color accentFaint;    // 旧 AccentBlueFaint
    public Color accentSolid;    // 旧 AccentBlueSolid
    public Color accentDanger;   // 旧 AccentRed
    public Color accentStrong;   // 旧 AccentSatBlue

    [Header("状態")]
    public Color hoverBG;
    public Color selectedBG;
    public Color doneBG;

    [Header("影")]
    public Color   shadowColor  = new Color(0f, 0f, 0f, 0.60f);
    public Vector2 shadowOffset = new Vector2(0f, -8f);

    [Header("タグ色（Phase 2 で TagConfig と統合予定・現状未使用）")]
    public List<TagColorEntry> tagColors = new List<TagColorEntry>();

    [System.Serializable]
    public class TagColorEntry
    {
        public string tagId;
        public Color  chipBG;
        public Color  chipBorder;
    }
}
