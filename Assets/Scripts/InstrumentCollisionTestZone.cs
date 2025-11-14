using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class InstrumentCollisionTestZone : MonoBehaviour
{
    [Tooltip("Debe coincidir con InstrumentCheck.id en el InspectableInstrument padre")]
    public string checkId = "mic_hit";

    [Header("Cuándo disparar")]
    public bool useTriggerEnter = true;   // si el collider es IsTrigger
    public bool useCollisionEnter = false; // si el collider NO es trigger
    public LayerMask collisionLayers = ~0; // capas que cuentan como golpe
    public bool onlyWhenInstrumentGrabbed = true; // solo cuando el instrumento esté agarrado

    InspectableInstrument instrument;
    XRGrabInteractable grab;

    void Awake()
    {
        instrument = GetComponentInParent<InspectableInstrument>();
        if (instrument)
            grab = instrument.GetComponent<XRGrabInteractable>();
    }

    // -------- TRIGGER ----------
    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerEnter) return;
        TryRunTestFromCollider(other.gameObject, "OnTriggerEnter");
    }

    // -------- COLLISION ----------
    void OnCollisionEnter(Collision collision)
    {
        if (!useCollisionEnter) return;
        TryRunTestFromCollider(collision.collider.gameObject, "OnCollisionEnter");
    }

    void TryRunTestFromCollider(GameObject other, string source)
    {
        if (!instrument || string.IsNullOrEmpty(checkId)) return;

        // Comprobar capas
        if (((1 << other.layer) & collisionLayers) == 0)
            return;

        // Opcional: solo cuando el instrumento está agarrado
        if (onlyWhenInstrumentGrabbed && grab && !grab.isSelected)
            return;

        // Ignorar colisión con el propio instrumento
        var otherInstrument = other.GetComponentInParent<InspectableInstrument>();
        if (otherInstrument == instrument) return;

        // Ejecutar prueba
        string reason = $"{source} en '{name}' con '{other.name}'";
        Debug.Log($"[InstrumentCollisionTestZone:{name}] Disparando prueba '{checkId}'. {reason}");
        instrument.RunTest(checkId, reason);
    }
}
