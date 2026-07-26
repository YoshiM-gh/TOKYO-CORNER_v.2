using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ポモドーロ計測のUI非依存な本体（仕様書v4 §11 / Phase 3）。
///
/// 目的: シーンとUIから計測を切り離す。
///  - お店(Cafe) ⇔ フォーカス(UI_Prototype) の往復でタイマーが止まらない
///  - Barモードでカードが非表示でも計測が進み、フェーズ切替の通知が鳴る
///
/// 設計方針:
///  - 状態機械は TimerController からの移植。**enumは新設せず** TimerController.TimerPhase /
///    TimerMode をそのまま使う（全参照元のコンパイル互換を保つため）
///  - 計測は DateTime.UtcNow 基準（フレームレート・ドメインリロードの影響を受けない）
///  - 設定値（作業/休憩/長休憩/サイクル数/長休憩の間隔）の**唯一の持ち主**。
///    移行前は TimerController の[SerializeField]とPomodoroTimerUIのローカル変数に
///    二重管理されていて、Rボタンと▶ボタンで別の値が使われていた（その不整合をここで解消する）
///  - フェーズ終了は自動で次へ進まない。QueueNextPhase で待機し、✓（AdvanceToNextPhase）で進む
///    ＝移行前の意味論をそのまま維持する
/// </summary>
public class PomodoroManager : MonoBehaviour
{
    // ---- シングルトン -------------------------------------------------------

    private static PomodoroManager _instance;

    /// <summary>常に生きているインスタンス。無ければその場で作る。</summary>
    public static PomodoroManager Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance == null) CreateInstance();
    }

    private static void CreateInstance()
    {
        // 既にシーンに居る場合は拾う（多重生成防止）
        var found = FindAnyObjectByType<PomodoroManager>(FindObjectsInactive.Include);
        if (found != null) { _instance = found; return; }

        var go = new GameObject("[PomodoroManager]");
        _instance = go.AddComponent<PomodoroManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
        SetupAudio();
        // 起動直後は「設定表示」状態にしておく（移行前の PreparePomodoro と同じ入り口）
        PreparePomodoro();
    }

    // ---- 設定値（PlayerPrefs永続化） ---------------------------------------

    private const string K_WORK   = "tc_pomo_work";
    private const string K_BREAK  = "tc_pomo_break";
    private const string K_LONG   = "tc_pomo_longbreak";
    private const string K_CYCLES = "tc_pomo_cycles";
    private const string K_EVERY  = "tc_pomo_every";
    private const string K_SOUND  = "tc_pomo_sound";

    private float _workMinutes      = 25f;
    private float _breakMinutes     = 5f;
    private float _longBreakMinutes = 15f;
    private int   _cycleCount       = 4;
    private int   _longBreakEvery   = 4;
    private bool  _soundEnabled     = true;

    public float WorkMinutes
    {
        get => _workMinutes;
        set { _workMinutes = Mathf.Clamp(value, 1f, 99f); PlayerPrefs.SetFloat(K_WORK, _workMinutes); RaiseChanged(); }
    }

    public float BreakMinutes
    {
        get => _breakMinutes;
        set { _breakMinutes = Mathf.Clamp(value, 1f, 99f); PlayerPrefs.SetFloat(K_BREAK, _breakMinutes); RaiseChanged(); }
    }

    public float LongBreakMinutes
    {
        get => _longBreakMinutes;
        set { _longBreakMinutes = Mathf.Clamp(value, 1f, 99f); PlayerPrefs.SetFloat(K_LONG, _longBreakMinutes); RaiseChanged(); }
    }

    public int CycleCount
    {
        get => _cycleCount;
        set { _cycleCount = Mathf.Clamp(value, 1, 99); PlayerPrefs.SetInt(K_CYCLES, _cycleCount); RaiseChanged(); }
    }

    /// <summary>何ラウンドごとに長休憩を挟むか。</summary>
    public int LongBreakEveryRounds
    {
        get => _longBreakEvery;
        set { _longBreakEvery = Mathf.Max(1, value); PlayerPrefs.SetInt(K_EVERY, _longBreakEvery); }
    }

    /// <summary>フェーズ切替の通知音。カード非表示・お店滞在中でも鳴る。</summary>
    public bool SoundEnabled
    {
        get => _soundEnabled;
        set { _soundEnabled = value; PlayerPrefs.SetInt(K_SOUND, value ? 1 : 0); }
    }

    private void LoadSettings()
    {
        _workMinutes      = PlayerPrefs.GetFloat(K_WORK,   25f);
        _breakMinutes     = PlayerPrefs.GetFloat(K_BREAK,   5f);
        _longBreakMinutes = PlayerPrefs.GetFloat(K_LONG,   15f);
        _cycleCount       = PlayerPrefs.GetInt(K_CYCLES,    4);
        _longBreakEvery   = PlayerPrefs.GetInt(K_EVERY,     4);
        _soundEnabled     = PlayerPrefs.GetInt(K_SOUND,     1) != 0;
    }

    // ---- 状態 ---------------------------------------------------------------

    public event Action OnTimerChanged;

    private readonly List<string> _laps = new List<string>();
    private TimerController.TimerMode  _mode  = TimerController.TimerMode.Pomodoro;
    private TimerController.TimerPhase _phase = TimerController.TimerPhase.Stopped;
    private bool     _isSessionActive;
    private bool     _isRunning;
    private bool     _isPaused;
    private DateTime _sessionStartUtc;
    private DateTime _phaseStartUtc;
    private float    _elapsedSessionSeconds;
    private float    _elapsedBeforePauseSeconds;
    private float    _phaseDurationSeconds;
    private float    _workDurationSeconds;
    private float    _shortBreakDurationSeconds;
    private float    _longBreakDurationSeconds;
    private float    _stopwatchElapsedSeconds;
    private int      _currentRound = 1;
    private int      _totalRounds  = 1;
    private string   _lastNotification = string.Empty;
    private float    _notificationHideAt = -1f;
    private float    _notificationDuration = 3f;
    private bool     _awaitingNextPhase;
    private TimerController.TimerPhase _queuedPhase = TimerController.TimerPhase.Stopped;
    private float    _queuedPhaseDurationSeconds;
    private string   _queuedPhaseNotification = string.Empty;

    public TimerController.TimerMode  Mode  => _mode;
    public TimerController.TimerPhase Phase => _phase;
    public bool IsSessionActive => _isSessionActive;
    public bool IsRunning => _isRunning;
    public bool IsPaused  => _isPaused;
    public int  CurrentRound => _currentRound;
    public int  TotalRounds  => _totalRounds;
    public float DurationSeconds => _phaseDurationSeconds;
    public float ElapsedSeconds => _phase == TimerController.TimerPhase.Stopped ? 0f :
        _mode == TimerController.TimerMode.Stopwatch ? GetPhaseElapsedSeconds()
                                                     : Mathf.Min(GetPhaseElapsedSeconds(), _phaseDurationSeconds);
    public float RemainingSeconds => _mode == TimerController.TimerMode.Stopwatch
        ? ElapsedSeconds : Mathf.Max(0f, _phaseDurationSeconds - ElapsedSeconds);
    public float Progress01 => _phaseDurationSeconds <= 0f ? 0f : Mathf.Clamp01(ElapsedSeconds / _phaseDurationSeconds);
    public string LastNotification => _lastNotification;
    public IReadOnlyList<string> Laps => _laps;
    public bool IsAwaitingNextPhase => _mode == TimerController.TimerMode.Pomodoro && _awaitingNextPhase;

    private void Update()
    {
        UpdateNotificationVisibility();

        if (!_isRunning) return;

        UpdateElapsedSessionSeconds();
        if (_mode == TimerController.TimerMode.Pomodoro && GetPhaseElapsedSeconds() >= _phaseDurationSeconds)
            AdvancePhase();

        RaiseChanged();
    }

    // ---- 操作 ---------------------------------------------------------------

    public void StartSession()
    {
        _isSessionActive = true;
        _sessionStartUtc = DateTime.UtcNow;
        _elapsedSessionSeconds = 0f;
        PreparePomodoro();
        Debug.Log("[Pomodoro] Focus session started.");
    }

    public void StopSession()
    {
        UpdateElapsedSessionSeconds();
        Stop();
        int minutes = Mathf.FloorToInt(_elapsedSessionSeconds / 60f);
        int seconds = Mathf.FloorToInt(_elapsedSessionSeconds % 60f);
        Debug.Log($"[Pomodoro] Focus session ended. Duration: {minutes:00}:{seconds:00}");
    }

    /// <summary>保持している設定で開始する（バーの▶から使う便宜メソッド）。</summary>
    public void StartWithSettings()
    {
        StartPomodoro(_workMinutes, _breakMinutes, _longBreakMinutes, _cycleCount);
    }

    public void StartPomodoro(float workMinutes, float shortBreakMinutes, float longBreakMinutesValue, int rounds)
    {
        _mode = TimerController.TimerMode.Pomodoro;
        ClearQueuedPhase();
        _workDurationSeconds       = MinutesToSeconds(workMinutes);
        _shortBreakDurationSeconds = MinutesToSeconds(shortBreakMinutes);
        _longBreakDurationSeconds  = MinutesToSeconds(longBreakMinutesValue);
        _totalRounds  = Mathf.Max(1, rounds);
        _currentRound = 1;
        _laps.Clear();
        StartPhase(TimerController.TimerPhase.Work, _workDurationSeconds);
        ShowNotification("Pomodoro started");
    }

    /// <summary>停止状態に戻す（Rボタン）。移行前と違い、設定は保持中の値を使う。</summary>
    public void PreparePomodoro()
    {
        _mode = TimerController.TimerMode.Pomodoro;
        ClearQueuedPhase();
        _workDurationSeconds       = MinutesToSeconds(_workMinutes);
        _shortBreakDurationSeconds = MinutesToSeconds(_breakMinutes);
        _longBreakDurationSeconds  = MinutesToSeconds(_longBreakMinutes);
        _totalRounds  = Mathf.Max(1, _cycleCount);
        _currentRound = 1;
        _phase = TimerController.TimerPhase.Stopped;
        _phaseDurationSeconds = _workDurationSeconds;
        _elapsedBeforePauseSeconds = 0f;
        _isRunning = false;
        _isPaused  = false;
        _laps.Clear();
        ClearNotification();
        RaiseChanged();
    }

    public void StartStopwatch()
    {
        _mode = TimerController.TimerMode.Stopwatch;
        ClearQueuedPhase();
        _totalRounds  = 1;
        _currentRound = 1;
        _stopwatchElapsedSeconds = 0f;
        _laps.Clear();
        StartPhase(TimerController.TimerPhase.Stopwatch, 0f);
        ShowNotification("Stopwatch started");
    }

    public void PrepareStopwatch()
    {
        _mode = TimerController.TimerMode.Stopwatch;
        ClearQueuedPhase();
        _totalRounds  = 1;
        _currentRound = 1;
        _phase = TimerController.TimerPhase.Stopped;
        _phaseDurationSeconds = 0f;
        _elapsedBeforePauseSeconds = 0f;
        _stopwatchElapsedSeconds = 0f;
        _isRunning = false;
        _isPaused  = false;
        _laps.Clear();
        ClearNotification();
        RaiseChanged();
    }

    public void StartShortBreak(float minutes)
    {
        _mode = TimerController.TimerMode.Pomodoro;
        StartPhase(TimerController.TimerPhase.ShortBreak, MinutesToSeconds(minutes));
        ShowNotification("Short break");
    }

    public void StartLongBreak(float minutes)
    {
        _mode = TimerController.TimerMode.Pomodoro;
        StartPhase(TimerController.TimerPhase.LongBreak, MinutesToSeconds(minutes));
        ShowNotification("Long break");
    }

    public void Pause()
    {
        if (!_isRunning) return;

        _elapsedBeforePauseSeconds = GetPhaseElapsedSeconds();
        _isRunning = false;
        _isPaused  = true;
        ShowNotification("Paused");
        RaiseChanged();
    }

    public void Resume()
    {
        if (!_isPaused) return;

        _phaseStartUtc = DateTime.UtcNow;
        _isRunning = true;
        _isPaused  = false;
        ShowNotification("Resumed");
        RaiseChanged();
    }

    public void TogglePause()
    {
        if (_isPaused) Resume();
        else           Pause();
    }

    public void Stop()
    {
        ClearQueuedPhase();
        _isRunning = false;
        _isPaused  = false;
        _isSessionActive = false;
        _phase = TimerController.TimerPhase.Stopped;
        _phaseDurationSeconds = 0f;
        _elapsedBeforePauseSeconds = 0f;
        _stopwatchElapsedSeconds = 0f;
        ClearNotification();
        RaiseChanged();
    }

    public void RecordLap()
    {
        if (_phase == TimerController.TimerPhase.Stopped || _phase == TimerController.TimerPhase.Completed) return;

        string lap = _mode == TimerController.TimerMode.Pomodoro
            ? $"{_laps.Count + 1}. R{_currentRound}/{_totalRounds} {GetPhaseLabel()} {FormatTime(ElapsedSeconds)}"
            : $"{_laps.Count + 1}. {FormatTime(ElapsedSeconds)}";
        _laps.Insert(0, lap);
        ShowNotification("Lap recorded");
        RaiseChanged();
    }

    /// <summary>待機中の次フェーズへ進む（✓）。</summary>
    public void AdvanceToNextPhase()
    {
        if (!IsAwaitingNextPhase) return;

        var nextPhase        = _queuedPhase;
        var nextDuration     = _queuedPhaseDurationSeconds;
        var nextNotification = _queuedPhaseNotification;
        ClearQueuedPhase();
        StartPhase(nextPhase, nextDuration);
        if (!string.IsNullOrEmpty(nextNotification)) ShowNotification(nextNotification);
    }

    /// <summary>実行中・一時停止中でも強制的に次フェーズへ進む。</summary>
    public void ForceAdvancePhase()
    {
        if (_phase == TimerController.TimerPhase.Stopped || _phase == TimerController.TimerPhase.Completed) return;

        _elapsedBeforePauseSeconds = _phaseDurationSeconds;
        _isRunning = false;
        _isPaused  = false;
        AdvancePhase();
        if (_awaitingNextPhase) AdvanceToNextPhase();
    }

    public string GetNextPhaseLabel() => IsAwaitingNextPhase ? GetPhaseLabel(_queuedPhase) : string.Empty;
    public string GetPhaseLabel()     => GetPhaseLabel(_phase);

    public static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int secs    = totalSeconds % 60;
        return string.Format("{0:00}:{1:00}", minutes, secs);
    }

    // ---- 内部 ---------------------------------------------------------------

    private void StartPhase(TimerController.TimerPhase nextPhase, float durationSeconds)
    {
        ClearQueuedPhase();
        _phase = nextPhase;
        _phaseDurationSeconds = _mode == TimerController.TimerMode.Stopwatch ? 0f : Mathf.Max(1f, durationSeconds);
        _elapsedBeforePauseSeconds = 0f;
        _phaseStartUtc = DateTime.UtcNow;
        _isRunning = true;
        _isPaused  = false;
        _isSessionActive = true;
        RaiseChanged();
    }

    private void AdvancePhase()
    {
        if (_phase == TimerController.TimerPhase.Work)
        {
            if (_currentRound >= _totalRounds)
            {
                CompleteTimer("Pomodoro complete");
                return;
            }

            bool useLongBreak = _currentRound % Mathf.Max(1, _longBreakEvery) == 0;
            QueueNextPhase(
                useLongBreak ? TimerController.TimerPhase.LongBreak : TimerController.TimerPhase.ShortBreak,
                useLongBreak ? _longBreakDurationSeconds : _shortBreakDurationSeconds,
                useLongBreak ? "Long break started" : "Short break started");
            return;
        }

        if (_phase == TimerController.TimerPhase.ShortBreak || _phase == TimerController.TimerPhase.LongBreak)
        {
            _currentRound++;
            QueueNextPhase(TimerController.TimerPhase.Work, _workDurationSeconds, "Work started");
        }
    }

    private void CompleteTimer(string message)
    {
        ClearQueuedPhase();
        _phase = TimerController.TimerPhase.Completed;
        _isRunning = false;
        _isPaused  = false;
        _elapsedBeforePauseSeconds = _phaseDurationSeconds;
        ShowNotification(message);
        PlayBeep(2); // 全サイクル完了は2音
        RaiseChanged();
    }

    private void QueueNextPhase(TimerController.TimerPhase nextPhase, float nextDurationSeconds, string nextNotification)
    {
        _awaitingNextPhase = true;
        _queuedPhase = nextPhase;
        _queuedPhaseDurationSeconds = Mathf.Max(1f, nextDurationSeconds);
        _queuedPhaseNotification = nextNotification;
        _elapsedBeforePauseSeconds = _phaseDurationSeconds;
        _isRunning = false;
        _isPaused  = false;
        ShowNotification("Phase complete");
        PlayBeep(1); // フェーズ終了は1音（カード非表示・お店滞在中でも鳴る）
        RaiseChanged();
    }

    private void ClearQueuedPhase()
    {
        _awaitingNextPhase = false;
        _queuedPhase = TimerController.TimerPhase.Stopped;
        _queuedPhaseDurationSeconds = 0f;
        _queuedPhaseNotification = string.Empty;
    }

    private float GetPhaseElapsedSeconds()
    {
        if (_phase == TimerController.TimerPhase.Stopped) return 0f;
        if (_isPaused || _phase == TimerController.TimerPhase.Completed) return _elapsedBeforePauseSeconds;
        if (_awaitingNextPhase) return _elapsedBeforePauseSeconds;
        if (!_isRunning) return _elapsedBeforePauseSeconds;

        float realtimeElapsed = (float)(DateTime.UtcNow - _phaseStartUtc).TotalSeconds;
        float elapsed = Mathf.Max(0f, _elapsedBeforePauseSeconds + realtimeElapsed);
        if (_mode == TimerController.TimerMode.Stopwatch) _stopwatchElapsedSeconds = elapsed;
        return elapsed;
    }

    private void UpdateElapsedSessionSeconds()
    {
        if (!_isSessionActive) return;
        float dt = (float)(DateTime.UtcNow - _sessionStartUtc).TotalSeconds;
        _elapsedSessionSeconds = Mathf.Max(0f, dt);
    }

    private void ShowNotification(string message)
    {
        _lastNotification = message;
        _notificationHideAt = _notificationDuration > 0f ? Time.unscaledTime + _notificationDuration : -1f;
    }

    private void ClearNotification()
    {
        _lastNotification = string.Empty;
        _notificationHideAt = -1f;
    }

    private void UpdateNotificationVisibility()
    {
        if (_notificationHideAt < 0f || Time.unscaledTime < _notificationHideAt) return;
        ClearNotification();
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        var handler = OnTimerChanged;
        if (handler != null) handler.Invoke();
    }

    private static string GetPhaseLabel(TimerController.TimerPhase targetPhase)
    {
        switch (targetPhase)
        {
            case TimerController.TimerPhase.Work:       return "WORK";
            case TimerController.TimerPhase.ShortBreak: return "SHORT BREAK";
            case TimerController.TimerPhase.LongBreak:  return "LONG BREAK";
            case TimerController.TimerPhase.Stopwatch:  return "STOPWATCH";
            case TimerController.TimerPhase.Completed:  return "DONE";
            default:                                    return "READY";
        }
    }

    private static float MinutesToSeconds(float minutes) => Mathf.Max(0.1f, minutes) * 60f;

    // ---- 通知音（アセット不要のプログラム生成波形） -------------------------

    private AudioSource _audio;
    private AudioClip   _beepClip;

    private void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake  = false;
        _audio.spatialBlend = 0f; // 2D
        _audio.volume       = 0.35f;
        _beepClip = BuildBeepClip();
    }

    /// <summary>
    /// 短いビープをその場で生成する（外部アセットに依存しない）。
    /// 880Hzの正弦波に立ち上がり/減衰の包絡を掛けた0.12秒。耳に刺さらない程度の音量。
    /// </summary>
    private static AudioClip BuildBeepClip()
    {
        const int   rate     = 44100;
        const float duration = 0.12f;
        const float freq     = 880f;
        int samples = Mathf.RoundToInt(rate * duration);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float envelope = Mathf.Min(1f, t / 0.008f)                       // 立ち上がり8ms
                           * Mathf.Clamp01(1f - (t - 0.02f) / (duration - 0.02f)); // 以降ゆるやかに減衰
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
        }
        var clip = AudioClip.Create("PomodoroBeep", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>ビープをcount回鳴らす（カードが非表示でも鳴る）。</summary>
    private void PlayBeep(int count)
    {
        if (!_soundEnabled || _audio == null || _beepClip == null) return;
        StopAllCoroutines();
        StartCoroutine(BeepRoutine(Mathf.Max(1, count)));
    }

    private System.Collections.IEnumerator BeepRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _audio.PlayOneShot(_beepClip);
            if (i < count - 1) yield return new WaitForSecondsRealtime(0.18f);
        }
    }
}
