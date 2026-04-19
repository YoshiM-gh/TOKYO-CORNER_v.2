using UnityEngine;
using UnityEngine.InputSystem;

public class SeatInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 1.5f;
    [SerializeField] private Transform sitPoint;
    [Tooltip("未指定時は椅子の前方（-forward）に離席します。")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private float standOffDistance = 1.1f;

    private Transform player;
    private bool isSeated = false;

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    private void Update()
    {
        if (player == null) return;

        if (!isSeated)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance <= interactRange && Input.GetKeyDown(KeyCode.E))
            {
                SitDown();
            }
        }
        else
        {
            Vector3 targetPos = sitPoint != null ? sitPoint.position : transform.position;
            player.position = targetPos;

            if (WasEscapePressed())
            {
                StandUp();
            }
        }
    }

    private void SitDown()
    {
        isSeated = true;
        Vector3 targetPos = sitPoint != null ? sitPoint.position : transform.position;
        player.position = targetPos;
        GameModeManager.Instance.EnterFocusMode(sitPoint != null ? sitPoint : transform);
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
        GameModeManager.Instance.ExitFocusMode(player);
    }

    private static bool WasEscapePressed()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) return true;
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
