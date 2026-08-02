using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 外部アセット（Assets/ithappy 等）に加えた自前の修正が、
/// パッケージ再インポートで消えていないかを検査する。
///
/// 【背景】2026-08-01にアセットを再インポートした際、CharacterMover.cs に入れていた
/// 「キャラの震え」修正が無言で巻き戻り、実機で見るまで気づけなかった。
/// 外部アセットへの修正は消える前提で、機械的に検知できるようにしておく。
///
/// - ビルド前に自動実行し、欠けていればビルドを中止する
/// - メニュー「Tools/TOKYO CORNER/外部アセットの修正を検査」で手動実行も可能
///
/// 修正を増やしたら Patches に1行足すこと。
/// </summary>
public class AssetPatchGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    private struct Patch
    {
        public string File;      // Assets からの相対パス
        public string Marker;    // 修正が入っていれば必ず含まれる文字列
        public string Why;       // 何のための修正か（消えていた時に何を直すか分かるように）
    }

    private static readonly Patch[] Patches =
    {
        new Patch {
            File = "Assets/ithappy/City_Characters/Scripts/Character_Controller/CharacterMover.cs",
            Marker = "Vector2.MoveTowards(m_FlowAxis",
            Why = "キャラが小刻みに震える問題の修正（ブレンド値の振動）。仕様書v6 §14"
        },
        new Patch {
            File = "Assets/ithappy/Creative_Characters/Scripts/Character_Controller/CharacterMover.cs",
            Marker = "Vector2.MoveTowards(m_FlowAxis",
            Why = "同上（Creative_Characters側にも同じバグ）"
        },
        new Patch {
            File = "Assets/ithappy/Cartoon_City/Traffic/Scripts/PrefabSwitch.cs",
            Marker = "#if UNITY_EDITOR",
            Why = "エディタ拡張がランタイムに混ざりビルドが通らないためのガード"
        },
        new Patch {
            File = "Assets/ithappy/Creative_Characters/Scripts/Editor/ithappy.Creative_Characters.Editor.asmdef",
            Marker = "\"Editor\"",
            Why = "Editor専用コードをビルドから除外するasmdef（無いとビルドが通らない）"
        },
        new Patch {
            File = "Assets/ithappy/Cartoon_City/Scripts/Editor/PedestrianTools.cs",
            Marker = "MenuItem",
            Why = "Editorフォルダへ移動済み（元の場所のままだとビルドが通らない）"
        },
    };

    public void OnPreprocessBuild(BuildReport report)
    {
        var missing = Check();
        if (missing.Count == 0) return;

        var msg = "外部アセットへの修正が失われています（再インポートで上書きされた可能性）:\n\n"
                + string.Join("\n", missing)
                + "\n\nビルドを中止しました。修正を再適用してください。";
        throw new BuildFailedException(msg);
    }

    [MenuItem("Tools/TOKYO CORNER/外部アセットの修正を検査")]
    public static void CheckFromMenu()
    {
        var missing = Check();
        if (missing.Count == 0)
            Debug.Log($"[AssetPatchGuard] 全{Patches.Length}件の修正が残っています。");
        else
            Debug.LogError("[AssetPatchGuard] 失われた修正:\n" + string.Join("\n", missing));
    }

    private static List<string> Check()
    {
        var missing = new List<string>();
        foreach (var p in Patches)
        {
            if (!File.Exists(p.File))
            {
                missing.Add($"  × ファイルが無い: {p.File}\n      用途: {p.Why}");
                continue;
            }
            if (!File.ReadAllText(p.File).Contains(p.Marker))
                missing.Add($"  × 修正が消えている: {p.File}\n      目印: \"{p.Marker}\"\n      用途: {p.Why}");
        }
        return missing;
    }
}
