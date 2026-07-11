using UnityEngine;
using Controller;

/// <summary>
/// スタッフの「一歩あるいて、しばらく佇む」DQ風の徘徊（カウンター内モード）。
/// カウンターに沿った左右(X軸)のみ一歩ずつ動き、佇み中は客側(faceDir)へ向き直る。
/// 時間で1歩を打ち切るため家具に引っかかっても歩き続けない。会話・メニュー中は停止。
/// </summary>
[RequireComponent(typeof(CharacterMover))]
public class StaffWander : MonoBehaviour
{
    [SerializeField] private float wanderRadius = 1.4f;   // ホームからの許容距離（左右）
    [SerializeField] private float inputScale = 0.45f;    // 歩きの強さ（ゆったり）
    [SerializeField] private float stepDuration = 0.55f;  // 1歩ぶんの歩行時間（秒）
    [SerializeField] private float idleMin = 3f;          // 佇む時間（秒）
    [SerializeField] private float idleMax = 8f;
    [SerializeField] private Vector3 faceDir = new Vector3(0f, 0f, -1f); // 佇み中に向く方向（客側）
    [SerializeField] private float faceTurnSpeed = 4f;    // 向き直りの速さ

    private CharacterMover _mover;
    private Vector3 _home;
    private Vector3 _stepDir;
    private float _timer;
    private bool _stepping;

    private void Awake() { _mover = GetComponent<CharacterMover>(); }

    private void Start()
    {
        _home = transform.position;
        _timer = Random.Range(0.5f, idleMax); // 起動直後にみんな同時に歩き出さないよう初回はバラす
        _stepping = false;
    }

    private void Update()
    {
        bool paused = (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen)
                   || (MenuShopUI.Instance != null && MenuShopUI.Instance.IsOpen);
        if (paused) { if (_stepping) EndStep(); FaceCustomers(); return; }

        _timer -= Time.deltaTime;
        if (_stepping)
        {
            _mover.SetInput(new Vector2(0f, inputScale), transform.position + _stepDir * 3f, false, false);
            if (_timer <= 0f) EndStep();
        }
        else
        {
            FaceCustomers(); // 佇み中は客側へ向き直る（背中を見せない）
            if (_timer <= 0f) BeginStep();
        }
    }

    private void BeginStep()
    {
        // カウンター沿い（左右）のみ。ホームから離れすぎたら戻る向きに。
        float dx = transform.position.x - _home.x;
        float dir = (Mathf.Abs(dx) > wanderRadius) ? -Mathf.Sign(dx) : (Random.value < 0.5f ? -1f : 1f);
        _stepDir = new Vector3(dir, 0f, 0f);
        _stepping = true;
        _timer = stepDuration;
    }

    private void EndStep()
    {
        _mover.SetInput(Vector2.zero, transform.position + transform.forward, false, false);
        _stepping = false;
        _timer = Random.Range(idleMin, idleMax);
    }

    private void FaceCustomers()
    {
        if (faceDir.sqrMagnitude < 0.001f) return;
        var target = Quaternion.LookRotation(faceDir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, faceTurnSpeed * Time.deltaTime);
    }
}
