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
