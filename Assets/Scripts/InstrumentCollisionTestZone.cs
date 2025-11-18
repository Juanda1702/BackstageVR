using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class InstrumentCollisionTestZone : MonoBehaviour
{
    [Tooltip("Debe coincidir con InstrumentCheck.id en el InspectableInstrument padre")]
    public string checkId = "mic_hit";

    [Header("Cuándo disparar")]
    public bool useTriggerEnter = true;     // si el collider es IsTrigger
    public bool useCollisionEnter = false;  // si el collider NO es trigger
    public LayerMask collisionLayers = ~0;  // capas que cuentan como golpe

    // ? IMPORTANTE:
    // Por defecto se puede probar sin agarrar; si lo activas en el inspector,
    // exige que el instrumento esté agarrado para disparar la prueba.
    public bool onlyWhenInstrumentGrabbed = false;

    InspectableInstrument instrument;
    XRGrabInteractable grab;
    Collider zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<Collider>();

        instrument = GetComponentInParent<InspectableInstrument>();
        if (!instrument)
        {
            Debug.LogWarning($"[InstrumentCollisionTestZone:{name}] No se encontró InspectableInstrument en padres.");
        }
        else
        {
            grab = instrument.GetComponent<XRGrabInteractable>();
        }
    }

    bool LayerAllowed(GameObject go)
    {
        return (collisionLayers.value & (1 << go.layer)) != 0;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerEnter || !instrument || !zoneCollider.isTrigger)
            return;

        HandleCollision(other, "OnTriggerEnter");
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!useCollisionEnter || !instrument || zoneCollider.isTrigger)
            return;

        HandleCollision(collision.collider, "OnCollisionEnter");
    }

    void HandleCollision(Collider other, string source)
    {
        if (!LayerAllowed(other.gameObject))
            return;

        // Opcional: solo cuando el instrumento está agarrado
        if (onlyWhenInstrumentGrabbed && grab && !grab.isSelected)
            return;

        // Ignorar colisión con el propio instrumento
        var otherInstrument = other.GetComponentInParent<InspectableInstrument>();
        if (otherInstrument == instrument)
            return;

        // Ejecutar prueba
        string reason = $"{source} en '{name}' con '{other.name}'";
        Debug.Log($"[InstrumentCollisionTestZone:{name}] Disparando prueba '{checkId}'. {reason}");
        instrument.RunTest(checkId, reason);
    }
}
