using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class InstrumentTestZone : MonoBehaviour
{
    [Tooltip("Debe coincidir con InstrumentCheck.id en el instrumento padre")]
    public string checkId;

    InspectableInstrument instrument;
    XRSimpleInteractable interactable;

    void Awake()
    {
        instrument = GetComponentInParent<InspectableInstrument>();
        interactable = GetComponent<XRSimpleInteractable>();

        if (interactable != null)
            interactable.selectEntered.AddListener(OnTestSelect);
    }

    void OnDestroy()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnTestSelect);
    }

    void OnTestSelect(SelectEnterEventArgs args)
    {
        if (!instrument || string.IsNullOrEmpty(checkId)) return;

        instrument.RunTest(checkId, $"InstrumentTestZone '{name}' selectEntered por {args.interactorObject}");
    }
}
