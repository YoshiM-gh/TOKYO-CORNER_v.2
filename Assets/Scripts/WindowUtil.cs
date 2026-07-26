using UnityEngine;

/// <summary>
/// OS別ウィンドウ操作のファサード（仕様書§6）。呼び出し側（AppModeManager等）は
/// このクラスだけを使い、Mac/Windowsの分岐を意識しない。
/// - macOS  → MacWindowUtil（objc_msgSend）
/// - Windows→ WinWindowUtil（user32.dll）
/// - その他/エディタ → 各Utilが内部ガードで安全に no-op
/// 実行時分岐（#ifではない）なので、両Utilともビルドに含まれるが
/// 各メソッド内部の #if ガードにより非対象OSでは何もしない。
/// </summary>
public static class WindowUtil
{
    private static bool IsWin =>
        Application.platform == RuntimePlatform.WindowsPlayer;

    /// <summary>
    /// 枠なし運用をOS単位で切り替えるためのフラグ。
    /// Windows: フォーカス画面は全モードで枠なし（タイトルバーが二段になるのを避ける）。
    ///          代わりに最小化/閉じるをヘッダーバーに置き、通常モードはリサイズ枠を残す。
    /// macOS  : 従来どおりBarモードのみ枠なし（信号機ボタンをそのまま使う）。
    /// </summary>
    // 2026-07-27の判断: 全モード枠なしは「最後の方」で有効化する。
    // タイトルバーを消すと端でのリサイズ・カーソル形状・終了手段をすべて自前で作る必要があり、
    // コアループが固まる前にやる作業ではないため、当面はOSのタイトルバーを使う。
    // ここを IsWin に戻すだけで、実装済みの全モード枠なし（切り抜き・内側リサイズ・−/×）が復活する。
    public static bool BorderlessAllModes => false;

    /// <summary>Windowsか（バーの−/×など、OS差のあるUIの出し分けに使う）。</summary>
    public static bool IsWindows => IsWin;

    /// <summary>タイトルバー非表示（枠なし）切替。keepResizeFrame=true なら端でのリサイズは残す（Windowsのみ有効）。</summary>
    public static void TrySetBorderless(bool on, bool keepResizeFrame = false)
    {
        if (IsWin) WinWindowUtil.TrySetBorderless(on, keepResizeFrame);
        else       MacWindowUtil.TrySetBorderless(on);
    }

    /// <summary>ウィンドウの可視領域をクライアント領域＋角丸に切り抜く（Windowsのみ）。
    /// 四隅の地の色と、残していたリサイズ枠の見た目を同時に消す。</summary>
    public static bool TryClipToClient(int cornerRadius)
        => IsWin && WinWindowUtil.TryClipToClient(cornerRadius);

    /// <summary>切り抜きを解除（タイトルバーを出す場面に戻すとき）。</summary>
    public static void TryClearClip()
    {
        if (IsWin) WinWindowUtil.TryClearClip();
    }

    /// <summary>OS標準のリサイズループを開始（dx=-1左/+1右、dy=-1下/+1上）。切り抜きで枠を掴めない代替。</summary>
    public static bool TryBeginResize(int dx, int dy)
        => IsWin && WinWindowUtil.TryBeginResize(dx, dy);

    /// <summary>ウィンドウ最小化。対応OSで実行できたら true（＝バーに「−」ボタンを出す判定にも使う）。</summary>
    public static bool TryMinimize() => IsWin && WinWindowUtil.TryMinimize();

    /// <summary>常に最前面の切替（Barモードのタスクバーヒーロー）。</summary>
    public static void TrySetFloating(bool on)
    {
        if (IsWin) WinWindowUtil.TrySetFloating(on);
        else       MacWindowUtil.TrySetFloating(on);
    }

    /// <summary>アスペクト比のネイティブ拘束（現在は常時解除運用・Macのみ実装）。</summary>
    public static void TrySetContentAspect(float w, float h)
    {
        if (!IsWin) MacWindowUtil.TrySetContentAspect(w, h);
        // Windows: WM_SIZINGフック未実装。確定後スナップ（EnforceMinSize）で代替＝仕様通り
    }

    /// <summary>クライアント領域サイズの直接指定（枠なしBar用）。Windowsのみ true を返す。
    /// false のときは呼び出し側が Screen.SetResolution にフォールバックする（Mac は従来どおり）。</summary>
    public static bool TrySetClientSize(int width, int height)
        => IsWin && WinWindowUtil.TrySetClientSize(width, height);

    /// <summary>グローバルマウス座標。ドラッグ差分計算専用（OS間で原点方向が異なるため絶対値では使わない）。</summary>
    public static Vector2 GlobalMouse()
        => IsWin ? WinWindowUtil.GlobalMouse() : MacWindowUtil.GlobalMouse();

    /// <summary>ウィンドウ原点。GlobalMouseと同一座標系（差分計算で使う）。</summary>
    public static Vector2 GetWindowOrigin()
        => IsWin ? WinWindowUtil.GetWindowOrigin() : MacWindowUtil.GetWindowOrigin();

    /// <summary>ウィンドウ原点を移動（Barドラッグ）。</summary>
    public static void SetWindowOrigin(Vector2 p)
    {
        if (IsWin) WinWindowUtil.SetWindowOrigin(p);
        else       MacWindowUtil.SetWindowOrigin(p);
    }
}
