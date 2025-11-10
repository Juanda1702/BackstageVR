using UnityEngine;
using UnityEngine.AI;

public class CrowdAgent : MonoBehaviour
{
    public Transform idleTarget;
    public float wanderRadius = 8f;
    public float decideEvery = 5f;
    public LayerMask stageAreaMask; // collider del área de público/escenario

    NavMeshAgent nav;
    float t;

    void Awake() { nav = GetComponent<NavMeshAgent>(); }

    public void GoWander()
    {
        SetRandomNear(transform.position, wanderRadius);
        t = 0f;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t > decideEvery) { t = 0; SetRandomNear(transform.position, wanderRadius); }
    }

    void SetRandomNear(Vector3 center, float radius)
    {
        Vector3 rnd = center + Random.insideUnitSphere * radius;
        rnd.y = center.y;
        if (NavMesh.SamplePosition(rnd, out var hit, 3f, NavMesh.AllAreas)) nav.SetDestination(hit.position);
    }

    public void Warn()
    {
        if (idleTarget)
        {
            nav.SetDestination(idleTarget.position);
        }
    }
}
