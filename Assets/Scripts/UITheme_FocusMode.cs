using UnityEngine;

/// <summary>
/// フォーカスモードUI のデザイントークン定義。
/// 色・フォントサイズ・角丸をここだけで管理する。
/// タグ色は TagConfig.cs で管理。
/// </summary>
public static class UITheme_FocusMode
{
    // ─── 背景色 ───────────────────────────────────────────
    public static readonly Color DashboardBG  = new Color(0.039f, 0.039f, 0.055f, 0.97f); // rgba(10,10,14,0.97)
    public static readonly Color PanelBG      = new Color(1f, 1f, 1f, 0.04f);             // rgba(255,255,255,0.04)
    public static readonly Color SubPanelBG   = new Color(1f, 1f, 1f, 0.02f);             // rgba(255,255,255,0.02)
    public static readonly Color InputBG      = new Color(1f, 1f, 1f, 0.055f);            // rgba(255,255,255,0.055)
    public static readonly Color FloatBG      = new Color(0.071f, 0.071f, 0.086f, 0.98f); // rgba(18,18,22,0.98)
    public static readonly Color DayCellBG    = new Color(1f, 1f, 1f, 0.025f);            // 月カレンダーの日セル

    // ─── テキスト色 ───────────────────────────────────────
    public static readonly Color TextPrimary     = new Color(1f, 1f, 1f, 1.00f); // 見出し・タイトル
    public static readonly Color TextSecondary   = new Color(1f, 1f, 1f, 0.82f); // パネルタイトル
    public static readonly Color TextBody        = new Color(1f, 1f, 1f, 0.65f); // 本文・入力
    public static readonly Color TextMuted       = new Color(1f, 1f, 1f, 0.35f); // 補足・ラベル
    public static readonly Color TextCaption     = new Color(1f, 1f, 1f, 0.28f); // フィールドラベル
    public static readonly Color TextPlaceholder = new Color(1f, 1f, 1f, 0.22f); // プレースホルダー
    public static readonly Color TextDisabled    = new Color(1f, 1f, 1f, 0.18f); // 非表示・空状態

    // ─── ボーダー色 ───────────────────────────────────────
    public static readonly Color BorderPanel   = new Color(1f, 1f, 1f, 0.08f);  // パネル・カード
    public static readonly Color BorderInput   = new Color(1f, 1f, 1f, 0.09f);  // 入力フィールド
    public static readonly Color BorderFloat   = new Color(1f, 1f, 1f, 0.13f);  // フローティングウィンドウ
    public static readonly Color BorderDivider = new Color(1f, 1f, 1f, 0.07f);  // 仕切り線
    public static readonly Color BorderSubtle  = new Color(1f, 1f, 1f, 0.05f);  // タイムライングリッド

    // ─── アクセント（タグ以外の用途） ────────────────────
    public static readonly Color AccentBlue       = new Color(0.314f, 0.549f, 1.0f, 0.50f); // ポモドーロリング・ボタン
    public static readonly Color AccentBlueFaint  = new Color(0.314f, 0.549f, 1.0f, 0.18f); // Today背景など
    public static readonly Color AccentBlueSolid  = new Color(0.314f, 0.549f, 1.0f, 0.75f); // Today circle
    public static readonly Color AccentRed        = new Color(1.0f, 0.392f, 0.392f, 0.75f); // 日曜・祝日
    public static readonly Color AccentSatBlue    = new Color(0.431f, 0.608f, 1.0f, 0.75f); // 土曜

    // ─── ホバー・選択状態 ─────────────────────────────────
    public static readonly Color HoverBG    = new Color(1f, 1f, 1f, 0.07f);
    public static readonly Color SelectedBG = new Color(1f, 1f, 1f, 0.09f);
    public static readonly Color DoneBG     = new Color(1f, 1f, 1f, 0.03f); // 完了済みアイテム

    // ─── フローティングウィンドウ影 ──────────────────────
    // UnityのUI Shadowを使う場合の設定値
    public static readonly Color ShadowColor    = new Color(0f, 0f, 0f, 0.60f);
    public static readonly Vector2 ShadowOffset = new Vector2(0f, -8f);

    // ─── フォントサイズ ───────────────────────────────────
    public const float FontDashboardTitle = 11f;
    public const float FontSectionTitle   = 12f;
    public const float FontBody           = 11f;
    public const float FontCaption        = 10f;
    public const float FontMicro          = 9f;
    public const float FontTabLabel       = 12f;
    public const float FontTimerDisplay   = 24f;

    // ─── 角丸（PixelsPerUnit に変換して使う） ────────────
    public const float RadiusPanel   = 8f;
    public const float RadiusFloat   = 10f;
    public const float RadiusInput   = 4f;
    public const float RadiusTag     = 4f;
    public const float RadiusDayCell = 4f;
    public const float RadiusChip    = 3f;
    public const float RadiusSpinner = 2f;

    // ─── スペーシング ─────────────────────────────────────
    public const float PaddingDashboard = 12f;
    public const float PaddingPanel     = 10f;
    public const float PaddingInput     = 5f;   // 上下
    public const float PaddingInputH    = 8f;   // 左右
    public const float GapSection       = 8f;
    public const float GapField         = 3f;   // ラベル→入力欄

    // ─── ボーダー幅 ───────────────────────────────────────
    public const float BorderWidth = 0.5f; // 全て0.5px統一

    // ─── ユーティリティ ───────────────────────────────────
    /// <summary>alpha値だけ変えた色を返す</summary>
    public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
