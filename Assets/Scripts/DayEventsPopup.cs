using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayEventsPopup : MonoBehaviour
{
    public TextMeshProUGUI dateLabel;
    public Transform       eventList;
    public Button          closeBtn;
    private Action<ScheduleEvent> _onEventClick;

    private void Awake() { closeBtn?.onClick.AddListener(Hide); gameObject.SetActive(false); }

    public void Show(string dateKey, List<ScheduleEvent> events,
                     Action<ScheduleEvent> onEventClick, Vector2 screenPos)
    {
        gameObject.SetActive(true);
        _onEventClick = onEventClick;
        if (dateLabel != null && !string.IsNullOrEmpty(dateKey) &&
            DateTime.TryParseExact(dateKey, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            dateLabel.text = dt.ToString("M月d日 (ddd)",
                new System.Globalization.CultureInfo("ja-JP"));
        if (eventList != null)
            foreach (Transform c in eventList) Destroy(c.gameObject);
        foreach (var ev in events)
            CreateRow(ev).transform.SetParent(eventList, false);

        // コルーチン廃止: SetActive 直後に ForceUpdateCanvases で同期的に位置決め
        // StartCoroutine は activeInHierarchy=false の場合に失敗するため置き換え
        Canvas.ForceUpdateCanvases();
        PositionPopup(GetComponent<RectTransform>(), screenPos);
    }

    public void Hide() => gameObject.SetActive(false);

    private void PositionPopup(RectTransform rt, Vector2 pos)
    {
        float w = rt.rect.width, h = rt.rect.height;
        float x = Mathf.Clamp(pos.x, w * 0.5f, Screen.width  - w * 0.5f);
        float y = Mathf.Clamp(pos.y, h * 0.5f, Screen.height - h * 0.5f);
        rt.anchoredPosition = new Vector2(x - Screen.width * 0.5f, y - Screen.height * 0.5f);
    }

    private GameObject CreateRow(ScheduleEvent ev)
    {
        var go  = new GameObject("EventRow");
        go.AddComponent<LayoutElement>().minHeight = 36f;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(12, 12, 0, 0);
        hlg.childForceExpandWidth = false; hlg.childControlWidth = true;
        hlg.childForceExpandHeight = true; hlg.childControlHeight = true;
        var dotGO = new GameObject("Dot"); dotGO.transform.SetParent(go.transform, false);
        var dLE = dotGO.AddComponent<LayoutElement>(); dLE.minWidth = 10f; dLE.preferredWidth = 10f; dLE.flexibleWidth = 0;
        var dImg = dotGO.AddComponent<Image>();
        var tag = TagConfig.GetById(ev.tagId);
        dImg.color = tag != null ? tag.chipBG : UITheme_FocusMode.AccentBlueFaint;
        var tGO = new GameObject("Text"); tGO.transform.SetParent(go.transform, false);
        tGO.AddComponent<LayoutElement>().flexibleWidth = 1;
        var tmp = tGO.AddComponent<TextMeshProUGUI>();
        tmp.text = (!string.IsNullOrEmpty(ev.time) ? ev.time + " " : "") + ev.title;
        tmp.fontSize = 18f; tmp.color = UITheme_FocusMode.TextBody;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.overflowMode = TextOverflowModes.Ellipsis; tmp.enableWordWrapping = false;
        var bgI = go.AddComponent<Image>(); bgI.color = Color.clear;
        var btn = go.AddComponent<Button>();
        var cb  = ColorBlock.defaultColorBlock; cb.highlightedColor = new Color(1f,1f,1f,0.08f);
        btn.colors = cb; btn.targetGraphic = bgI;
        var cap = ev; btn.onClick.AddListener(() => { Hide(); _onEventClick?.Invoke(cap); });
        return go;
    }
}
