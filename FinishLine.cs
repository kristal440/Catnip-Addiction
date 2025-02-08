using UnityEngine;

public class FinishLine : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player has touched the finish line!");
            // Add any additional logic here, like ending the game or triggering an event.
        }
    }
}
