using UnityEngine;
using Unity.Netcode;

public class NetworkBootstrapper : MonoBehaviour
{
    void Awake()
    {
        if (NetworkManager.Singleton != null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}