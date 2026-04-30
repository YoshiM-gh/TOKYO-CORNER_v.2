using Controller;
using UnityEngine;
using UnityEngine.InputSystem;

public class SeatInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 1.5f;
    [SerializeField] private Transform sitPoint;
    [Tooltip("未指定時は椅子の前方（-forward）に離席します。")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private float standOffDistance = 1.1f;
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
    private SitPoseHotkeyDebug sitDebugBridge;
    private bool isSeated = false;
    private bool warnedPlayerMissing = false;

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

            // Keep sit state asserted while seated.
            SetSitAnimatorState(true);

            if (WasEscapePressed())
                StandUp();
        }
    }

    private void SitDown()
    {
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
        Vector3 standPos;
        if (standPoint != null)
            standPos = standPoint.position;
        else
        {
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;
            else
                flatForward.Normalize();
            standPos = sitWorld - flatForward * standOffDistance;
            standPos.y = 0.525f;
        }
        player.position = standPos;

        if (sitDebugBridge != null) sitDebugBridge.SetSitState(false);
        if (playerMover != null) playerMover.enabled = true;
        SetSitAnimatorState(false);
        GameModeManager.Instance.ExitFocusMode(player);
        Debug.Log($"[Seat] Stood up from: {gameObject.name}");
    }

    private void SetSitAnimatorState(bool sit)
    {
        if (!useAnimatorSitParameters) return;
        if (playerAnimator == null) return;

        if (!string.IsNullOrWhiteSpace(sitBoolParameter) && HasAnimatorParameter(sitBoolParameter, AnimatorControllerParameterType.Bool))
            playerAnimator.SetBool(sitBoolParameter, sit);
        if (!string.IsNullOrWhiteSpace(sitBoolParameterAlt) && HasAnimatorParameter(sitBoolParameterAlt, AnimatorControllerParameterType.Bool))
            playerAnimator.SetBool(sitBoolParameterAlt, sit);

        if (sit && !string.IsNullOrWhiteSpace(sitTriggerParameter) && HasAnimatorParameter(sitTriggerParameter, AnimatorControllerParameterType.Trigger))
            playerAnimator.SetTrigger(sitTriggerParameter);

        if (forceCrossFadeState)
        {
            string targetState = sit ? sitStateName : moveStateName;
            if (!string.IsNullOrWhiteSpace(targetState) && playerAnimator.HasState(0, Animator.StringToHash(targetState)))
                playerAnimator.CrossFadeInFixedTime(targetState, 0.05f, 0, 0f);
        }

        if (debugLogs)
            Debug.Log($"[Seat] Animator sit={sit} bool='{sitBoolParameter}' altBool='{sitBoolParameterAlt}' trigger='{sitTriggerParameter}'");
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
    {
        var parameters = playerAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == type)
                return true;
        }
        return false;
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
            sitDebugBridge = playerObj.GetComponent<SitPoseHotkeyDebug>();
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
