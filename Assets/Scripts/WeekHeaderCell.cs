using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>週ヘッダーセル（曜日・日付・今日強調）</summary>
public class WeekHeaderCell : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI dowText;
    [SerializeField] private TextMeshProUGUI dateText;
    private Button btn;

    private void Awake() { btn = GetComponent<Button>(); }

public void Set(string dow, string date, bool isToday, bool isSunOrHol, bool isSat)
    {
        if (dowText  != null)
        {
            dowText.text  = dow;
            dowText.color = isSunOrHol
                ? UITheme_FocusMode.AccentRed
                : isSat
                    ? UITheme_FocusMode.AccentSatBlue
                    : UITheme_FocusMode.TextMuted;
        }
        if (dateText != null)
        {
            dateText.text  = date;
            dateText.color = isToday
                ? Color.white
                : isSunOrHol
                    ? UITheme_FocusMode.AccentRed
                    : isSat
                        ? UITheme_FocusMode.AccentSatBlue
                        : UITheme_FocusMode.TextBody;
            dateText.fontStyle = isToday ? FontStyles.Bold : FontStyles.Normal;
        }

        // セル全面塗りは廃止（脱Excel）。今日は日付数字背面の丸バッジで示す
        var img = GetComponent<Image>();
        if (img != null) img.color = Color.clear;

        var badge = transform.Find("TodayBadge");
        if (isToday && dateText != null)
        {
            if (badge == null)
            {
                var b = new GameObject("TodayBadge", typeof(RectTransform));
                b.transform.SetParent(transform, false);
                var brt = (RectTransform)b.transform;
                var drt = dateText.rectTransform;
                brt.anchorMin = drt.anchorMin; brt.anchorMax = drt.anchorMax;
                brt.pivot = drt.pivot;
                brt.offsetMin = drt.offsetMin + new Vector2(0f, 2f);
                brt.offsetMax = drt.offsetMax - new Vector2(0f, 2f);
                var bi = b.AddComponent<Image>();
                bi.sprite = UISprites.Circle;
                bi.preserveAspect = true;
                bi.color = new Color(0.31f, 0.55f, 0.95f, 0.90f);
                bi.raycastTarget = false;
                var ble = b.AddComponent<LayoutElement>(); ble.ignoreLayout = true;
                b.transform.SetAsFirstSibling();
                badge = b.transform;
            }
            badge.gameObject.SetActive(true);
        }
        else if (badge != null) badge.gameObject.SetActive(false);
    }

    public void SetClickAction(UnityEngine.Events.UnityAction action)
    {
        if (btn == null) btn = GetComponent<Button>();
        btn?.onClick.RemoveAllListeners();
        btn?.onClick.AddListener(action);
    }
}
