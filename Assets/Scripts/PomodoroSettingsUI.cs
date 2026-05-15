using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PomodoroSettingsUI : MonoBehaviour
{
    [Header("TimerController")]
    [SerializeField] private TimerController timerController;

    [Header("Sliders")]
    [SerializeField] private Slider workMinutesSlider;
    [SerializeField] private Slider breakMinutesSlider;
    [SerializeField] private Slider roundsSlider;

    [Header("Value Labels")]
    [SerializeField] private TextMeshProUGUI workValueText;
    [SerializeField] private TextMeshProUGUI breakValueText;
    [SerializeField] private TextMeshProUGUI roundsValueText;

    [Header("Button")]
    [SerializeField] private ButtonManager startButton;

    [Header("Limits")]
    [SerializeField] private int maxWorkMinutes  = 99;
    [SerializeField] private int maxBreakMinutes = 20;
    [SerializeField] private int maxRounds       = 99;

    private int WorkMinutes  => Mathf.RoundToInt(workMinutesSlider  != null ? workMinutesSlider .value : 25);
    private int BreakMinutes => Mathf.RoundToInt(breakMinutesSlider != null ? breakMinutesSlider.value : 5);
    private int Rounds       => Mathf.RoundToInt(roundsSlider       != null ? roundsSlider      .value : 4);

    private void OnEnable()
    {
        BindEvents();
        RefreshLabels();
    }

    private void OnDisable() => UnbindEvents();

    private void BindEvents()
    {
        if (workMinutesSlider  != null) workMinutesSlider .onValueChanged.AddListener(OnWorkChanged);
        if (breakMinutesSlider != null) breakMinutesSlider.onValueChanged.AddListener(OnBreakChanged);
        if (roundsSlider       != null) roundsSlider      .onValueChanged.AddListener(OnRoundsChanged);
        if (startButton        != null) startButton.onClick.AddListener(OnStart);
    }

    private void UnbindEvents()
    {
        if (workMinutesSlider  != null) workMinutesSlider .onValueChanged.RemoveListener(OnWorkChanged);
        if (breakMinutesSlider != null) breakMinutesSlider.onValueChanged.RemoveListener(OnBreakChanged);
        if (roundsSlider       != null) roundsSlider      .onValueChanged.RemoveListener(OnRoundsChanged);
        if (startButton        != null) startButton.onClick.RemoveListener(OnStart);
    }

    private void OnWorkChanged(float v)  { RefreshLabels(); }
    private void OnBreakChanged(float v) { RefreshLabels(); }
    private void OnRoundsChanged(float v){ RefreshLabels(); }

    private void RefreshLabels()
    {
        if (workValueText  != null) workValueText .text = WorkMinutes .ToString();
        if (breakValueText != null) breakValueText.text = BreakMinutes.ToString();
        if (roundsValueText!= null) roundsValueText.text = Rounds     .ToString();
    }

    private void OnStart()
    {
        if (timerController == null) return;
        timerController.StartPomodoro(WorkMinutes, BreakMinutes, BreakMinutes, Rounds);
        if (FocusOverlayController.Instance != null) FocusOverlayController.Instance.ShowRuntime();
    }
}
