using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Monthly / Weekly / Daily / Todo / Memo の保存・読み込みを一元管理。
/// SaveDataManager とは独立したファイルで保持する。
/// </summary>
public class NotebookManager : MonoBehaviour
{
    public static NotebookManager Instance { get; private set; }

    private MonthlyData  monthlyData  = new MonthlyData();
    private WeeklyData   weeklyData   = new WeeklyData();
    private DailyData    dailyData    = new DailyData();
    private TodoData     todoData     = new TodoData();
    private MemoData     memoData     = new MemoData();

    private string PathMonthly => Path.Combine(Application.persistentDataPath, "notebook_monthly.json");
    private string PathWeekly  => Path.Combine(Application.persistentDataPath, "notebook_weekly.json");
    private string PathDaily   => Path.Combine(Application.persistentDataPath, "notebook_daily.json");
    private string PathTodo    => Path.Combine(Application.persistentDataPath, "notebook_todo.json");
    private string PathMemo    => Path.Combine(Application.persistentDataPath, "notebook_memo.json");

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadAll();
    }

    private void OnApplicationPause(bool pause) { if (pause) SaveAll(); }
    private void OnApplicationFocus(bool focus)  { if (!focus) SaveAll(); }
    private void OnApplicationQuit()             { SaveAll(); }

    // ─── Save / Load ──────────────────────────────────────
    public void SaveAll()
    {
        WriteJson(PathMonthly, monthlyData);
        WriteJson(PathWeekly,  weeklyData);
        WriteJson(PathDaily,   dailyData);
        WriteJson(PathTodo,    todoData);
        WriteJson(PathMemo,    memoData);
    }

    private void LoadAll()
    {
        monthlyData = ReadJson<MonthlyData>(PathMonthly)  ?? new MonthlyData();
        weeklyData  = ReadJson<WeeklyData>(PathWeekly)    ?? new WeeklyData();
        dailyData   = ReadJson<DailyData>(PathDaily)      ?? new DailyData();
        todoData    = ReadJson<TodoData>(PathTodo)        ?? new TodoData();
        memoData    = ReadJson<MemoData>(PathMemo)        ?? new MemoData();
    }

    private static void WriteJson<T>(string path, T data)
        => File.WriteAllText(path, JsonUtility.ToJson(data, true));

    private static T ReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<T>(File.ReadAllText(path)); }
        catch { return null; }
    }

    // ─── Monthly ──────────────────────────────────────────
    public MonthlyEntry GetMonthlyEntry(DateTime date)
    {
        string key = DateKey(date);
        return monthlyData.entries.Find(e => e.date == key);
    }

    public void SetMonthlyEntry(DateTime date, string text, int colorMark = 0)
    {
        string key = DateKey(date);
        var entry = monthlyData.entries.Find(e => e.date == key);
        if (entry == null)
        {
            entry = new MonthlyEntry { date = key };
            monthlyData.entries.Add(entry);
        }
        entry.text = text;
        entry.colorMark = colorMark;
        SaveAll();
    }

    // ─── Weekly ───────────────────────────────────────────
    public WeeklyEntry GetWeeklyEntry(string weekKey, int dayOfWeek)
        => weeklyData.entries.Find(e => e.weekKey == weekKey && e.dayOfWeek == dayOfWeek);

    public void SetWeeklyEntry(string weekKey, int dayOfWeek, string note)
    {
        var entry = weeklyData.entries.Find(e => e.weekKey == weekKey && e.dayOfWeek == dayOfWeek);
        if (entry == null)
        {
            entry = new WeeklyEntry { weekKey = weekKey, dayOfWeek = dayOfWeek };
            weeklyData.entries.Add(entry);
        }
        entry.note = note;
        SaveAll();
    }

    public static string GetWeekKey(DateTime date)
    {
        var cal = CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{date.Year}-W{week:D2}";
    }

    // ─── Daily ────────────────────────────────────────────
    public DailyEntry GetDailyEntry(DateTime date)
    {
        string key = DateKey(date);
        return dailyData.entries.Find(e => e.date == key);
    }

    public DailyEntry GetOrCreateDailyEntry(DateTime date)
    {
        string key = DateKey(date);
        var entry = dailyData.entries.Find(e => e.date == key);
        if (entry != null) return entry;
        entry = new DailyEntry { date = key };
        dailyData.entries.Add(entry);
        return entry;
    }

    public void SaveDailyEntry(DailyEntry entry)
    {
        var existing = dailyData.entries.Find(e => e.date == entry.date);
        if (existing == null) dailyData.entries.Add(entry);
        SaveAll();
    }

    // ─── Todo ─────────────────────────────────────────────
    public System.Collections.Generic.List<TodoItem> GetAllTodos()
        => todoData.items;

    public TodoItem AddTodo(string text)
    {
        if (todoData.items.Count >= 100) return null;
        var item = new TodoItem
        {
            id          = Guid.NewGuid().ToString(),
            text        = text,
            isCompleted = false,
            createdAt   = DateKey(DateTime.Now)
        };
        todoData.items.Add(item);
        SaveAll();
        return item;
    }

    public void SetTodoCompleted(string id, bool completed)
    {
        var item = todoData.items.Find(i => i.id == id);
        if (item == null) return;
        item.isCompleted = completed;
        SaveAll();
    }

    public void DeleteTodo(string id)
    {
        todoData.items.RemoveAll(i => i.id == id);
        SaveAll();
    }

    // ─── Memo ─────────────────────────────────────────────
    public System.Collections.Generic.List<MemoEntry> GetAllMemos()
        => memoData.entries;

    public MemoEntry AddMemo(string title = "新しいメモ")
    {
        var entry = new MemoEntry
        {
            id        = Guid.NewGuid().ToString(),
            title     = title,
            body      = "",
            updatedAt = NowKey()
        };
        memoData.entries.Insert(0, entry);
        SaveAll();
        return entry;
    }

    public void SaveMemo(string id, string title, string body)
    {
        var entry = memoData.entries.Find(e => e.id == id);
        if (entry == null) return;
        entry.title     = title;
        entry.body      = body;
        entry.updatedAt = NowKey();
        SaveAll();
    }

    public void DeleteMemo(string id)
    {
        memoData.entries.RemoveAll(e => e.id == id);
        SaveAll();
    }

    // ─── Helpers ──────────────────────────────────────────
    private static string DateKey(DateTime d)
        => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NowKey()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
