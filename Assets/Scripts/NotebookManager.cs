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
    private MomentLogData   momentLog   = new MomentLogData();

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
    private string PathMoments   => Path.Combine(Application.persistentDataPath, "notebook_moments.json");

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
        WriteJson(PathMoments,   momentLog);
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
        momentLog   = ReadJson<MomentLogData>(PathMoments)     ?? new MomentLogData();
        EnsureDefaultMemoFolder();
        EnsureMemoNoteSortOrders();
        PruneOldEvents();
        ArchiveOldTodos();
        AutoTrashStaleMemoNotes();
        PurgeOldTrashedMemoNotes();
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

    /// <summary>指定タグのイベントを全削除（カテゴリー改名の削除パス用）。削除件数を返す。</summary>
    public int DeleteEventsByTag(string tagId)
    {
        if (string.IsNullOrEmpty(tagId)) return 0;
        int removed = scheduleData.events.RemoveAll(e => e.tagId == tagId);
        if (removed > 0) SaveAll();
        return removed;
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

    // ─── DailyMoment（キャラカードの今日の一言・1日1件） ───────────
    /// <summary>指定日に記録済みの moment を返す（無ければ null）。</summary>
    public DailyMoment GetMomentForDate(string dateKey)
        => momentLog.moments.Find(m => m.id == dateKey);

    /// <summary>その日の moment を記録（既に有ればそのまま返す）。</summary>
    public DailyMoment RecordMoment(DailyMoment m)
    {
        if (m == null) return null;
        var existing = momentLog.moments.Find(x => x.id == m.id);
        if (existing != null) return existing;
        momentLog.moments.Add(m);
        SaveAll();
        return m;
    }

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
    private const int MEMO_AUTO_TRASH_DAYS = 30;       // 更新がこの日数ないメモを自動でゴミ箱へ
    private const int MEMO_TRASH_RETENTION_DAYS = 10;  // ゴミ箱でこの日数を超えたら完全削除

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
        // sortOrder は既存最大+1 を採番（各タスクが一意の並び順を持つ＝並べ替えの前提）。
        int nextOrder = (todoData.items.Count > 0) ? todoData.items.Max(x => x.sortOrder) + 1 : 0;
        var t = new TodoItem { id = Guid.NewGuid().ToString(), title = title, createdAt = NowKey(), sortOrder = nextOrder };
        todoData.items.Add(t);
        SaveAll();
        return t;
    }

    /// <summary>2つのTodoのsortOrderを入れ替えて保存する（同一日付グループ内の上下並べ替え用）。</summary>
    public void SwapTodoOrder(string idA, string idB)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB) || idA == idB) return;
        var a = todoData.items.Find(t => t.id == idA);
        var b = todoData.items.Find(t => t.id == idB);
        if (a == null || b == null) return;
        // sortOrderが同値（未採番=0同士など）だと交換しても順序が変わらない。
        // その場合は現在のリスト内の相対位置から一意な値を一旦焼き付けてから交換する。
        if (a.sortOrder == b.sortOrder)
        {
            for (int i = 0; i < todoData.items.Count; i++) todoData.items[i].sortOrder = i;
        }
        int tmp = a.sortOrder; a.sortOrder = b.sortOrder; b.sortOrder = tmp;
        SaveAll();
    }

    /// <summary>当日タスクを翌日へ送る（Daily の「→」翌日送り）。
    /// 日付を変えるグループ移動なので sortOrder は破棄（0）する（フェーズ3の大原則）。</summary>
    public void MoveTodoToNextDay(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var t = todoData.items.Find(x => x.id == id);
        if (t == null) return;
        DateTime baseDate;
        if (!DateTime.TryParse(t.dateKey, out baseDate)) baseDate = DateTime.Now.Date;
        t.dateKey = DateKey(baseDate.AddDays(1)); // 翌日
        t.sortOrder = 0;                          // 別の日付塊へ移るので並び順は破棄
        SaveAll();
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

    public List<MemoFolder> GetMemoFolders()
    {
        var list = new List<MemoFolder>(memoNotes.folders);
        list.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return list;
    }

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

    /// <summary>フォルダ名の変更。空名は無視。デフォルトフォルダも改名可（idは固定なので安全）。</summary>
    public void RenameMemoFolder(string folderId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        if (folderId == DefaultMemoFolderId) return; // 既定フォルダはリネーム不可（退避先の器）
        var f = memoNotes.folders.Find(x => x.id == folderId);
        if (f == null) return;
        f.name = newName.Trim();
        SaveAll();
    }

    /// <summary>ノートを別フォルダへ移動。存在しないフォルダ指定はデフォルトへ丸める。</summary>
    public void MoveMemoNoteToFolder(string noteId, string folderId)
    {
        var m = memoNotes.notes.Find(x => x.id == noteId);
        if (m == null) return;
        bool valid = !string.IsNullOrEmpty(folderId) && memoNotes.folders.Exists(f => f.id == folderId);
        m.folderId = valid ? folderId : DefaultMemoFolderId;
        SaveAll();
    }

    /// <summary>ピン留め→作成日降順。folderId=null で全フォルダ。ゴミ箱は除外</summary>
    public List<MemoNote> GetMemoNotes(string folderId = null)
    {
        var list = memoNotes.notes.FindAll(m =>
            !m.IsTrashed && (folderId == null || m.folderId == folderId));
        list.Sort((a, b) => {
            if (a.isPinned != b.isPinned) return a.isPinned ? -1 : 1;
            if (a.sortOrder != b.sortOrder) return a.sortOrder.CompareTo(b.sortOrder);
            return string.Compare(b.createdAt, a.createdAt, StringComparison.Ordinal);
        });
        return list;
    }

    // 手動並べ替え用の連番を一度だけ焼き付ける（既存メモは sortOrder 未設定=全0）。
    private void EnsureMemoNoteSortOrders()
    {
        if (memoNotes.notes.Count == 0) return;
        if (memoNotes.notes.Any(n => n.sortOrder != 0)) return;   // 既に採番済み
        var ordered = new List<MemoNote>(memoNotes.notes);
        ordered.Sort((a, b) => {
            if (a.isPinned != b.isPinned) return a.isPinned ? -1 : 1;
            return string.Compare(b.createdAt, a.createdAt, StringComparison.Ordinal);
        });
        for (int i = 0; i < ordered.Count; i++) ordered[i].sortOrder = i;
        SaveAll();
    }

    /// <summary>2メモの並び順を交換（▲▼）。同値なら現在の表示順を焼き付けてから交換。</summary>
    public void SwapMemoNoteOrder(string idA, string idB)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB) || idA == idB) return;
        var a = memoNotes.notes.Find(m => m.id == idA);
        var b = memoNotes.notes.Find(m => m.id == idB);
        if (a == null || b == null) return;
        if (a.sortOrder == b.sortOrder)
        {
            var ordered = new List<MemoNote>(memoNotes.notes);
            ordered.Sort((x, y) => {
                if (x.isPinned != y.isPinned) return x.isPinned ? -1 : 1;
                return string.Compare(y.createdAt, x.createdAt, StringComparison.Ordinal);
            });
            for (int i = 0; i < ordered.Count; i++) ordered[i].sortOrder = i;
        }
        int tmp = a.sortOrder; a.sortOrder = b.sortOrder; b.sortOrder = tmp;
        SaveAll();
    }

    /// <summary>2フォルダの並び順を交換（▲▼）。同値なら現在の sortOrder 順を焼き付けてから交換。</summary>
    public void SwapMemoFolderOrder(string idA, string idB)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB) || idA == idB) return;
        var a = memoNotes.folders.Find(f => f.id == idA);
        var b = memoNotes.folders.Find(f => f.id == idB);
        if (a == null || b == null) return;
        if (a.sortOrder == b.sortOrder)
        {
            var ordered = new List<MemoFolder>(memoNotes.folders);
            ordered.Sort((x, y) => x.sortOrder.CompareTo(y.sortOrder));
            for (int i = 0; i < ordered.Count; i++) ordered[i].sortOrder = i;
        }
        int tmp = a.sortOrder; a.sortOrder = b.sortOrder; b.sortOrder = tmp;
        SaveAll();
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
        m.sortOrder = memoNotes.notes.Count == 0 ? 0 : memoNotes.notes.Min(x => x.sortOrder) - 1;   // 先頭に積む
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

    /// <summary>ピン留めの掛け外し。updatedAt は更新しない（ピンは内容編集ではないため）。</summary>
    public void SetMemoNotePinned(string id, bool pinned)
    {
        var m = memoNotes.notes.Find(x => x.id == id);
        if (m == null || m.isPinned == pinned) return;
        m.isPinned = pinned;
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

    // 30日更新のないメモを自動でゴミ箱へ（ピン留めは除外）。LoadAll で実行。
    private void AutoTrashStaleMemoNotes()
    {
        if (memoNotes == null || memoNotes.notes == null || memoNotes.notes.Count == 0) return;
        var threshold = DateTime.Now.Date.AddDays(-MEMO_AUTO_TRASH_DAYS);
        int n = 0;
        foreach (var m in memoNotes.notes)
        {
            if (m.IsTrashed || m.isPinned) continue;   // ピン留めは残す意思として除外
            string src = string.IsNullOrEmpty(m.updatedAt) ? m.createdAt : m.updatedAt;
            if (DateTime.TryParseExact(src, "yyyy-MM-dd HH:mm",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt)
                && dt.Date <= threshold)
            {
                m.deletedAt = NowKey();
                n++;
            }
        }
        if (n > 0) { SaveAll(); Debug.Log("[NotebookManager] メモ自動ゴミ箱: " + n + "件（30日更新なし）"); }
    }

    // ゴミ箱で保持日数を超えたメモを完全削除。LoadAll で実行。
    private void PurgeOldTrashedMemoNotes()
    {
        if (memoNotes == null || memoNotes.notes == null || memoNotes.notes.Count == 0) return;
        var threshold = DateTime.Now.Date.AddDays(-MEMO_TRASH_RETENTION_DAYS);
        int before = memoNotes.notes.Count;
        memoNotes.notes.RemoveAll(m =>
        {
            if (!m.IsTrashed) return false;
            return DateTime.TryParseExact(m.deletedAt, "yyyy-MM-dd HH:mm",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt)
                && dt.Date <= threshold;
        });
        int removed = before - memoNotes.notes.Count;
        if (removed > 0) { SaveAll(); Debug.Log("[NotebookManager] メモ完全削除(保持期限切れ): " + removed + "件"); }
    }

    /// <summary>ゴミ箱のメモが完全削除されるまでの残り日数（表示用・0なら期限到達）。</summary>
    public int MemoTrashDaysLeft(MemoNote note)
    {
        if (note == null || !note.IsTrashed) return 0;
        if (!DateTime.TryParseExact(note.deletedAt, "yyyy-MM-dd HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt)) return MEMO_TRASH_RETENTION_DAYS;
        int left = MEMO_TRASH_RETENTION_DAYS - (DateTime.Now.Date - dt.Date).Days;
        return left < 0 ? 0 : left;
    }

}
