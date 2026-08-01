using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 画質プリセットの決定と適用（Phase 4）。
///
/// URPでは影・レンダースケール・MSAAは QualitySettings ではなく **URPアセット** 側にある
/// （`QualitySettings.shadows = Disable` はURPでは無視される。実測で確認済み）。
///
/// 切り替えは **URPアセットを直接差し替える**。QualityLevelの切り替えを使わない理由は、
/// Mobileレベルが除外プラットフォームに Standalone を含んでおり、
/// デスクトップビルドではレベル自体が存在しなくなるため（実行時に names=[PC] だけになる）。
/// アセット差し替えならプラットフォーム設定に左右されない。
///
///   低   = Mobile_RPAsset … レンダースケール0.8 / ソフト影OFF / 追加ライトの影OFF / カスケード1 / 影距離20
///   標準 = PC_RPAsset     … 等倍 / ソフト影 / カスケード4 / 影距離30
///
/// 設定画面はまだ無いため、当面は**初回起動時に自動判定**して PlayerPrefs に保存する。
/// 設定画面ができたら Preference を Low/High に書き換えるUIを足すだけでよい。
/// </summary>
public static class QualityPresetManager
{
    public enum Preset { Auto = 0, Low = 1, High = 2 }

    private const string K_PREF = "tc_quality_pref";
    private const string PATH_LOW  = "Settings/Mobile_RPAsset"; // Resources基準ではなくAssetsパスで解決する
    private const string PATH_HIGH = "Settings/PC_RPAsset";

    private static RenderPipelineAsset _low, _high;
    private static bool _loaded;
    private static bool _applied;

    /// <summary>ユーザーの選択。設定画面ができたらここを書き換える。</summary>
    public static Preset Preference
    {
        get => (Preset)PlayerPrefs.GetInt(K_PREF, (int)Preset.Auto);
        // 設定画面から変更しても即時には切り替えない（再構築で画面が固まるため）。次回起動時に反映される。
        set { PlayerPrefs.SetInt(K_PREF, (int)value); PlayerPrefs.Save(); }
    }

    /// <summary>いま低画質が当たっているか。</summary>
    public static bool IsLow
    {
        get
        {
            EnsureLoaded();
            return _low != null && GraphicsSettings.defaultRenderPipeline == _low;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot() => Apply();

    /// <summary>
    /// URPアセットを読み込む。ビルドに含めるため Resources 配下に置く必要があるが、
    /// 既存の Assets/Settings を動かすと参照が切れるので、
    /// QualitySettings に割り当て済みのアセットを実行時に拾う方式にしている。
    /// </summary>
    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        // ビルドに含まれる全QualityLevelを走査してURPアセットを集める
        int cur = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            var rp = QualitySettings.renderPipeline;
            if (rp == null) continue;
            if (rp.name.Contains("Mobile")) _low = rp;
            else if (rp.name.Contains("PC")) _high = rp;
        }
        QualitySettings.SetQualityLevel(cur, false);

        // 既定（Graphics設定）も候補に入れる
        var def = GraphicsSettings.defaultRenderPipeline;
        if (_high == null && def != null) _high = def;
        if (_low == null) _low = Resources.Load<RenderPipelineAsset>(PATH_LOW);
        if (_high == null) _high = Resources.Load<RenderPipelineAsset>(PATH_HIGH);
    }

    /// <summary>
    /// 適用する。**起動時に一度だけ呼ぶ想定**。
    /// GraphicsSettings.defaultRenderPipeline の差し替えはレンダーパイプラインの再構築を伴い、
    /// エディタでは実行中に切り替えるとPlay Modeが一時停止する（実測で確認）。
    /// そのため設定画面から変更したときは「次回起動時に反映」とし、実行中は切り替えない。
    /// </summary>
    public static void Apply()
    {
        if (_applied) return; // 二重適用しない（実行中の切り替えを避ける）
        _applied = true;
        EnsureLoaded();

        bool low = Preference == Preset.Low || (Preference == Preset.Auto && IsWeakMachine());
        var want = low ? _low : _high;
        if (want == null)
        {
            Debug.LogWarning($"[Quality] {(low ? "低" : "標準")}用のURPアセットが見つからないため既定のまま");
            return;
        }

        if (GraphicsSettings.defaultRenderPipeline != want)
            GraphicsSettings.defaultRenderPipeline = want;
        QualitySettings.renderPipeline = want; // QualityLevel側の指定が勝たないよう揃える

        Debug.Log($"[Quality] {Preference} → {(low ? "低" : "標準")} ({want.name}) / {DescribeMachine()}");
    }

    /// <summary>
    /// 非力なマシンかの推定。厳しくしすぎると普通のPCが低画質になるので、
    /// 「明らかに弱い」ときだけ低に倒す（迷ったら標準）。実機での妥当性はWindowsテストで詰める。
    /// </summary>
    private static bool IsWeakMachine()
    {
        if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 1024) return true;
        if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 4096) return true;
        if (SystemInfo.processorCount > 0 && SystemInfo.processorCount <= 2) return true;

        var gpu = (SystemInfo.graphicsDeviceName ?? string.Empty).ToLowerInvariant();
        if (gpu.Contains("intel") && (gpu.Contains("hd graphics") || gpu.Contains("uhd graphics"))) return true;

        return false;
    }

    public static string DescribeMachine()
        => $"GPU={SystemInfo.graphicsDeviceName} VRAM={SystemInfo.graphicsMemorySize}MB " +
           $"RAM={SystemInfo.systemMemorySize}MB Cores={SystemInfo.processorCount}";
}
