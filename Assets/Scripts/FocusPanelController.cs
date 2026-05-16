using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FocusPanelController : MonoBehaviour
{
    [Header("依存")]
    [SerializeField] private TimerController timerController;

    [Header("円弧")]
    [SerializeField] private Image arcFill;
    [SerializeField] private Image arcTrack;
    [SerializeField] private RectTransform arcRoot;

    [Header("円弧内テキスト")]
    [SerializeField] private TextMeshProUGUI phaseLabelText;
    [SerializeField] private TextMeshProUGUI timerDisplayText;
    [SerializeField] private TextMeshProUGUI cycleText;

    [Header("設定エリア（動作中は非表示）")]
    [SerializeField] private GameObject settingsArea;

    [Header("ステッパー値")]
    [SerializeField] private TextMeshProUGUI workValueText;
    [SerializeField] private TextMeshProUGUI breakValueText;
    [SerializeField] private TextMeshProUGUI cycleValueText;

    [Header("ステッパーボタン")]
    [SerializeField] private Button workMinusBtn;
    [SerializeField] private Button workPlusBtn;
    [SerializeField] private Button breakMinusBtn;
    [SerializeField] private Button breakPlusBtn;
    [SerializeField] private Button cycleMinusBtn;
    [SerializeField] private Button cyclePlusBtn;

    [Header("コントロール")]
    [SerializeField] private Button resetBtn;
    [SerializeField] private Button playPauseBtn;
    [SerializeField] private TextMeshProUGUI playPauseBtnText;
    [SerializeField] private Button skipBtn;

    [Header("色")]
    [SerializeField] private Color colorWork  = new Color(0.22f, 0.54f, 0.87f, 1f);
    [SerializeField] private Color colorBreak = new Color(0.11f, 0.62f, 0.46f, 1f);
    [SerializeField] private Color colorIdle  = new Color(1f, 1f, 1f, 0.08f);

    [Header("設定値")]
    [SerializeField, Range(20,99)] private int workMinutes  = 25;
    [SerializeField, Range(5, 99)] private int breakMinutes = 5;
    [SerializeField, Range(1, 99)] private int cycleCount   = 4;

    private bool _sessionStarted;
    private bool _isRunning;

    private void Awake()
    {
        if (timerController == null)
            timerController = GetComponentInParent<TimerController>();
    }

    private void OnEnable()
    {
        if (timerController != null) timerController.OnTimerChanged += Refresh;

        workMinusBtn? .onClick.AddListener(() => Adj(ref workMinutes,  -1, 20, 99, workValueText));
        workPlusBtn?  .onClick.AddListener(() => Adj(ref workMinutes,  +1, 20, 99, workValueText));
        breakMinusBtn?.onClick.AddListener(() => Adj(ref breakMinutes, -1, 5,  99, breakValueText));
        breakPlusBtn? .onClick.AddListener(() => Adj(ref breakMinutes, +1, 5,  99, breakValueText));
        cycleMinusBtn?.onClick.AddListener(() => Adj(ref cycleCount,   -1, 1,  99, cycleValueText));
        cyclePlusBtn? .onClick.AddListener(() => Adj(ref cycleCount,   +1, 1,  99, cycleValueText));

        resetBtn?    .onClick.AddListener(OnReset);
        playPauseBtn?.onClick.AddListener(OnPlayPause);
        skipBtn?     .onClick.AddListener(OnSkip);

        _sessionStarted = false;
        _isRunning      = false;
        RefreshSteppers();
        ApplyLayout(false);
        Refresh();
    }

    private void OnDisable()
    {
        if (timerController != null) timerController.OnTimerChanged -= Refresh;
        workMinusBtn? .onClick.RemoveAllListeners();
        workPlusBtn?  .onClick.RemoveAllListeners();
        breakMinusBtn?.onClick.RemoveAllListeners();
        breakPlusBtn? .onClick.RemoveAllListeners();
        cycleMinusBtn?.onClick.RemoveAllListeners();
        cyclePlusBtn? .onClick.RemoveAllListeners();
        resetBtn?    .onClick.RemoveAllListeners();
        playPauseBtn?.onClick.RemoveAllListeners();
        skipBtn?     .onClick.RemoveAllListeners();
    }

    private bool IsLocked => timerController != null &&
                             (timerController.IsRunning || timerController.IsPaused);

    private void Adj(ref int val, int d, int min, int max, TextMeshProUGUI lbl)
    {
        if (IsLocked) return;
        val = Mathf.Clamp(val + d, min, max);
        if (lbl != null) lbl.text = val.ToString();
        if (!_sessionStarted || timerController.Phase == TimerController.TimerPhase.Stopped)
            UpdateIdleDisplay();
    }

    private void RefreshSteppers()
    {
        if (workValueText  != null) workValueText .text = workMinutes .ToString();
        if (breakValueText != null) breakValueText.text = breakMinutes.ToString();
        if (cycleValueText != null) cycleValueText.text = cycleCount  .ToString();
        bool u = !IsLocked;
        SetBtn(workMinusBtn, u);  SetBtn(workPlusBtn,  u);
        SetBtn(breakMinusBtn, u); SetBtn(breakPlusBtn, u);
        SetBtn(cycleMinusBtn, u); SetBtn(cyclePlusBtn, u);
    }

    private static void SetBtn(Button b, bool v)
    {
        if (b == null) return;
        b.interactable = v;
        var cg = b.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = v ? 1f : 0.3f;
    }

    private void OnPlayPause()
    {
        if (timerController == null) return;
        var ph = timerController.Phase;
        if (!_sessionStarted
            || ph == TimerController.TimerPhase.Stopped
            || ph == TimerController.TimerPhase.Completed)
        {
            timerController.StartPomodoro(workMinutes, breakMinutes, breakMinutes, cycleCount);
            _sessionStarted = true;
        }
        else if (timerController.IsAwaitingNextPhase)
            timerController.AdvanceToNextPhase();
        else
            timerController.TogglePause();
    }

    private void OnReset()
    {
        if (timerController == null) return;
        timerController.Stop();
        _sessionStarted = false;
        RefreshSteppers();
        Refresh();
    }

    private void OnSkip()
    {
        if (timerController == null) return;
        if (timerController.IsAwaitingNextPhase)
            timerController.AdvanceToNextPhase();
        else if (timerController.IsRunning || timerController.IsPaused)
            timerController.ForceAdvancePhase();
    }

    private void ApplyLayout(bool running)
    {
        if (settingsArea != null) settingsArea.SetActive(!running);
    }

    private void Refresh()
    {
        if (timerController == null) return;
        var ph      = timerController.Phase;
        bool active = timerController.IsRunning || timerController.IsPaused
                      || timerController.IsAwaitingNextPhase;

        if (active != _isRunning)
        {
            _isRunning = active;
            ApplyLayout(active);
        }

        if (phaseLabelText != null)
            phaseLabelText.text = ph switch
            {
                TimerController.TimerPhase.Work       => "作業中",
                TimerController.TimerPhase.ShortBreak => "休憩中",
                TimerController.TimerPhase.LongBreak  => "休憩中",
                TimerController.TimerPhase.Completed  => "完了",
                _ => timerController.IsAwaitingNextPhase ? "フェーズ終了" : "設定"
            };

        if (cycleText != null)
            cycleText.text = ph != TimerController.TimerPhase.Stopped
                ? $"サイクル {timerController.CurrentRound} / {timerController.TotalRounds}"
                : $"サイクル 1 / {cycleCount}";

        if (timerDisplayText != null)
            timerDisplayText.text = ph == TimerController.TimerPhase.Stopped
                ? TimerController.FormatTime(workMinutes * 60f)
                : TimerController.FormatTime(timerController.RemainingSeconds);

        if (arcFill != null)
        {
            arcFill.fillAmount = active ? (1f - timerController.Progress01) : 1f;
            bool isBreak = ph == TimerController.TimerPhase.ShortBreak
                        || ph == TimerController.TimerPhase.LongBreak;
            arcFill.color = active ? (isBreak ? colorBreak : colorWork) : colorIdle;
        }

        if (playPauseBtnText != null)
            playPauseBtnText.text =
                (timerController.IsPaused || timerController.IsAwaitingNextPhase) ? "▶"
                : timerController.IsRunning ? "⏸" : "▶";

        if (skipBtn != null)
            skipBtn.interactable = timerController.IsRunning || timerController.IsPaused
                                   || timerController.IsAwaitingNextPhase;

        RefreshSteppers();
    }

    private void UpdateIdleDisplay()
    {
        if (timerDisplayText != null)
            timerDisplayText.text = TimerController.FormatTime(workMinutes * 60f);
        if (arcFill != null) { arcFill.fillAmount = 1f; arcFill.color = colorIdle; }
        if (phaseLabelText != null) phaseLabelText.text = "設定";
        if (cycleText != null) cycleText.text = $"サイクル 1 / {cycleCount}";
    }
}
