using TMPro;
using UnityEngine;

/// <summary>
/// 情報密度の高いビュー（カレンダー/Todo/Routine）向けの可読性フォント選択。
/// Kotonoruは装飾性が高く小サイズで潰れやすいため、小さい文字だけ
/// Noto Sans JP（TMPフォールバックの先頭）に切り替える。
/// 大きい見出し・タイマー表示などはKotonoruのまま（アプリの個性を維持）。
/// </summary>
public static class UIFonts
{
    /// <summary>このサイズ以下の文字は可読性優先フォントを使う</summary>
    private const float SMALL_MAX = 18f;

    private static TMP_FontAsset _readable;

    /// <summary>可読性優先フォント（NotoSansJP）。取得失敗時は null。</summary>
    public static TMP_FontAsset Readable
    {
        get
        {
            if (_readable == null)
            {
                var fb = TMP_Settings.fallbackFontAssets;
                if (fb != null && fb.Count > 0 && fb[0] != null)
                    _readable = fb[0]; // NotoSansJP-Regular SDF
            }
            return _readable;
        }
    }

    /// <summary>
    /// fontSize が小さいテキストに可読性フォントを適用する。
    /// fontSize を設定した「後」に呼ぶこと（テキスト幅計測の前）。
    /// </summary>
    public static void ApplySmall(TMP_Text tmp)
    {
        if (tmp == null || Readable == null) return;
        if (tmp.fontSize <= SMALL_MAX) tmp.font = Readable;
    }


/// <summary>ランタイム生成した TMP_InputField の仕上げ。
    /// AddComponent 時（OnEnable）に textComponent が未設定だとキャレット・選択ハイライトの
    /// レンダラーが生成されず、全選択（Cmd/Ctrl+A）・ドラッグ選択・コピペが視覚的に機能しない。
    /// 参照を全て設定した後にこれを呼び、OnEnable を再実行して初期化させる。</summary>
    public static void FinalizeRuntimeInput(TMPro.TMP_InputField input)
    {
        if (input == null) return;
        if (input.gameObject.activeInHierarchy)
        {
            input.enabled = false;
            input.enabled = true;
        }
    }
}
