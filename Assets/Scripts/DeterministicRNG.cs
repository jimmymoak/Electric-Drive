using UnityEngine;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System;

public class DeterministicRNG : MonoBehaviour
{
    [Header("Seed Word")]
    [Tooltip("Will be converted to all UPPERCASE")]
    public string seed;

    private System.Random rng;

    void Awake()
    {
        InitializeRNG();
    }

    private void InitializeRNG()
    {
        MD5 MD5 = MD5.Create();
        byte[] hash = MD5.ComputeHash(Encoding.UTF8.GetBytes(seed.ToUpper()));
        int seedAsInt = BitConverter.ToInt32(hash, 0);
        rng = new System.Random(seedAsInt);
    }

    public int GetNextInt(int min = 0, int max = int.MaxValue)
    {
        return rng.Next(min, max);
    }

    public float GetNextFloat(float min = 0f, float max = 1f)
    {
        return (float)(rng.NextDouble() * (max - min) + min);
    }

    public void TestRNG()
    {
        for (int i = 0; i < 10; i++)
        {
            Debug.Log("Random int: " + GetNextInt(0, 10));
        }
    }
}