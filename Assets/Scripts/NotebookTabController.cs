using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Notebookパネルのタブ切り替え管理。
/// 月 / 週 / 日 / Todo / Memo の5タブ。
/// </summary>
public class NotebookTabController : MonoBehaviour
{
    [Header("タブボタン (月/週/日/Todo/Memo 順)")]
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private Image[]  tabButtonImages;
    [SerializeField] private TextMeshProUGUI[] tabLabels;

    [Header("コンテンツパネル (同順)")]
    [SerializeField] private GameObject[] contentPanels;

    [Header("色")]
    [SerializeField] private Color colorActive   = new Color(1f, 1f, 1f, 0.14f);
    [SerializeField] private Color colorInactive = new Color(1f, 1f, 1f, 0.04f);
    [SerializeField] private Color labelActive   = new Color(1f, 1f, 1f, 1.00f);
    [SerializeField] private Color labelInactive = new Color(1f, 1f, 1f, 0.45f);

    private int currentTab = 0;

    private void OnEnable()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int idx = i;
            tabButtons[i].onClick.RemoveAllListeners();
            tabButtons[i].onClick.AddListener(() => SwitchTab(idx));
        }
        SwitchTab(currentTab);
    }

    public void SwitchTab(int index)
    {
        currentTab = index;
        for (int i = 0; i < contentPanels.Length; i++)
        {
            bool active = i == index;
            if (contentPanels[i]  != null) contentPanels[i].SetActive(active);
            if (tabButtonImages[i] != null) tabButtonImages[i].color = active ? colorActive : colorInactive;
            if (tabLabels[i]       != null) tabLabels[i].color       = active ? labelActive : labelInactive;
        }
    }
}
