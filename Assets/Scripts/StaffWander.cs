using UnityEngine;
using Controller;

/// <summary>
/// スタッフの「数歩ぶんウロウロ」。初期位置を中心に小半径で目標点を選び、
/// CharacterMover(SetInput)でゆっくり歩く（歩行アニメはMover側が駆動）。
/// 会話UI・メニュー表示中は所作として立ち止まる。
/// 必要: CharacterController + CharacterMover + プレイヤーと同じ移動Animatorコントローラ。
/// </summary>
[RequireComponent(typeof(CharacterMover))]
public class StaffWander : MonoBehaviour
{
    [SerializeField] private float wanderRadius = 1.4f; // 初期位置からの徘徊半径
    [SerializeField] private float inputScale = 0.45f;  // 歩きの強さ（ゆったり）
    [SerializeField] private float waitMin = 2.5f;      // 目標到着後の待機（秒）
    [SerializeField] private float waitMax = 7f;
    [SerializeField] private float arriveDist = 0.25f;

    private CharacterMover _mover;
    private Vector3 _home;
    private Vector3 _goal;
    private float _waitUntil;
    private bool _walking;

    private void Awake() { _mover = GetComponent<CharacterMover>(); }

    private void Start()
    {
        _home = transform.position;
        PickNextGoal();
    }

    private void Update()
    {
        // 会話中・メニュー中は立ち止まる（接客の所作）
        bool paused = (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen)
                   || (MenuShopUI.Instance != null && MenuShopUI.Instance.IsOpen);
        if (paused) { StopWalk(); return; }

        if (!_walking)
        {
            if (Time.time < _waitUntil) return;
            _walking = true;
        }

        Vector3 to = _goal - transform.position;
        to.y = 0f;
        if (to.magnitude <= arriveDist) { StopWalk(); PickNextGoal(); return; }

        // targetを進行方向の先に置き、axis=(0, scale)で「その方向へ前進」を伝える
        Vector3 dir = to.normalized;
        _mover.SetInput(new Vector2(0f, inputScale), transform.position + dir * 3f, false, false);
    }

    private void StopWalk()
    {
        if (_mover != null) _mover.SetInput(Vector2.zero, transform.position + transform.forward, false, false);
        _walking = false;
    }

    private void PickNextGoal()
    {
        var r = Random.insideUnitCircle * wanderRadius;
        _goal = _home + new Vector3(r.x, 0f, r.y);
        _waitUntil = Time.time + Random.Range(waitMin, waitMax);
    }
}
