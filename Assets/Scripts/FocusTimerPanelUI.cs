using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FocusTimerPanelUI : MonoBehaviour
{
    [SerializeField] private TimerController timerController;
    [SerializeField] private bool autoBindOnAwake = true;

    [Header("Panels")]
    [SerializeField] private GameObject runtimePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Status Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI cycleText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI percentText;

    [Header("Progress Bars (Modern UI Pack)")]
    [SerializeField] private ProgressBar radialProgressBar;
    [SerializeField] private ProgressBar horizontalProgressBar;

    [Header("Buttons (Modern UI Pack)")]
    [SerializeField] private ButtonManager primaryActionButton;
    [SerializeField] private ButtonManager nextPhaseButton;
    [SerializeField] private ButtonManager backToSettingsButton;
    [SerializeField] private ButtonManager startWithSettingsButton;

    [Header("Settings Sliders (Modern UI Pack)")]
    [SerializeField] private SliderManager workMinutesSlider;
    [SerializeField] private SliderManager breakMinutesSlider;
    [SerializeField] private SliderManager roundsSlider;

    [Header("Limits")]
    [SerializeField, Range(1, 99)] private int minWorkMinutes = 1;
    [SerializeField, Range(1, 99)] private int maxWorkMinutes = 99;
    [SerializeField, Range(1, 30)] private int minBreakMinutes = 1;
    [SerializeField, Range(1, 30)] private int maxBreakMinutes = 30;
    [SerializeField, Range(1, 99)] private int minRounds = 1;
    [SerializeField, Range(1, 99)] private int maxRounds = 99;

    private int selectedWorkMinutes = 25;
    private int selectedBreakMinutes = 5;
    private int selectedRounds = 4;
    private bool completionHandled;

    private void Awake()
    {
        if (autoBindOnAwake)
            AutoBindIfMissing();

        if (timerController == null)
            timerController = GetComponent<TimerController>();
        if (timerController == null)
            timerController = GetComponentInParent<TimerController>();

        if (timerController != null)
        {
            selectedWorkMinutes = Mathf.Clamp(Mathf.RoundToInt(timerController.DefaultWorkMinutes), minWorkMinutes, maxWorkMinutes);
            selectedBreakMinutes = Mathf.Clamp(Mathf.RoundToInt(timerController.DefaultShortBreakMinutes), minBreakMinutes, maxBreakMinutes);
            selectedRounds = Mathf.Clamp(timerController.DefaultRounds, minRounds, maxRounds);
        }

        ConfigureSliders();
        PushSelectionToSliders();
        LogMissingCriticalReferences();
    }

    private void OnEnable()
    {
        if (timerController != null)
            timerController.OnTimerChanged += Refresh;

        BindUiEvents();
        ShowSettingsAsDefault();
        Refresh();
    }

    private void OnDisable()
    {
        if (timerController != null)
            timerController.OnTimerChanged -= Refresh;

        UnbindUiEvents();
    }

    private void BindUiEvents()
    {
        if (primaryActionButton != null) primaryActionButton.onClick.AddListener(HandlePrimaryAction);
        if (nextPhaseButton != null) nextPhaseButton.onClick.AddListener(HandleNextPhase);
        if (backToSettingsButton != null) backToSettingsButton.onClick.AddListener(HandleBackToSettings);
        if (startWithSettingsButton != null) startWithSettingsButton.onClick.AddListener(ApplySettingsAndStart);

        if (workMinutesSlider != null) workMinutesSlider.sliderEvent.AddListener(OnWorkSliderChanged);
        if (breakMinutesSlider != null) breakMinutesSlider.sliderEvent.AddListener(OnBreakSliderChanged);
        if (roundsSlider != null) roundsSlider.sliderEvent.AddListener(OnRoundsSliderChanged);
    }

    private void UnbindUiEvents()
    {
        if (primaryActionButton != null) primaryActionButton.onClick.RemoveListener(HandlePrimaryAction);
        if (nextPhaseButton != null) nextPhaseButton.onClick.RemoveListener(HandleNextPhase);
        if (backToSettingsButton != null) backToSettingsButton.onClick.RemoveListener(HandleBackToSettings);
        if (startWithSettingsButton != null) startWithSettingsButton.onClick.RemoveListener(ApplySettingsAndStart);

        if (workMinutesSlider != null) workMinutesSlider.sliderEvent.RemoveListener(OnWorkSliderChanged);
        if (breakMinutesSlider != null) breakMinutesSlider.sliderEvent.RemoveListener(OnBreakSliderChanged);
        if (roundsSlider != null) roundsSlider.sliderEvent.RemoveListener(OnRoundsSliderChanged);
    }

    private void ConfigureSliders()
    {
        ConfigureSlider(workMinutesSlider, minWorkMinutes, maxWorkMinutes);
        ConfigureSlider(breakMinutesSlider, minBreakMinutes, maxBreakMinutes);
        ConfigureSlider(roundsSlider, minRounds, maxRounds);
    }

    [ContextMenu("Auto Bind Missing Focus UI References")]
    private void AutoBindIfMissing()
    {
        if (runtimePanel == null)
            runtimePanel = FindChildGameObject("FocusRuntimePanel");
        if (settingsPanel == null)
            settingsPanel = FindChildGameObject("FocusSettingsPanel");

        if (titleText == null)
            titleText = FindChildText("TitleText") ?? FindChildText("PomodoroNotificationText");
        if (cycleText == null)
            cycleText = FindChildText("CycleText");
        if (timeText == null)
            timeText = FindChildText("TimeText") ?? FindChildText("TimerText");
        if (percentText == null)
            percentText = FindChildText("PercentText");

        if (radialProgressBar == null)
            radialProgressBar = FindProgressBarByName("PB - Radial (Regular)");
        if (horizontalProgressBar == null)
            horizontalProgressBar = FindProgressBarByName("PB - Standard");

        if (primaryActionButton == null)
            primaryActionButton = FindButtonManager("PrimaryActionButton");
        if (nextPhaseButton == null)
            nextPhaseButton = FindButtonManager("NextPhaseButton");
        if (backToSettingsButton == null)
            backToSettingsButton = FindButtonManager("BackToSettingsButton");
        if (startWithSettingsButton == null)
            startWithSettingsButton = FindButtonManager("StartWithSettingsButton");

        if (workMinutesSlider == null)
            workMinutesSlider = FindSliderManager("WorkMinutesSlider");
        if (breakMinutesSlider == null)
            breakMinutesSlider = FindSliderManager("BreakMinutesSlider");
        if (roundsSlider == null)
            roundsSlider = FindSliderManager("RoundsSlider");
    }

    private GameObject FindChildGameObject(string name)
    {
        Transform child = FindDescendantByName(name);
        return child != null ? child.gameObject : null;
    }

    private TextMeshProUGUI FindChildText(string name)
    {
        Transform child = FindDescendantByName(name);
        if (child == null) return null;
        return child.GetComponent<TextMeshProUGUI>();
    }

    private ProgressBar FindProgressBarByName(string name)
    {
        Transform child = FindDescendantByName(name);
        if (child == null) return null;
        return child.GetComponent<ProgressBar>();
    }

    private ButtonManager FindButtonManager(string name)
    {
        Transform child = FindDescendantByName(name);
        if (child == null) return null;
        return child.GetComponent<ButtonManager>();
    }

    private SliderManager FindSliderManager(string name)
    {
        Transform child = FindDescendantByName(name);
        if (child == null) return null;
        return child.GetComponent<SliderManager>();
    }

    private Transform FindDescendantByName(string name)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }

    private void LogMissingCriticalReferences()
    {
        if (timerController == null)
            Debug.LogWarning("[FocusUI] TimerController is not assigned.", this);
        if (runtimePanel == null)
            Debug.LogWarning("[FocusUI] RuntimePanel is missing.", this);
        if (settingsPanel == null)
            Debug.LogWarning("[FocusUI] FocusSettingsPanel is missing. Settings screen will not be shown.", this);
        if (primaryActionButton == null)
            Debug.LogWarning("[FocusUI] PrimaryActionButton is missing. Start/Pause action will be unavailable.", this);
    }

    private static void ConfigureSlider(SliderManager sliderManager, int min, int max)
    {
        if (sliderManager == null || sliderManager.mainSlider == null) return;

        Slider slider = sliderManager.mainSlider;
        slider.wholeNumbers = true;
        slider.minValue = min;
        slider.maxValue = max;
    }

    private void PushSelectionToSliders()
    {
        SetSliderValue(workMinutesSlider, selectedWorkMinutes);
        SetSliderValue(breakMinutesSlider, selectedBreakMinutes);
        SetSliderValue(roundsSlider, selectedRounds);
    }

    private static void SetSliderValue(SliderManager sliderManager, int value)
    {
        if (sliderManager == null || sliderManager.mainSlider == null) return;

        sliderManager.mainSlider.SetValueWithoutNotify(value);
        sliderManager.UpdateUI();
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
            selectedBreakMinutes,
            selectedBreakMinutes,
            selectedRounds);

        completionHandled = false;
        SetRuntimeVisible(true);
        Refresh();
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

    private void ApplySettingsAndStart()
    {
        if (timerController == null) return;

        timerController.StartPomodoro(
            selectedWorkMinutes,
            selectedBreakMinutes,
            selectedBreakMinutes,
            selectedRounds);

        completionHandled = false;
        SetRuntimeVisible(true);
        Refresh();
    }

    private void OnWorkSliderChanged(float value)
    {
        selectedWorkMinutes = Mathf.Clamp(Mathf.RoundToInt(value), minWorkMinutes, maxWorkMinutes);
        Refresh();
    }

    private void OnBreakSliderChanged(float value)
    {
        selectedBreakMinutes = Mathf.Clamp(Mathf.RoundToInt(value), minBreakMinutes, maxBreakMinutes);
        Refresh();
    }

    private void OnRoundsSliderChanged(float value)
    {
        selectedRounds = Mathf.Clamp(Mathf.RoundToInt(value), minRounds, maxRounds);
        Refresh();
    }

    private void ShowSettingsAsDefault()
    {
        if (timerController != null)
            timerController.PreparePomodoro();

        completionHandled = false;
        SetRuntimeVisible(false);
        Refresh();
    }

    private void SetRuntimeVisible(bool showRuntime)
    {
        if (runtimePanel != null)
            runtimePanel.SetActive(showRuntime);
        if (settingsPanel != null)
            settingsPanel.SetActive(!showRuntime);
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

        bool isTimerVisibleState = timerController.IsRunning
                                   || timerController.IsPaused
                                   || timerController.IsAwaitingNextPhase
                                   || timerController.Phase == TimerController.TimerPhase.Completed;

        float remainingSeconds = isTimerVisibleState
            ? timerController.RemainingSeconds
            : selectedWorkMinutes * 60f;

        float progress01 = isTimerVisibleState ? timerController.Progress01 : 0f;
        int percent = Mathf.RoundToInt(progress01 * 100f);

        if (titleText != null) titleText.text = GetTitleText();
        if (cycleText != null)
            cycleText.text = isTimerVisibleState
                ? $"{timerController.CurrentRound}/{timerController.TotalRounds}サイクル"
                : $"0/{selectedRounds}サイクル";
        if (timeText != null) timeText.text = TimerController.FormatTime(remainingSeconds);
        if (percentText != null) percentText.text = $"{percent}%";

        SetProgressBarValue(radialProgressBar, percent);
        SetProgressBarValue(horizontalProgressBar, percent);
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        if (primaryActionButton != null)
        {
            if (timerController.IsPaused)
                primaryActionButton.SetText("再開");
            else if (timerController.IsRunning)
                primaryActionButton.SetText("一時停止");
            else
                primaryActionButton.SetText("開始");
        }

        if (nextPhaseButton != null)
            nextPhaseButton.Interactable(timerController.IsAwaitingNextPhase);
    }

    private static void SetProgressBarValue(ProgressBar progressBar, int percent)
    {
        if (progressBar == null) return;
        progressBar.SetValue(Mathf.Clamp(percent, 0, 100));
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
            "LONG BREAK" => "長休憩",
            _ => "作業準備"
        };
    }
}
