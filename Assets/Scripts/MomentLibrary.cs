using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public class MomentTemplate { public string speakerId = "owner"; public string type = "line"; public string body; }
[System.Serializable] public class MomentPoolData { public List<MomentTemplate> moments = new List<MomentTemplate>(); }

/// <summary>
/// キャラカードの『今日の一言』プール。Resources/moments.json を読み、日付シードで1件を決定的に選ぶ。
/// セリフ本文の編集は moments.json 側で行う（コード変更不要）。
/// </summary>
public static class MomentLibrary
{
    static List<MomentTemplate> _pool;

    static void EnsureLoaded()
    {
        if (_pool != null) return;
        _pool = new List<MomentTemplate>();
        var ta = Resources.Load<TextAsset>("moments");
        if (ta != null)
        {
            try { var d = JsonUtility.FromJson<MomentPoolData>(ta.text); if (d != null && d.moments != null) _pool = d.moments; }
            catch { }
        }
        if (_pool.Count == 0)
            _pool.Add(new MomentTemplate { speakerId = "owner", type = "line", body = "今日もここにいる。" });
    }

    /// <summary>日付キー(yyyy-MM-dd)から決定的に1件選ぶ（同じ日は同じ結果）。</summary>
    public static MomentTemplate PickForDate(string dateKey)
    {
        EnsureLoaded();
        if (_pool.Count == 0) return null;
        int seed = 0;
        if (dateKey != null) foreach (char c in dateKey) seed = seed * 31 + c;
        int idx = ((seed % _pool.Count) + _pool.Count) % _pool.Count;
        return _pool[idx];
    }

    /// <summary>プールからランダムに1件。excludeBody と同じ本文は（可能なら）避ける。セッション内ローテ用。</summary>
    public static MomentTemplate PickRandom(string excludeBody = null)
    {
        EnsureLoaded();
        if (_pool.Count == 0) return null;
        if (_pool.Count == 1) return _pool[0];
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var c = _pool[Random.Range(0, _pool.Count)];
            if (string.IsNullOrEmpty(excludeBody) || c.body != excludeBody) return c;
        }
        return _pool[Random.Range(0, _pool.Count)];
    }

    public static int PoolCount { get { EnsureLoaded(); return _pool.Count; } }
}
