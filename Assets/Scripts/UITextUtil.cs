using TMPro;
using UnityEngine;

/// <summary>TMP テキストの一行省略ユーティリティ（TMP Ellipsis の日本語誤動作回避）</summary>
public static class UITextUtil
{
    /// <summary>幅 width に収まるよう s を切り詰めて「…」を付ける（収まるならそのまま返す）</summary>
    public static string EllipsizeOneLine(TMP_Text txt, string s, float width)
    {
        if (string.IsNullOrEmpty(s) || width <= 0f) return s;
        if (txt.GetPreferredValues(s).x <= width) return s;
        for (int n = s.Length - 1; n > 0; n--)
        {
            string cand = s.Substring(0, n) + "…";
            if (txt.GetPreferredValues(cand).x <= width) return cand;
        }
        return "…";
    }
}
