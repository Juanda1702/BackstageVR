// InspectableInstrument.cs (reemplaza)
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class InspectableInstrument : MonoBehaviour
{
    [Header("Datos")]
    public InstrumentType type = InstrumentType.Otro;
    public InstrumentState state = InstrumentState.Good;

    public bool Inspected { get; private set; }  // nuevo
    public bool IsApproved => Inspected && state == InstrumentState.Good;

    [Tooltip("Prefab en buen estado para reemplazo automático (opcional).")]
    public GameObject goodReplacementPrefab;

    [Header("UI de inspección")]
    public GameObject inspectionUIPrefab;
    public Transform uiAnchor;
    GameObject uiInstance;

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;

    public System.Action<InspectableInstrument> OnReplaced;
    public System.Action<InspectableInstrument> OnStateChanged;

    void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.selectEntered.AddListener(_ => ShowUI(true));
        grab.selectExited.AddListener(_ => ShowUI(false));
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            // Si en el inspector tienes otros listeners, quítalos a mano; aquí solo quitamos los que añadimos.
            grab.selectEntered.RemoveAllListeners();
            grab.selectExited.RemoveAllListeners();
        }
    }

    void ShowUI(bool on)
    {
        if (on)
        {
            if (!inspectionUIPrefab || uiInstance) return;
            var anchor = uiAnchor ? uiAnchor : transform;
            uiInstance = Instantiate(inspectionUIPrefab, anchor.position, anchor.rotation);
            var follow = uiInstance.GetComponent<InstrumentInspectionPanel>();
            if (follow) follow.Bind(this, anchor);
        }
        else if (uiInstance) Destroy(uiInstance);
    }

    public void MarkDamaged()
    {
        state = InstrumentState.Damaged;
        Inspected = true;
        OnStateChanged?.Invoke(this);
    }

    public void ConfirmGood() // nuevo
    {
        state = InstrumentState.Good;
        Inspected = true;
        OnStateChanged?.Invoke(this);
    }

    public void ReplaceNow()
    {
        GameObject prefab = goodReplacementPrefab ?? ReplacementService.Instance?.GetGoodPrefab(type);
        if (!prefab)
        {
            Debug.LogWarning($"No hay prefab de reemplazo para {type}. Manteniendo el objeto.");
            return;
        }

        var pos = transform.position; var rot = transform.rotation;

        // invocar handlers ANTES de destruir es más seguro
        var replacedHandlers = OnReplaced;

        Destroy(gameObject);
        var newGo = Instantiate(prefab, pos, rot);
        var newIns = newGo.GetComponent<InspectableInstrument>();
        if (newIns) { newIns.state = InstrumentState.Good; newIns.type = type; newIns.ConfirmGood(); }

        replacedHandlers?.Invoke(newIns);
    }
}
