using UnityEngine;
using System.Collections;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -6f);
    [SerializeField] private float smoothSpeed = 20f;
    [SerializeField] private float focusEnterDuration = 1.2f;
    [SerializeField] private float focusExitDuration = 0.8f;

    private bool lookAtTarget = false;
    private Vector3 lookAtOffset = Vector3.zero;
    private Quaternion roamingRotation;
    private Transform focusTransform = null;
    private bool isTransitioning = false;

    private void Start()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        roamingRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (isTransitioning) return;
        if (target == null) return;
        if (focusTransform != null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        if (lookAtTarget)
        {
            Vector3 lookAtPoint = target.position + lookAtOffset;
            Quaternion desiredRotation = Quaternion.LookRotation(lookAtPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, roamingRotation, smoothSpeed * Time.deltaTime);
        }
    }

    public void SetFocusTransform(Transform t)
    {
        focusTransform = t;
        StopAllCoroutines();
        StartCoroutine(TransitionToFocus(t.position, t.rotation, focusEnterDuration));
    }

    public void ClearFocusTransform()
    {
        focusTransform = null;
        StopAllCoroutines();
        StartCoroutine(TransitionToRoaming(focusExitDuration));
    }

    // フォーカス地点へスムーズに移動
    private IEnumerator TransitionToFocus(Vector3 toPos, Quaternion toRot, float duration)
    {
        isTransitioning = true;
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t); // smoothstep

            transform.position = Vector3.Lerp(fromPos, toPos, eased);
            transform.rotation = Quaternion.Slerp(fromRot, toRot, eased);
            yield return null;
        }

        transform.position = toPos;
        transform.rotation = toRot;
        isTransitioning = false;
    }

    // ローミング位置へ戻る（プレイヤーが動いても追従）
    private IEnumerator TransitionToRoaming(float duration)
    {
        isTransitioning = true;
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            Vector3 toPos = target != null ? target.position + offset : fromPos;
            transform.position = Vector3.Lerp(fromPos, toPos, eased);
            transform.rotation = Quaternion.Slerp(fromRot, roamingRotation, eased);
            yield return null;
        }

        isTransitioning = false;
    }

    public Vector3 GetOffset() => offset;
    public void SetOffset(Vector3 newOffset) => offset = newOffset;
    public void SetTarget(Transform newTarget) => target = newTarget;
    public void SetLookAt(bool enabled) => lookAtTarget = enabled;
    public void SetLookAtOffset(Vector3 o) => lookAtOffset = o;
}
