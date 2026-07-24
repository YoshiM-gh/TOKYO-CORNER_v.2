using System;
using UnityEngine;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// macOSネイティブウィンドウ操作（Barモードのタイトルバー非表示用）。
/// objc_msgSend 直叩きで NSWindow の styleMask を切り替える。
/// エディタでは絶対に動かさない（エディタ自身のウィンドウを壊すため）。
/// </summary>
public static class MacWindowUtil
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private const string OBJC = "/usr/lib/libobjc.A.dylib";
    [DllImport(OBJC)] private static extern IntPtr objc_getClass(string name);
    [DllImport(OBJC)] private static extern IntPtr sel_registerName(string name);
    [DllImport(OBJC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr r, IntPtr s);
    [DllImport(OBJC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPtr(IntPtr r, IntPtr s, IntPtr a);
    [DllImport(OBJC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendUL(IntPtr r, IntPtr s, ulong a);
    [DllImport(OBJC, EntryPoint = "objc_msgSend")] private static extern ulong SendGetUL(IntPtr r, IntPtr s);

    private struct NSPoint { public double x, y; }
    private struct NSSize  { public double w, h; }
    [DllImport(OBJC, EntryPoint = "objc_msgSend")] private static extern void SendSetSize(IntPtr r, IntPtr s, NSSize v);
    private struct NSRect  { public double x, y, w, h; }
    [DllImport(OBJC, EntryPoint = "objc_msgSend")] private static extern NSPoint SendGetPoint(IntPtr r, IntPtr s);
    [DllImport(OBJC, EntryPoint = "objc_msgSend")] private static extern NSRect  SendGetRect(IntPtr r, IntPtr s);
    [DllImport(OBJC, EntryPoint = "objc_msgSend")] private static extern void    SendSetPoint(IntPtr r, IntPtr s, NSPoint p);

    private static ulong _origMask;
    private static bool  _saved;
    private static bool  _borderless;
    private static bool  _floating;
    private static bool  _transparent;

    private static IntPtr Win()
    {
        var app = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
        if (app == IntPtr.Zero) return IntPtr.Zero;
        var w = Send(app, sel_registerName("mainWindow"));
        if (w == IntPtr.Zero)
        {
            var arr = Send(app, sel_registerName("windows"));
            if (arr != IntPtr.Zero && SendGetUL(arr, sel_registerName("count")) > 0)
                w = SendUL(arr, sel_registerName("objectAtIndex:"), 0UL);
        }
        return w;
    }
#endif

    /// <summary>タイトルバー非表示（枠なし）を切り替える。失敗しても例外は投げない。</summary>
    public static void TrySetBorderless(bool on)
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            if (_borderless == on) return;
            var w = Win();
            if (w == IntPtr.Zero) return;
            var selSet = sel_registerName("setStyleMask:");
            if (on)
            {
                if (!_saved) { _origMask = SendGetUL(w, sel_registerName("styleMask")); _saved = true; }
                SendUL(w, selSet, 0UL); // NSWindowStyleMaskBorderless
                SendPtr(w, sel_registerName("makeKeyAndOrderFront:"), IntPtr.Zero);
            }
            else if (_saved)
            {
                SendUL(w, selSet, _origMask);
                SendPtr(w, sel_registerName("makeKeyAndOrderFront:"), IntPtr.Zero);
            }
            _borderless = on;
            Debug.Log($"[MacWindowUtil] borderless={on}");
        }
        catch (Exception e) { Debug.LogWarning("[MacWindowUtil] " + e.Message); }
#else
        _ = on;
#endif
    }

/// <summary>ウィンドウ背景の透過を切り替える（Barモードの角丸フローティング用）。
    /// NSWindowを非不透明＋clearColor化し、Metalレイヤーも非不透明にする。</summary>
    public static void TrySetTransparent(bool on)
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            if (_transparent == on) return;
            var w = Win();
            if (w == IntPtr.Zero) return;
            SendUL(w, sel_registerName("setOpaque:"), on ? 0UL : 1UL);
            var colorCls = objc_getClass("NSColor");
            var bg = on ? Send(colorCls, sel_registerName("clearColor"))
                        : Send(colorCls, sel_registerName("windowBackgroundColor"));
            SendPtr(w, sel_registerName("setBackgroundColor:"), bg);
            SendUL(w, sel_registerName("setHasShadow:"), on ? 0UL : 1UL); // 透過中は矩形影を消す
            var view = Send(w, sel_registerName("contentView"));
            if (view != IntPtr.Zero)
            {
                var layer = Send(view, sel_registerName("layer"));
                if (layer != IntPtr.Zero)
                    SendUL(layer, sel_registerName("setOpaque:"), on ? 0UL : 1UL);
            }
            Send(w, sel_registerName("invalidateShadow"));
            _transparent = on;
            Debug.Log($"[MacWindowUtil] transparent={on}");
        }
        catch (Exception e) { Debug.LogWarning("[MacWindowUtil] transparent: " + e.Message); }
#else
        _ = on;
#endif
    }

    /// <summary>常に最前面（NSFloatingWindowLevel）を切り替える。Barモードのタスクバーヒーロー用。</summary>
    public static void TrySetFloating(bool on)
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            if (_floating == on) return;
            var w = Win();
            if (w == IntPtr.Zero) return;
            SendUL(w, sel_registerName("setLevel:"), on ? 3UL : 0UL); // 3 = NSFloatingWindowLevel
            _floating = on;
            Debug.Log($"[MacWindowUtil] floating={on}");
        }
        catch (Exception e) { Debug.LogWarning("[MacWindowUtil] floating: " + e.Message); }
#else
        _ = on;
#endif
    }

    /// <summary>ウィンドウのリサイズを指定アスペクト比に拘束する（w,hを0にすると解除）。</summary>
    public static void TrySetContentAspect(float w, float h)
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            var win = Win();
            if (win == IntPtr.Zero) return;
            SendSetSize(win, sel_registerName("setContentAspectRatio:"), new NSSize { w = w, h = h });
        }
        catch (Exception e) { Debug.LogWarning("[MacWindowUtil] aspect: " + e.Message); }
#else
        _ = w; _ = h;
#endif
    }


    /// <summary>グローバルマウス座標（画面ポイント・左下原点）。非対応環境はVector2.zero。</summary>
    public static Vector2 GlobalMouse()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            var p = SendGetPoint(objc_getClass("NSEvent"), sel_registerName("mouseLocation"));
            return new Vector2((float)p.x, (float)p.y);
        }
        catch { return Vector2.zero; }
#else
        return Vector2.zero;
#endif
    }

    /// <summary>ウィンドウ左下原点（画面ポイント）。</summary>
    public static Vector2 GetWindowOrigin()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            var w = Win();
            if (w == IntPtr.Zero) return Vector2.zero;
            var r = SendGetRect(w, sel_registerName("frame"));
            return new Vector2((float)r.x, (float)r.y);
        }
        catch { return Vector2.zero; }
#else
        return Vector2.zero;
#endif
    }

    /// <summary>ウィンドウ位置を移動（画面ポイント・左下原点）。</summary>
    public static void SetWindowOrigin(Vector2 o)
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        try
        {
            var w = Win();
            if (w == IntPtr.Zero) return;
            SendSetPoint(w, sel_registerName("setFrameOrigin:"), new NSPoint { x = o.x, y = o.y });
        }
        catch { }
#else
        _ = o;
#endif
    }
}
