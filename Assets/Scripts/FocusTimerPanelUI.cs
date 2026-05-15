using Michsky.MUIP;
using TMPro;
using UnityEngine;

/// <summary>
/// フォーカスモード ランタイムパネル UI。
/// タイマー表示・プログレスバー・操作ボタンを管理する。
/// </summary>
public class FocusTimerPanelUI : MonoBehaviour
{
    [SerializeField] private TimerController timerController;
    [SerializeField] private bool autoBindOnAwake = true;

    [Header("Status Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI cycleText;
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Progress Bar (Modern UI Pack)")]
    [SerializeField] private ProgressBar progressBar;

    [Header("Buttons (Modern UI Pack)")]
    [SerializeField] private ButtonManager pauseResumeButton;
    [SerializeField] private ButtonManager skipButton;
    [SerializeField] private ButtonManager stopButton;

    private bool _completionHandled;

    private void Awake()
    {
        if (autoBindOnAwake) AutoBind();
        if (timerController == null) timerController = GetComponentInParent<TimerController>();
    }

    private void OnEnable()
    {
        if (timerController != null) timerController.OnTimerChanged += Refresh;
        BindButtons();
        _completionHandled = false;
        Refresh();
    }

    private void OnDisable()
    {
        if (timerController != null) timerController.OnTimerChanged -= Refresh;
        UnbindButtons();
    }

    private void BindButtons()
    {
        if (pauseResumeButton != null) pauseResumeButton.onClick.AddListener(OnPauseResume);
        if (skipButton        != null) skipButton       .onClick.AddListener(OnSkip);
        if (stopButton        != null) stopButton       .onClick.AddListener(OnStop);
    }

    private void UnbindButtons()
    {
        if (pauseResumeButton != null) pauseResumeButton.onClick.RemoveListener(OnPauseResume);
        if (skipButton        != null) skipButton       .onClick.RemoveListener(OnSkip);
        if (stopButton        != null) stopButton       .onClick.RemoveListener(OnStop);
    }

    private void OnPauseResume() { if (timerController != null) timerController.TogglePause(); }

    private void OnSkip()
    {
        if (timerController == null) return;
        // 実行中・一時停止中でも強制的に次フェーズへ
        if (timerController.IsAwaitingNextPhase)
            timerController.AdvanceToNextPhase();
        else if (timerController.IsRunning || timerController.IsPaused)
            timerController.ForceAdvancePhase();
    }

    private void OnStop()
    {
        if (timerController != null) timerController.Stop();
        if (FocusOverlayController.Instance != null) FocusOverlayController.Instance.ShowSettings();
    }

    private void Refresh()
    {
        if (timerController == null) return;

        if (timerController.Phase == TimerController.TimerPhase.Completed && !_completionHandled)
        {
            _completionHandled = true;
            if (FocusOverlayController.Instance != null) FocusOverlayController.Instance.ShowSettings();
            return;
        }

        if (titleText != null) titleText.text = GetTitleText();
        if (cycleText != null) cycleText.text = $"{timerController.CurrentRound}/{timerController.TotalRounds} サイクル";
        if (timeText  != null) timeText .text = TimerController.FormatTime(timerController.RemainingSeconds);

        int percent = Mathf.RoundToInt(timerController.Progress01 * 100f);
        if (progressBar != null) progressBar.SetValue(Mathf.Clamp(percent, 0, 100));

        UpdateButtonLabels();
    }

    private void UpdateButtonLabels()
    {
        if (pauseResumeButton == null || timerController == null) return;
        if      (timerController.IsPaused)  pauseResumeButton.SetText("再開");
        else if (timerController.IsRunning) pauseResumeButton.SetText("一時停止");
        else                                pauseResumeButton.SetText("開始");
        if (skipButton != null) skipButton.Interactable(timerController.IsRunning || timerController.IsPaused || timerController.IsAwaitingNextPhase);
    }

    private string GetTitleText()
    {
        if (timerController.Phase == TimerController.TimerPhase.Completed) return "完了";
        if (timerController.IsAwaitingNextPhase)                           return "フェーズ終了";
        return timerController.GetPhaseLabel() switch
        {
            "WORK"        => "作業中",
            "SHORT BREAK" => "休憩中",
            "LONG BREAK"  => "長休憩",
            _              => "準備中"
        };
    }

    [UnityEngine.ContextMenu("Auto Bind")]
    private void AutoBind()
    {
        T Find<T>(string n) where T : UnityEngine.Component
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) { var c = t.GetComponent<T>(); if (c != null) return c; }
            return null;
        }
        if (titleText     == null) titleText     = Find<TextMeshProUGUI>("TitleText");
        if (cycleText     == null) cycleText     = Find<TextMeshProUGUI>("CycleText");
        if (timeText      == null) timeText      = Find<TextMeshProUGUI>("TimeText");
        if (progressBar   == null) progressBar   = Find<ProgressBar>("PB - Standard");
        if (pauseResumeButton == null) pauseResumeButton = Find<ButtonManager>("PauseResumeButton");
        if (skipButton    == null) skipButton    = Find<ButtonManager>("SkipButton");
        if (stopButton    == null) stopButton    = Find<ButtonManager>("StopButton");
    }
}
