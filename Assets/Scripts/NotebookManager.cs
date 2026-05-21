using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScheduleEvent / MemoEntry / LifetimeStats の保存・読み込みを一元管理。
/// 月・週・日・管理タブは全て ScheduleEvent を共有する。
/// </summary>
public class NotebookManager : MonoBehaviour
{
    public static NotebookManager Instance { get; private set; }

    private ScheduleData  scheduleData  = new ScheduleData();
    private MemoData      memoData      = new MemoData();
    private LifetimeStats lifetimeStats = new LifetimeStats();

    // 表示対象期間：過去1年・未来1年
    private static readonly int RANGE_DAYS = 365;

    private string PathSchedule  => Path.Combine(Application.persistentDataPath, "notebook_schedule.json");
    private string PathMemo      => Path.Combine(Application.persistentDataPath, "notebook_memo.json");
    private string PathLifetime  => Path.Combine(Application.persistentDataPath, "notebook_lifetime.json");

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadAll();
    }

    private void OnApplicationPause(bool pause) { if (pause)  SaveAll(); }
    private void OnApplicationFocus(bool focus)  { if (!focus) SaveAll(); }
    private void OnApplicationQuit()             { SaveAll(); }

    // ─── Save / Load ──────────────────────────────────────
    public void SaveAll()
    {
        WriteJson(PathSchedule, scheduleData);
        WriteJson(PathMemo,     memoData);
        WriteJson(PathLifetime, lifetimeStats);
    }

    private void LoadAll()
    {
        scheduleData  = ReadJson<ScheduleData>(PathSchedule)   ?? new ScheduleData();
        memoData      = ReadJson<MemoData>(PathMemo)           ?? new MemoData();
        lifetimeStats = ReadJson<LifetimeStats>(PathLifetime)  ?? new LifetimeStats();
        PruneOldEvents();
    }

    private static void WriteJson<T>(string path, T data)
        => File.WriteAllText(path, JsonUtility.ToJson(data, true));

    private static T ReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<T>(File.ReadAllText(path)); }
        catch { return null; }
    }

    // ─── ScheduleEvent CRUD ───────────────────────────────

    /// <summary>全イベントを返す</summary>
    public List<ScheduleEvent> GetAllEvents() => scheduleData.events;

    /// <summary>指定日のイベントを返す</summary>
    public List<ScheduleEvent> GetEventsByDate(DateTime date)
    {
        string key = DateKey(date);
        return scheduleData.events.Where(e => e.date == key).ToList();
    }

    /// <summary>指定月のイベントを返す</summary>
    public List<ScheduleEvent> GetEventsByMonth(int year, int month)
    {
        string prefix = $"{year}-{month:D2}";
        return scheduleData.events.Where(e => e.date != null && e.date.StartsWith(prefix)).ToList();
    }

    /// <summary>指定週（月曜起点）のイベントを返す</summary>
    public List<ScheduleEvent> GetEventsByWeek(DateTime anyDayInWeek)
    {
        var monday = anyDayInWeek.AddDays(-(int)anyDayInWeek.DayOfWeek + (int)DayOfWeek.Monday);
        if (anyDayInWeek.DayOfWeek == DayOfWeek.Sunday) monday = anyDayInWeek.AddDays(-6);
        var keys = Enumerable.Range(0, 7).Select(i => DateKey(monday.AddDays(i))).ToHashSet();
        return scheduleData.events.Where(e => e.date != null && keys.Contains(e.date)).ToList();
    }

    /// <summary>タグでフィルタ</summary>
    public List<ScheduleEvent> GetEventsByTag(string tagId)
        => scheduleData.events.Where(e => e.tagId == tagId).ToList();

    /// <summary>イベントを追加</summary>
    public ScheduleEvent AddEvent(string tagId, string title, string date = null, string time = null, string memo = "")
    {
        var ev = new ScheduleEvent
        {
            id          = Guid.NewGuid().ToString(),
            tagId       = tagId,
            title       = title,
            date        = date,
            time        = time,
            memo        = memo,
            isCompleted = false,
            createdAt   = NowKey(),
        };
        scheduleData.events.Add(ev);
        SaveAll();
        return ev;
    }

    /// <summary>イベントを更新</summary>
    public bool UpdateEvent(string id, string tagId, string title, string date, string time, string endTime, string memo)
    {
        var ev = scheduleData.events.Find(e => e.id == id);
        if (ev == null) return false;
        ev.tagId   = tagId;
        ev.title   = title;
        ev.date    = date;
        ev.time    = time;
        ev.endTime = endTime;
        ev.memo    = memo;
        SaveAll();
        return true;
    }

    /// <summary>完了状態を切り替え</summary>
    public void SetCompleted(string id, bool completed)
    {
        var ev = scheduleData.events.Find(e => e.id == id);
        if (ev == null) return;
        ev.isCompleted  = completed;
        ev.completedAt  = completed ? NowKey() : null;
        if (completed) RecordCompletion(ev.tagId);
        SaveAll();
    }

    /// <summary>イベントを削除</summary>
    public bool DeleteEvent(string id)
    {
        int removed = scheduleData.events.RemoveAll(e => e.id == id);
        if (removed > 0) SaveAll();
        return removed > 0;
    }

    /// <summary>表示範囲外の古いイベントを削除（日付なしは残す）</summary>
    private void PruneOldEvents()
    {
        var cutoff = DateTime.Now.AddDays(-RANGE_DAYS);
        var cutoffKey = DateKey(cutoff);
        scheduleData.events.RemoveAll(e =>
            e.date != null && string.Compare(e.date, cutoffKey) < 0);
    }

    // ─── MemoEntry CRUD ───────────────────────────────────

    public List<MemoEntry> GetAllMemos() => memoData.entries;

    public MemoEntry AddMemo(string title = "新しいメモ")
    {
        var entry = new MemoEntry
        {
            id        = Guid.NewGuid().ToString(),
            title     = title,
            body      = "",
            createdAt = NowKey(),
            updatedAt = NowKey(),
        };
        memoData.entries.Insert(0, entry);
        SaveAll();
        return entry;
    }

    public bool SaveMemo(string id, string title, string body)
    {
        var entry = memoData.entries.Find(e => e.id == id);
        if (entry == null) return false;
        entry.title     = title;
        entry.body      = body;
        entry.updatedAt = NowKey();
        SaveAll();
        return true;
    }

    public bool DeleteMemo(string id)
    {
        int removed = memoData.entries.RemoveAll(e => e.id == id);
        if (removed > 0) SaveAll();
        return removed > 0;
    }

    // ─── LifetimeStats ────────────────────────────────────

    public LifetimeStats GetLifetimeStats() => lifetimeStats;

    private void RecordCompletion(string tagId)
    {
        lifetimeStats.totalCompleted++;

        var tagCount = lifetimeStats.completedByTag.Find(t => t.tagId == tagId);
        if (tagCount == null)
        {
            tagCount = new TagCompletionCount { tagId = tagId, count = 0 };
            lifetimeStats.completedByTag.Add(tagCount);
        }
        tagCount.count++;

        string today = DateKey(DateTime.Now);
        var daily = lifetimeStats.dailyRecords.Find(d => d.date == today);
        if (daily == null)
        {
            daily = new DailyCompletionRecord { date = today, count = 0 };
            lifetimeStats.dailyRecords.Add(daily);
        }
        daily.count++;

        UpdateStreak();
    }

    private void UpdateStreak()
    {
        var sorted = lifetimeStats.dailyRecords
            .Where(d => d.count > 0)
            .OrderByDescending(d => d.date)
            .ToList();

        int current = 0;
        var today = DateTime.Now.Date;
        for (int i = 0; i < sorted.Count; i++)
        {
            var d = DateTime.Parse(sorted[i].date).Date;
            if ((today - d).Days == i) current++;
            else break;
        }
        lifetimeStats.currentStreak = current;
        if (current > lifetimeStats.longestStreak)
            lifetimeStats.longestStreak = current;
    }

    // ─── Helpers ──────────────────────────────────────────
    public static string DateKey(DateTime d)
        => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string NowKey()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string GetWeekKey(DateTime date)
    {
        var cal = CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{date.Year}-W{week:D2}";
    }
}
