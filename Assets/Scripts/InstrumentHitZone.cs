using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class InstrumentHitZone : MonoBehaviour
{
    [Tooltip("Debe coincidir con InstrumentCheck.id en el InspectableInstrument padre")]
    public string checkId = "mic_hit";

    [Header("Condiciones")]
    public bool requireInstrumentGrabbed = true;   // solo cuando el instrumento est� agarrado
    public bool useSelect = true;                  // disparar al seleccionar con el ray
    public bool useTriggerEnter = true;            // disparar al golpear f�sicamente
    public LayerMask collisionLayers = ~0;         // capas que cuentan como golpe

    InspectableInstrument instrument;
    XRGrabInteractable grab;
    XRSimpleInteractable interactable;

    void Awake()
    {
        instrument = GetComponentInParent<InspectableInstrument>();
        if (!instrument)
        {
            Debug.LogWarning($"[InstrumentHitZone:{name}] No se encontr� InspectableInstrument en padres.");
            return;
        }

        grab = instrument.GetComponent<XRGrabInteractable>();
        interactable = GetComponent<XRSimpleInteractable>();

        if (interactable && useSelect)
        {
            interactable.selectEntered.AddListener(OnSelect);
        }

        // El collider debe ser trigger para OnTriggerEnter
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnDestroy()
    {
        if (interactable && useSelect)
            interactable.selectEntered.RemoveListener(OnSelect);
    }

    // ---------- SELECT CON RAY ----------
    void OnSelect(SelectEnterEventArgs args)
    {
        if (!instrument) return;

        if (requireInstrumentGrabbed && grab && !grab.isSelected)
        {
            Debug.Log($"[InstrumentHitZone:{name}] Ignorado select: instrumento no est� agarrado.");
            return;
        }

        // Ignorar si es la misma mano que est� agarrando el instrumento
        var interactor = args.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
        if (grab && grab.isSelected && interactor != null &&
            grab.interactorsSelecting.Contains(interactor))
        {
            Debug.Log($"[InstrumentHitZone:{name}] Ignorado select: misma mano que sostiene el instrumento.");
            return;
        }

        string reason = $"selectEntered por {args.interactorObject}";
        Debug.Log($"[InstrumentHitZone:{name}] Test por SELECT. Motivo: {reason}");
        instrument.RunTest(checkId, reason);
    }

    // ---------- GOLPE F�SICO ----------
    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerEnter || !instrument) return;

        // capas
        if (((1 << other.gameObject.layer) & collisionLayers) == 0)
            return;

        // ignorar colisi�n con el propio instrumento
        var otherInst = other.GetComponentInParent<InspectableInstrument>();
        if (otherInst == instrument) return;

        // ignorar si el collider viene de la misma mano que sostiene el instrumento
        var otherInteractor = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>();
        if (grab && grab.isSelected && otherInteractor != null &&
            grab.interactorsSelecting.Contains(otherInteractor))
        {
            Debug.Log($"[InstrumentHitZone:{name}] Ignorado golpe: misma mano que sostiene el instrumento.");
            return;
        }

        if (requireInstrumentGrabbed && grab && !grab.isSelected)
            return;

        string reason = $"OnTriggerEnter con '{other.name}'";
        Debug.Log($"[InstrumentHitZone:{name}] Test por GOLPE. Motivo: {reason}");
        instrument.RunTest(checkId, reason);
    }
}
