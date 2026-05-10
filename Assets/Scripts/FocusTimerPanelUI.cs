using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FocusTimerPanelUI : MonoBehaviour
{
    [SerializeField] private TimerController timerController;

    [Header("Panels")]
    [SerializeField] private GameObject runtimePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Status Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI cycleText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI percentText;

    [Header("Setting Value Text")]
    [SerializeField] private TextMeshProUGUI workValueText;
    [SerializeField] private TextMeshProUGUI breakValueText;
    [SerializeField] private TextMeshProUGUI roundsValueText;

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

        if (workValueText != null) workValueText.text = $"{selectedWorkMinutes}";
        if (breakValueText != null) breakValueText.text = $"{selectedBreakMinutes}";
        if (roundsValueText != null) roundsValueText.text = $"{selectedRounds}";

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
