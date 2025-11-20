using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Collider))]
public class InstrumentCollisionTestZone : MonoBehaviour
{
    [Tooltip("Debe coincidir con InstrumentCheck.id en el InspectableInstrument padre")]
    public string checkId = "mic_hit";

    [Header("Cuándo disparar")]
    [Tooltip("Si el collider es IsTrigger, usar OnTriggerEnter.")]
    public bool useTriggerEnter = true;

    [Tooltip("Si el collider no es trigger, usar OnCollisionEnter.")]
    public bool useCollisionEnter = false;

    [Tooltip("Capas que cuentan como golpe válido.")]
    public LayerMask collisionLayers = ~0;

    [Tooltip("Solo cuando el instrumento esté agarrado.")]
    public bool onlyWhenInstrumentGrabbed = true;

    [Tooltip("Ignorar golpes de la MISMA mano que sostiene el instrumento.")]
    public bool ignoreSameHandAsGrab = true;

    InspectableInstrument instrument;
    XRGrabInteractable grab;

    void Awake()
    {
        instrument = GetComponentInParent<InspectableInstrument>();
        if (!instrument)
        {
            Debug.LogWarning($"[InstrumentCollisionTestZone:{name}] No se encontró InspectableInstrument en padres.");
        }
        else
        {
            grab = instrument.GetComponent<XRGrabInteractable>();
        }

        var col = GetComponent<Collider>();
        // No forzamos isTrigger aquí porque depende de si quieres trigger o colisión real
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerEnter || !instrument) return;

        if (((1 << other.gameObject.layer) & collisionLayers) == 0)
            return;

        HandleHit("TriggerEnter", other.gameObject, other);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!useCollisionEnter || !instrument) return;

        if (((1 << collision.gameObject.layer) & collisionLayers) == 0)
            return;

        HandleHit("CollisionEnter", collision.gameObject, null);
    }

    void HandleHit(string source, GameObject other, Collider otherCollider)
    {
        if (!instrument) return;

        // Opcional: solo cuando el instrumento está agarrado
        if (onlyWhenInstrumentGrabbed && grab && !grab.isSelected)
            return;

        // Ignorar colisión con el propio instrumento
        var otherInstrument = other.GetComponentInParent<InspectableInstrument>();
        if (otherInstrument == instrument) return;

        // Ignorar golpes de la misma mano que sostiene el instrumento
        if (ignoreSameHandAsGrab && grab && grab.isSelected)
        {
            XRBaseInteractor otherInteractor = null;

            if (otherCollider != null)
                otherInteractor = otherCollider.GetComponentInParent<XRBaseInteractor>();
            if (!otherInteractor)
                otherInteractor = other.GetComponentInParent<XRBaseInteractor>();

            if (otherInteractor != null && grab.interactorsSelecting.Contains(otherInteractor))
            {
                Debug.Log($"[InstrumentCollisionTestZone:{name}] Ignorado golpe ({source}): misma mano que sostiene el instrumento.");
                return;
            }
        }

        // Ejecutar prueba
        string reason = $"{source} en '{name}' con '{other.name}'";
        Debug.Log($"[InstrumentCollisionTestZone:{name}] Disparando prueba '{checkId}'. {reason}");
        instrument.RunTest(checkId, reason);
    }
}
