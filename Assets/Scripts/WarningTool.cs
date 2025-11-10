using UnityEngine;
using UnityEngine.InputSystem;

public class WarningTool : MonoBehaviour
{
    [Header("Input de XRI/Player Input")]
    public InputActionProperty triggerAction; // asigna el gatillo del controlador

    public float range = 15f;
    public LayerMask hitMask = ~0; // por defecto todo

    void OnEnable() { triggerAction.action.Enable(); }
    void OnDisable() { triggerAction.action.Disable(); }

    void Update()
    {
        if (triggerAction.action.WasPressedThisFrame())
        {
            if (Physics.Raycast(transform.position, transform.forward, out var hit, range, hitMask))
            {
                var agent = hit.collider.GetComponentInParent<CrowdAgent>();
                if (agent) agent.Warn();
            }
        }
    }
}
