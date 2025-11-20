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
    public string checkId = "mic_hit";

    [Header("Condiciones")]
    [Tooltip("Solo ejecutar la prueba cuando el instrumento esté agarrado.")]
    public bool requireInstrumentGrabbed = true;

    [Tooltip("Disparar al seleccionar esta zona con el ray (Select).")]
    public bool useSelect = true;

    [Tooltip("Disparar al golpear físicamente el collider (OnTriggerEnter).")]
    public bool useTriggerEnter = true;

    [Tooltip("Capas que cuentan como golpe válido.")]
    public LayerMask allowedLayers = ~0;

    [Tooltip("Ignorar golpes de la MISMA mano que sostiene el instrumento.")]
    public bool ignoreSameHandAsGrab = true;

    InspectableInstrument instrument;
    XRGrabInteractable grab;
    XRSimpleInteractable interactable;

    void Awake()
    {
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
        if (useSelect && interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelect);
        }

        var col = GetComponent<Collider>();
        if (useTriggerEnter)
        {
            col.isTrigger = true;
        }
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
            Debug.Log($"[InstrumentHitZone:{name}] Ignora Select: instrumento no está agarrado.");
            return;
        }

        string reason = $"Select con {args.interactorObject}";
        Debug.Log($"[InstrumentHitZone:{name}] Test por SELECT. Motivo: {reason}");
        instrument.RunTest(checkId, reason);
    }

    // ---------- GOLPE FÍSICO ----------
    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerEnter || !instrument) return;

        if (((1 << other.gameObject.layer) & allowedLayers) == 0)
            return;

        if (ignoreSameHandAsGrab && grab && grab.isSelected)
        {
            var otherInteractor = other.GetComponentInParent<XRBaseInteractor>();
            if (otherInteractor != null && grab.interactorsSelecting.Contains(otherInteractor))
            {
                Debug.Log($"[InstrumentHitZone:{name}] Ignorado golpe: misma mano que sostiene el instrumento.");
                return;
            }
        }

        if (requireInstrumentGrabbed && grab && !grab.isSelected)
            return;

        string reason = $"OnTriggerEnter con '{other.name}'";
        Debug.Log($"[InstrumentHitZone:{name}] Test por GOLPE. Motivo: {reason}");
        instrument.RunTest(checkId, reason);
    }
}
