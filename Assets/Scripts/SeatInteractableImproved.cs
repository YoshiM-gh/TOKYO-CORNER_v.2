using Controller;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 改善版 SeatInteractable (Fixed版)
/// CharacterController を一時的に無効化してテレポート
/// interactRange を 0.9f に調整（椅子を近づけて配置しやすく）
/// </summary>
public class SeatInteractableImproved : MonoBehaviour
{
    [SerializeField] private float interactRange = 0.9f;  // ✅ 0.9f に変更（より小さい判定範囲）
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
    [SerializeField] private bool debugLogs = true;

    private Transform player;
    private CharacterMover playerMover;
    private CharacterController playerController;
    private Collider[] seatColliders;
    private HashSet<Collider> seatCollidersSet;
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
        seatCollidersSet = new HashSet<Collider>(seatColliders);
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
        Vector3 validatedStandPos = ValidateStandPosition(standPos, sitWorld);
        
        if (debugLogs)
        {
            Debug.Log($"[Seat] StandUp: '{gameObject.name}'");
            Debug.Log($"  sitWorld: ({sitWorld.x:F2}, {sitWorld.y:F2}, {sitWorld.z:F2})");
            Debug.Log($"  computedPos: ({standPos.x:F2}, {standPos.y:F2}, {standPos.z:F2})");
            Debug.Log($"  validatedPos: ({validatedStandPos.x:F2}, {validatedStandPos.y:F2}, {validatedStandPos.z:F2})");
            if (standPoint != null)
                Debug.Log($"  standPoint (assigned): ({standPoint.position.x:F2}, {standPoint.position.y:F2}, {standPoint.position.z:F2})");
            else
                Debug.Log($"  standPoint: NOT ASSIGNED (using auto-stand)");
        }
        
        // ✅ CRITICAL FIX: CharacterController を一時的に完全に無効化してテレポート
        bool ccWasEnabled = false;
        if (playerController != null && playerController.enabled)
        {
            ccWasEnabled = true;
            playerController.enabled = false;
            if (debugLogs)
                Debug.Log($"[Seat] CharacterController disabled for teleport");
        }
        
        // プレイヤーを確実に移動
        player.position = validatedStandPos;
        if (debugLogs)
            Debug.Log($"[Seat] Teleported player to: ({validatedStandPos.x:F2}, {validatedStandPos.y:F2}, {validatedStandPos.z:F2})");
        
        // CharacterController を再度有効化
        if (ccWasEnabled)
        {
            playerController.enabled = true;
            if (debugLogs)
                Debug.Log($"[Seat] CharacterController re-enabled");
        }

        if (sitDebugBridge != null) sitDebugBridge.SetSitState(false);
        SetSitAnimatorState(false);
        
        if (playerMover != null)
            playerMover.enabled = true;
        
        GameModeManager.Instance.ExitFocusMode(player);
        
        Debug.Log($"[Seat] Stood up from: {gameObject.name}");
    }

    private Vector3 ComputeStandPosition(Vector3 sitWorld)
    {
        if (standPoint != null)
        {
            Vector3 explicitPos = standPoint.position;
            
            if (!IsHeightAboveGround(explicitPos))
            {
                if (debugLogs)
                    Debug.LogWarning($"[Seat] StandPoint Y ({explicitPos.y:F2}) is too low, attempting correction");
                
                if (TrySnapToGround(explicitPos, out float safeY))
                {
                    explicitPos.y = safeY;
                    if (debugLogs)
                        Debug.Log($"[Seat] Corrected standPoint.y to ground: {safeY:F2}");
                }
                else
                {
                    explicitPos.y = sitWorld.y + 1.7f;
                    if (debugLogs)
                        Debug.Log($"[Seat] Set standPoint.y to sit.y + 1.7m: {explicitPos.y:F2}");
                }
            }
            
            Vector3 deltaXZ = new Vector3(explicitPos.x - sitWorld.x, 0, explicitPos.z - sitWorld.z);
            float distXZ = deltaXZ.magnitude;
            
            if (debugLogs)
                Debug.Log($"[Seat] StandPoint XZ distance: {distXZ:F2} (min required: {standOffDistance * 0.5f:F2})");
            
            if (distXZ < standOffDistance * 0.5f)
            {
                if (debugLogs)
                    Debug.LogWarning($"[Seat] StandPoint too close, pushing away...");
                
                Vector3 dir = distXZ > 0.0001f ? deltaXZ.normalized : -transform.forward;
                Vector3 corrected = sitWorld + dir * standOffDistance;
                corrected.y = explicitPos.y;
                return corrected;
            }
            
            return explicitPos;
        }

        return ComputeAutoStandPosition(sitWorld);
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

        if (TrySnapToGround(standPos, out float groundedY))
            standPos.y = groundedY;
        else
            standPos.y = player != null ? player.position.y : 0.525f;

        if (debugLogs)
        {
            Debug.Log($"[Seat] Auto stand computed on '{gameObject.name}'");
            Debug.Log($"  result xz=({standPos.x:F2},{standPos.z:F2}) y={standPos.y:F2}");
            Debug.Log($"  sit xz=({seatOrigin.x:F2},{seatOrigin.z:F2})");
        }

        return standPos;
    }

    private Vector3 ValidateStandPosition(Vector3 standPos, Vector3 sitWorld)
    {
        Vector2 deltaXZ = new Vector2(standPos.x - sitWorld.x, standPos.z - sitWorld.z);
        float distXZ = deltaXZ.magnitude;
        
        if (distXZ < standOffDistance * 0.4f)
        {
            Debug.LogWarning($"[Seat] Stand Position XZ too close to sit! Distance: {distXZ:F2}");
            Vector2 dir = distXZ > 0.01f ? deltaXZ.normalized : new Vector2(-transform.forward.x, -transform.forward.z);
            if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(1, 0);
            
            Vector3 corrected = sitWorld + new Vector3(dir.x, 0, dir.y) * (standOffDistance * 1.2f);
            corrected.y = standPos.y;
            
            if (debugLogs)
                Debug.Log($"[Seat] Corrected XZ distance to {(corrected - sitWorld).magnitude:F2}");
            
            return corrected;
        }
        
        if (TryGetSeatBounds(out Bounds seatBounds))
        {
            float seatTopY = seatBounds.max.y;
            if (standPos.y < seatTopY + 0.3f)
            {
                Debug.LogWarning($"[Seat] Stand Position Y ({standPos.y:F2}) below seat top ({seatTopY:F2})!");
                standPos.y = seatTopY + 1.7f;
                
                if (debugLogs)
                    Debug.Log($"[Seat] Corrected Y to {standPos.y:F2}");
            }
        }
        
        return standPos;
    }

    private bool IsHeightAboveGround(Vector3 pos)
    {
        if (!TrySnapToGround(pos, out float groundY))
            return true;
        
        return pos.y >= groundY + 0.1f;
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
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;

            if (seatCollidersSet.Contains(hitCollider))
            {
                if (debugLogs)
                    Debug.Log($"[Seat] Raycast hit ignored (seat collider): {hitCollider.gameObject.name}");
                continue;
            }

            float y = hits[i].point.y;
            if (y < bestY)
            {
                bestY = y;
                found = true;
                if (debugLogs)
                    Debug.Log($"[Seat] Raycast ground hit: {hitCollider.gameObject.name} at y={y:F2}");
            }
        }

        if (!found)
        {
            if (debugLogs)
                Debug.Log($"[Seat] No ground found in raycast");
            return false;
        }

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
            Debug.Log($"[Seat] Animator sit={sit}");

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
