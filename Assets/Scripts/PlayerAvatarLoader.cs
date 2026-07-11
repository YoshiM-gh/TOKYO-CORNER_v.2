using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Cafe起動時、オープニングで選んだアバターへプレイヤーの体を差し替える。
/// 方式: 選択プレハブをInstantiate → 旧23_Businessmanから機能コンポーネントを移植 →
/// 旧体を破棄 → 新体を"23_Businessman"へリネーム（既存の名前依存を全て生かす）。
/// savedata.jsonを直読みするためSaveDataManagerの初期化順に依存しない。
/// </summary>
[DefaultExecutionOrder(-500)]
public class PlayerAvatarLoader : MonoBehaviour
{
    [SerializeField] private AvatarCatalog catalog;

    private void Awake()
    {
        if (catalog == null) return;
        string path = Path.Combine(Application.persistentDataPath, "savedata.json");
        if (!File.Exists(path)) return;
        SaveData data = null;
        try { data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
        catch { return; }
        if (data == null || string.IsNullOrEmpty(data.avatarId)) return;

        var entry = catalog.FindByPrefabName(data.avatarId);
        if (entry == null || entry.prefab == null) return;
        if (entry.prefab.name == "23_Businessman") return; // 既定の体なら差し替え不要

        var old = GameObject.Find("23_Businessman");
        if (old == null) return;

        var neo = Instantiate(entry.prefab, old.transform.position, old.transform.rotation);
        neo.tag = old.tag;

        // Animator: 移動コントローラを【先に】設定する。
        // CharacterMover.Awake(AddComponent時に即実行)が BuildAnimatorTargets で
        // 親Animatorのcontrollerを全子パーツへ配布するため、後から設定すると全身Tポーズになる。
        var oldAn = old.GetComponent<Animator>();
        var neoAn = neo.GetComponent<Animator>();
        if (neoAn == null) neoAn = neo.AddComponent<Animator>(); // 素プレハブはAnimator無し（Transformのみ）
        if (oldAn != null) neoAn.runtimeAnimatorController = oldAn.runtimeAnimatorController;
        if (entry.avatar != null) neoAn.avatar = entry.avatar; // 体型別Avatar（Adult/Senior/Child）

        // 機能コンポーネントの移植（順序: CC → Mover → Input。Input内のMover参照は新体側へ付け替わる）
        CopyComponentTo(old, neo, typeof(CharacterController));
        CopyComponentTo(old, neo, System.Type.GetType("Controller.CharacterMover, Assembly-CSharp"));
        CopyComponentTo(old, neo, System.Type.GetType("Controller.MovePlayerInput, Assembly-CSharp"));
        CopyComponentTo(old, neo, System.Type.GetType("SitPoseHotkeyDebug, Assembly-CSharp"));

        // カメラの追従先を差し替え（ithappy公式API）
        foreach (var cam in FindObjectsByType<Controller.PlayerCamera>(FindObjectsSortMode.None))
            if (cam.Player == old.transform) cam.BindPlayer(neo.transform);

        // シーン内の旧体参照（CameraFollow.target・URP VolumeTrigger等）を新体へ付け替え
        ReplaceSceneReferences(old, neo);

        // Destroyは遅延実行のため、同フレーム内の他スクリプト(Interactable等)のStart時Findが
        // 旧体を掴まないよう即座に隠す（destroyed参照は==null扱いでクリック判定が静かに死ぬ）
        old.name = "OldPlayerBody(Destroying)";
        old.SetActive(false);
        Destroy(old);
        neo.name = "23_Businessman";
        Debug.Log("[AvatarLoader] player avatar -> " + entry.prefab.name + " (" + entry.label + ")");
    }

    /// <summary>シーン内MonoBehaviourの旧体参照（Transform/GameObject/Component）を新体へ一括付け替え。
    /// カメラ追従target等の[SerializeField]直参照がDestroyでぶら下がるのを防ぐ。</summary>
    private static void ReplaceSceneReferences(GameObject old, GameObject neo)
    {
        var oldT = old.transform;
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.gameObject == old || mb.transform.IsChildOf(oldT)) continue;
            var type = mb.GetType();
            foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.IsNotSerialized) continue;
                if (f.FieldType == typeof(Transform))
                {
                    if (ReferenceEquals(f.GetValue(mb) as Transform, oldT) || (Transform)f.GetValue(mb) == oldT)
                        f.SetValue(mb, neo.transform);
                }
                else if (f.FieldType == typeof(GameObject))
                {
                    if ((GameObject)f.GetValue(mb) == old) f.SetValue(mb, neo);
                }
                else if (typeof(Component).IsAssignableFrom(f.FieldType))
                {
                    var c = f.GetValue(mb) as Component;
                    if (c != null && c.gameObject == old)
                    {
                        var repl = neo.GetComponent(f.FieldType);
                        if (repl != null) f.SetValue(mb, repl);
                    }
                }
            }
        }
    }

    /// <summary>コンポーネント移植（値コピー・旧体内の参照は新体側の同型へ差し替え）</summary>
    private static void CopyComponentTo(GameObject src, GameObject dst, System.Type type)
    {
        if (type == null) return;
        var s = src.GetComponent(type);
        if (s == null) return;
        var d = dst.GetComponent(type);
        if (d == null) d = dst.AddComponent(type);

        // CharacterControllerはプロパティベース（フィールド走査では写らない）
        if (s is CharacterController scc && d is CharacterController dcc)
        {
            dcc.height = scc.height; dcc.radius = scc.radius; dcc.center = scc.center;
            dcc.slopeLimit = scc.slopeLimit; dcc.stepOffset = scc.stepOffset;
            dcc.skinWidth = scc.skinWidth; dcc.minMoveDistance = scc.minMoveDistance;
            dcc.enabled = scc.enabled;
            return;
        }

        foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.IsNotSerialized) continue;
            object v = f.GetValue(s);
            // 未割当のObject参照はコピーしない（AddComponent時のAwakeによる自己解決を尊重）
            // ※旧体のm_Mover等はシーン上null→実行時自己解決の設計。nullで上書きすると新体のAwake解決を壊す
            if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
            {
                var uo = v as UnityEngine.Object;
                if (uo == null) continue; // 真null/破棄済みの両方をスキップ
            }
            if (v is Component c && c != null && c.gameObject == src)
            {
                var repl = dst.GetComponent(c.GetType());
                v = repl != null ? (object)repl : null;
            }
            else if (v is GameObject go && go == src) v = dst;
            try { f.SetValue(d, v); } catch { }
        }
        if (s is Behaviour sb && d is Behaviour db) db.enabled = sb.enabled;
    }
}
