using Controller;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class SeatInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 1.5f;
    [SerializeField] private Transform sitPoint;
    [Tooltip("未指定時は椅子の前方（-forward）に離席します。")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private float standOffDistance = 1.1f;
    [Tooltip("standPoint 未指定時、座る直前の位置側へ離席させる。ソファのような形状で有効。")]
    [SerializeField] private bool useApproachSideForAutoStand = true;
    [Tooltip("離席時の床スナップに使うレイヤー。通常は Default のままでOK。")]
    [SerializeField] private LayerMask standSnapLayerMask = Physics.DefaultRaycastLayers;
    [SerializeField] private bool lockToSitPointRotation = true;

    [Header("Animator (Optional, non-forced)")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private bool useAnimatorSitParameters = false;
    [SerializeField] private string sitBoolParameter = "IsSitting";
    [SerializeField] private string sitBoolParameterAlt = "isSitting";
    [SerializeField] private string sitTriggerParameter = "";
    [SerializeField] private bool forceCrossFadeState = true;
    [SerializeField] private string sitStateName = "Adult_SitTableIdle";
    [SerializeField] private string moveStateName = "Movement";
    [SerializeField] private bool debugLogs = false;

    private Transform player;
    private CharacterMover playerMover;
    private CharacterController playerController;
    private Collider[] seatColliders;
    private SitPoseHotkeyDebug sitDebugBridge;
    private Animator[] playerAnimators;
    private bool isSeated = false;
    private bool warnedPlayerMissing = false;
    private Vector3 lastApproachGroundPos;
    private bool hasAppliedSitAnimatorState = false;
    private bool appliedSitAnimatorState;

    private void Awake()
    {
        AutoAssignPointsIfMissing();
        seatColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        TryResolvePlayer();
    }

    private void Update()
    {
        if (player == null)
        {
            TryResolvePlayer();
            return;
        }

        if (!isSeated)
        {
            Vector3 origin = sitPoint != null ? sitPoint.position : transform.position;
            float distanceXZ = Vector2.Distance(
                new Vector2(origin.x, origin.z),
                new Vector2(player.position.x, player.position.z));

            if (WasInteractPressed() && distanceXZ <= interactRange)
                SitDown();
        }
        else
        {
            Vector3 targetPos = sitPoint != null ? sitPoint.position : transform.position;
            player.position = targetPos;
            if (lockToSitPointRotation)
            {
                Quaternion targetRot = sitPoint != null ? sitPoint.rotation : transform.rotation;
                player.rotation = targetRot;
            }

            if (WasEscapePressed())
                StandUp();
        }
    }

    private void SitDown()
    {
        // Remember where the player came from so auto-stand can return to that side.
        lastApproachGroundPos = player.position;
        lastApproachGroundPos.y = 0f;

        isSeated = true;
        Vector3 targetPos = sitPoint != null ? sitPoint.position : transform.position;
        player.position = targetPos;
        if (lockToSitPointRotation)
        {
            Quaternion targetRot = sitPoint != null ? sitPoint.rotation : transform.rotation;
            player.rotation = targetRot;
        }

        SetSitAnimatorState(true);
        if (sitDebugBridge != null) sitDebugBridge.SetSitState(true);
        if (playerMover != null) playerMover.enabled = false;
        GameModeManager.Instance.EnterFocusMode(sitPoint != null ? sitPoint : transform);
        Debug.Log($"[Seat] Sat down at: {gameObject.name}");
    }

    private void StandUp()
    {
        isSeated = false;
        Vector3 sitWorld = sitPoint != null ? sitPoint.position : transform.position;
        Vector3 standPos = ComputeStandPosition(sitWorld);
        bool restoreCollisionNextFrame = TeleportPlayerSafely(standPos);

        if (sitDebugBridge != null) sitDebugBridge.SetSitState(false);
        if (playerMover != null)
            playerMover.enabled = true;
        SetSitAnimatorState(false);
        GameModeManager.Instance.ExitFocusMode(player);
        if (restoreCollisionNextFrame)
            StartCoroutine(RestoreCollisionsNextFrame());
        Debug.Log($"[Seat] Stood up from: {gameObject.name}");
    }

    private Vector3 ComputeStandPosition(Vector3 sitWorld)
    {
        if (standPoint == null)
            return ComputeAutoStandPosition(sitWorld);

        Vector3 explicitPos = standPoint.position;
        Vector3 delta = explicitPos - sitWorld;
        delta.y = 0f;

        float minDistance = standOffDistance;
        if (TryGetSeatBounds(out Bounds seatBounds))
            minDistance = Mathf.Max(minDistance, Mathf.Max(seatBounds.extents.x, seatBounds.extents.z) + 0.2f);

        if (delta.sqrMagnitude >= minDistance * minDistance)
            return explicitPos;

        Vector3 dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : -transform.forward;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        Vector3 corrected = sitWorld + dir * minDistance;
        corrected.y = explicitPos.y;
        return corrected;
    }

    private Vector3 ComputeAutoStandPosition(Vector3 sitWorld)
    {
        Vector3 seatOrigin = sitWorld;
        seatOrigin.y = 0f;

        Vector3 dir;
        if (useApproachSideForAutoStand)
        {
            dir = lastApproachGroundPos - seatOrigin;
            dir.y = 0f;
        }
        else
        {
            dir = -transform.forward;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude < 0.0001f)
            dir = -transform.forward;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        dir.Normalize();

        float seatRadius = 0.35f;
        if (TryGetSeatBounds(out Bounds seatBounds))
            seatRadius = Mathf.Max(seatBounds.extents.x, seatBounds.extents.z);

        float distance = Mathf.Max(standOffDistance, seatRadius + 0.35f);
        Vector3 standPos = seatOrigin + dir * distance;

        // Snap to floor while ignoring this seat's own colliders.
        if (TrySnapToGround(standPos, out float groundedY))
            standPos.y = groundedY;
        else
            standPos.y = player != null ? player.position.y : 0.525f;

        if (debugLogs)
        {
            Debug.Log($"[Seat] Auto stand computed on '{gameObject.name}' -> xz=({standPos.x:F2},{standPos.z:F2}) y={standPos.y:F2} (sit xz=({seatOrigin.x:F2},{seatOrigin.z:F2}))");
        }

        return standPos;
    }

    private bool TryGetSeatBounds(out Bounds bounds)
    {
        bounds = default;
        if (seatColliders == null || seatColliders.Length == 0)
            return false;

        bool initialized = false;
        for (int i = 0; i < seatColliders.Length; i++)
        {
            Collider c = seatColliders[i];
            if (c == null || !c.enabled || c.isTrigger)
                continue;

            if (!initialized)
            {
                bounds = c.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        return initialized;
    }

    private bool TeleportPlayerSafely(Vector3 worldPos)
    {
        if (player == null)
            return false;

        if (playerController == null || !playerController.enabled)
        {
            player.position = worldPos;
            return false;
        }

        bool restoreCollisionNextFrame = playerController.detectCollisions;
        if (restoreCollisionNextFrame)
            playerController.detectCollisions = false;

        player.position = worldPos;
        return restoreCollisionNextFrame;
    }

    private IEnumerator RestoreCollisionsNextFrame()
    {
        yield return null;
        if (playerController != null && playerController.enabled)
            playerController.detectCollisions = true;
    }

    private bool TrySnapToGround(Vector3 standPos, out float groundedY)
    {
        groundedY = 0f;
        Vector3 rayStart = standPos + Vector3.up * 2f;
        Ray ray = new Ray(rayStart, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 6f, standSnapLayerMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        float bestY = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
            if (hitTransform == null) continue;

            // Ignore this seat's colliders so we don't stand on top of the chair/sofa mesh.
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            float y = hits[i].point.y;
            if (y < bestY)
            {
                bestY = y;
                found = true;
            }
        }

        if (!found)
            return false;

        groundedY = bestY;
        return true;
    }

    private void SetSitAnimatorState(bool sit)
    {
        if (!useAnimatorSitParameters) return;
        if ((playerAnimators == null || playerAnimators.Length == 0) && playerAnimator == null) return;
        if (hasAppliedSitAnimatorState && appliedSitAnimatorState == sit) return;

        ApplyToAnimators(anim =>
        {
            if (!string.IsNullOrWhiteSpace(sitBoolParameter) && HasAnimatorParameter(anim, sitBoolParameter, AnimatorControllerParameterType.Bool))
                anim.SetBool(sitBoolParameter, sit);
            if (!string.IsNullOrWhiteSpace(sitBoolParameterAlt) && HasAnimatorParameter(anim, sitBoolParameterAlt, AnimatorControllerParameterType.Bool))
                anim.SetBool(sitBoolParameterAlt, sit);

            if (sit && !string.IsNullOrWhiteSpace(sitTriggerParameter) && HasAnimatorParameter(anim, sitTriggerParameter, AnimatorControllerParameterType.Trigger))
                anim.SetTrigger(sitTriggerParameter);

            if (forceCrossFadeState)
            {
                string targetState = sit ? sitStateName : moveStateName;
                if (!string.IsNullOrWhiteSpace(targetState) && anim.HasState(0, Animator.StringToHash(targetState)))
                    anim.CrossFadeInFixedTime(targetState, 0.05f, 0, 0f);
            }
        });

        if (debugLogs)
            Debug.Log($"[Seat] Animator sit={sit} bool='{sitBoolParameter}' altBool='{sitBoolParameterAlt}' trigger='{sitTriggerParameter}'");

        appliedSitAnimatorState = sit;
        hasAppliedSitAnimatorState = true;
    }

    private bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType type)
    {
        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == type)
                return true;
        }
        return false;
    }

    private void ApplyToAnimators(System.Action<Animator> action)
    {
        if (playerAnimators != null && playerAnimators.Length > 0)
        {
            for (int i = 0; i < playerAnimators.Length; i++)
            {
                if (playerAnimators[i] != null) action(playerAnimators[i]);
            }
            return;
        }

        if (playerAnimator != null)
            action(playerAnimator);
    }

    private static bool WasEscapePressed()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) return true;
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private static bool WasInteractPressed()
    {
        if (Input.GetKeyDown(KeyCode.E)) return true;
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private void TryResolvePlayer()
    {
        GameObject playerObj = null;

        MovePlayerInput moverInput = Object.FindAnyObjectByType<MovePlayerInput>();
        if (moverInput != null) playerObj = moverInput.gameObject;

        if (playerObj == null)
        {
            GameObject tagged = GameObject.FindWithTag("Player");
            if (tagged != null)
            {
                MovePlayerInput taggedMover = tagged.GetComponentInChildren<MovePlayerInput>();
                playerObj = taggedMover != null ? taggedMover.gameObject : tagged;
            }
        }

        if (playerObj == null) playerObj = GameObject.Find("23_Businessman");
        if (playerObj == null) playerObj = GameObject.Find("Player");

        if (playerObj != null)
        {
            player = playerObj.transform;
            playerMover = playerObj.GetComponent<CharacterMover>();
            playerController = playerObj.GetComponent<CharacterController>();
            sitDebugBridge = playerObj.GetComponent<SitPoseHotkeyDebug>();
            playerAnimators = playerObj.GetComponentsInChildren<Animator>(true);
            warnedPlayerMissing = false;
            if (playerAnimator == null)
                playerAnimator = player.GetComponentInChildren<Animator>();

            if (debugLogs)
                Debug.Log($"[Seat] Bound player '{player.name}' on seat '{gameObject.name}'");
            return;
        }

        if (!warnedPlayerMissing)
        {
            Debug.LogWarning("[Seat] Player not found. Set a GameObject tag to 'Player'.");
            warnedPlayerMissing = true;
        }
    }

    private void AutoAssignPointsIfMissing()
    {
        if (sitPoint == null)
        {
            Transform autoSit = FindNamedChild("SitPoint");
            if (autoSit != null)
                sitPoint = autoSit;
        }

        if (standPoint == null)
        {
            Transform autoStand = FindNamedChild("StandPoint", "Standpoint", "Stand Point");
            if (autoStand != null)
            {
                standPoint = autoStand;
            }
        }
    }

    private Transform FindNamedChild(params string[] names)
    {
        if (names == null || names.Length == 0) return null;
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            for (int n = 0; n < names.Length; n++)
            {
                if (candidate.name == names[n])
                    return candidate;
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
