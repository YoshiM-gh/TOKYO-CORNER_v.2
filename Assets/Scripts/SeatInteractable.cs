using UnityEngine;

public class SeatInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 1.5f;
    [SerializeField] private Transform sitPoint;

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

            if (Input.GetKeyDown(KeyCode.Escape))
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
        Vector3 standPos = player.position;
        standPos.y = 0.525f;
        player.position = standPos;
        GameModeManager.Instance.ExitFocusMode(player);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
