using UnityEngine;

public class VanPlatform : MonoBehaviour
{
    [Header("Player Control")]
    public bool isPlayerOnVan = false;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerOnVan = true;
            // No SetParent - PlayerPlatformMotion handles motion via CharacterController.Move()
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerOnVan = false;
            // No SetParent removal needed - motion handled by PlayerPlatformMotion
        }
    }

    public void Transform()
    {
        
    }
}
