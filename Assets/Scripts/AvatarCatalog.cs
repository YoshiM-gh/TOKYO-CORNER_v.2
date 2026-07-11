using UnityEngine;

/// <summary>
/// アバター選択の台帳（オープニングのカルーセルとCafe反映が共用）。
/// entriesの順がカルーセルの並び。差し替えはこのアセットのプレハブ参照を変えるだけ。
/// </summary>
[CreateAssetMenu(fileName = "AvatarCatalog", menuName = "TOKYO CORNER/Avatar Catalog")]
public class AvatarCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public GameObject prefab; // ithappyキャラプレハブ
        public string label;      // 表示名（おにいさん等・ナギの復唱にも使用）
        public Avatar avatar;     // 体型別Avatar（Adult/Senior/Child…素プレハブはAnimator無しのため必須）
    }

    public Entry[] entries;

    public RuntimeAnimatorController idleController; // プレビュー用（オープニングで棒立ちさせないため）

    public Entry FindByPrefabName(string prefabName)
    {
        if (entries == null) return null;
        foreach (var e in entries)
            if (e != null && e.prefab != null && e.prefab.name == prefabName) return e;
        return null;
    }
}
