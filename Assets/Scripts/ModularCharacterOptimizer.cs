using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// モジュラーキャラ（体・顔・髪・服・靴などのパーツを組み合わせるキャラ）の実行時最適化。
///
/// ithappy の City Characters は、パーツ単位のプレハブがそれぞれ Animator と骨格を持っている。
/// 組み合わせたキャラをそのまま動かすと、1体につき次の無駄が出る:
///   A) 骨格を共有しているキャラ（シーン配置のスタッフ）
///      → パーツ側の Animator が同じ骨に同じ結果を書くだけ。Animator が7重に評価される
///   B) パーツごとに骨格を持つキャラ（オープニングで選ぶプレイヤーのアバター）
///      → 44ボーン×6体分の Transform が毎フレーム動く。しかも同期は「全 Animator に
///        同じ再生速度を配る」ことだけで保たれており、ズレると服と体が分離しかねない
///
/// このクラスは A) では余分な Animator を止め、B) ではパーツのメッシュを体の骨格へ
/// 張り替えてから余分な骨格と Animator を破棄する。
///
/// **安全側の設計**: 張り替えはボーン名がすべて一致したパーツにだけ行う。
/// 1つでも解決できない名前があればそのパーツには触れない（見た目が壊れるくらいなら遅いほうがよい）。
/// </summary>
public static class ModularCharacterOptimizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded += (scene, mode) => OptimizeScene();
        OptimizeScene(); // 初回シーンぶん
    }

    private static void OptimizeScene()
    {
        int chars = 0, animatorsOff = 0, skeletonsRemoved = 0;

        foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // 親に Animator がいないもの＝キャラのルートだけを起点にする
            var parent = animator.transform.parent;
            if (parent != null && parent.GetComponentInParent<Animator>() != null) continue;

            var children = animator.GetComponentsInChildren<Animator>(true);
            if (children.Length <= 1) continue;

            int a, s;
            Optimize(animator, out a, out s);
            if (a > 0 || s > 0) chars++;
            animatorsOff += a;
            skeletonsRemoved += s;
        }

        if (chars > 0)
            Debug.Log($"[CharacterOptimizer] {chars}体を最適化: Animator {animatorsOff}個を停止 / 重複骨格 {skeletonsRemoved}個を破棄");
    }

    private static void Optimize(Animator root, out int animatorsOff, out int skeletonsRemoved)
    {
        animatorsOff = 0;
        skeletonsRemoved = 0;

        var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0) return;

        // マスター骨格 = 最もボーン数の多いパーツ（＝体）が参照している骨格
        var master = renderers[0];
        foreach (var r in renderers)
            if (r.bones != null && r.bones.Length > master.bones.Length) master = r;
        if (master.rootBone == null) return;

        // 骨格ツリーの入れ物（例: .../Body_02/Skeleton_Adult）。この配下の名前で引けるようにする
        var masterSkeleton = master.rootBone.parent != null ? master.rootBone.parent : master.rootBone;
        var byName = new Dictionary<string, Transform>();
        foreach (var t in masterSkeleton.GetComponentsInChildren<Transform>(true))
            if (!byName.ContainsKey(t.name)) byName[t.name] = t;

        // --- パーツのメッシュをマスター骨格へ張り替える（B のケース）---
        foreach (var smr in renderers)
        {
            if (smr == master) continue;
            if (smr.rootBone != null && smr.rootBone.IsChildOf(masterSkeleton)) continue; // すでに共有済み

            var bones = smr.bones;
            if (bones == null || bones.Length == 0) continue;

            var rebound = new Transform[bones.Length];
            bool allResolved = true;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) { rebound[i] = null; continue; }
                if (!byName.TryGetValue(bones[i].name, out rebound[i])) { allResolved = false; break; }
            }
            if (!allResolved) continue; // 1つでも一致しなければ触らない

            Transform oldSkeleton = null;
            if (smr.rootBone != null)
            {
                oldSkeleton = smr.rootBone.parent != null ? smr.rootBone.parent : smr.rootBone;
                Transform newRoot;
                if (byName.TryGetValue(smr.rootBone.name, out newRoot)) smr.rootBone = newRoot;
            }

            smr.bones = rebound;

            if (oldSkeleton != null && !oldSkeleton.IsChildOf(masterSkeleton))
            {
                Object.Destroy(oldSkeleton.gameObject);
                skeletonsRemoved++;
            }
        }

        // --- 余分な Animator を止める（A・B 共通）---
        // ルート以外の Animator は、骨格を共有した時点で同じ結果を二重に書くだけになる。
        foreach (var a in root.GetComponentsInChildren<Animator>(true))
        {
            if (a == root || !a.enabled) continue;
            a.enabled = false;
            animatorsOff++;
        }
    }
}
