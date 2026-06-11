using UnityEngine;

/// <summary>
/// フォーカスモードUI のデザイントークン定義。
///
/// 【CSS Variables 相当の仕組み】
///   UITheme_FocusMode.SetFontScale(FontScale.Large)
///   → OnThemeChanged イベント発火
///   → 各 MonoBehaviour が Refresh() を呼ぶ
///   → 全 UI が新サイズで再描画
///
/// 【使い方】
///   // パネルの MonoBehaviour に追記
///   private void OnEnable()  { UITheme_FocusMode.OnThemeChanged += Refresh; Refresh(); }
///   private void OnDisable() { UITheme_FocusMode.OnThemeChanged -= Refresh; }
/// </summary>
/// <summary>
/// ThemedText コンポーネントが参照するフォントロール。
/// 用途に対して一意に割り当てる（サイズは UITheme_FocusMode が管理）。
/// </summary>
public enum FontRole
{
    CalendarDate,  // 月カレンダー日付数字  Medium=22
    ChipTitle,     // イベント・チップタイトル  Medium=18
    MoreLabel,     // 「他N件」ラベル  Medium=15
    SectionTitle,  // セクション見出し  Medium=14
    DialogTitle,   // ダイアログ・モーダルタイトル  Medium=24
    FormLabel,     // フォームラベル・入力テキスト  Medium=18
    ButtonLabel,   // ボタンテキスト  Medium=16
    Body,          // 本文・リスト  Medium=13
    Caption,       // キャプション・小ラベル  Medium=12
    Micro,         // タイムライン時刻など最小  Medium=11
    TimerDisplay,  // ポモドーロタイマー数字  Medium=24
}

public static class UITheme_FocusMode
{
    // ─── フォントスケール設定 ─────────────────────────────────
    public enum FontScale { Small, Medium, Large }
    private static FontScale _fontScale = FontScale.Medium;
    public  static FontScale CurrentFontScale => _fontScale;

    /// <summary>フォントスケールを変更し、全 UI に再描画を通知する</summary>
    public static void SetFontScale(FontScale scale)
    {
        if (_fontScale == scale) return;
        _fontScale = scale;
        OnThemeChanged?.Invoke();
    }

    /// <summary>テーマ変更イベント（各パネルはここで Refresh() を呼ぶ）</summary>
    public static event System.Action OnThemeChanged;

    /// <summary>ThemeManager がテーマ切替時に呼ぶ（カラー変更通知）</summary>
    internal static void RaiseThemeChanged() => OnThemeChanged?.Invoke();

    // スケール係数（Medium = 1.0 基準）
    private static float S => _fontScale switch {
        FontScale.Small  => 0.82f,
        FontScale.Large  => 1.20f,
        _                => 1.00f,
    };

    // 0.5px 単位で丸める（TMP のサブピクセルレンダリングに合わせる）
    private static float Sz(float baseSize) =>
        Mathf.Round(baseSize * S * 2f) / 2f;

    // ─── フォントサイズ階層 ───────────────────────────────────
    // 名前は「用途」で定義。スケール変更時に全て自動的に連動する。

    /// <summary>月カレンダー日付数字・大きな見出し  Medium=22</summary>
    public static float FontCalendarDate  => Sz(22f);

    /// <summary>チップ・イベントブロックタイトル  Medium=18</summary>
    public static float FontChipTitle     => Sz(18f);

    /// <summary>「他N件」「その他」ラベル  Medium=15</summary>
    public static float FontMoreLabel     => Sz(15f);

    /// <summary>ダッシュボードタイトル・セクション見出し  Medium=14</summary>
    public static float FontSectionTitle  => Sz(14f);
    public static float FontDashboardTitle => Sz(14f); // 後方互換

    /// <summary>本文・入力フィールド  Medium=13</summary>
    public static float FontBody          => Sz(13f);

    /// <summary>キャプション・小ラベル・タブ  Medium=12</summary>
    public static float FontCaption       => Sz(12f);
    public static float FontTabLabel      => Sz(12f);  // 後方互換

    /// <summary>タイムライン時刻・最小テキスト  Medium=11</summary>
    public static float FontMicro         => Sz(11f);

    /// <summary>ポモドーロタイマー数字表示  Medium=24</summary>
    public static float FontTimerDisplay  => Sz(24f);

    /// <summary>ダイアログ・モーダルタイトル  Medium=24</summary>
    public static float FontDialogTitle   => Sz(24f);

    /// <summary>フォームラベル・入力テキスト  Medium=18</summary>
    public static float FontFormLabel     => Sz(18f);

    /// <summary>ボタンテキスト  Medium=16</summary>
    public static float FontButtonLabel   => Sz(16f);

    /// <summary>FontRole から対応サイズを返す（ThemedText が使用）</summary>
    public static float GetFontSize(FontRole role)
    {
        switch (role)
        {
            case FontRole.CalendarDate : return FontCalendarDate;
            case FontRole.ChipTitle    : return FontChipTitle;
            case FontRole.MoreLabel    : return FontMoreLabel;
            case FontRole.SectionTitle : return FontSectionTitle;
            case FontRole.DialogTitle  : return FontDialogTitle;
            case FontRole.FormLabel    : return FontFormLabel;
            case FontRole.ButtonLabel  : return FontButtonLabel;
            case FontRole.Body         : return FontBody;
            case FontRole.Caption      : return FontCaption;
            case FontRole.Micro        : return FontMicro;
            case FontRole.TimerDisplay : return FontTimerDisplay;
            default                    : return FontBody;
        }
    }
    // ─── 背景色 ───────────────────────────────────────────────
    public static Color DashboardBG  => ThemeManager.Current.dashboardBG;
    public static Color PanelBG      => ThemeManager.Current.panelBG;
    public static Color SubPanelBG   => ThemeManager.Current.subPanelBG;
    public static Color InputBG      => ThemeManager.Current.inputBG;
    public static Color FloatBG      => ThemeManager.Current.floatBG;
    public static Color DayCellBG    => ThemeManager.Current.dayCellBG;

    // ─── テキスト色 ───────────────────────────────────────────
    public static Color TextPrimary     => ThemeManager.Current.textPrimary;
    public static Color TextSecondary   => ThemeManager.Current.textSecondary;
    public static Color TextBody        => ThemeManager.Current.textBody;
    public static Color TextMuted       => ThemeManager.Current.textMuted;
    public static Color TextCaption     => ThemeManager.Current.textCaption;
    public static Color TextPlaceholder => ThemeManager.Current.textPlaceholder;
    public static Color TextDisabled    => ThemeManager.Current.textDisabled;

    // ─── ボーダー色 ───────────────────────────────────────────
    public static Color BorderPanel   => ThemeManager.Current.borderPanel;
    public static Color BorderInput   => ThemeManager.Current.borderInput;
    public static Color BorderFloat   => ThemeManager.Current.borderFloat;
    public static Color BorderDivider => ThemeManager.Current.borderDivider;
    public static Color BorderSubtle  => ThemeManager.Current.borderSubtle;

    // ─── アクセント ───────────────────────────────────────────
    public static Color AccentBlue       => ThemeManager.Current.accentMain;
    public static Color AccentBlueFaint  => ThemeManager.Current.accentFaint;
    public static Color AccentBlueSolid  => ThemeManager.Current.accentSolid;
    public static Color AccentRed        => ThemeManager.Current.accentDanger;
    public static Color AccentSatBlue    => ThemeManager.Current.accentStrong;

    // ─── ホバー・選択状態 ─────────────────────────────────────
    public static Color HoverBG    => ThemeManager.Current.hoverBG;
    public static Color SelectedBG => ThemeManager.Current.selectedBG;
    public static Color DoneBG     => ThemeManager.Current.doneBG;

    // ─── 影 ──────────────────────────────────────────────────
    public static Color ShadowColor    => ThemeManager.Current.shadowColor;
    public static Vector2 ShadowOffset => ThemeManager.Current.shadowOffset;

    // ─── 角丸 ─────────────────────────────────────────────────
    public const float RadiusPanel   = 8f;
    public const float RadiusFloat   = 10f;
    public const float RadiusInput   = 4f;
    public const float RadiusTag     = 4f;
    public const float RadiusDayCell = 4f;
    public const float RadiusChip    = 3f;
    public const float RadiusSpinner = 2f;

    // ─── スペーシング ─────────────────────────────────────────
    public const float PaddingDashboard = 12f;
    public const float PaddingPanel     = 10f;
    public const float PaddingInput     = 5f;
    public const float PaddingInputH    = 8f;
    public const float GapSection       = 8f;
    public const float GapField         = 3f;

    // ─── ボーダー幅 ───────────────────────────────────────────
    public const float BorderWidth = 0.5f;

    // ─── ユーティリティ ───────────────────────────────────────
    public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>タグ色（半透明）をダッシュボード背景にアルファ合成した不透明色。</summary>
    /// <remarks>モーダルのカテゴリーボタンと同じ見え方を透過なしで再現する。
    /// PanelBG は白の半透明オーバーレイ色のためブレンド元には使えない。</remarks>
    public static Color CardBG(Color tagColor)
    {
        var b = DashboardBG; float a = tagColor.a;
        return new Color(
            tagColor.r * a + b.r * (1f - a),
            tagColor.g * a + b.g * (1f - a),
            tagColor.b * a + b.b * (1f - a), 1f);
    }

    /// <summary>デザイン2pxのヘアラインを、現在のCanvasスケールで物理ピクセル整数になる太さへ丸める。
    /// 縮小時に線が1.1pxのような非整数になり太さムラ・消失が起きる問題の対策。</summary>
    public static float Hairline(float scaleFactor)
        => Mathf.Max(1f, Mathf.Round(2f * scaleFactor)) / Mathf.Max(scaleFactor, 0.0001f);
}
