using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    public float speed = 5f;

    private void Update()
    {
        if (IsOwner) // Only process input for the owning client
        {
            Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            MoveServerRpc(input);
        }
    }

    [ServerRpc]
    void MoveServerRpc(Vector3 direction)
    {
        transform.Translate(direction * (speed * Time.deltaTime));
    }
}
