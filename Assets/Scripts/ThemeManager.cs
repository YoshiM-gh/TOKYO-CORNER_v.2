using UnityEngine;

/// <summary>
/// 現在テーマの保持と切替。
/// ・テーマアセットは Resources/Themes/ からロード
/// ・SetTheme() で切替 → UITheme_FocusMode.OnThemeChanged が発火し全 UI が追従
/// ・アセットが無い環境でも動くよう、レガシー定数と同値のフォールバックを内蔵
/// </summary>
public static class ThemeManager
{
    public const string DefaultThemeId = "classic_dark";
    private const string ResourcesDir  = "Themes/";

    private static ThemeAsset _current;

    /// <summary>現在のテーマ（未設定ならデフォルトをロード）</summary>
    public static ThemeAsset Current
    {
        get
        {
            if (_current == null) _current = LoadById(DefaultThemeId) ?? CreateFallback();
            return _current;
        }
    }

    /// <summary>テーマを直接切替（設定画面・デバッグ用）</summary>
    public static void SetTheme(ThemeAsset theme)
    {
        if (theme == null || _current == theme) return;
        _current = theme;
        UITheme_FocusMode.RaiseThemeChanged();
    }

    /// <summary>themeId で切替（Resources/Themes/ 内のアセット名と一致させる運用）</summary>
    public static bool SetTheme(string themeId)
    {
        var t = LoadById(themeId);
        if (t == null) return false;
        SetTheme(t);
        return true;
    }

    /// <summary>登録済みテーマ一覧（設定画面のプリセットグリッド用）</summary>
    public static ThemeAsset[] GetAllThemes() =>
        Resources.LoadAll<ThemeAsset>("Themes");

    private static ThemeAsset LoadById(string id)
    {
        // アセット名 = themeId の規約。違っても全ロードから themeId 照合で救済
        var direct = Resources.Load<ThemeAsset>(ResourcesDir + id);
        if (direct != null) return direct;
        foreach (var t in Resources.LoadAll<ThemeAsset>("Themes"))
            if (t.themeId == id) return t;
        return null;
    }

    /// <summary>アセット不在時のフォールバック（レガシー UITheme_FocusMode と同値）</summary>
    private static ThemeAsset CreateFallback()
    {
        var t = ScriptableObject.CreateInstance<ThemeAsset>();
        t.themeId = "fallback"; t.displayName = "Fallback"; t.isDark = true;
        t.dashboardBG  = new Color(0.039f, 0.039f, 0.055f, 0.97f);
        t.panelBG      = new Color(1f, 1f, 1f, 0.04f);
        t.subPanelBG   = new Color(1f, 1f, 1f, 0.02f);
        t.inputBG      = new Color(1f, 1f, 1f, 0.055f);
        t.floatBG      = new Color(0.071f, 0.071f, 0.086f, 0.98f);
        t.dayCellBG    = new Color(1f, 1f, 1f, 0.025f);
        t.textPrimary     = new Color(1f, 1f, 1f, 1.00f);
        t.textSecondary   = new Color(1f, 1f, 1f, 0.82f);
        t.textBody        = new Color(1f, 1f, 1f, 0.65f);
        t.textMuted       = new Color(1f, 1f, 1f, 0.35f);
        t.textCaption     = new Color(1f, 1f, 1f, 0.28f);
        t.textPlaceholder = new Color(1f, 1f, 1f, 0.22f);
        t.textDisabled    = new Color(1f, 1f, 1f, 0.18f);
        t.borderPanel   = new Color(1f, 1f, 1f, 0.08f);
        t.borderInput   = new Color(1f, 1f, 1f, 0.09f);
        t.borderFloat   = new Color(1f, 1f, 1f, 0.13f);
        t.borderDivider = new Color(1f, 1f, 1f, 0.07f);
        t.borderSubtle  = new Color(1f, 1f, 1f, 0.05f);
        t.accentMain   = new Color(0.314f, 0.549f, 1.0f, 0.50f);
        t.accentFaint  = new Color(0.314f, 0.549f, 1.0f, 0.18f);
        t.accentSolid  = new Color(0.314f, 0.549f, 1.0f, 0.75f);
        t.accentDanger = new Color(1.0f, 0.392f, 0.392f, 0.75f);
        t.accentStrong = new Color(0.431f, 0.608f, 1.0f, 0.75f);
        t.hoverBG    = new Color(1f, 1f, 1f, 0.07f);
        t.selectedBG = new Color(1f, 1f, 1f, 0.09f);
        t.doneBG     = new Color(1f, 1f, 1f, 0.03f);
        return t;
    }
}
