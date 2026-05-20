using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────
// Monthly
// ─────────────────────────────────────────
[Serializable]
public class MonthlyEntry
{
    public string date;          // "yyyy-MM-dd"
    public string text;          // その日に書き留めたこと
    public int colorMark;        // 0=なし 1=赤 2=青 3=緑 4=黄
}

[Serializable]
public class MonthlyData
{
    public List<MonthlyEntry> entries = new List<MonthlyEntry>();
}

// ─────────────────────────────────────────
// Weekly
// ─────────────────────────────────────────
[Serializable]
public class WeeklyEntry
{
    public string weekKey;       // "2026-W21"
    public int dayOfWeek;        // 0=日 〜 6=土
    public string note;
}

[Serializable]
public class WeeklyData
{
    public List<WeeklyEntry> entries = new List<WeeklyEntry>();
}

// ─────────────────────────────────────────
// Daily
// ─────────────────────────────────────────
[Serializable]
public class DailyScheduleBlock
{
    public int hour;             // 0〜23
    public string text;
}

[Serializable]
public class DailyEntry
{
    public string date;          // "yyyy-MM-dd"
    public List<DailyScheduleBlock> schedule = new List<DailyScheduleBlock>();
    public string freeMemo;
}

[Serializable]
public class DailyData
{
    public List<DailyEntry> entries = new List<DailyEntry>();
}

// ─────────────────────────────────────────
// Todo
// ─────────────────────────────────────────
[Serializable]
public class TodoItem
{
    public string id;
    public string text;
    public bool isCompleted;
    public string createdAt;     // "yyyy-MM-dd"
}

[Serializable]
public class TodoData
{
    public List<TodoItem> items = new List<TodoItem>();
}

// ─────────────────────────────────────────
// Memo
// ─────────────────────────────────────────
[Serializable]
public class MemoEntry
{
    public string id;
    public string title;
    public string body;
    public string updatedAt;     // "yyyy-MM-dd HH:mm"
}

[Serializable]
public class MemoData
{
    public List<MemoEntry> entries = new List<MemoEntry>();
}
