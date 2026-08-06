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

            // 【注意】プレイヤーも対象に含める。
            // ヒューマノイドのAvatarが動かすのは体の骨格だけで、他パーツは自前の骨格を
            // 持つのに誰も動かさない。張り替えないと服・髪・顔が初期姿勢のまま固まり、
            // 全身がTポーズに見える（実機で確認）。
            // 以前ここでプレイヤーを除外したが、当時の例外の真因は PlayerAvatarLoader の
            // CopyComponentTo がランタイム状態まで上書きしていたことで、本クラスとは無関係だった。

            // 【重要】Animatorの数で足切りしてはいけない。
            // ヒューマノイドのアバターは Animator が1個だけで、Avatarが対応づけているのは
            // **体の骨格のみ**。他パーツは自前の骨格を持つのに誰も動かさないので、
            // 体だけが動いて髪や服が初期姿勢で固まる（実際に発生した）。
            // → 骨格が複数あるなら、Animatorが1個でも張り替えの対象にする。
            var children = animator.GetComponentsInChildren<Animator>(true);
            var skeletons = new HashSet<int>();
            foreach (var smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.rootBone != null) skeletons.Add(smr.rootBone.GetInstanceID());
            if (children.Length <= 1 && skeletons.Count <= 1) continue;

            int a, s;
            Optimize(animator, out a, out s);
            if (a > 0 || s > 0) chars++;
            animatorsOff += a;
            skeletonsRemoved += s;
        }

        if (chars > 0)
            Debug.Log($"[CharacterOptimizer] {chars}体を最適化: Animator {animatorsOff}個を停止 / 重複骨格 {skeletonsRemoved}個を停止");
    }

    private static void Optimize(Animator root, out int animatorsOff, out int skeletonsRemoved)
    {
        animatorsOff = 0;
        skeletonsRemoved = 0;

        var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0) return;

        // マスター骨格の選定。
        // 【重要】ボーン数だけで選んではいけない。モジュラーキャラは全パーツが同じ本数（44）を
        // 持つことがあり、その場合「階層で最初のパーツ」が選ばれてしまう。
        // ルートAnimatorが実際に動かすのは**体**の骨格なので、体以外を基準にすると
        // 髪や服がアニメーションしない骨格に繋がり、体だけが動く状態になる（実際に発生）。
        // → 名前に Body を含むパーツを最優先し、無い場合のみボーン数で決める。
        SkinnedMeshRenderer master = null;
        foreach (var r in renderers)
            if (r.gameObject.name.IndexOf("Body", System.StringComparison.OrdinalIgnoreCase) >= 0) { master = r; break; }
        if (master == null)
        {
            master = renderers[0];
            foreach (var r in renderers)
                if (r.bones != null && r.bones.Length > master.bones.Length) master = r;
        }
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

            // 【重要】旧骨格を破棄してはいけない。
            // CharacterMover は内部（MovementHandler）で骨格配下のTransformを掴んでおり、
            // 破棄すると Update() が毎フレーム NullReferenceException を投げて移動処理が止まる
            // （アニメだけ動いて操作不能になる。Windowsビルドで実際に発生）。
            // メッシュの張り替えだけで描画コストは下がるので、骨格は非アクティブ化に留める。
            if (oldSkeleton != null && !oldSkeleton.IsChildOf(masterSkeleton))
            {
                oldSkeleton.gameObject.SetActive(false);
                skeletonsRemoved++;
            }
        }

        // --- 余分な Animator を止める ---
        // 骨格を共有できたパーツの Animator は、同じ骨に同じ結果を二重に書くだけなので止めてよい。
        // 【重要】張り替えに失敗して自前の骨格が残っているパーツの Animator は止めてはいけない。
        // 止めるとそのパーツだけアニメーションが凍りつく（体だけ動く状態になる）。
        foreach (var a in root.GetComponentsInChildren<Animator>(true))
        {
            if (a == root || !a.enabled) continue;

            bool stillIndependent = false;
            foreach (var smr in a.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.rootBone != null && !smr.rootBone.IsChildOf(masterSkeleton)) stillIndependent = true;
            if (stillIndependent) continue; // 自前の骨格で動いている＝止めない

            a.enabled = false;
            animatorsOff++;
        }
    }
}
