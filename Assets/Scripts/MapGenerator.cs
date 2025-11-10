using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    private DeterministicRNG rng;

    [Header("Map Generation")]
    public int numberOfChunks = 4;
    public int chunkLength = 200;
    public GameObject[] mapChunks;
    void Start()
    {
        rng = GetComponent<DeterministicRNG>();
        GenerateChunks();
    }

    private void GenerateChunks()
    {
        for (int i = 0; i < numberOfChunks; i++)
        {
            GameObject chunk = Instantiate(mapChunks[rng.GetNextInt(0, mapChunks.Length)], transform);
            chunk.transform.position = new Vector3(0, 0, i * chunkLength);
        }
    }
}
