using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手続き生成スプライト（円・角丸）。Phase 1 部品規格の基盤。
/// テクスチャはランタイム生成・キャッシュ。アセット不要なので Git 管理も汚さない。
/// </summary>
public static class UISprites
{
    private static Sprite _circle;
    private static Sprite _rounded24;

    /// <summary>アンチエイリアス付き正円（128px）。Image.preserveAspect=true で使用</summary>
    public static Sprite Circle
    {
        get { if (_circle == null) _circle = MakeCircle(128); return _circle; }
    }

    /// <summary>角丸矩形（コーナー24px・9-slice）。pixelsPerUnitMultiplier で実半径を制御:
    /// 実半径 = 24 / multiplier。例: multiplier=2 → 12px角丸、=3 → 8px角丸</summary>
    public static Sprite Rounded24
    {
        get { if (_rounded24 == null) _rounded24 = MakeRounded(24); return _rounded24; }
    }

    private static Sprite _roundedOutline24;

    /// <summary>角丸アウトライン（コーナー24px・線幅4px・9-slice）。
    /// ppu=2 のとき視覚上 12px角丸・2px線。塗りなしゴーストボタン用</summary>
    public static Sprite RoundedOutline24
    {
        get { if (_roundedOutline24 == null) _roundedOutline24 = MakeRoundedOutline(24, 4); return _roundedOutline24; }
    }

    public const float RoundedTexRadius = 24f;

    private static Sprite MakeCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.hideFlags = HideFlags.DontSave;
        tex.wrapMode  = TextureWrapMode.Clamp;
        float c = (size - 1) * 0.5f;
        float R = size * 0.5f - 1.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01(R - d + 0.5f);  // 1px スムージング
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite MakeRounded(int radius)
    {
        int size = radius * 2 + 4;   // 中央4pxが伸縮帯
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.hideFlags = HideFlags.DontSave;
        tex.wrapMode  = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;
        float hx = half - 0.5f, hy = half - 0.5f;   // 矩形ハーフサイズ
        float R  = radius - 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // SDF rounded rect
            float qx = Mathf.Abs(x - half) - (hx - R);
            float qy = Mathf.Abs(y - half) - (hy - R);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            float d  = Mathf.Sqrt(ox * ox + oy * oy) - R;
            float a  = Mathf.Clamp01(0.5f - d);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px); tex.Apply();
        var border = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite MakeRoundedOutline(int radius, int stroke)
    {
        int size = radius * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.hideFlags = HideFlags.DontSave;
        tex.wrapMode  = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;
        float hx = half - 0.5f, hy = half - 0.5f;
        float R  = radius - 0.5f;
        float hs = stroke * 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float qx = Mathf.Abs(x - half) - (hx - R);
            float qy = Mathf.Abs(y - half) - (hy - R);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            float d  = Mathf.Sqrt(ox * ox + oy * oy) - R;   // 縁からの符号付き距離
            float a  = Mathf.Clamp01(hs - Mathf.Abs(d) + 0.5f); // 縁の帯のみ不透明
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px); tex.Apply();
        var border = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}

/// <summary>
/// Phase 1 共通スタイル適用キット。既存 GO に「形」を後付けする Apply 系。
/// 色は変更しない（既存の色ロジック・テーマファサードに任せる）。
/// </summary>
public static class UIStyleKit
{
    /// <summary>円形ゴーストボタン（< > ナビ用）。矩形でも preserveAspect で正円描画</summary>
    public static void ApplyCircleGhost(Image img)
    {
        if (img == null) return;
        img.sprite = UISprites.Circle;
        img.type   = Image.Type.Simple;
        img.preserveAspect = true;
    }

    /// <summary>任意の角丸を適用（cornerRadius px）</summary>
    public static void ApplyRounded(Image img, float cornerRadius)
    {
        if (img == null || cornerRadius <= 0f) return;
        img.sprite = UISprites.Rounded24;
        img.type   = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = UISprites.RoundedTexRadius / cornerRadius;
        img.preserveAspect = false;
    }

    /// <summary>角丸アウトライン（塗りなし・線のみ）。タイマー操作ボタンなどゴースト系に使用</summary>
    public static void ApplyRoundedOutline(Image img, float cornerRadius)
    {
        if (img == null || cornerRadius <= 0f) return;
        img.sprite = UISprites.RoundedOutline24;
        img.type   = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = UISprites.RoundedTexRadius / cornerRadius;
        img.preserveAspect = false;
    }

    /// <summary>完全ピル化（角丸 = 高さの半分）。レイアウト確定後に呼ぶこと</summary>
    public static void ApplyPill(Image img)
    {
        if (img == null) return;
        float h = ((RectTransform)img.transform).rect.height;
        ApplyRounded(img, Mathf.Max(h * 0.5f, 4f));
    }

    /// <summary>カード角丸（12px 規格）</summary>
    public static void ApplyCard(Image img)    => ApplyRounded(img, 12f);

    /// <summary>コントロール角丸（8px 規格）</summary>
    public static void ApplyControl(Image img) => ApplyRounded(img, 8f);
}

/// <summary>
/// カレンダーヘッダー（Monthly/Weekly/Daily 共通）のスタイル規格適用。
/// < > → 円形ゴースト、はじまりトグル → ピル。
/// 各 CalendarUI の初期化後に Style(headerTransform) を1回呼ぶ。
/// </summary>
public static class NavHeaderStyler
{
    private static readonly string[] NavBtnNames =
        { "PrevMonthBtn", "NextMonthBtn", "PrevDayBtn", "NextDayBtn" };
    private static readonly string[] ToggleNames =
        { "WeekStartSunBtn", "WeekStartMonBtn", "SunBtn", "MonBtn" };

    public static void Style(Transform header)
    {
        if (header == null) return;
        Canvas.ForceUpdateCanvases();   // ピル半径計算のため rect を確定させる

        foreach (var n in NavBtnNames)
        {
            var t = header.Find(n);
            if (t != null) UIStyleKit.ApplyRounded(t.GetComponent<Image>(), 12f);  // カード規格と統一
        }
        foreach (var n in ToggleNames)
        {
            var t = header.Find(n);
            if (t != null) UIStyleKit.ApplyRounded(t.GetComponent<Image>(), 12f);  // カード規格と統一
        }
    }
}
