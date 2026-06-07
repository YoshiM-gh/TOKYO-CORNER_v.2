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
        var img = GetComponent<Image>();
        if (img != null)
            img.color = isToday
                ? UITheme_FocusMode.AccentBlueFaint
                : Color.clear;
    }

    public void SetClickAction(UnityEngine.Events.UnityAction action)
    {
        if (btn == null) btn = GetComponent<Button>();
        btn?.onClick.RemoveAllListeners();
        btn?.onClick.AddListener(action);
    }
}
