using UnityEngine;

/// <summary>
/// 固定カメラ環境でプレイヤーを操作するための入力（2026-08-02）。
///
/// 【なぜ標準の MovePlayerInput をそのまま使えないか】
/// 標準版は target を PlayerCamera から取る設計で、カメラが無いと Vector3.zero を渡す。
/// CharacterMover は target を「向いてほしい方向の先の点」として使うため
/// （targetForward = target - 自分の位置）、zero だと常にワールド原点を向いてしまう。
/// このプロジェクトのカフェは固定俯瞰カメラで PlayerCamera を使っていないため、
/// 立ち位置によって前方向が変わるという状態になっていた。
///
/// 【対処】アセットが想定している使い方に合わせる。
/// 同梱の StaffWander は NPC 用に
///     _mover.SetInput(axis, transform.position + _stepDir * 3f, ...)
/// と「進みたい方向の先の点」を target に渡している。プレイヤーでも同じにすればよい。
/// CharacterMover / MovePlayerInput には手を加えないので、再インポートでも壊れない。
///
/// 移動そのものは m_Space = World（シーン側の設定）により画面基準
/// （W=画面上 / D=画面右）で、target は向きだけに使われる。
/// </summary>
[RequireComponent(typeof(Controller.MovePlayerInput))]
public class FixedCameraPlayerInput : MonoBehaviour
{
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";
    [SerializeField] private string jumpButton = "Jump";
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;

    [Tooltip("向き先をどれだけ前に置くか。StaffWanderと同じ3mを既定にしている")]
    [SerializeField] private float lookAhead = 3f;

    private Controller.CharacterMover _mover;
    private Controller.MovePlayerInput _stock;
    private Vector3 _lastDir = Vector3.forward;

    /// <summary>
    /// 参照は毎フレーム検証して取り直す。
    /// 【重要】PlayerAvatarLoader はアバター差し替え時に、AddComponent した直後の
    /// コンポーネントへ旧プレイヤーのフィールド値を機械的にコピーする。
    /// そのため Awake で解決した参照は「破棄される旧オブジェクト」で上書きされ、
    /// Destroy(old) 後に実質nullとなって入力が完全に死ぬ（実際に発生）。
    /// Awakeで解決して持ち回る書き方をしてはいけない。
    /// </summary>
    private bool Resolve()
    {
        if (_mover == null) _mover = GetComponent<Controller.CharacterMover>();
        if (_stock == null) _stock = GetComponent<Controller.MovePlayerInput>();

        // 標準版と二重に入力を渡さないよう、こちらが動く間は止めておく
        if (_stock != null && _stock.enabled) _stock.enabled = false;

        return _mover != null;
    }

    private void Update()
    {
        if (!Resolve()) return;

        var raw = new Vector2(Input.GetAxis(horizontalAxis), Input.GetAxis(verticalAxis));
        bool isRun = Input.GetKey(runKey);
        bool isJump = Input.GetButton(jumpButton);

        // 画面基準の進行方向（W=画面奥/+Z、D=画面右/+X）。止まっている間は直前の向きを保つ
        var dir = new Vector3(raw.x, 0f, raw.y);
        float amount = Mathf.Clamp01(dir.magnitude);
        if (dir.sqrMagnitude > 0.0001f) _lastDir = dir.normalized;

        // 【要点】StaffWander と同じ渡し方にする:
        //   target = 進みたい方向の先の点／axis = 前方向だけ (0, 量)
        // CharacterMover(Space.Self) は target 方向を「前」として移動・回転・アニメ軸を
        // すべて算出するため、移動方向・キャラの向き・歩行アニメの3つが自動的に揃う。
        // axis に生の入力を渡すと「前」の基準と食い違い、
        // 横移動しているのに前歩きのアニメが出る等のズレが起きる。
        var axis = new Vector2(0f, amount);
        var target = transform.position + _lastDir * lookAhead;
        _mover.SetInput(in axis, in target, in isRun, isJump);
    }
}
