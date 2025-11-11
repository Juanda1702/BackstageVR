using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;                 // SelectEnter/ExitEventArgs
using UnityEngine.XR.Interaction.Toolkit.Interactables;   // XRGrabInteractable (XRI 3)

// Enums en tu InstrumentDefs.cs:
// public enum InstrumentType { ... }
// public enum ReportedState { Unknown, Good, ReportedDamaged }
// public enum ActualCondition { Good, Defective }

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class InspectableInstrument : MonoBehaviour
{
    [Header("Datos")]
    public InstrumentType type = InstrumentType.Otro;

    // Condición real (oculta al jugador)
    [SerializeField] ActualCondition actualCondition = ActualCondition.Good;
    [Range(0, 1)] public float defectiveProbability = 0.25f;
    public bool randomizeOnStart = false;

    // Estado declarado por el jugador (UI / lógica)
    [SerializeField] ReportedState reported = ReportedState.Unknown;
    public ReportedState Reported => reported;
    public bool Inspected => reported != ReportedState.Unknown;
    public bool IsApproved => reported == ReportedState.Good;

    [Header("Reemplazo")]
    public GameObject goodReplacementPrefab;  // si es null, usa ReplacementService
    public Transform fallbackParent;          // arrastra aquí el GO "Instrumentos"
    public LayerMask groundMask = ~0;         // capas de Suelo/Escenario
    public float dropClearance = 0.01f;       // separación mínima desde el piso

    [Header("UI de inspección")]
    public GameObject inspectionUIPrefab;
    public Transform uiAnchor;                // punto de referencia en el instrumento
    GameObject uiInstance;

    XRGrabInteractable grab;

    // Eventos
    public System.Action<InspectableInstrument> OnReplaced;
    public System.Action<InspectableInstrument> OnStateChanged;

    // ---------------- LIFECYCLE ----------------
    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void Start()
    {
        if (randomizeOnStart)
            actualCondition = (Random.value < defectiveProbability)
                ? ActualCondition.Defective : ActualCondition.Good;

        reported = ReportedState.Unknown;
        OnStateChanged?.Invoke(this);
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
        }
    }

    // ---------------- GRAB EVENTS ----------------
    void OnSelectEntered(SelectEnterEventArgs args)
    {
        ShowUI(true, (args.interactorObject as Component)?.transform);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        ShowUI(false, null);
    }

    void ShowUI(bool on, Transform interactorTf = null)
    {
        if (on)
        {
            if (!inspectionUIPrefab || uiInstance) return;

            uiInstance = Instantiate(inspectionUIPrefab);
            var panel = uiInstance.GetComponent<InstrumentInspectionPanel>();
            var anchor = uiAnchor ? uiAnchor : transform;

            // Bind(inst, anchor, interactor) → panel se coloca frente a la cámara
            if (panel != null) panel.Bind(this, anchor, interactorTf);
        }
        else if (uiInstance) Destroy(uiInstance);
    }

    // ---------------- ACCIONES UI ----------------
    public void ReportDamaged()
    {
        reported = ReportedState.ReportedDamaged;
        OnStateChanged?.Invoke(this);
        // No iluminamos socket todavía; la guía aparece al reemplazar o si aprueba.
    }

    public void ConfirmGood()
    {
        reported = ReportedState.Good;
        OnStateChanged?.Invoke(this);

        // Enciende la guía del socket correspondiente
        InstrumentSnapTarget.HighlightFor(type, true);
    }

    public bool IsActuallyDefective() => actualCondition == ActualCondition.Defective;

    public void ReplaceNow()
    {
        if (reported != ReportedState.ReportedDamaged)
        {
            Debug.LogWarning($"[{name}] Debe 'Reportar' antes de Reemplazar.");
            return;
        }

        // 1) Soltar si está agarrado
        if (grab && grab.isSelected && grab.interactionManager != null)
        {
            var selecting = grab.interactorsSelecting.ToList();
            foreach (var interactor in selecting)
                grab.interactionManager.SelectExit(interactor, grab);
        }

        // 2) Parent correcto
        Transform parent = transform.parent != null ? transform.parent : fallbackParent;
        if (parent == null)
        {
            var p = GameObject.Find("Instrumentos");
            if (p) parent = p.transform;
        }

        // 3) Pose base del reemplazo
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;

        GameObject prefab = goodReplacementPrefab ?? ReplacementService.Instance?.GetGoodPrefab(type);
        if (!prefab)
        {
            Debug.LogWarning($"No hay prefab de reemplazo para {type}.");
            return;
        }

        var replacedHandlers = OnReplaced;

        // 4) Destruir el dañado e instanciar el nuevo como hijo de 'Instrumentos'
        Destroy(gameObject);

        var newGo = Instantiate(prefab, pos, rot, parent);
        newGo.name = prefab.name; // opcional, evita "(Clone)"

        // 5) Reposicionar para que no quede hundido
        var rb = newGo.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        Bounds b = GetWorldBounds(newGo);
        Vector3 grounded = GetGroundedPos(pos, b, groundMask, dropClearance);
        newGo.transform.position = grounded;

        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // 6) El nuevo está bueno y aprobado
        var newIns = newGo.GetComponent<InspectableInstrument>();
        if (newIns)
        {
            newIns.type = type;
            newIns.actualCondition = ActualCondition.Good;
            newIns.reported = ReportedState.Good;
            newIns.OnStateChanged?.Invoke(newIns);
        }

        // 7) Enciende guía en el socket correspondiente
        InstrumentSnapTarget.HighlightFor(type, true);

        // 8) Notificar reemplazo
        replacedHandlers?.Invoke(newIns);
    }

    // ---------------- HELPERS ----------------
    static Bounds GetWorldBounds(GameObject go)
    {
        bool has = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);

        foreach (var c in go.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled) continue;
            if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }

        if (!has) b = new Bounds(go.transform.position, Vector3.one * 0.1f);
        return b;
    }

    static Vector3 GetGroundedPos(Vector3 startPos, Bounds worldBounds, LayerMask groundMask, float clearance)
    {
        float cast = 5f;
        Vector3 origin = new Vector3(startPos.x, worldBounds.max.y + cast, startPos.z);

        if (Physics.Raycast(origin, Vector3.down, out var hit, cast * 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            float halfHeight = worldBounds.extents.y;
            float y = hit.point.y + halfHeight + clearance;
            return new Vector3(startPos.x, y, startPos.z);
        }
        return startPos;
    }
}
