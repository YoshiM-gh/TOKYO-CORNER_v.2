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
    public static readonly Color DashboardBG  = new Color(0.039f, 0.039f, 0.055f, 0.97f);
    public static readonly Color PanelBG      = new Color(1f, 1f, 1f, 0.04f);
    public static readonly Color SubPanelBG   = new Color(1f, 1f, 1f, 0.02f);
    public static readonly Color InputBG      = new Color(1f, 1f, 1f, 0.055f);
    public static readonly Color FloatBG      = new Color(0.071f, 0.071f, 0.086f, 0.98f);
    public static readonly Color DayCellBG    = new Color(1f, 1f, 1f, 0.025f);

    // ─── テキスト色 ───────────────────────────────────────────
    public static readonly Color TextPrimary     = new Color(1f, 1f, 1f, 1.00f);
    public static readonly Color TextSecondary   = new Color(1f, 1f, 1f, 0.82f);
    public static readonly Color TextBody        = new Color(1f, 1f, 1f, 0.65f);
    public static readonly Color TextMuted       = new Color(1f, 1f, 1f, 0.35f);
    public static readonly Color TextCaption     = new Color(1f, 1f, 1f, 0.28f);
    public static readonly Color TextPlaceholder = new Color(1f, 1f, 1f, 0.22f);
    public static readonly Color TextDisabled    = new Color(1f, 1f, 1f, 0.18f);

    // ─── ボーダー色 ───────────────────────────────────────────
    public static readonly Color BorderPanel   = new Color(1f, 1f, 1f, 0.08f);
    public static readonly Color BorderInput   = new Color(1f, 1f, 1f, 0.09f);
    public static readonly Color BorderFloat   = new Color(1f, 1f, 1f, 0.13f);
    public static readonly Color BorderDivider = new Color(1f, 1f, 1f, 0.07f);
    public static readonly Color BorderSubtle  = new Color(1f, 1f, 1f, 0.05f);

    // ─── アクセント ───────────────────────────────────────────
    public static readonly Color AccentBlue       = new Color(0.314f, 0.549f, 1.0f, 0.50f);
    public static readonly Color AccentBlueFaint  = new Color(0.314f, 0.549f, 1.0f, 0.18f);
    public static readonly Color AccentBlueSolid  = new Color(0.314f, 0.549f, 1.0f, 0.75f);
    public static readonly Color AccentRed        = new Color(1.0f, 0.392f, 0.392f, 0.75f);
    public static readonly Color AccentSatBlue    = new Color(0.431f, 0.608f, 1.0f, 0.75f);

    // ─── ホバー・選択状態 ─────────────────────────────────────
    public static readonly Color HoverBG    = new Color(1f, 1f, 1f, 0.07f);
    public static readonly Color SelectedBG = new Color(1f, 1f, 1f, 0.09f);
    public static readonly Color DoneBG     = new Color(1f, 1f, 1f, 0.03f);

    // ─── 影 ──────────────────────────────────────────────────
    public static readonly Color ShadowColor    = new Color(0f, 0f, 0f, 0.60f);
    public static readonly Vector2 ShadowOffset = new Vector2(0f, -8f);

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
}
