using UnityEngine;

public class SeatInteractable : MonoBehaviour
{
    [SerializeField] private float interactRange = 1.5f;
    [SerializeField] private Transform sitPoint;

    private Transform player;
    private bool isPlayerNear = false;

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    private void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        isPlayerNear = distance <= interactRange;

        if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            SitDown();
        }
    }

    private void SitDown()
    {
        Vector3 targetPos = sitPoint != null ? sitPoint.position : transform.position;
        player.position = targetPos;
        Debug.Log("着席しました");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
