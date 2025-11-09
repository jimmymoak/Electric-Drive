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
            other.transform.SetParent(transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerOnVan = false;
            // Oyuncuyu platformdan ayır
            other.transform.SetParent(null);
        }
    }

    public void Transform()
    {
        
    }
}
