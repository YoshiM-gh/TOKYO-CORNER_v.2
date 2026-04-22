using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private bool movementEnabled = true;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!movementEnabled)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(h, 0f, v).normalized;
        rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        if (animator != null)
            animator.SetFloat("Speed", direction.magnitude);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        Debug.Log($"[Move] Movement {(enabled ? "enabled" : "disabled")}.");
    }
}
