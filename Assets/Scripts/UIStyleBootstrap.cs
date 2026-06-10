using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 1: カード角丸規格（12px）の一括適用ブートストラップ。
/// Canvas に取り付け、Start で1回だけ走る。
///
/// 【なぜランタイム適用か】
/// UISprites は手続き生成テクスチャ（HideFlags.DontSave）のため、
/// Edit Mode でシーンの Image.sprite に代入して保存すると missing reference になる。
/// よって形の規格化は必ず実行時に行う。
/// </summary>
public class UIStyleBootstrap : MonoBehaviour
{
    private void Start()
    {
        ApplyCardStandard();
    }

    private void ApplyCardStandard()
    {
        // ── 左カラムカード ──────────────────────────────
        ApplyCardAt("LeftColumn/TimerCard");
        ApplyCardAt("LeftColumn/CharacterCard");

        // ── タブコンテンツパネル（全タブ）─────────────────
        var windows = transform.Find("WindowManagerArea/Window Manager/Windows");
        if (windows != null)
        {
            foreach (Transform tab in windows)
            {
                var content = tab.Find("Content");
                if (content != null)
                    UIStyleKit.ApplyCard(content.GetComponent<Image>());
            }
        }

        // ── モーダル・ポップアップ・フロート ──────────────
        ApplyCardAt("EventModalDialog/Background");
        ApplyCardAt("DayEventsPopup");
        ApplyCardAt("FloatingWindow/WindowPanel");

        // ── タイマー操作ボタン（アウトライン 12px・ヘッダーボタンと角丸統一）──
        ApplyOutlineAt("LeftColumn/TimerCard/ControlButtons/ResetBtn");
        ApplyOutlineAt("LeftColumn/TimerCard/ControlButtons/PlayPauseBtn");
        ApplyOutlineAt("LeftColumn/TimerCard/ControlButtons/ConfirmBtn");
    }

    private void ApplyCardAt(string path)
    {
        var t = transform.Find(path);
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img != null) UIStyleKit.ApplyCard(img);
    }

    private void ApplyOutlineAt(string path)
    {
        var t = transform.Find(path);
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img != null) UIStyleKit.ApplyRoundedOutline(img, 12f);
    }
}
