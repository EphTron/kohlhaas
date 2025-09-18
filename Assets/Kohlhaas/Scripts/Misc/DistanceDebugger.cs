using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DistancePair
{
    public string name;
    public Transform pointA;
    public Transform pointB;
    public float distance;

    public DistancePair(Transform a, Transform b)
    {
        pointA = a;
        pointB = b;
        distance = Vector3.Distance(a.position, b.position);
    }
}

public class DistanceDebugger : MonoBehaviour
{
    public List<DistancePair> distancePairs = new List<DistancePair>();

    public float debugCooldown = 1f;
    private float debugTimer = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (debugTimer > debugCooldown)
        {
            debugTimer = 0f;
            foreach (var pair in distancePairs)
            {
                pair.distance = Vector3.Distance(pair.pointA.position, pair.pointB.position);
                Debug.Log($"Distance between {pair.name}: {pair.distance}");
            }
        }
        else
        {
            debugTimer += Time.deltaTime;
        }
        
    }
}
