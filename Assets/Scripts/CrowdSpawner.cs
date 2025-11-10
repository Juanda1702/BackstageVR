using UnityEngine;
using UnityEngine.AI;

public class CrowdSpawner : MonoBehaviour
{
    public GameObject npcPrefab;       // requiere NavMeshAgent + CrowdAgent
    public Transform[] spawnPoints;
    public Transform idleArea;         // punto para “volver” cuando se les advierte
    public int initialCount = 6;

    bool running = false;

    public void StartSimulation()
    {
        if (running) return;
        running = true;
        for (int i = 0; i < initialCount; i++) SpawnOne();
    }

    void SpawnOne()
    {
        if (spawnPoints.Length == 0 || !npcPrefab) return;
        var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var go = Instantiate(npcPrefab, p.position, p.rotation);
        var agent = go.GetComponent<CrowdAgent>();
        agent.idleTarget = idleArea;
        agent.GoWander();
    }
}
