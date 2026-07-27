using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// ポモドーロ計測の『顔』だけを残したプロキシ（Phase 3 Step 2）。
/// 実体は <see cref="PomodoroManager"/>（DontDestroyOnLoadシングルトン）に移動した。
///
/// このクラスを残す理由: シーン内のシリアライズ参照
/// （GameModeManager / PomodoroTimerUI / FocusPanelController / FocusPanelSetup）を
/// 一切張り替えずに移行を終わらせるため。**公開API・enum・FormatTime は移行前と同一**。
///
/// - 状態は持たない（すべて Manager に委譲）
/// - Update() での計測は行わない（Manager が計測する）
/// - OnTimerChanged は Manager のイベントを転送する
/// - [SerializeField] のインスペクタ値は『初回起動時の種』としてのみ使う（SeedDefaultsIfUnset）
/// - timerText / notificationText のレガシー表示は、Manager の状態から更新し続ける
/// </summary>
public class TimerController : MonoBehaviour
{
    public enum TimerMode { Pomodoro, Stopwatch }
    public enum TimerPhase { Stopped, Work, ShortBreak, LongBreak, Stopwatch, Completed }

    [Header("Legacy Text")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField, Min(0f)] private float notificationDuration = 3f;

    [Header("Pomodoro Defaults（初回起動時の種。以降は PomodoroManager が PlayerPrefs で保持）")]
    [SerializeField, Min(0.1f)] private float focusMinutes = 25f;
    [SerializeField, Min(0.1f)] private float breakMinutes = 5f;
    [SerializeField, Min(0.1f)] private float longBreakMinutes = 15f;
    [SerializeField, Min(1)] private int pomodoroRounds = 4;
    [SerializeField, Min(1)] private int longBreakEveryRounds = 4;

    /// <summary>Manager のイベントを転送する（購読側のコードは移行前と同じ）。</summary>
    public event Action OnTimerChanged;

    private static PomodoroManager M => PomodoroManager.Instance;

    private void Awake()
    {
        var m = M;
        m.NotificationDuration = notificationDuration;
        m.SeedDefaultsIfUnset(focusMinutes, breakMinutes, longBreakMinutes, pomodoroRounds, longBreakEveryRounds);
    }

    private void OnEnable()
    {
        M.OnTimerChanged += HandleManagerChanged;
        HandleManagerChanged();
    }

    private void OnDisable()
    {
        // 終了時に Instance で作り直さないよう、生成しないアクセサを使う
        var m = PomodoroManager.InstanceOrNull;
        if (m != null) m.OnTimerChanged -= HandleManagerChanged;
    }

    private void HandleManagerChanged()
    {
        UpdateLegacyDisplay();
        var handler = OnTimerChanged;
        if (handler != null) handler.Invoke();
    }

    // ---- 状態（すべて Manager へ委譲） -------------------------------------

    public TimerMode Mode => M.Mode;
    public TimerPhase Phase => M.Phase;
    public bool IsSessionActive => M.IsSessionActive;
    public bool IsRunning => M.IsRunning;
    public bool IsPaused => M.IsPaused;
    public int CurrentRound => M.CurrentRound;
    public int TotalRounds => M.TotalRounds;
    public float DurationSeconds => M.DurationSeconds;
    public float ElapsedSeconds => M.ElapsedSeconds;
    public float RemainingSeconds => M.RemainingSeconds;
    public float Progress01 => M.Progress01;
    public string LastNotification => M.LastNotification;
    public IReadOnlyList<string> Laps => M.Laps;
    public bool IsAwaitingNextPhase => M.IsAwaitingNextPhase;

    /// <summary>移行前は[SerializeField]の既定値だった。現在は Manager の保持値を返す。</summary>
    public float DefaultWorkMinutes => M.WorkMinutes;
    public float DefaultShortBreakMinutes => M.BreakMinutes;
    public float DefaultLongBreakMinutes => M.LongBreakMinutes;
    public int DefaultRounds => M.CycleCount;

    /// <summary>設定値（Phase 3で追加）。カードのUIはここを読み書きする＝Rボタンと▶で同じ値を使う。</summary>
    public float WorkMinutes      { get => M.WorkMinutes;      set => M.WorkMinutes = value; }
    public float BreakMinutes     { get => M.BreakMinutes;     set => M.BreakMinutes = value; }
    public float LongBreakMinutes { get => M.LongBreakMinutes; set => M.LongBreakMinutes = value; }
    public int   CycleCount       { get => M.CycleCount;       set => M.CycleCount = value; }

    // ---- 操作（すべて Manager へ委譲） -------------------------------------

    public void StartSession() => M.StartSession();
    public void StopSession() => M.StopSession();
    public void StartPomodoro(float workMinutesValue, float shortBreakMinutes, float longBreakMinutesValue, int rounds)
        => M.StartPomodoro(workMinutesValue, shortBreakMinutes, longBreakMinutesValue, rounds);
    /// <summary>保持している設定で開始する（Phase 3で追加）。</summary>
    public void StartWithSettings() => M.StartWithSettings();
    public void PreparePomodoro() => M.PreparePomodoro();
    public void StartStopwatch() => M.StartStopwatch();
    public void PrepareStopwatch() => M.PrepareStopwatch();
    public void StartTimer(float minutes) => M.StartStopwatch(); // 移行前と同じ挙動（引数は未使用）
    public void StartShortBreak(float minutes) => M.StartShortBreak(minutes);
    public void StartLongBreak(float minutes) => M.StartLongBreak(minutes);
    public void Pause() => M.Pause();
    public void Resume() => M.Resume();
    public void TogglePause() => M.TogglePause();
    public void Stop() => M.Stop();
    public void RecordLap() => M.RecordLap();
    public void AdvanceToNextPhase() => M.AdvanceToNextPhase();
    public void ForceAdvancePhase() => M.ForceAdvancePhase();
    public string GetNextPhaseLabel() => M.GetNextPhaseLabel();
    public string GetPhaseLabel() => M.GetPhaseLabel();

    public static string FormatTime(float seconds) => PomodoroManager.FormatTime(seconds);

    // ---- レガシー表示（旧FocusUIのテキストが繋がっている場合のみ動く） ------

    private void UpdateLegacyDisplay()
    {
        if (notificationText != null)
        {
            var note = M.LastNotification;
            notificationText.text = note;
            notificationText.enabled = !string.IsNullOrEmpty(note);
        }

        if (timerText == null) return;

        if (M.Phase == TimerPhase.Stopped)
        {
            timerText.text = FormatTime(0f);
            return;
        }

        if (M.Mode == TimerMode.Stopwatch)
            timerText.text = $"{M.GetPhaseLabel()} {FormatTime(M.ElapsedSeconds)}";
        else if (M.IsAwaitingNextPhase)
            timerText.text = $"{M.GetPhaseLabel()} {FormatTime(0f)}";
        else
            timerText.text = $"{M.GetPhaseLabel()} {FormatTime(M.RemainingSeconds)}";
    }
}
