using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FocusTimerPanelUI : MonoBehaviour
{
    [SerializeField] private TimerController timerController;
    [SerializeField] private TMP_FontAsset timerFont;
    [SerializeField] private Vector2 panelSize = new Vector2(940f, 520f);
    [SerializeField, Range(1, 99)] private int minWorkMinutes = 1;
    [SerializeField, Range(1, 99)] private int maxWorkMinutes = 99;
    [SerializeField, Range(1, 20)] private int minBreakMinutes = 1;
    [SerializeField, Range(1, 20)] private int maxBreakMinutes = 20;
    [SerializeField, Range(1, 99)] private int minRounds = 1;
    [SerializeField, Range(1, 99)] private int maxRounds = 99;

    private RectTransform runtimePanel;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI cycleText;
    private TextMeshProUGUI timeText;
    private TextMeshProUGUI workValueText;
    private TextMeshProUGUI breakValueText;
    private TextMeshProUGUI roundsValueText;
    private Image progressFill;
    private TextMeshProUGUI primaryButtonText;
    private TextMeshProUGUI nextButtonText;
    private Button nextPhaseButton;
    private GameObject settingsPanel;
    private int selectedWorkMinutes = 25;
    private int selectedShortBreakMinutes = 5;
    private int selectedRounds = 4;
    private float nextRefresh;
    private bool completionHandled;

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
            selectedRounds = Mathf.Clamp(timerController.DefaultRounds, minRounds, maxRounds);
        }

        EnsurePanelBuilt();
    }

    private void OnEnable()
    {
        EnsurePanelBuilt();
        if (timerController != null)
            timerController.OnTimerChanged += Refresh;
        ShowSettingsAsDefault();
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
        overlayImage.color = new Color(0f, 0f, 0f, 0.55f);

        runtimePanel = CreateRect("FocusRuntimePanel", overlay, panelSize);
        runtimePanel.anchorMin = new Vector2(0.5f, 0.5f);
        runtimePanel.anchorMax = new Vector2(0.5f, 0.5f);
        runtimePanel.pivot = new Vector2(0.5f, 0.5f);
        runtimePanel.anchoredPosition = new Vector2(0f, 60f);

        var group = runtimePanel.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(24, 24, 20, 20);
        group.spacing = 8f;
        group.childAlignment = TextAnchor.UpperCenter;
        group.childControlHeight = false;
        group.childControlWidth = true;
        group.childForceExpandHeight = false;
        group.childForceExpandWidth = true;

        titleText = CreateText("Title", runtimePanel, "作業中", 68, TextAlignmentOptions.Center, 84f);
        cycleText = CreateText("Cycle", runtimePanel, "1/4サイクル", 36, TextAlignmentOptions.Center, 52f);
        timeText = CreateText("Time", runtimePanel, "25:00", 200, TextAlignmentOptions.Center, 220f);
        timeText.enableWordWrapping = false;
        timeText.overflowMode = TextOverflowModes.Overflow;

        var progressTrack = CreateRect("ProgressTrack", runtimePanel, new Vector2(0f, 14f));
        var progressLayout = progressTrack.gameObject.AddComponent<LayoutElement>();
        progressLayout.preferredHeight = 14f;
        var trackImage = progressTrack.gameObject.AddComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.35f);

        var progressFillRect = CreateRect("ProgressFill", progressTrack, Vector2.zero);
        progressFillRect.anchorMin = Vector2.zero;
        progressFillRect.anchorMax = Vector2.one;
        progressFillRect.offsetMin = Vector2.zero;
        progressFillRect.offsetMax = Vector2.zero;
        progressFill = progressFillRect.gameObject.AddComponent<Image>();
        progressFill.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;

        var controlRow = CreateRect("ControlRow", runtimePanel, new Vector2(0f, 72f));
        controlRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
        var controlGroup = controlRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        controlGroup.spacing = 8f;
        controlGroup.childControlHeight = true;
        controlGroup.childControlWidth = false;
        controlGroup.childForceExpandHeight = true;
        controlGroup.childForceExpandWidth = false;
        CreateActionButton("設定に戻る", controlRow, 320f, HandleBackToSettings);
        var primaryButton = CreateActionButton("開始", controlRow, 220f, HandlePrimaryAction);
        primaryButtonText = primaryButton.GetComponentInChildren<TextMeshProUGUI>();
        nextPhaseButton = CreateActionButton("次へ", controlRow, 180f, HandleNextPhase);
        nextButtonText = nextPhaseButton.GetComponentInChildren<TextMeshProUGUI>();

        BuildSettingsPanel(overlay);

        overlay.SetAsLastSibling();
        var stamp = transform.Find("StampCard");
        if (stamp != null)
            stamp.SetAsLastSibling();
    }

    private void EnsurePanelBuilt()
    {
        var existingOverlay = transform.Find("FullscreenFocusTimerOverlay");
        if (existingOverlay != null && titleText != null && timeText != null && primaryButtonText != null)
            return;

        if (existingOverlay != null)
            Destroy(existingOverlay.gameObject);

        BuildPanel();
    }

    private Button CreateActionButton(string label, RectTransform parent, float width, UnityEngine.Events.UnityAction action)
    {
        var rect = CreateRect(label.Replace(" ", "") + "Button", parent, new Vector2(width, 72f));
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = 72f;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var text = CreateText("Label", rect, label, 34, TextAlignmentOptions.Center, 72f);
        text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private void HandlePrimaryAction()
    {
        if (timerController == null) return;

        if (timerController.IsRunning || timerController.IsPaused)
        {
            timerController.TogglePause();
            return;
        }

        timerController.StartPomodoro(
            selectedWorkMinutes,
            selectedShortBreakMinutes,
            selectedShortBreakMinutes,
            selectedRounds);
        completionHandled = false;
        SetRuntimeVisible(true);
    }

    private void HandleNextPhase()
    {
        if (timerController == null || !timerController.IsAwaitingNextPhase) return;
        timerController.AdvanceToNextPhase();
    }

    private void HandleBackToSettings()
    {
        ShowSettingsAsDefault();
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

    private void ApplySettingsAndStart()
    {
        if (timerController == null) return;

        timerController.StartPomodoro(
            selectedWorkMinutes,
            selectedShortBreakMinutes,
            selectedShortBreakMinutes,
            selectedRounds);
        completionHandled = false;
        SetRuntimeVisible(true);
    }

    private void ShowSettingsAsDefault()
    {
        if (timerController != null)
            timerController.PreparePomodoro();
        completionHandled = false;
        SetRuntimeVisible(false);
    }

    private void SetRuntimeVisible(bool visible)
    {
        if (runtimePanel != null)
            runtimePanel.gameObject.SetActive(visible);
        if (settingsPanel != null)
            settingsPanel.SetActive(!visible);
    }

    private void Refresh()
    {
        if (timerController == null) return;

        if (timerController.Phase == TimerController.TimerPhase.Completed && !completionHandled)
        {
            completionHandled = true;
            ShowSettingsAsDefault();
            return;
        }

        titleText.text = GetTitleText();
        timeText.text = TimerController.FormatTime(timerController.IsRunning || timerController.IsPaused
                || timerController.IsAwaitingNextPhase || timerController.Phase == TimerController.TimerPhase.Completed
                ? timerController.RemainingSeconds
                : selectedWorkMinutes * 60f);
        progressFill.fillAmount = timerController.IsRunning || timerController.IsPaused || timerController.IsAwaitingNextPhase
            ? timerController.Progress01
            : 0f;
        cycleText.text = timerController.IsRunning || timerController.IsPaused || timerController.IsAwaitingNextPhase
            ? $"{timerController.CurrentRound}/{timerController.TotalRounds}サイクル"
            : $"0/{selectedRounds}サイクル";

        if (workValueText != null)
            workValueText.text = selectedWorkMinutes.ToString();
        if (breakValueText != null)
            breakValueText.text = selectedShortBreakMinutes.ToString();
        if (roundsValueText != null)
            roundsValueText.text = selectedRounds.ToString();

        if (primaryButtonText != null)
        {
            if (timerController.IsPaused)
                primaryButtonText.text = "再開";
            else if (timerController.IsRunning)
                primaryButtonText.text = "停止";
            else
                primaryButtonText.text = "開始";
        }

        if (nextPhaseButton != null)
            nextPhaseButton.interactable = timerController.IsAwaitingNextPhase;
        if (nextButtonText != null)
            nextButtonText.color = timerController.IsAwaitingNextPhase
                ? new Color(0.08f, 0.08f, 0.08f, 1f)
                : new Color(0.35f, 0.35f, 0.35f, 1f);
    }

    private void BuildSettingsPanel(RectTransform overlay)
    {
        settingsPanel = CreateRect("TimerSettingsPanel", overlay, new Vector2(760f, 430f)).gameObject;
        var rect = settingsPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 40f);

        var image = settingsPanel.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);

        var group = settingsPanel.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(20, 20, 16, 16);
        group.spacing = 10f;
        group.childControlHeight = false;
        group.childControlWidth = true;
        group.childForceExpandHeight = false;
        group.childForceExpandWidth = true;

        var header = CreateRect("SettingsHeader", settingsPanel.transform, new Vector2(0f, 60f));
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        var headerGroup = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerGroup.spacing = 8f;
        headerGroup.childControlHeight = true;
        headerGroup.childControlWidth = true;
        headerGroup.childForceExpandWidth = false;

        var title = CreateText("SettingsTitle", header, "ポモドーロ設定", 46, TextAlignmentOptions.Left, 60f);
        title.color = Color.white;
        title.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

        CreateStepperRow(settingsPanel.transform, "作業時間", "分", selectedWorkMinutes, out workValueText,
            () => OnWorkSliderChanged(selectedWorkMinutes - 1f),
            () => OnWorkSliderChanged(selectedWorkMinutes + 1f));
        CreateStepperRow(settingsPanel.transform, "休憩時間", "分", selectedShortBreakMinutes, out breakValueText,
            () => OnBreakSliderChanged(selectedShortBreakMinutes - 1f),
            () => OnBreakSliderChanged(selectedShortBreakMinutes + 1f));
        CreateStepperRow(settingsPanel.transform, "サイクル数", "サイクル", selectedRounds, out roundsValueText,
            () => OnRoundsSliderChanged(selectedRounds - 1f),
            () => OnRoundsSliderChanged(selectedRounds + 1f));

        var footer = CreateRect("SettingsFooter", settingsPanel.transform, new Vector2(0f, 72f));
        footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
        CreateActionButton("この設定で開始", footer, 320f, ApplySettingsAndStart);

        settingsPanel.SetActive(false);
    }

    private void CreateStepperRow(
        Transform parent,
        string label,
        string unit,
        int value,
        out TextMeshProUGUI valueText,
        UnityEngine.Events.UnityAction onDecrease,
        UnityEngine.Events.UnityAction onIncrease)
    {
        var row = CreateRect(label.Replace(" ", "") + "Row", parent, new Vector2(0f, 72f));
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;

        var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = 6f;
        group.childControlHeight = true;
        group.childControlWidth = true;
        group.childForceExpandHeight = true;
        group.childForceExpandWidth = false;

        var labelText = CreateText("Label", row, label, 32, TextAlignmentOptions.Left, 72f);
        labelText.color = Color.white;
        var labelLayout = labelText.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 220f;
        labelLayout.flexibleWidth = 1f;

        CreateMiniButton("-", row, onDecrease);

        var valueBox = CreateRect("ValueBox", row, new Vector2(100f, 56f));
        var valueLayout = valueBox.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 100f;
        valueLayout.preferredHeight = 56f;
        var valueBg = valueBox.gameObject.AddComponent<Image>();
        valueBg.color = new Color(1f, 1f, 1f, 0.95f);

        valueText = CreateText("Value", valueBox, value.ToString(), 30, TextAlignmentOptions.Center, 56f);
        valueText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        valueText.rectTransform.anchorMin = Vector2.zero;
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = Vector2.zero;
        valueText.rectTransform.offsetMax = Vector2.zero;

        CreateMiniButton("+", row, onIncrease);

        var unitText = CreateText("Unit", row, unit, 30, TextAlignmentOptions.Left, 72f);
        unitText.color = Color.white;
        unitText.gameObject.GetComponent<LayoutElement>().preferredWidth = 120f;
    }

    private Button CreateMiniButton(string label, Transform parent, UnityEngine.Events.UnityAction action)
    {
        var rect = CreateRect(label + "MiniButton", parent, new Vector2(56f, 56f));
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 56f;
        layout.preferredHeight = 56f;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.95f);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var text = CreateText("Label", rect, label, 34, TextAlignmentOptions.Center, 56f);
        text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, float preferredHeight)
    {
        var rect = CreateRect(name, parent, new Vector2(0f, preferredHeight));
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        if (timerFont != null)
            label.font = timerFont;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;

        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        return label;
    }

    private string GetTitleText()
    {
        if (timerController.Phase == TimerController.TimerPhase.Completed)
            return "完了";

        if (timerController.IsAwaitingNextPhase)
            return "フェーズ終了";

        return timerController.GetPhaseLabel() switch
        {
            "WORK" => "作業中",
            "SHORT BREAK" => "休憩中",
            "LONG BREAK" => "休憩中",
            _ => "作業準備"
        };
    }
}
