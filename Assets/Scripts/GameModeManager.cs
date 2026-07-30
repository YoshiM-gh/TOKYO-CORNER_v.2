using UnityEngine;

/// <summary>
/// ゲームモード（散策 / フォーカス）の参照点。
///
/// 2026-07-27（Phase 5）: 旧アーキのフォーカス処理（EnterFocusMode / ExitFocusMode）を撤去した。
/// フォーカスは Cafe 内のオーバーレイではなく別シーン(UI_Prototype)へ切り替える方式になり、
/// これらのメソッドはどこからも呼ばれない死んだコードになっていた。
/// その結果 CurrentMode が永久に Roaming のままで、SaveDataManager の集中時間が
/// すべて散策時間に計上されていた（同コミットで修正）。
///
/// 現在の CurrentMode は**アクティブなシーン**から導出する。
/// 参照元: DrinkHudUI / HudStatsUI / StampCardUI / SaveDataManager
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }

    public enum GameMode { Roaming, Focus }

    /// <summary>フォーカス画面に居るなら Focus。このコンポーネントはCafeの住人なので、
    /// フォーカス中は Instance 自体が null になる点に注意（判定は SceneRouter.IsFocusScene が本体）。</summary>
    public GameMode CurrentMode => SceneRouter.IsFocusScene ? GameMode.Focus : GameMode.Roaming;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
