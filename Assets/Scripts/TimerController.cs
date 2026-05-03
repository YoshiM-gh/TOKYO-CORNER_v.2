using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TimerController : MonoBehaviour
{
    public enum TimerMode { Pomodoro, Stopwatch }
    public enum TimerPhase { Stopped, Work, ShortBreak, LongBreak, Stopwatch, Completed }

    [Header("Legacy Text")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField, Min(0f)] private float notificationDuration = 3f;

    [Header("Pomodoro Defaults")]
    [SerializeField, Min(0.1f)] private float focusMinutes = 25f;
    [SerializeField, Min(0.1f)] private float breakMinutes = 5f;
    [SerializeField, Min(0.1f)] private float longBreakMinutes = 15f;
    [SerializeField, Min(1)] private int pomodoroRounds = 4;
    [SerializeField, Min(1)] private int longBreakEveryRounds = 4;

    public event Action OnTimerChanged;

    private readonly List<string> laps = new();
    private TimerMode mode = TimerMode.Pomodoro;
    private TimerPhase phase = TimerPhase.Stopped;
    private bool isSessionActive;
    private bool isRunning;
    private bool isPaused;
    private DateTime sessionStartUtc;
    private DateTime phaseStartUtc;
    private float elapsedSessionSeconds;
    private float elapsedBeforePauseSeconds;
    private float phaseDurationSeconds;
    private float workDurationSeconds;
    private float shortBreakDurationSeconds;
    private float longBreakDurationSeconds;
    private float stopwatchElapsedSeconds;
    private int currentRound = 1;
    private int totalRounds = 1;
    private float notificationHideAt = -1f;
    private string lastNotification = string.Empty;
    private bool awaitingNextPhase;
    private TimerPhase queuedPhase = TimerPhase.Stopped;
    private float queuedPhaseDurationSeconds;
    private string queuedPhaseNotification = string.Empty;

    public TimerMode Mode => mode;
    public TimerPhase Phase => phase;
    public bool IsSessionActive => isSessionActive;
    public bool IsRunning => isRunning;
    public bool IsPaused => isPaused;
    public int CurrentRound => currentRound;
    public int TotalRounds => totalRounds;
    public float DurationSeconds => phaseDurationSeconds;
    public float ElapsedSeconds => phase == TimerPhase.Stopped ? 0f :
        mode == TimerMode.Stopwatch ? GetPhaseElapsedSeconds() : Mathf.Min(GetPhaseElapsedSeconds(), phaseDurationSeconds);
    public float RemainingSeconds => mode == TimerMode.Stopwatch ? ElapsedSeconds : Mathf.Max(0f, phaseDurationSeconds - ElapsedSeconds);
    public float Progress01 => phaseDurationSeconds <= 0f ? 0f : Mathf.Clamp01(ElapsedSeconds / phaseDurationSeconds);
    public string LastNotification => lastNotification;
    public IReadOnlyList<string> Laps => laps;
    public bool IsAwaitingNextPhase => mode == TimerMode.Pomodoro && awaitingNextPhase;
    public float DefaultWorkMinutes => focusMinutes;
    public float DefaultShortBreakMinutes => breakMinutes;
    public float DefaultLongBreakMinutes => longBreakMinutes;
    public int DefaultRounds => pomodoroRounds;

    private void Update()
    {
        UpdateNotificationVisibility();

        if (!isRunning) return;

        UpdateElapsedSessionSeconds();
        if (mode == TimerMode.Pomodoro && GetPhaseElapsedSeconds() >= phaseDurationSeconds)
            AdvancePhase();

        UpdateLegacyDisplay();
        OnTimerChanged?.Invoke();
    }

    public void StartSession()
    {
        isSessionActive = true;
        sessionStartUtc = DateTime.UtcNow;
        elapsedSessionSeconds = 0f;
        PreparePomodoro();
        Debug.Log("[Timer] Focus session started.");
    }

    public void StopSession()
    {
        UpdateElapsedSessionSeconds();
        Stop();
        int minutes = Mathf.FloorToInt(elapsedSessionSeconds / 60f);
        int seconds = Mathf.FloorToInt(elapsedSessionSeconds % 60f);
        Debug.Log($"[Timer] Focus session ended. Duration: {minutes:00}:{seconds:00}");
        // 滞在時間は SaveDataManager が GameMode に応じて毎フレーム集計
    }

    public void StartPomodoro(float workMinutes, float shortBreakMinutes, float longBreakMinutesValue, int rounds)
    {
        mode = TimerMode.Pomodoro;
        ClearQueuedPhase();
        workDurationSeconds = MinutesToSeconds(workMinutes);
        shortBreakDurationSeconds = MinutesToSeconds(shortBreakMinutes);
        longBreakDurationSeconds = MinutesToSeconds(longBreakMinutesValue);
        totalRounds = Mathf.Max(1, rounds);
        currentRound = 1;
        laps.Clear();
        StartPhase(TimerPhase.Work, workDurationSeconds);
        ShowNotification("Pomodoro started");
    }

    public void PreparePomodoro()
    {
        mode = TimerMode.Pomodoro;
        ClearQueuedPhase();
        workDurationSeconds = MinutesToSeconds(focusMinutes);
        shortBreakDurationSeconds = MinutesToSeconds(breakMinutes);
        longBreakDurationSeconds = MinutesToSeconds(longBreakMinutes);
        totalRounds = Mathf.Max(1, pomodoroRounds);
        currentRound = 1;
        phase = TimerPhase.Stopped;
        phaseDurationSeconds = workDurationSeconds;
        elapsedBeforePauseSeconds = 0f;
        isRunning = false;
        isPaused = false;
        laps.Clear();
        ClearNotification();
        UpdateLegacyDisplay();
        OnTimerChanged?.Invoke();
    }

    public void StartStopwatch()
    {
        mode = TimerMode.Stopwatch;
        ClearQueuedPhase();
        totalRounds = 1;
        currentRound = 1;
        stopwatchElapsedSeconds = 0f;
        laps.Clear();
        StartPhase(TimerPhase.Stopwatch, 0f);
        ShowNotification("Stopwatch started");
    }

    public void PrepareStopwatch()
    {
        mode = TimerMode.Stopwatch;
        ClearQueuedPhase();
        totalRounds = 1;
        currentRound = 1;
        phase = TimerPhase.Stopped;
        phaseDurationSeconds = 0f;
        elapsedBeforePauseSeconds = 0f;
        stopwatchElapsedSeconds = 0f;
        isRunning = false;
        isPaused = false;
        laps.Clear();
        ClearNotification();
        UpdateLegacyDisplay();
        OnTimerChanged?.Invoke();
    }

    public void StartTimer(float minutes)
    {
        StartStopwatch();
    }

    public void StartShortBreak(float minutes)
    {
        mode = TimerMode.Pomodoro;
        StartPhase(TimerPhase.ShortBreak, MinutesToSeconds(minutes));
        ShowNotification("Short break");
    }

    public void StartLongBreak(float minutes)
    {
        mode = TimerMode.Pomodoro;
        StartPhase(TimerPhase.LongBreak, MinutesToSeconds(minutes));
        ShowNotification("Long break");
    }

    public void Pause()
    {
        if (!isRunning) return;

        elapsedBeforePauseSeconds = GetPhaseElapsedSeconds();
        isRunning = false;
        isPaused = true;
        ShowNotification("Paused");
        UpdateLegacyDisplay();
        OnTimerChanged?.Invoke();
    }

    public void Resume()
    {
        if (!isPaused) return;

        phaseStartUtc = DateTime.UtcNow;
        isRunning = true;
        isPaused = false;
        ShowNotification("Resumed");
        OnTimerChanged?.Invoke();
    }

    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }

    public void Stop()
    {
        ClearQueuedPhase();
        isRunning = false;
        isPaused = false;
        isSessionActive = false;
        phase = TimerPhase.Stopped;
        phaseDurationSeconds = 0f;
        elapsedBeforePauseSeconds = 0f;
        stopwatchElapsedSeconds = 0f;
        ClearNotification();
        UpdateLegacyDisplay();
        OnTimerChanged?.Invoke();
    }

    public void RecordLap()
    {
        if (phase == TimerPhase.Stopped || phase == TimerPhase.Completed) return;

        string lap = mode == TimerMode.Pomodoro
            ? $"{laps.Count + 1}. R{currentRound}/{totalRounds} {GetPhaseLabel()} {FormatTime(ElapsedSeconds)}"
            : $"{laps.Count + 1}. {FormatTime(ElapsedSeconds)}";
        laps.Insert(0, lap);
        ShowNotification("Lap recorded");
        OnTimerChanged?.Invoke();
    }

    public void AdvanceToNextPhase()
    {
        if (!IsAwaitingNextPhase) return;

        var nextPhase = queuedPhase;
        var nextDuration = queuedPhaseDurationSeconds;
        var nextNotification = queuedPhaseNotification;
        ClearQueuedPhase();
        StartPhase(nextPhase, nextDuration);
        if (!string.IsNullOrEmpty(nextNotification))
            ShowNotification(nextNotification);
    }

    public string GetNextPhaseLabel()
    {
        return IsAwaitingNextPhase ? GetPhaseLabel(queuedPhase) : string.Empty;
    }

    public string GetPhaseLabel()
    {
        return GetPhaseLabel(phase);
    }

    public static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return string.Format("{0:00}:{1:00}", minutes, secs);
    }

    private void StartPhase(TimerPhase nextPhase, float durationSeconds)
    {
        ClearQueuedPhase();
        phase = nextPhase;
        phaseDurationSeconds = mode == TimerMode.Stopwatch ? 0f : Mathf.Max(1f, durationSeconds);
        elapsedBeforePauseSeconds = 0f;
        phaseStartUtc = DateTime.UtcNow;
        isRunning = true;
        isPaused = false;
        isSessionActive = true;
        UpdateLegacyDisplay();
        OnTimerChanged?.Invoke();
    }

    private void AdvancePhase()
    {
        if (phase == TimerPhase.Work)
        {
            if (currentRound >= totalRounds)
            {
                CompleteTimer("Pomodoro complete");
                return;
            }

            bool useLongBreak = currentRound % Mathf.Max(1, longBreakEveryRounds) == 0;
            QueueNextPhase(
                useLongBreak ? TimerPhase.LongBreak : TimerPhase.ShortBreak,
                useLongBreak ? longBreakDurationSeconds : shortBreakDurationSeconds,
                useLongBreak ? "Long break started" : "Short break started");
            return;
        }

        if (phase == TimerPhase.ShortBreak || phase == TimerPhase.LongBreak)
        {
            currentRound++;
            QueueNextPhase(TimerPhase.Work, workDurationSeconds, "Work started");
        }
    }

    private void CompleteTimer(string message)
    {
        ClearQueuedPhase();
        phase = TimerPhase.Completed;
        isRunning = false;
        isPaused = false;
        elapsedBeforePauseSeconds = phaseDurationSeconds;
        ShowNotification(message);
        UpdateLegacyDisplay();
        OnTimerChanged?.Invoke();
    }

    private float GetPhaseElapsedSeconds()
    {
        if (phase == TimerPhase.Stopped)
            return 0f;

        if (isPaused || phase == TimerPhase.Completed)
            return elapsedBeforePauseSeconds;

        if (awaitingNextPhase)
            return elapsedBeforePauseSeconds;

        if (!isRunning)
            return elapsedBeforePauseSeconds;

        float realtimeElapsed = (float)(DateTime.UtcNow - phaseStartUtc).TotalSeconds;
        float elapsed = Mathf.Max(0f, elapsedBeforePauseSeconds + realtimeElapsed);
        if (mode == TimerMode.Stopwatch)
            stopwatchElapsedSeconds = elapsed;
        return elapsed;
    }

    private void UpdateElapsedSessionSeconds()
    {
        if (!isSessionActive) return;

        float dt = (float)(DateTime.UtcNow - sessionStartUtc).TotalSeconds;
        elapsedSessionSeconds = Mathf.Max(0f, dt);
    }

    private void UpdateLegacyDisplay()
    {
        if (timerText == null) return;

        if (phase == TimerPhase.Stopped)
        {
            timerText.text = FormatTime(0f);
            return;
        }

        if (mode == TimerMode.Stopwatch)
            timerText.text = $"{GetPhaseLabel()} {FormatTime(ElapsedSeconds)}";
        else if (IsAwaitingNextPhase)
            timerText.text = $"{GetPhaseLabel()} {FormatTime(0f)}";
        else
            timerText.text = $"{GetPhaseLabel()} {FormatTime(RemainingSeconds)}";
    }

    private void ShowNotification(string message)
    {
        lastNotification = message;
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.enabled = true;
        }

        notificationHideAt = notificationDuration > 0f
            ? Time.unscaledTime + notificationDuration
            : -1f;
    }

    private void ClearNotification()
    {
        lastNotification = string.Empty;
        if (notificationText != null)
        {
            notificationText.text = string.Empty;
            notificationText.enabled = false;
        }

        notificationHideAt = -1f;
    }

    private void UpdateNotificationVisibility()
    {
        if (notificationHideAt < 0f || Time.unscaledTime < notificationHideAt) return;

        ClearNotification();
        OnTimerChanged?.Invoke();
    }

    private void QueueNextPhase(TimerPhase nextPhase, float nextDurationSeconds, string nextNotification)
    {
        awaitingNextPhase = true;
        queuedPhase = nextPhase;
        queuedPhaseDurationSeconds = Mathf.Max(1f, nextDurationSeconds);
        queuedPhaseNotification = nextNotification;
        elapsedBeforePauseSeconds = phaseDurationSeconds;
        isRunning = false;
        isPaused = false;
        ShowNotification("Phase complete");
        UpdateLegacyDisplay();
        OnTimerChanged?.Invoke();
    }

    private void ClearQueuedPhase()
    {
        awaitingNextPhase = false;
        queuedPhase = TimerPhase.Stopped;
        queuedPhaseDurationSeconds = 0f;
        queuedPhaseNotification = string.Empty;
    }

    private static string GetPhaseLabel(TimerPhase targetPhase)
    {
        return targetPhase switch
        {
            TimerPhase.Work => "WORK",
            TimerPhase.ShortBreak => "SHORT BREAK",
            TimerPhase.LongBreak => "LONG BREAK",
            TimerPhase.Stopwatch => "STOPWATCH",
            TimerPhase.Completed => "DONE",
            _ => "READY"
        };
    }

    private static float MinutesToSeconds(float minutes) => Mathf.Max(0.1f, minutes) * 60f;
}
