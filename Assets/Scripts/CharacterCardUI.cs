using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 左下 CharacterCard。Phase A: 上部に『今日の一言』吹き出し（日付シードで1日1セリフ・初回表示で記録）。
/// キャラ表示(idle)・エモートは Phase B/C。吹き出しは常時表示（消えない）。
/// </summary>
public class CharacterCardUI : MonoBehaviour
{
    GameObject _bubble;
    TextMeshProUGUI _lineText;
    TMP_FontAsset _font;
    string _shownDate;

    [Header("セリフのセッション内ローテーション")]
    [SerializeField] bool rotateMoments = true;            // セッション中に別のセリフへ切り替えるか
    [SerializeField] float momentRotateSeconds = 1200f;    // 切り替え間隔（秒）。既定1200=20分
    float _rotateTimer;

    void OnEnable()
    {
        EnsureBuilt();
        RefreshToday();
        _rotateTimer = momentRotateSeconds;
    }

    void Update()
    {
        if (!rotateMoments || _lineText == null) return;
        if (MomentLibrary.PoolCount <= 1) return;
        _rotateTimer -= Time.unscaledDeltaTime;
        if (_rotateTimer <= 0f) RotateMomentNow();
    }

    /// <summary>セッション内ローテーション：プールから現在と違うセリフをランダム表示（記録はしない＝アーカイブは日次1本のまま）。</summary>
    [ContextMenu("▶ セリフを今すぐ切り替え(テスト)")]
    public void RotateMomentNow()
    {
        if (_lineText == null) return;
        var tmpl = MomentLibrary.PickRandom(_lineText.text);
        if (tmpl != null) _lineText.text = tmpl.body;
        _rotateTimer = momentRotateSeconds;
    }

    void EnsureBuilt()
    {
        if (_bubble != null) return;
        _font = FindFont();

        _bubble = new GameObject("SpeechBubble", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _bubble.transform.SetParent(transform, false);
        var brt = (RectTransform)_bubble.transform;
        brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
        brt.sizeDelta = new Vector2(-28f, 0f);          // 横: カード幅 - 28（左右14マージン）
        brt.anchoredPosition = new Vector2(0f, -28f);   // 上から16px
        var bimg = _bubble.GetComponent<Image>();
        bimg.color = new Color(1f, 1f, 1f, 1f);  // 白で固定（背景に色をつけてコントラスト）
        UIStyleKit.ApplyRounded(bimg, 12f);
        var vlg = _bubble.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 13, 13);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fit = _bubble.GetComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var tGO = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI));
        tGO.transform.SetParent(_bubble.transform, false);
        _lineText = tGO.GetComponent<TextMeshProUGUI>();
        if (_font != null) _lineText.font = _font;
        _lineText.text = "";
        _lineText.fontSize = 17f;
        _lineText.color = new Color(0.11f, 0.11f, 0.12f, 1f);   // 黒文字（やや黒気味グレー）
        _lineText.alignment = TextAlignmentOptions.TopLeft;
        _lineText.textWrappingMode = TextWrappingModes.Normal;
        _lineText.lineSpacing = -35f;   // 行間を詰める（フォント標準が広いので負で寄せる）
    }

    void RefreshToday()
    {
        if (_lineText == null) return;
        string dk = NotebookManager.DateKey(DateTime.Now);
        if (_shownDate == dk && !string.IsNullOrEmpty(_lineText.text)) return;
        var rec = GetOrCreateToday(dk);
        _lineText.text = rec != null ? rec.body : "";
        _shownDate = dk;
    }

    DailyMoment GetOrCreateToday(string dk)
    {
        var nm = NotebookManager.Instance;
        DailyMoment rec = nm != null ? nm.GetMomentForDate(dk) : null;
        if (rec == null)
        {
            var tmpl = MomentLibrary.PickForDate(dk);
            if (tmpl != null)
            {
                rec = new DailyMoment { id = dk, date = dk, speakerId = tmpl.speakerId, type = tmpl.type, body = tmpl.body };
                if (nm != null) nm.RecordMoment(rec);
            }
        }
        return rec;
    }

    TMP_FontAsset FindFont()
    {
        var all = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all) if (t != null && t.font != null && t.gameObject != gameObject) return t.font;
        return TMP_Settings.defaultFontAsset;
    }
}
