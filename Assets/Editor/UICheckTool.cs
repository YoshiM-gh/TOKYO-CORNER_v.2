
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Text;
using System.IO;

public class UICheckTool
{
    [MenuItem("TOKYO CORNER/Check UI Settings")]
    public static void Check()
    {
        var sb = new StringBuilder();

        // Canvas Scaler
        var canvas = GameObject.Find("Canvas");
        if (canvas != null)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                sb.AppendLine("=== Canvas Scaler ===");
                sb.AppendLine("UIScaleMode: " + scaler.uiScaleMode);
                sb.AppendLine("ReferenceResolution: " + scaler.referenceResolution);
                sb.AppendLine("MatchWidthOrHeight: " + scaler.matchWidthOrHeight);
            }
            else sb.AppendLine("CanvasScaler: NOT FOUND");
        }
        else sb.AppendLine("Canvas: NOT FOUND");

        // NotebookPanel
        var nb = GameObject.Find("NotebookPanel");
        if (nb != null)
        {
            var rt = nb.GetComponent<RectTransform>();
            var hlg = nb.GetComponent<HorizontalLayoutGroup>();
            sb.AppendLine("=== NotebookPanel ===");
            sb.AppendLine("anchorMin: " + rt.anchorMin);
            sb.AppendLine("anchorMax: " + rt.anchorMax);
            sb.AppendLine("sizeDelta: " + rt.sizeDelta);
            sb.AppendLine("anchoredPosition: " + rt.anchoredPosition);
            sb.AppendLine("HLG: " + (hlg != null ? "YES" : "NO"));
        }
        else sb.AppendLine("NotebookPanel: NOT FOUND");

        // TabBar
        var tb = GameObject.Find("TabBar");
        if (tb != null)
        {
            var rt = tb.GetComponent<RectTransform>();
            var hlg = tb.GetComponent<HorizontalLayoutGroup>();
            sb.AppendLine("=== TabBar ===");
            sb.AppendLine("sizeDelta: " + rt.sizeDelta);
            sb.AppendLine("anchorMin: " + rt.anchorMin);
            sb.AppendLine("anchorMax: " + rt.anchorMax);
            if (hlg != null)
            {
                sb.AppendLine("HLG.childControlWidth: " + hlg.childControlWidth);
                sb.AppendLine("HLG.childForceExpandWidth: " + hlg.childForceExpandWidth);
            }
            foreach (Transform child in tb.transform)
            {
                var crt = child.GetComponent<RectTransform>();
                var le = child.GetComponent<LayoutElement>();
                string leStr = le != null ? " LE_w:" + le.preferredWidth + " LE_h:" + le.preferredHeight : "";
                sb.AppendLine("  Tab[" + child.name + "] sz:" + crt.sizeDelta + leStr);
            }
        }
        else sb.AppendLine("TabBar: NOT FOUND");

        // Card
        var card = GameObject.Find("Card");
        if (card != null)
        {
            var rt = card.GetComponent<RectTransform>();
            sb.AppendLine("=== Card ===");
            sb.AppendLine("anchorMin: " + rt.anchorMin);
            sb.AppendLine("anchorMax: " + rt.anchorMax);
            sb.AppendLine("sizeDelta: " + rt.sizeDelta);
            sb.AppendLine("anchoredPosition: " + rt.anchoredPosition);
        }
        else sb.AppendLine("Card: NOT FOUND");

        string result = sb.ToString();
        string outPath = "C:/Users/yoshi/UnityProjects/TOKYO-CORNER_v.2/ui_check_result.txt";
        File.WriteAllText(outPath, result);
        Debug.Log("[UICheck]\n" + result);
    }
}
