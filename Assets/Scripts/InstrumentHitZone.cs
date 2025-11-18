using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class InstrumentHitZone : MonoBehaviour
{
    [Tooltip("Debe coincidir con InstrumentCheck.id en el InspectableInstrument padre")]
    public string checkId;

    [Header("Condiciones")]
    // ⬇ IMPORTANTE:
    // Por defecto se puede probar sin agarrar; si lo activas en el inspector,
    // exige que el instrumento esté agarrado para disparar la prueba.
    public bool requireInstrumentGrabbed = false;

    public bool useSelect = true;                  // disparar al seleccionar con el ray
    public bool useTriggerEnter = true;            // disparar al golpear físicamente
    public LayerMask collisionLayers = ~0;         // capas que cuentan como golpe

    InspectableInstrument instrument;
    XRGrabInteractable grab;
    XRSimpleInteractable interactable;
    Collider hitCollider;

    void Awake()
    {
        hitCollider = GetComponent<Collider>();
        if (!hitCollider.isTrigger)
            hitCollider.isTrigger = true;          // esta zona se usa como trigger

        instrument = GetComponentInParent<InspectableInstrument>();
        if (!instrument)
        {
            Debug.LogWarning($"[InstrumentHitZone:{name}] No se encontró InspectableInstrument en padres.");
        }
        else
        {
            grab = instrument.GetComponent<XRGrabInteractable>();
        }

        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
        }
    }

    void OnDestroy()
    {
        if (interactable)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
        }
    }

    bool LayerAllowed(GameObject go)
    {
        return (collisionLayers.value & (1 << go.layer)) != 0;
    }

    // ---------- SELECT CON RAY ----------
    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (!useSelect || !instrument)
            return;

        // Si se exige que esté agarrado y no lo está, no disparar
        if (requireInstrumentGrabbed && (!grab || !grab.isSelected))
        {
            Debug.Log($"[InstrumentHitZone:{name}] Ignorado select: instrumento no está agarrado.");
            return;
        }

        // Ignorar si es la misma mano que está sosteniendo el instrumento
        if (grab != null && grab.isSelected)
        {
            var interactor = args.interactorObject as IXRSelectInteractor;
            if (interactor != null && grab.interactorsSelecting.Contains(interactor))
            {
                Debug.Log($"[InstrumentHitZone:{name}] Ignorado select: misma mano que sostiene el instrumento.");
                return;
            }
        }

        string reason = $"selectEntered por {args.interactorObject}";
        Debug.Log($"[InstrumentHitZone:{name}] Test por SELECT. Motivo: {reason}");
        instrument.RunTest(checkId, reason);
    }

    // ---------- GOLPE FÍSICO ----------
    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerEnter || !instrument)
            return;

        if (!LayerAllowed(other.gameObject))
            return;

        // Opcional: ignorar colisión con el propio instrumento
        var otherInstrument = other.GetComponentInParent<InspectableInstrument>();
        if (otherInstrument == instrument)
            return;

        // Ignorar si es la misma mano que está agarrando el instrumento
        if (grab != null && grab.isSelected)
        {
            var otherInteractor = other.GetComponentInParent<IXRSelectInteractor>();
            if (otherInteractor != null && grab.interactorsSelecting.Contains(otherInteractor))
            {
                Debug.Log($"[InstrumentHitZone:{name}] Ignorado golpe: misma mano que sostiene el instrumento.");
                return;
            }
        }

        // Si se exige que esté agarrado y no lo está, no disparar
        if (requireInstrumentGrabbed && grab && !grab.isSelected)
            return;

        string reason = $"OnTriggerEnter con '{other.name}'";
        Debug.Log($"[InstrumentHitZone:{name}] Test por GOLPE. Motivo: {reason}");
        instrument.RunTest(checkId, reason);
    }
}
