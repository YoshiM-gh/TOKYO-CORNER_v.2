using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PomodoroTimerUI : MonoBehaviour
{
    [Header("Controller")]
    [SerializeField] private TimerController timer;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI phaseLabel;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI cycleText;
    [SerializeField] private Image ringProgress;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Values")]
    [SerializeField] private TextMeshProUGUI workMinutesText;
    [SerializeField] private TextMeshProUGUI breakMinutesText;
    [SerializeField] private TextMeshProUGUI cycleCountText;

    [Header("Settings Buttons")]
    [SerializeField] private Button workMinus;
    [SerializeField] private Button workPlus;
    [SerializeField] private Button breakMinus;
    [SerializeField] private Button breakPlus;
    [SerializeField] private Button cycleMinus;
    [SerializeField] private Button cyclePlus;

    [Header("Control Buttons")]
    [SerializeField] private Button resetButton;
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI playPauseIcon;

    private float workMinutes  = 25f;
    private float breakMinutes = 5f;
    private int   cycleCount   = 4;

    void Start()
    {
        timer.OnTimerChanged += UpdateDisplay;

        resetButton.onClick.AddListener(OnReset);
        playPauseButton.onClick.AddListener(OnPlayPause);
        confirmButton.onClick.AddListener(OnConfirm);

        workMinus.onClick.AddListener(() => AdjustWork(-1));
        workPlus.onClick.AddListener(()  => AdjustWork(+1));
        breakMinus.onClick.AddListener(() => AdjustBreak(-1));
        breakPlus.onClick.AddListener(()  => AdjustBreak(+1));
        cycleMinus.onClick.AddListener(() => AdjustCycle(-1));
        cyclePlus.onClick.AddListener(()  => AdjustCycle(+1));

        UpdateDisplay();
    }

    void OnDestroy()
    {
        if (timer != null) timer.OnTimerChanged -= UpdateDisplay;
    }

    void UpdateDisplay()
    {
        bool isStopped = timer.Phase == TimerController.TimerPhase.Stopped;

        timeText.text = isStopped
            ? TimerController.FormatTime(workMinutes * 60f)
            : TimerController.FormatTime(timer.RemainingSeconds);

        ringProgress.fillAmount = isStopped ? 0f : 1f - timer.Progress01;

        ringProgress.color =
            timer.Phase == TimerController.TimerPhase.ShortBreak ||
            timer.Phase == TimerController.TimerPhase.LongBreak
            ? new Color(0.18f, 0.80f, 0.44f, 1f)
            : new Color(0.06f, 0.59f, 0.99f, 1f);

        phaseLabel.text = GetJapaneseLabel(timer.Phase);
        cycleText.text  = $"サイクル {timer.CurrentRound} / {timer.TotalRounds}";
        playPauseIcon.text = timer.IsRunning ? "II" : "\u25B6";

        if (settingsPanel != null)
            settingsPanel.SetActive(isStopped);

        confirmButton.interactable = true;
    }

    void OnReset()      => timer.PreparePomodoro();
    void OnPlayPause()
    {
        if (timer.Phase == TimerController.TimerPhase.Stopped)
            timer.StartPomodoro(workMinutes, breakMinutes, breakMinutes * 3f, cycleCount);
        else
            timer.TogglePause();
    }
    void OnConfirm()
    {
        if (timer.Phase == TimerController.TimerPhase.Stopped)
            timer.StartPomodoro(workMinutes, breakMinutes, breakMinutes * 3f, cycleCount);
        else if (timer.IsAwaitingNextPhase)
            timer.AdvanceToNextPhase();
        else
            timer.ForceAdvancePhase();
    }

    public void AdjustWork(int delta)
    {
        workMinutes = Mathf.Clamp(workMinutes + delta, 1f, 99f);
        workMinutesText.text = workMinutes.ToString("0");
    }
    public void AdjustBreak(int delta)
    {
        breakMinutes = Mathf.Clamp(breakMinutes + delta, 1f, 99f);
        breakMinutesText.text = breakMinutes.ToString("0");
    }
    public void AdjustCycle(int delta)
    {
        cycleCount = Mathf.Clamp(cycleCount + delta, 1, 99);
        cycleCountText.text = cycleCount.ToString();
    }

    static string GetJapaneseLabel(TimerController.TimerPhase phase) => phase switch
    {
        TimerController.TimerPhase.Work       => "作業中",
        TimerController.TimerPhase.ShortBreak => "休憩中",
        TimerController.TimerPhase.LongBreak  => "長休憩",
        TimerController.TimerPhase.Completed  => "完了",
        _                                     => "設定",
    };
}
