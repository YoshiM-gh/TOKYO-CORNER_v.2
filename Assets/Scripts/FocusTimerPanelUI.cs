using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FocusTimerPanelUI : MonoBehaviour
{
    [SerializeField] private TimerController timerController;
    [SerializeField] private Vector2 panelSize = new Vector2(520f, 500f);
    [SerializeField, Range(5, 60)] private int minWorkMinutes = 5;
    [SerializeField, Range(5, 60)] private int maxWorkMinutes = 60;
    [SerializeField, Range(1, 30)] private int minBreakMinutes = 1;
    [SerializeField, Range(1, 30)] private int maxBreakMinutes = 30;
    [SerializeField, Range(1, 99)] private int minRounds = 1;
    [SerializeField, Range(1, 99)] private int maxRounds = 99;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI timeText;
    private TextMeshProUGUI detailText;
    private TextMeshProUGUI workValueText;
    private TextMeshProUGUI breakValueText;
    private TextMeshProUGUI roundsValueText;
    private TextMeshProUGUI lapText;
    private Image progressFill;
    private TextMeshProUGUI startPauseText;
    private GameObject settingsPanel;
    private Slider workSlider;
    private Slider breakSlider;
    private Slider roundsSlider;
    private TimerController.TimerMode selectedMode = TimerController.TimerMode.Pomodoro;
    private int selectedWorkMinutes = 25;
    private int selectedShortBreakMinutes = 5;
    private float selectedLongBreakMinutes = 15f;
    private int selectedRounds = 4;
    private float nextRefresh;

    private void Awake()
    {
        if (timerController == null)
            timerController = GetComponent<TimerController>();

        if (timerController == null)
            timerController = GetComponentInParent<TimerController>();

        if (timerController != null)
        {
            selectedWorkMinutes = Mathf.Clamp(Mathf.RoundToInt(timerController.DefaultWorkMinutes), minWorkMinutes, maxWorkMinutes);
            selectedShortBreakMinutes = Mathf.Clamp(Mathf.RoundToInt(timerController.DefaultShortBreakMinutes), minBreakMinutes, maxBreakMinutes);
            selectedLongBreakMinutes = timerController.DefaultLongBreakMinutes;
            selectedRounds = Mathf.Clamp(timerController.DefaultRounds, minRounds, maxRounds);
        }

        BuildPanel();
    }

    private void OnEnable()
    {
        if (timerController != null)
            timerController.OnTimerChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (timerController != null)
            timerController.OnTimerChanged -= Refresh;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        Refresh();
    }

    private void BuildPanel()
    {
        var overlay = CreateRect("FullscreenFocusTimerOverlay", transform, Vector2.zero);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        var overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0.02f, 0.02f, 0.025f, 0.48f);

        var panel = CreateRect("MinimalFocusTimerPanel", overlay, panelSize);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;

        var group = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(16, 16, 16, 16);
        group.spacing = 10f;
        group.childAlignment = TextAnchor.UpperCenter;
        group.childControlHeight = false;
        group.childControlWidth = true;
        group.childForceExpandHeight = false;
        group.childForceExpandWidth = true;

        titleText = CreateText("Title", panel, "Focus timer", 22, TextAlignmentOptions.Left, 34f);

        var modeRow = CreateButtonRow(panel, ("Pomodoro", SelectPomodoro), ("Stopwatch", SelectStopwatch), ("Settings", ToggleSettings));
        modeRow.gameObject.GetComponent<LayoutElement>().preferredHeight = 38f;

        var ring = CreateRect("ProgressRing", panel, new Vector2(260f, 260f));
        var ringLayout = ring.gameObject.AddComponent<LayoutElement>();
        ringLayout.preferredHeight = 260f;
        ringLayout.preferredWidth = 260f;
        ringLayout.flexibleWidth = 0f;

        var ringBg = CreateCircleImage("RingBackground", ring, new Color(0.82f, 0.81f, 0.88f, 0.32f));
        ringBg.rectTransform.anchorMin = Vector2.zero;
        ringBg.rectTransform.anchorMax = Vector2.one;
        ringBg.rectTransform.offsetMin = Vector2.zero;
        ringBg.rectTransform.offsetMax = Vector2.zero;

        progressFill = CreateCircleImage("ProgressFill", ring, new Color(0.88f, 0.39f, 0.55f, 1f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Radial360;
        progressFill.fillOrigin = (int)Image.Origin360.Bottom;
        progressFill.fillClockwise = true;
        progressFill.fillAmount = 0f;
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = Vector2.one;
        progressFill.rectTransform.offsetMin = Vector2.zero;
        progressFill.rectTransform.offsetMax = Vector2.zero;

        var inner = CreateCircleImage("RingInner", ring, new Color(0.02f, 0.02f, 0.025f, 0.28f));
        inner.rectTransform.anchorMin = new Vector2(0.2f, 0.2f);
        inner.rectTransform.anchorMax = new Vector2(0.8f, 0.8f);
        inner.rectTransform.offsetMin = Vector2.zero;
        inner.rectTransform.offsetMax = Vector2.zero;

        timeText = CreateText("Time", ring, "25:00", 54, TextAlignmentOptions.Center, 96f);
        var timeRect = timeText.rectTransform;
        timeRect.anchorMin = Vector2.zero;
        timeRect.anchorMax = Vector2.one;
        timeRect.offsetMin = Vector2.zero;
        timeRect.offsetMax = Vector2.zero;

        detailText = CreateText("Detail", panel, "0/4  Start focus", 14, TextAlignmentOptions.Center, 24f);

        var controlRow = CreateButtonRow(panel, ("Start", ToggleStartPause), ("Reset", ResetTimer), ("Lap", RecordLap));
        startPauseText = controlRow.GetChild(0).GetComponentInChildren<TextMeshProUGUI>();
        lapText = CreateText("LapText", panel, "", 10, TextAlignmentOptions.Center, 16f);

        BuildSettingsPanel(overlay);

        overlay.SetAsLastSibling();
        var stamp = transform.Find("StampCard");
        if (stamp != null)
            stamp.SetAsLastSibling();
    }

    private RectTransform CreateButtonRow(RectTransform parent, params (string label, UnityEngine.Events.UnityAction action)[] buttons)
    {
        var row = CreateRect("ButtonRow", parent, new Vector2(0f, 34f));
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

        var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = 4f;
        group.childControlHeight = true;
        group.childControlWidth = true;
        group.childForceExpandHeight = true;
        group.childForceExpandWidth = true;

        foreach (var (label, action) in buttons)
            CreateButton(label, row, action);

        return row;
    }

    private Button CreateButton(string label, RectTransform parent, UnityEngine.Events.UnityAction action)
    {
        var rect = CreateRect(label.Replace(" ", "") + "Button", parent, new Vector2(0f, 34f));
        var image = rect.gameObject.AddComponent<Image>();
        image.color = GetButtonColor(label);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var text = CreateText("Label", rect, label, 12, TextAlignmentOptions.Center, 34f);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private void SelectPomodoro()
    {
        selectedMode = TimerController.TimerMode.Pomodoro;
        timerController?.Stop();
        Refresh();
    }

    private void SelectStopwatch()
    {
        selectedMode = TimerController.TimerMode.Stopwatch;
        timerController?.PrepareStopwatch();
        Refresh();
    }

    private void ToggleStartPause()
    {
        if (timerController == null) return;

        if (timerController.IsRunning || timerController.IsPaused)
        {
            timerController.TogglePause();
            return;
        }

        if (selectedMode == TimerController.TimerMode.Stopwatch)
            timerController.StartStopwatch();
        else
            timerController.StartPomodoro(selectedWorkMinutes, selectedShortBreakMinutes, selectedLongBreakMinutes, selectedRounds);
    }

    private void ResetTimer()
    {
        if (timerController == null) return;

        timerController.Stop();
        if (selectedMode == TimerController.TimerMode.Pomodoro)
            timerController.Stop();
        else
            timerController.PrepareStopwatch();
    }

    private void RecordLap()
    {
        timerController?.RecordLap();
    }

    private void ToggleSettings()
    {
        if (settingsPanel == null) return;
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    private void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void OnWorkSliderChanged(float value)
    {
        selectedWorkMinutes = Mathf.Clamp(Mathf.RoundToInt(value), minWorkMinutes, maxWorkMinutes);
        Refresh();
    }

    private void OnBreakSliderChanged(float value)
    {
        selectedShortBreakMinutes = Mathf.Clamp(Mathf.RoundToInt(value), minBreakMinutes, maxBreakMinutes);
        Refresh();
    }

    private void OnRoundsSliderChanged(float value)
    {
        selectedRounds = Mathf.Clamp(Mathf.RoundToInt(value), minRounds, maxRounds);
        Refresh();
    }

    private void Refresh()
    {
        if (timerController == null) return;

        bool stopwatch = selectedMode == TimerController.TimerMode.Stopwatch;
        bool activeStopwatch = timerController.Mode == TimerController.TimerMode.Stopwatch;
        titleText.text = stopwatch ? "STOPWATCH" : "POMODORO";
        timeText.text = stopwatch
            ? TimerController.FormatTime(activeStopwatch ? timerController.ElapsedSeconds : 0f)
            : TimerController.FormatTime(timerController.IsRunning || timerController.IsPaused
                ? timerController.RemainingSeconds
                : selectedWorkMinutes * 60f);
        progressFill.fillAmount = stopwatch ? 0f : timerController.Progress01;
        detailText.text = stopwatch
            ? (timerController.IsRunning ? "RUNNING" : "READY")
            : timerController.IsRunning || timerController.IsPaused
                ? $"{timerController.GetPhaseLabel()}  {timerController.CurrentRound}/{timerController.TotalRounds}"
                : $"0/{selectedRounds}  Start focus";

        if (workValueText != null)
            workValueText.text = selectedWorkMinutes.ToString();
        if (breakValueText != null)
            breakValueText.text = selectedShortBreakMinutes.ToString();
        if (roundsValueText != null)
            roundsValueText.text = selectedRounds.ToString();

        if (startPauseText != null)
        {
            if (timerController.IsPaused)
                startPauseText.text = "Resume";
            else if (timerController.IsRunning)
                startPauseText.text = "Pause";
            else
                startPauseText.text = "Start";
        }

        lapText.text = timerController.Laps.Count > 0 ? timerController.Laps[0] : "";
    }

    private void BuildSettingsPanel(RectTransform overlay)
    {
        settingsPanel = CreateRect("TimerSettingsPanel", overlay, new Vector2(520f, 300f)).gameObject;
        var rect = settingsPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 18f);

        var image = settingsPanel.AddComponent<Image>();
        image.color = new Color(1f, 0.93f, 0.95f, 0.98f);

        var group = settingsPanel.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(18, 18, 14, 14);
        group.spacing = 12f;
        group.childControlHeight = false;
        group.childControlWidth = true;
        group.childForceExpandHeight = false;
        group.childForceExpandWidth = true;

        var header = CreateRect("SettingsHeader", settingsPanel.transform, new Vector2(0f, 32f));
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
        var headerGroup = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerGroup.childControlHeight = true;
        headerGroup.childControlWidth = true;
        headerGroup.childForceExpandWidth = false;

        var title = CreateText("SettingsTitle", header, "Timer Settings", 16, TextAlignmentOptions.Left, 32f);
        title.color = new Color(0.18f, 0.15f, 0.18f, 1f);
        title.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
        CreateButton("X", header, CloseSettings);

        workSlider = CreateSliderRow(settingsPanel.transform, "Focus duration (min)", minWorkMinutes, maxWorkMinutes, selectedWorkMinutes, out workValueText, OnWorkSliderChanged);
        breakSlider = CreateSliderRow(settingsPanel.transform, "Break duration (min)", minBreakMinutes, maxBreakMinutes, selectedShortBreakMinutes, out breakValueText, OnBreakSliderChanged);
        roundsSlider = CreateSliderRow(settingsPanel.transform, "Sessions", minRounds, maxRounds, selectedRounds, out roundsValueText, OnRoundsSliderChanged);

        settingsPanel.SetActive(false);
    }

    private Slider CreateSliderRow(Transform parent, string label, int min, int max, int value, out TextMeshProUGUI valueText, UnityEngine.Events.UnityAction<float> onChanged)
    {
        var row = CreateRect(label.Replace(" ", "") + "Row", parent, new Vector2(0f, 62f));
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;

        var group = row.gameObject.AddComponent<VerticalLayoutGroup>();
        group.spacing = 4f;
        group.childControlHeight = false;
        group.childControlWidth = true;

        var labelRow = CreateRect("LabelRow", row, new Vector2(0f, 22f));
        labelRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
        var labelGroup = labelRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        labelGroup.childControlWidth = true;
        labelGroup.childForceExpandWidth = false;

        var labelText = CreateText("Label", labelRow, label, 12, TextAlignmentOptions.Left, 22f);
        labelText.color = new Color(0.3f, 0.25f, 0.3f, 1f);
        labelText.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

        valueText = CreateText("Value", labelRow, value.ToString(), 12, TextAlignmentOptions.Right, 22f);
        valueText.color = new Color(0.45f, 0.36f, 0.42f, 1f);
        valueText.gameObject.GetComponent<LayoutElement>().preferredWidth = 48f;

        var sliderRect = CreateRect("Slider", row, new Vector2(0f, 28f));
        sliderRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
        var slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.value = value;

        var background = CreateRect("Background", sliderRect, Vector2.zero);
        background.anchorMin = new Vector2(0f, 0.45f);
        background.anchorMax = new Vector2(1f, 0.55f);
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        var bgImage = background.gameObject.AddComponent<Image>();
        bgImage.color = new Color(0.9f, 0.78f, 0.84f, 1f);
        slider.targetGraphic = bgImage;

        var fillArea = CreateRect("Fill Area", sliderRect, Vector2.zero);
        fillArea.anchorMin = new Vector2(0f, 0.45f);
        fillArea.anchorMax = new Vector2(1f, 0.55f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        var fill = CreateRect("Fill", fillArea, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.67f, 0.28f, 0.43f, 1f);
        slider.fillRect = fill;

        var handle = CreateRect("Handle", sliderRect, new Vector2(18f, 18f));
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.67f, 0.28f, 0.43f, 1f);
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;

        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, float preferredHeight)
    {
        var rect = CreateRect(name, parent, new Vector2(0f, preferredHeight));
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        label.alignment = alignment;
        label.raycastTarget = false;

        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        return label;
    }

    private static Image CreateCircleImage(string name, Transform parent, Color color)
    {
        var rect = CreateRect(name, parent, Vector2.zero);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = CreateCircleSprite(96);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite CreateCircleSprite(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        float radius = size * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float alpha = dx * dx + dy * dy <= radius * radius ? 255f : 0f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Color GetButtonColor(string label)
    {
        if (label.Contains("Pomodoro"))
            return new Color(0.82f, 0.32f, 0.48f, 1f);
        if (label.Contains("Stopwatch"))
            return new Color(0.55f, 0.56f, 0.82f, 1f);
        if (label.Contains("Settings"))
            return new Color(0.9f, 0.84f, 0.9f, 1f);
        if (label.Contains("Reset"))
            return new Color(0.82f, 0.74f, 0.78f, 1f);
        if (label.Contains("min") || label.Contains("lap"))
            return new Color(0.12f, 0.12f, 0.12f, 1f);
        return new Color(0.78f, 0.79f, 0.95f, 1f);
    }
}
