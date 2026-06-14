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

    /// <summary>データ変更カウンタ（SaveAll ごとに増加。UI 側の変更検知用）</summary>
    public int DataVersion { get; private set; }

    private ScheduleData  scheduleData  = new ScheduleData();
    private MemoData      memoData      = new MemoData();
    private LifetimeStats lifetimeStats = new LifetimeStats();
    private WeeklyMemoData weeklyMemoData = new WeeklyMemoData();
    private StickyNotesData stickyNotesData = new StickyNotesData();
    private TodoListData    todoData    = new TodoListData();
    private RoutineListData routineData = new RoutineListData();
    private MemoNotesData   memoNotes   = new MemoNotesData();

    // 表示対象期間：過去1年・未来1年
    private static readonly int RANGE_DAYS = 365;

    private string PathSchedule  => Path.Combine(Application.persistentDataPath, "notebook_schedule.json");
    private string PathMemo      => Path.Combine(Application.persistentDataPath, "notebook_memo.json");
    private string PathLifetime     => Path.Combine(Application.persistentDataPath, "notebook_lifetime.json");
    private string PathWeeklyMemo  => Path.Combine(Application.persistentDataPath, "notebook_weekly_memo.json");
    private string PathStickyNotes => Path.Combine(Application.persistentDataPath, "notebook_sticky.json");
    private string PathTodo      => Path.Combine(Application.persistentDataPath, "notebook_todo.json");
    private string PathTodoArchive => Path.Combine(Application.persistentDataPath, "notebook_todo_archive.json");
    private string PathRoutine   => Path.Combine(Application.persistentDataPath, "notebook_routine.json");
    private string PathMemoNotes => Path.Combine(Application.persistentDataPath, "notebook_memo_notes.json");

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
        DataVersion++;
        WriteJson(PathSchedule, scheduleData);
        WriteJson(PathMemo,     memoData);
        WriteJson(PathLifetime, lifetimeStats);
        WriteJson(PathWeeklyMemo, weeklyMemoData);
        WriteJson(PathStickyNotes, stickyNotesData);
        WriteJson(PathTodo,      todoData);
        WriteJson(PathRoutine,   routineData);
        WriteJson(PathMemoNotes, memoNotes);
    }

    private void LoadAll()
    {
        scheduleData  = ReadJson<ScheduleData>(PathSchedule)   ?? new ScheduleData();
        memoData      = ReadJson<MemoData>(PathMemo)           ?? new MemoData();
        lifetimeStats   = ReadJson<LifetimeStats>(PathLifetime)   ?? new LifetimeStats();
        weeklyMemoData  = ReadJson<WeeklyMemoData>(PathWeeklyMemo) ?? new WeeklyMemoData();
        stickyNotesData = ReadJson<StickyNotesData>(PathStickyNotes) ?? new StickyNotesData();
        todoData    = ReadJson<TodoListData>(PathTodo)         ?? new TodoListData();
        routineData = ReadJson<RoutineListData>(PathRoutine)   ?? new RoutineListData();
        memoNotes   = ReadJson<MemoNotesData>(PathMemoNotes)   ?? new MemoNotesData();
        EnsureDefaultMemoFolder();
        PruneOldEvents();
        ArchiveOldTodos();
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
        // 日曜始まりで週の日曜を求める
        var sunday = anyDayInWeek.AddDays(-(int)anyDayInWeek.DayOfWeek);
        var keys = Enumerable.Range(0, 7).Select(i => DateKey(sunday.AddDays(i))).ToHashSet();
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

    // ─── WeeklyMemo ────────────────────────────────────────

    /// <summary>週メモを取得（weekKey = その週の先頭日 "yyyy-MM-dd"）</summary>
    public string GetWeeklyMemo(string weekKey)
    {
        var entry = weeklyMemoData.entries.Find(e => e.weekKey == weekKey);
        return entry?.text ?? string.Empty;
    }

    /// <summary>週メモを保存・更新</summary>
    public void SetWeeklyMemo(string weekKey, string text)
    {
        var entry = weeklyMemoData.entries.Find(e => e.weekKey == weekKey);
        if (entry == null)
        {
            weeklyMemoData.entries.Add(new WeeklyMemoEntry { weekKey = weekKey, text = text });
        }
        else
        {
            entry.text = text;
        }
        SaveAll();
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

    // ─── StickyNote CRUD ──────────────────────────────────
    public List<StickyNoteData> GetStickyNotes(string dateKey)
        => stickyNotesData.notes.Where(n => n.dateKey == dateKey).ToList();

    public StickyNoteData AddStickyNote(string dateKey, float ax, float ay, string tagId = "")
    {
        var note = new StickyNoteData {
            id = Guid.NewGuid().ToString(), dateKey = dateKey,
            content = "", anchorX = ax, anchorY = ay,
            width = 200f, height = 200f, colorIndex = 0 };
        stickyNotesData.notes.Add(note);
        SaveAll();
        return note;
    }

    public void UpdateStickyNote(StickyNoteData data)
    {
        var idx = stickyNotesData.notes.FindIndex(n => n.id == data.id);
        if (idx >= 0) stickyNotesData.notes[idx] = data;
        SaveAll();
    }

    public void DeleteStickyNote(string id)
    {
        stickyNotesData.notes.RemoveAll(n => n.id == id);
        SaveAll();
    }

    // ─── Todo CRUD ────────────────────────────────────────
    /// <summary>優先度高→sortOrder→作成順。includeCompleted=false で未完了のみ</summary>
    // ─── Todoアーカイブ ───────────────────────────────────
    // 完了後30日経過したTodoを本体から外し、アーカイブファイルへ追記する(起動時に1回)。
    // アーカイブは追記専用で、ランタイムでは読み込まない(将来の振り返り・統計用)。
    private const int TODO_ARCHIVE_DAYS = 30;

    private void ArchiveOldTodos()
    {
        if (todoData == null || todoData.items == null || todoData.items.Count == 0) return;
        var threshold = DateTime.Now.Date.AddDays(-TODO_ARCHIVE_DAYS);
        var old = todoData.items.Where(t =>
            t.isCompleted &&
            !string.IsNullOrEmpty(t.completedAt) &&
            DateTime.TryParseExact(t.completedAt, "yyyy-MM-dd HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt) &&
            dt.Date < threshold).ToList();
        if (old.Count == 0) return;

        var archive = ReadJson<TodoListData>(PathTodoArchive) ?? new TodoListData();
        archive.items.AddRange(old);
        WriteJson(PathTodoArchive, archive);

        foreach (var t in old) todoData.items.Remove(t);
        WriteJson(PathTodo, todoData);
        Debug.Log("[NotebookManager] Todoアーカイブ: " + old.Count + "件を notebook_todo_archive.json へ移動");
    }

    public List<TodoItem> GetTodos(bool includeCompleted = true)
    {
        var list = includeCompleted ? new List<TodoItem>(todoData.items)
                                    : todoData.items.FindAll(t => !t.isCompleted);
        list.Sort((a, b) => {
            if (a.priorityHigh != b.priorityHigh) return a.priorityHigh ? -1 : 1;
            if (a.sortOrder != b.sortOrder) return a.sortOrder.CompareTo(b.sortOrder);
            return string.Compare(a.createdAt, b.createdAt, StringComparison.Ordinal);
        });
        return list;
    }

    public List<TodoItem> GetTodosOn(string dateKey) =>
        todoData.items.FindAll(t => t.dateKey == dateKey);

    public TodoItem AddTodo(string title)
    {
        var t = new TodoItem { id = Guid.NewGuid().ToString(), title = title, createdAt = NowKey() };
        todoData.items.Add(t);
        SaveAll();
        return t;
    }

    public void UpdateTodo(TodoItem item)
    {
        if (item == null) return;
        var existing = todoData.items.Find(t => t.id == item.id);
        if (existing == null) return;
        // 【重要】インスタンスを差し替えず、既存インスタンスへ全フィールドをコピーする。
        // これにより todoData.items 内のインスタンス同一性が保たれ、
        // リストの行・詳細ペイン・カレンダー等が同じ参照を共有し続ける（双方向同期の前提）。
        // 差し替え方式だと、別箇所が古いインスタンスを保持して上書き事故が起きる。
        if (ReferenceEquals(existing, item)) { SaveAll(); return; } // 同一なら何もしない
        existing.title        = item.title;
        existing.memo         = item.memo;
        existing.dateKey      = item.dateKey;
        existing.time         = item.time;
        existing.priorityHigh = item.priorityHigh;
        existing.isCompleted  = item.isCompleted;
        existing.completedAt  = item.completedAt;
        existing.createdAt    = item.createdAt;
        existing.sortOrder    = item.sortOrder;
        SaveAll();
    }

    public void SetTodoCompleted(string id, bool done)
    {
        var t = todoData.items.Find(x => x.id == id);
        if (t == null) return;
        t.isCompleted = done;
        t.completedAt = done ? NowKey() : null;
        SaveAll();
    }

    public void DeleteTodo(string id)
    {
        todoData.items.RemoveAll(t => t.id == id);
        SaveAll();
    }

    // ─── Routine CRUD ─────────────────────────────────────
    public List<RoutineItem> GetRoutines() => new List<RoutineItem>(routineData.items);

    /// <summary>指定日に出現する Routine 一覧（カレンダーアイコン・Daily 表示用）</summary>
    public List<RoutineItem> GetRoutinesOn(DateTime day) =>
        routineData.items.FindAll(r => r.OccursOn(day));

    public RoutineItem AddRoutine(string title)
    {
        var r = new RoutineItem {
            id = Guid.NewGuid().ToString(), title = title,
            startDate = DateTime.Now.ToString("yyyy-MM-dd"), createdAt = NowKey()
        };
        routineData.items.Add(r);
        SaveAll();
        return r;
    }

    public void UpdateRoutine(RoutineItem item)
    {
        var idx = routineData.items.FindIndex(r => r.id == item.id);
        if (idx >= 0) routineData.items[idx] = item;
        SaveAll();
    }

    /// <summary>occurrence（日付）単位の完了トグル</summary>
    public void SetRoutineDone(string id, string dateKey, bool done)
    {
        var r = routineData.items.Find(x => x.id == id);
        if (r == null) return;
        if (done) { if (!r.completedDates.Contains(dateKey)) r.completedDates.Add(dateKey); }
        else        r.completedDates.Remove(dateKey);
        SaveAll();
    }

    public void DeleteRoutine(string id)
    {
        routineData.items.RemoveAll(r => r.id == id);
        SaveAll();
    }

    // ─── Memo（新仕様）CRUD ───────────────────────────────
    public const string DefaultMemoFolderId = "default";

    private void EnsureDefaultMemoFolder()
    {
        if (memoNotes.folders.Exists(f => f.id == DefaultMemoFolderId)) return;
        memoNotes.folders.Insert(0, new MemoFolder { id = DefaultMemoFolderId, name = "メモ", sortOrder = 0 });
    }

    public List<MemoFolder> GetMemoFolders() => new List<MemoFolder>(memoNotes.folders);

    public MemoFolder AddMemoFolder(string name)
    {
        var f = new MemoFolder { id = Guid.NewGuid().ToString(), name = name,
                                 sortOrder = memoNotes.folders.Count };
        memoNotes.folders.Add(f);
        SaveAll();
        return f;
    }

    /// <summary>フォルダ削除。中のノートはデフォルトフォルダへ移動</summary>
    public void DeleteMemoFolder(string folderId)
    {
        if (folderId == DefaultMemoFolderId) return; // デフォルトは削除不可
        foreach (var note in memoNotes.notes)
            if (note.folderId == folderId) note.folderId = DefaultMemoFolderId;
        memoNotes.folders.RemoveAll(f => f.id == folderId);
        SaveAll();
    }

    /// <summary>ピン留め→更新日降順。folderId=null で全フォルダ。ゴミ箱は除外</summary>
    public List<MemoNote> GetMemoNotes(string folderId = null)
    {
        var list = memoNotes.notes.FindAll(m =>
            !m.IsTrashed && (folderId == null || m.folderId == folderId));
        list.Sort((a, b) => {
            if (a.isPinned != b.isPinned) return a.isPinned ? -1 : 1;
            return string.Compare(b.updatedAt, a.updatedAt, StringComparison.Ordinal);
        });
        return list;
    }

    public List<MemoNote> GetTrashedMemoNotes() =>
        memoNotes.notes.FindAll(m => m.IsTrashed);

    public List<MemoNote> GetMemoNotesOn(string dateKey) =>
        memoNotes.notes.FindAll(m => !m.IsTrashed && m.dateKey == dateKey);

    public MemoNote AddMemoNote(string folderId = DefaultMemoFolderId)
    {
        var m = new MemoNote {
            id = Guid.NewGuid().ToString(), folderId = folderId ?? DefaultMemoFolderId,
            title = "", createdAt = NowKey(), updatedAt = NowKey()
        };
        memoNotes.notes.Add(m);
        SaveAll();
        return m;
    }

    public void UpdateMemoNote(MemoNote note)
    {
        var idx = memoNotes.notes.FindIndex(m => m.id == note.id);
        if (idx < 0) return;
        note.updatedAt = NowKey();
        memoNotes.notes[idx] = note;
        SaveAll();
    }

    public void TrashMemoNote(string id)
    {
        var m = memoNotes.notes.Find(x => x.id == id);
        if (m == null) return;
        m.deletedAt = NowKey();
        SaveAll();
    }

    public void RestoreMemoNote(string id)
    {
        var m = memoNotes.notes.Find(x => x.id == id);
        if (m == null) return;
        m.deletedAt = null;
        SaveAll();
    }

    public void DeleteMemoNotePermanently(string id)
    {
        memoNotes.notes.RemoveAll(m => m.id == id);
        SaveAll();
    }
}
