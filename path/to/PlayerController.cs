using UnityEngine;
using Photon.Pun;

public class PlayerController : MonoBehaviourPunCallbacks
{
    public float speedThreshold = 0.1f; // Threshold for considering speed as zero
    private Animator animator;
    private Rigidbody rb;
    private float timeSinceSpeedZero;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        if (animator == null || rb == null)
        {
            Debug.LogError("Animator or Rigidbody component is missing.");
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return; // Only run for the local player

        Vector3 velocity = rb.velocity;
        float currentSpeed = new Vector2(velocity.x, velocity.z).magnitude;

        if (currentSpeed <= speedThreshold)
        {
            timeSinceSpeedZero += Time.deltaTime;
        }
        else
        {
            timeSinceSpeedZero = 0f;
        }

        if (timeSinceSpeedZero > 3f && !animator.GetBool("IsLaying"))
        {
            animator.SetBool("IsLaying", true);
        }

        if (currentSpeed > speedThreshold && animator.GetBool("IsLaying"))
        {
            animator.SetBool("IsLaying", false);
        }
    }
}
