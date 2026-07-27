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

    // 設定値は PomodoroManager が持つ（timer はプロキシなので同じ値を指す）。
    // 移行前はここにローカル変数があり、Rボタン（TimerControllerのSerializeField）と
    // ▶ボタン（このローカル値）で別々の設定が使われていた。

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

        RefreshSettingTexts();
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
            ? TimerController.FormatTime(timer.WorkMinutes * 60f)
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

        // バー側から設定が変わることもあるので、表示のたびに追従させる
        RefreshSettingTexts();

        confirmButton.interactable = true;
    }

    void OnReset()      => timer.PreparePomodoro();
    void OnPlayPause()
    {
        if (timer.Phase == TimerController.TimerPhase.Stopped)
            timer.StartWithSettings(); // 長休憩は break*3 の決め打ちをやめ、設定値を使う
        else
            timer.TogglePause();
    }
    void OnConfirm()
    {
        if (timer.Phase == TimerController.TimerPhase.Stopped)
            timer.StartWithSettings();
        else if (timer.IsAwaitingNextPhase)
            timer.AdvanceToNextPhase();
        else
            timer.ForceAdvancePhase();
    }

    public void AdjustWork(int delta)
    {
        timer.WorkMinutes += delta; // Manager側でクランプ＆PlayerPrefs保存
        RefreshSettingTexts();
        UpdateDisplay(); // 停止中の表示（mm:ss）にも即反映する
    }
    public void AdjustBreak(int delta)
    {
        timer.BreakMinutes += delta;
        RefreshSettingTexts();
    }
    public void AdjustCycle(int delta)
    {
        timer.CycleCount += delta;
        RefreshSettingTexts();
    }

    /// <summary>設定欄の数字をManagerの保持値に合わせる。</summary>
    void RefreshSettingTexts()
    {
        if (workMinutesText  != null) workMinutesText.text  = timer.WorkMinutes.ToString("0");
        if (breakMinutesText != null) breakMinutesText.text = timer.BreakMinutes.ToString("0");
        if (cycleCountText   != null) cycleCountText.text   = timer.CycleCount.ToString();
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
