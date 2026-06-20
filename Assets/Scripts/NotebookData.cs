using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────
// ScheduleEvent
// 月・週・日・管理タブが共有する予定データ
// タグは TagConfig.cs で定義された id を参照
// ─────────────────────────────────────────────────────────
[Serializable]
public class ScheduleEvent
{
    public string id;           // GUID
    public string tagId;        // TagConfig の id（"habit","yotei","mokuhyo","todo"）
    public string title;
    public string date;         // "yyyy-MM-dd"（null可＝日付なし）
    public string time;         // "HH:mm"（null可＝時間未設定）
    public string endTime;      // "HH:mm"（null可）
    public string memo;
    public bool isCompleted;
    public string completedAt;  // "yyyy-MM-dd HH:mm"（完了した日時）
    public string createdAt;    // "yyyy-MM-dd HH:mm"
}

[Serializable]
public class ScheduleData
{
    public List<ScheduleEvent> events = new List<ScheduleEvent>();
}

// ─────────────────────────────────────────────────────────
// MemoEntry
// メモタブ専用。日付に紐づかない独立ノート
// ─────────────────────────────────────────────────────────
[Serializable]
public class MemoEntry
{
    public string id;
    public string title;
    public string body;
    public string updatedAt;    // "yyyy-MM-dd HH:mm"
    public string createdAt;    // "yyyy-MM-dd HH:mm"
}

[Serializable]
public class MemoData
{
    public List<MemoEntry> entries = new List<MemoEntry>();
}

// ─────────────────────────────────────────────────────────
// LifetimeStats
// 完了数などの生涯カウントデータ（永久保存）
// ─────────────────────────────────────────────────────────
[Serializable]
public class TagCompletionCount
{
    public string tagId;
    public int count;
}

[Serializable]
public class DailyCompletionRecord
{
    public string date;         // "yyyy-MM-dd"
    public int count;
}

[Serializable]
public class LifetimeStats
{
    public int totalCompleted;
    public List<TagCompletionCount> completedByTag = new List<TagCompletionCount>();
    public List<DailyCompletionRecord> dailyRecords = new List<DailyCompletionRecord>();
    public int longestStreak;
    public int currentStreak;
}

// ─────────────────────────────────────────────────────────
// WeeklyMemoEntry / WeeklyMemoData
// Weeklyタブ専用のメモ（週単位・先頭日キーで管理）
// ─────────────────────────────────────────────────────────
[Serializable]
public class WeeklyMemoEntry
{
    public string weekKey;   // "yyyy-MM-dd"（その週の先頭日）
    public string text;
}

[Serializable]
public class WeeklyMemoData
{
    public List<WeeklyMemoEntry> entries = new List<WeeklyMemoEntry>();
}

// ─────────────────────────────────────────────────────────
// StickyNoteData / StickyNotesData
// Dailyタブ付箋データ（日付ごとに管理）
// ─────────────────────────────────────────────────────────
[Serializable]
public class StickyNoteData
{
    public string id;           // GUID
    public string dateKey;      // "yyyy-MM-dd"
    public string content;
    public float  anchorX;      // canvas 内正規化位置 X (0-1, 左=0)
    public float  anchorY;      // canvas 内正規化位置 Y (0-1, 上=0)
    public float  width  = 200f;
    public float  height = 200f;
    public int    colorIndex;
    public string tagId;        // "habit","yotei","mokuhyo","todo"
}

[Serializable]
public class StickyNotesData
{
    public List<StickyNoteData> notes = new List<StickyNoteData>();
}

// ─────────────────────────────────────────────────────────
// TodoItem / TodoListData
// Todo タブ専用。タスクの羅列（フォルダなし・一覧管理）
// ─────────────────────────────────────────────────────────
[Serializable]
public class TodoItem
{
    public string id;            // GUID
    public string title;
    public string memo;
    public string dateKey;       // "yyyy-MM-dd"（null可＝日付なし。設定時はカレンダーにアイコン表示）
    public string time;          // "HH:mm"（null可）
    public bool   priorityHigh;  // 高=true / 普通=false
    public bool   isCompleted;
    public string completedAt;   // "yyyy-MM-dd HH:mm"
    public string createdAt;
    public int    sortOrder;
}

[Serializable]
public class TodoListData
{
    public List<TodoItem> items = new List<TodoItem>();
}

// ─────────────────────────────────────────────────────────
// RoutineItem / RoutineListData
// Routine タブ専用。繰り返し設定つきタスク。
// 完了は「日付ごとの出現（occurrence）」単位で completedDates に記録する
// ─────────────────────────────────────────────────────────
[Serializable]
public class RoutineItem
{
    public string id;            // GUID
    public string title;
    public string memo;
    public string time;          // "HH:mm"（null可）
    public bool   priorityHigh;

    // ── 繰り返しルール（MVP: daily / weekly / interval）──
    public string repeatType = "daily";   // "daily" | "weekly" | "interval"
    public List<int> weekdays = new List<int>(); // weekly: 0=日 〜 6=土
    public int    intervalDays = 1;          // interval: N日ごと
    public string startDate;     // "yyyy-MM-dd" 繰り返し起点（interval計算・表示開始）
    public string endDate;       // "yyyy-MM-dd"（null可＝終了なし）

    public List<string> completedDates = new List<string>(); // 完了した dateKey の集合
    public string createdAt;

    /// <summary>この Routine が指定日に出現するか（カレンダー表示・一覧展開用）</summary>
    public bool OccursOn(DateTime day)
    {
        day = day.Date;
        if (!TryParseKey(startDate, out var start)) start = day; // 起点不明なら当日扱い
        if (day < start) return false;
        if (TryParseKey(endDate, out var end) && day > end) return false;

        switch (repeatType)
        {
            case "daily":    return true;
            case "weekly":   return weekdays != null && weekdays.Contains((int)day.DayOfWeek);
            case "interval": return intervalDays > 0 && ((day - start).Days % intervalDays) == 0;
            default:           return false;
        }
    }

    public bool IsDoneOn(string dateKey) =>
        completedDates != null && completedDates.Contains(dateKey);

    private static bool TryParseKey(string key, out DateTime dt)
    {
        dt = default;
        return !string.IsNullOrEmpty(key) &&
               DateTime.TryParseExact(key, "yyyy-MM-dd",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out dt);
    }
}

[Serializable]
public class RoutineListData
{
    public List<RoutineItem> items = new List<RoutineItem>();
}

// ─────────────────────────────────────────────────────────
// MemoFolder / MemoNote / MemoNotesData
// 新メモ仕様（プレーンテキスト本文・フラット1段フォルダ・ピン留め・ゴミ箱）
// ※ 旧 MemoEntry/MemoData は Memo タブ実装時に移行のうえ撤去予定
// ─────────────────────────────────────────────────────────
[Serializable]
public class MemoFolder
{
    public string id;        // GUID。デフォルトフォルダは固定 id "default"
    public string name;
    public int    sortOrder;
}

[Serializable]
public class MemoNote
{
    public string id;          // GUID
    public string folderId;    // MemoFolder.id
    public string title;
    public string body = "";          // プレーンテキスト本文（M-1：ブロック型を廃止）
    public string dateKey;     // "yyyy-MM-dd"（null可＝カレンダー紐づけなし）
    public string createdAt;   // "yyyy-MM-dd HH:mm"
    public string updatedAt;
    public bool   isPinned;    // 上位表示固定
    public int    sortOrder;   // 手動並べ替え順（小さいほど上・ピングループ内）
    public string deletedAt;   // null=通常 / 値あり=ゴミ箱（"yyyy-MM-dd HH:mm"）

    public bool IsTrashed => !string.IsNullOrEmpty(deletedAt);
}

[Serializable]
public class MemoNotesData
{
    public List<MemoFolder> folders = new List<MemoFolder>();
    public List<MemoNote>   notes   = new List<MemoNote>();
}
