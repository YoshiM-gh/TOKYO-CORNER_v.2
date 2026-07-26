using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

/// <summary>
/// Windowsネイティブウィンドウ操作（MacWindowUtilのWindows版・仕様書§6）。
/// user32.dll 直叩きで NSWindow 相当の操作を行う。
/// - Barモードのタイトルバー非表示（WS_CAPTION/WS_THICKFRAME除去）
/// - 常に最前面（WS_EX_TOPMOST）
/// - バードラッグ移動用のグローバルマウス座標／ウィンドウ原点の取得・設定
///
/// MacのNSWindowと異なり、WindowsのUnityプレイヤーは Screen.SetResolution 等で
/// ウィンドウを作り直す／スタイルを復元することがある。そのため:
///  (1) HWNDは毎回 IsWindow で検証し、無効なら再解決する（プロセス内の UnityWndClass を列挙）
///  (2) 望む状態はキャッシュのboolではなく『実際のスタイルビット』と比較し、ズレていれば再適用する
/// エディタでは絶対に動かさない（#if ガード）。全メソッド try/catch で例外を漏らさない。
/// 座標系はマウス・ウィンドウ原点とも『スクリーン左上原点・Y下向き』で統一
/// （Macは左下原点だが、ドラッグ計算は差分ベースなので同一座標系内で閉じていれば互換）。
/// </summary>
public static class WinWindowUtil
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern int  GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)] private static extern int  SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
    [DllImport("user32.dll")] private static extern int  SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("gdi32.dll")]  private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport("gdi32.dll")]  private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT  { public int left, top, right, bottom; }

    private const int GWL_STYLE      = -16;
    private const int GWL_EXSTYLE    = -20;
    private const int WS_CAPTION     = unchecked((int)0x00C00000); // タイトルバー
    private const int WS_THICKFRAME  = 0x00040000;                 // リサイズ枠（Barは固定サイズなので除去）
    private const int WS_EX_TOPMOST  = 0x00000008;
    private const int FRAME_BITS     = WS_CAPTION | WS_THICKFRAME;
    private const int SW_MINIMIZE    = 6;
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    // ヒットテスト定数（OS標準のリサイズ／移動ループを起動するために送る）
    private const int HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12,
                      HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15,
                      HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
    private static int _rgnW, _rgnH, _rgnR = -1; // 直近に適用した切り抜き（毎フレーム再生成しない）
    private static readonly IntPtr HWND_TOPMOST   = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004,
                       SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;

    private static IntPtr _hwnd;
    private static int    _origFrameBits = -1; // 起動時の WS_CAPTION|WS_THICKFRAME の状態
    private static bool   _wantBorderless;
    private static bool   _wantFloating;
    private static int    _reapplyBorderless, _reapplyFloating, _reapplySize;
    private static EnumWindowsProc _enumProc; // GC回収防止のため静的保持
    private static IntPtr _found;

    /// <summary>
    /// Unityメインウィンドウのハンドル。IsWindowで毎回検証し、無効なら再解決する。
    /// Unityがウィンドウを作り直してもハンドルを追従できる（Mac版との最大の違い）。
    /// </summary>
    private static IntPtr Win()
    {
        if (_hwnd != IntPtr.Zero && IsWindow(_hwnd)) return _hwnd;
        _hwnd = IntPtr.Zero;
        _found = IntPtr.Zero;
        try
        {
            if (_enumProc == null) _enumProc = EnumCallback;
            EnumWindows(_enumProc, IntPtr.Zero);
        }
        catch { }
        if (_found == IntPtr.Zero)
        {
            var a = GetActiveWindow();
            if (a != IntPtr.Zero && IsWindow(a)) _found = a; // フォールバック
        }
        if (_found != IntPtr.Zero)
        {
            _hwnd = _found;
            var cls = new StringBuilder(128);
            GetClassName(_hwnd, cls, cls.Capacity);
            int st = GetWindowLong(_hwnd, GWL_STYLE);
            if (_origFrameBits < 0) _origFrameBits = st & FRAME_BITS;
            Debug.Log($"[WinWindowUtil] hwnd resolved 0x{_hwnd.ToInt64():X} class={cls} style=0x{st:X} origFrame=0x{_origFrameBits:X}");
        }
        return _hwnd;
    }

    private static bool EnumCallback(IntPtr h, IntPtr l)
    {
        uint pid;
        GetWindowThreadProcessId(h, out pid);
        if (pid != GetCurrentProcessId()) return true;   // 別プロセス
        if (!IsWindowVisible(h)) return true;            // 非表示の補助ウィンドウ
        var cls = new StringBuilder(128);
        GetClassName(h, cls, cls.Capacity);
        var name = cls.ToString();
        if (name.IndexOf("UnityWndClass", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            _found = h;
            return false; // 見つかったので列挙終了
        }
        if (_found == IntPtr.Zero) _found = h; // 保険（クラス名が想定外でも可視な自プロセス窓）
        return true;
    }
#endif

    /// <summary>タイトルバー非表示（枠なし）を切り替える。実スタイルを見て必要なら毎フレーム再適用する。</summary>
    public static void TrySetBorderless(bool on, bool keepResizeFrame = false)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var w = Win();
            if (w == IntPtr.Zero) return;
            int cur  = GetWindowLong(w, GWL_STYLE);
            if (_origFrameBits < 0) _origFrameBits = cur & FRAME_BITS;
            // keepResizeFrame=true: WS_CAPTION だけ外して WS_THICKFRAME を残す。
            // タイトルバーは消えるが、ウィンドウ端をドラッグしてリサイズできる状態になる（通常モード用）。
            // false: 両方外す＝完全固定サイズ（Barモード用）。
            int want;
            if (on)
            {
                int remove = keepResizeFrame ? WS_CAPTION : FRAME_BITS;
                want = cur & ~remove;
                if (keepResizeFrame) want |= (_origFrameBits & WS_THICKFRAME);
            }
            else want = (cur & ~FRAME_BITS) | _origFrameBits;
            bool changedIntent = (_wantBorderless != on);
            _wantBorderless = on;
            if (cur == want) return; // 既に望む状態＝何もしない
            SetWindowLong(w, GWL_STYLE, want);
            SetWindowPos(w, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            int after = GetWindowLong(w, GWL_STYLE);
            _reapplyBorderless++;
            // 意図が変わった瞬間と、以後は間引いてログ（Unityと取り合いになっていないかの検知用）
            if (changedIntent || _reapplyBorderless % 120 == 0)
                Debug.Log($"[WinWindowUtil] borderless={on} keepResize={keepResizeFrame} hwnd=0x{w.ToInt64():X} style 0x{cur:X}→0x{after:X} (want 0x{want:X}) applies={_reapplyBorderless} err={Marshal.GetLastWin32Error()}");
        }
        catch (Exception e) { Debug.LogWarning("[WinWindowUtil] borderless: " + e.Message); }
#else
        _ = on;
#endif
    }

    /// <summary>常に最前面（WS_EX_TOPMOST）を切り替える。実際の拡張スタイルを見て必要なら再適用する。</summary>
    public static void TrySetFloating(bool on)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var w = Win();
            if (w == IntPtr.Zero) return;
            int ex = GetWindowLong(w, GWL_EXSTYLE);
            bool isTop = (ex & WS_EX_TOPMOST) != 0;
            bool changedIntent = (_wantFloating != on);
            _wantFloating = on;
            if (isTop == on) return; // 既に望む状態
            SetWindowPos(w, on ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE); // ※Zオーダーを変えるので SWP_NOZORDER は付けない
            int after = GetWindowLong(w, GWL_EXSTYLE);
            _reapplyFloating++;
            if (changedIntent || _reapplyFloating % 120 == 0)
                Debug.Log($"[WinWindowUtil] floating={on} hwnd=0x{w.ToInt64():X} ex 0x{ex:X}→0x{after:X} applies={_reapplyFloating} err={Marshal.GetLastWin32Error()}");
        }
        catch (Exception e) { Debug.LogWarning("[WinWindowUtil] floating: " + e.Message); }
#else
        _ = on;
#endif
    }

    /// <summary>
    /// ウィンドウの可視領域をクライアント領域＋角丸に切り抜く。
    /// Unityのウィンドウは矩形・不透明なので、アプリ側で角丸を描いても四隅に地の色が残る。
    /// SetWindowRgn はOSに窓の形そのものを教えるため、四隅が本当に切り取られる（クリックも貫通する）。
    /// 同時に、残していたリサイズ枠（約8px）も領域外になって見えなくなる
    /// ＝枠の掴み代は TryBeginResize で内側から代替する。
    /// </summary>
    public static bool TryClipToClient(int cornerRadius)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var hw = Win();
            if (hw == IntPtr.Zero) return false;
            RECT wr, cr;
            if (!GetWindowRect(hw, out wr) || !GetClientRect(hw, out cr)) return false;
            int cw = cr.right - cr.left, ch = cr.bottom - cr.top;
            if (cw <= 0 || ch <= 0) return false;
            if (_rgnW == cw && _rgnH == ch && _rgnR == cornerRadius) return true; // 変化なし

            // クライアント原点がウィンドウ矩形のどこにあるか（＝枠の厚み）
            POINT o; o.x = 0; o.y = 0;
            if (!ClientToScreen(hw, ref o)) return false;
            int offX = o.x - wr.left, offY = o.y - wr.top;

            int d = Mathf.Max(0, cornerRadius) * 2;
            var rgn = CreateRoundRectRgn(offX, offY, offX + cw + 1, offY + ch + 1, d, d);
            if (rgn == IntPtr.Zero) return false;
            if (SetWindowRgn(hw, rgn, true) == 0) { DeleteObject(rgn); return false; } // 成功時はOS所有
            _rgnW = cw; _rgnH = ch; _rgnR = cornerRadius;
            Debug.Log($"[WinWindowUtil] clip client={cw}x{ch} frame=({offX},{offY}) radius={cornerRadius}");
            return true;
        }
        catch (Exception e) { Debug.LogWarning("[WinWindowUtil] clip: " + e.Message); return false; }
#else
        _ = cornerRadius;
        return false;
#endif
    }

    /// <summary>切り抜きを解除して通常のウィンドウ形状に戻す（お店シーンなどタイトルバーを出す場面）。</summary>
    public static void TryClearClip()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            if (_rgnR < 0) return; // 未適用
            var hw = Win();
            if (hw == IntPtr.Zero) return;
            SetWindowRgn(hw, IntPtr.Zero, true);
            _rgnW = _rgnH = 0; _rgnR = -1;
        }
        catch (Exception e) { Debug.LogWarning("[WinWindowUtil] unclip: " + e.Message); }
#endif
    }

    /// <summary>
    /// OS標準のリサイズループを開始する。dx=-1左/+1右、dy=-1下/+1上（Unityのy軸に合わせて上が+）。
    /// 切り抜きでOSの枠が掴めなくなるため、クライアント内側の縁からこれを呼ぶ。
    /// スナップやマルチモニタ跨ぎはOSが面倒を見てくれる。
    /// </summary>
    public static bool TryBeginResize(int dx, int dy)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var hw = Win();
            if (hw == IntPtr.Zero) return false;
            int code;
            if      (dx < 0 && dy > 0) code = HTTOPLEFT;
            else if (dx > 0 && dy > 0) code = HTTOPRIGHT;
            else if (dx < 0 && dy < 0) code = HTBOTTOMLEFT;
            else if (dx > 0 && dy < 0) code = HTBOTTOMRIGHT;
            else if (dx < 0)           code = HTLEFT;
            else if (dx > 0)           code = HTRIGHT;
            else if (dy > 0)           code = HTTOP;
            else if (dy < 0)           code = HTBOTTOM;
            else return false;
            ReleaseCapture();
            SendMessage(hw, WM_NCLBUTTONDOWN, (IntPtr)code, IntPtr.Zero);
            return true;
        }
        catch (Exception e) { Debug.LogWarning("[WinWindowUtil] resize: " + e.Message); return false; }
#else
        _ = dx; _ = dy;
        return false;
#endif
    }

    /// <summary>ウィンドウを最小化する（枠なし運用ではタイトルバーの「−」が無いため、バーのボタンから呼ぶ）。</summary>
    public static bool TryMinimize()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var hw = Win();
            if (hw == IntPtr.Zero) return false;
            ShowWindow(hw, SW_MINIMIZE);
            return true;
        }
        catch (Exception e) { Debug.LogWarning("[WinWindowUtil] minimize: " + e.Message); return false; }
#else
        return false;
#endif
    }

    /// <summary>
    /// クライアント領域のサイズを直接指定する（枠なしBarモード用）。成功したら true。
    /// Windowsでは枠なし中に Screen.SetResolution を呼ぶとUnityがウィンドウを作り直して
    /// タイトルバーが復活する（＝剥がす／戻すの無限ループ＝点滅）。それを避けるため、
    /// Barモードのサイズ確定だけはUnityではなくこちらで行う。
    /// 現在のスタイルから AdjustWindowRectEx で枠込みサイズを逆算するので、
    /// タイトルバーの有無に関わらず狙ったクライアント幅高になる。
    /// </summary>
    public static bool TrySetClientSize(int w, int h)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var hw = Win();
            if (hw == IntPtr.Zero) return false;
            RECT c;
            if (!GetClientRect(hw, out c)) return false;
            int cw = c.right - c.left, ch = c.bottom - c.top;
            if (Mathf.Abs(cw - w) <= 1 && Mathf.Abs(ch - h) <= 1) return true; // 既に一致＝何もしない

            int style = GetWindowLong(hw, GWL_STYLE);
            int ex    = GetWindowLong(hw, GWL_EXSTYLE);
            RECT r; r.left = 0; r.top = 0; r.right = w; r.bottom = h;
            if (!AdjustWindowRectEx(ref r, style, false, ex)) return false;

            SetWindowPos(hw, IntPtr.Zero, 0, 0, r.right - r.left, r.bottom - r.top,
                SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
            _reapplySize++;
            if (_reapplySize <= 5 || _reapplySize % 120 == 0)
                Debug.Log($"[WinWindowUtil] clientSize {cw}x{ch}→{w}x{h} (outer {r.right - r.left}x{r.bottom - r.top}) applies={_reapplySize} err={Marshal.GetLastWin32Error()}");
            return true;
        }
        catch (Exception e) { Debug.LogWarning("[WinWindowUtil] clientSize: " + e.Message); return false; }
#else
        _ = w; _ = h;
        return false; // 非Windowsは呼び出し側が Screen.SetResolution にフォールバック
#endif
    }

    /// <summary>グローバルマウス座標（スクリーンpx・左上原点）。非対応環境は Vector2.zero。</summary>
    public static Vector2 GlobalMouse()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            POINT p;
            if (GetCursorPos(out p)) return new Vector2(p.x, p.y);
            return Vector2.zero;
        }
        catch { return Vector2.zero; }
#else
        return Vector2.zero;
#endif
    }

    /// <summary>ウィンドウ原点（スクリーンpx・左上原点）。</summary>
    public static Vector2 GetWindowOrigin()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var w = Win();
            RECT r;
            if (w != IntPtr.Zero && GetWindowRect(w, out r)) return new Vector2(r.left, r.top);
            return Vector2.zero;
        }
        catch { return Vector2.zero; }
#else
        return Vector2.zero;
#endif
    }

    /// <summary>ウィンドウ原点を移動（Barドラッグ用）。</summary>
    public static void SetWindowOrigin(Vector2 p)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var w = Win();
            if (w == IntPtr.Zero) return;
            SetWindowPos(w, IntPtr.Zero, (int)p.x, (int)p.y, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
        catch (Exception e) { Debug.LogWarning("[WinWindowUtil] move: " + e.Message); }
#else
        _ = p;
#endif
    }
}
