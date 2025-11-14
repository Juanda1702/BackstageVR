using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

    // Estado declarado por el jugador
    [SerializeField] ReportedState reported = ReportedState.Unknown;
    public ReportedState Reported => reported;

    // Checklist de pruebas simples
    public List<InstrumentCheck> checks = new List<InstrumentCheck>();

    public bool Inspected => reported != ReportedState.Unknown;
    public bool AllRequiredChecksDone =>
        checks == null || checks.Count == 0
            ? true
            : checks.TrueForAll(c => !c.required || c.done);

    // Aprobado sólo si el jugador marcó "Good" Y todas las pruebas requeridas están hechas
    public bool IsApproved => reported == ReportedState.Good && AllRequiredChecksDone;

    [Header("Reemplazo")]
    public GameObject goodReplacementPrefab;  // si es null, usa ReplacementService
    public Transform fallbackParent;          // arrastra aquí el GO "Instrumentos"
    public LayerMask groundMask = ~0;         // capas de Suelo/Escenario
    public float dropClearance = 0.01f;       // separación mínima desde el piso

    [Header("UI de inspección")]
    public GameObject inspectionUIPrefab;
    public Transform uiAnchor;                // punto de referencia en el instrumento
    GameObject uiInstance;

    [Header("Audio de pruebas")]
    public AudioSource testAudioSource;
    public List<InstrumentTestSound> testSounds = new List<InstrumentTestSound>();

    [Tooltip("Si no está vacío, al pulsar 'Activate/Trigger' mientras el instrumento está agarrado se dispara esta prueba")]
    public string activateCheckId;            // ej. "mic_hit" para el micrófono

    XRGrabInteractable grab;

    // Eventos que puede escuchar el checklist general, etc.
    public System.Action<InspectableInstrument> OnReplaced;
    public System.Action<InspectableInstrument> OnStateChanged;
    public System.Action<InspectableInstrument> OnChecklistChanged;

    // ---------------- LIFECYCLE ----------------
    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
        grab.activated.AddListener(OnActivated);   // <- disparar prueba con gatillo
    }

    void Start()
    {
        if (randomizeOnStart)
            actualCondition = (Random.value < defectiveProbability)
                ? ActualCondition.Defective : ActualCondition.Good;

        // Checklist arranca sin pruebas hechas
        if (checks != null)
        {
            foreach (var c in checks)
                c.done = false;
        }

        reported = ReportedState.Unknown;
        OnStateChanged?.Invoke(this);
        OnChecklistChanged?.Invoke(this);
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
            grab.activated.RemoveListener(OnActivated);
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

    void OnActivated(ActivateEventArgs args)
    {
        // Esto se lanza cuando se pulsa la acción "Activate" (normalmente el gatillo)
        if (!string.IsNullOrEmpty(activateCheckId))
        {
            RunTest(activateCheckId, "Activate (gatillo mientras está agarrado)");
        }
    }

    void ShowUI(bool on, Transform interactorTf = null)
    {
        if (on)
        {
            if (!inspectionUIPrefab || uiInstance) return;

            uiInstance = Instantiate(inspectionUIPrefab);
            var panel = uiInstance.GetComponent<InstrumentInspectionPanel>();
            var anchor = uiAnchor ? uiAnchor : transform;

            if (panel != null)
                panel.Bind(this, anchor, interactorTf);
        }
        else if (uiInstance)
        {
            Destroy(uiInstance);
            uiInstance = null;
        }
    }

    // ---------------- CHECKLIST ----------------
    public void MarkCheckDone(string id)
    {
        if (checks == null) return;
        var c = checks.Find(ch => ch.id == id);
        if (c == null || c.done) return;

        c.done = true;
        Debug.Log($"[InspectableInstrument:{name}] Check '{id}' marcado como done.");
        OnChecklistChanged?.Invoke(this);
    }

    public bool IsActuallyDefective() => actualCondition == ActualCondition.Defective;

    // Método central para TODAS las pruebas (UI, zonas, botón Activate, etc.)
    public void RunTest(string checkId, string reason = null)
    {
        Debug.Log($"[InspectableInstrument:{name}] RunTest '{checkId}'. Motivo: {reason}");

        // 1. Buscar la configuración de sonido para este check
        InstrumentTestSound ts = null;
        if (testSounds != null)
            ts = testSounds.FirstOrDefault(t => t.checkId == checkId);

        AudioClip clip = null;
        if (ts != null)
        {
            if (IsActuallyDefective() && ts.defectiveClip != null)
            {
                clip = ts.defectiveClip;
            }
            else if (ts.goodClip != null)
            {
                clip = ts.goodClip;
            }
        }

        // 2. Reproducir audio (si hay)
        if (!testAudioSource)
        {
            Debug.LogWarning($"[InspectableInstrument:{name}] testAudioSource no asignado.");
        }
        else if (clip == null)
        {
            Debug.LogWarning($"[InspectableInstrument:{name}] No hay clip para '{checkId}' y la condición actual.");
        }
        else
        {
            testAudioSource.PlayOneShot(clip);
            Debug.Log($"[InspectableInstrument:{name}] Reproduciendo clip '{clip.name}' para prueba '{checkId}'.");
        }

        // 3. Marcar el check correspondiente
        if (!string.IsNullOrEmpty(checkId))
            MarkCheckDone(checkId);
    }

    // ---------------- ACCIONES UI ----------------
    public void ReportDamaged()
    {
        reported = ReportedState.ReportedDamaged;
        OnStateChanged?.Invoke(this);
    }

    public void ConfirmGood()
    {
        if (!AllRequiredChecksDone)
        {
            Debug.Log($"[{name}] No se puede aprobar: faltan pruebas.");
            return;
        }

        reported = ReportedState.Good;
        OnStateChanged?.Invoke(this);

        // Enciende la guía del socket correspondiente
        InstrumentSnapTarget.HighlightFor(type, true);
    }

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

        // 4) Destruir el dañado
        Destroy(gameObject);

        // 5) Instanciar nuevo hijo de 'Instrumentos'
        var newGo = Object.Instantiate(prefab, pos, rot, parent);
        newGo.name = prefab.name;

        // 6) Asegurar que no quede hundido
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

        // 7) Marcar nuevo como bueno y checklist completo por defecto
        var newIns = newGo.GetComponent<InspectableInstrument>();
        if (newIns)
        {
            newIns.type = type;
            newIns.actualCondition = ActualCondition.Good;

            if (newIns.checks != null)
                foreach (var c in newIns.checks)
                    c.done = true; // asumimos que el reemplazo viene probado

            newIns.reported = ReportedState.Good;
            newIns.OnChecklistChanged?.Invoke(newIns);
            newIns.OnStateChanged?.Invoke(newIns);
        }

        // 8) Enciende guía en el socket correspondiente
        InstrumentSnapTarget.HighlightFor(type, true);

        // 9) Notificar reemplazo
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
            if (!has) { b = c.bounds; has = true; }
            else b.Encapsulate(c.bounds);
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
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
