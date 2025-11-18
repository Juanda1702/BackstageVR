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
    public bool randomizeOnStart = true;

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

    [Header("Inspección obligatoria / resaltado")]
    [Tooltip("Si está activo, este instrumento forma parte de la lista de instrumentos que el acomodador DEBE probar.")]
    public bool mustBeInspected = false;

    [Tooltip("Renderers que se van a teñir cuando el instrumento sea obligatorio de probar.")]
    public Renderer[] highlightRenderers;

    [Tooltip("Color de resaltado para instrumentos que deben probarse.")]
    public Color requiredHighlightColor = new Color(0f, 1f, 1f, 1f);

    bool highlightCached = false;
    Color[] highlightBaseColors;

    [Header("Reemplazo (con prefab)")]
    [Tooltip("Prefab bueno que reemplaza a este instrumento cuando se reporta como dañado. Si es null, se le pedirá a ReplacementService.")]
    public GameObject goodReplacementPrefab;
    [Tooltip("Transform padre opcional para el nuevo instrumento. Si es null, usa el mismo padre que este.")]
    public Transform fallbackParent;
    [Tooltip("Capas que se consideran suelo/escenario para posicionar el reemplazo.")]
    public LayerMask groundMask = ~0;
    [Tooltip("Separación vertical mínima entre la base del instrumento y el suelo.")]
    public float dropClearance = 0.01f;

    [Header("UI de inspección")]
    public GameObject inspectionUIPrefab;
    [Tooltip("Punto de referencia en el instrumento para colocar el panel. Si es null, se usa el propio instrumento.")]
    public Transform uiAnchor;
    GameObject uiInstance;

    [Header("Posición del panel de inspección")]
    [Tooltip("Offset local aplicado sobre el uiAnchor (o el instrumento) para ajustar dónde aparece el panel.")]
    public Vector3 inspectionPanelLocalOffset = new Vector3(0.3f, 0.2f, 0f);

    [Header("Audio de pruebas")]
    public AudioSource testAudioSource;
    public List<InstrumentTestSound> testSounds = new List<InstrumentTestSound>();

    [Header("Pruebas con gatillo (Activate)")]
    [Tooltip("IDs de las pruebas que se pueden disparar con el gatillo mientras el instrumento está agarrado. Se intentará ejecutar siempre la primera que no se haya hecho.")]
    public List<string> activateCheckIds = new List<string>();
    int nextActivateIndex = 0;

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
        grab.activated.AddListener(OnActivated);
    }

    void Start()
    {
        if (randomizeOnStart)
        {
            actualCondition = (Random.value < defectiveProbability)
                ? ActualCondition.Defective
                : ActualCondition.Good;
        }

        if (checks != null)
        {
            foreach (var c in checks)
                c.done = false;
        }

        reported = ReportedState.Unknown;

        // Resaltado inicial
        UpdateInspectionHighlight();

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

    // ---------------- GRAB / ACTIVATE ----------------
    void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Si ya está aprobado (Good), NO mostramos más el panel al volver a agarrarlo.
        if (reported == ReportedState.Good)
            return;

        // Crea el panel solo si aún no existe
        ShowUI(true, (args.interactorObject as Component)?.transform);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        // El panel NO se cierra al soltar el instrumento.
        // Solo se cierra con el botón "Cerrar" o al pulsar "Reemplazar" en el panel.
    }

    void OnActivated(ActivateEventArgs args)
    {
        RunActivateTest();
    }

    void RunActivateTest()
    {
        if (activateCheckIds == null || activateCheckIds.Count == 0)
            return;

        string idToUse = null;

        if (checks != null && checks.Count > 0)
        {
            int count = activateCheckIds.Count;

            // Buscar la primera prueba de la lista que aún no esté hecha
            for (int offset = 0; offset < count; offset++)
            {
                int idx = (nextActivateIndex + offset) % count;
                var id = activateCheckIds[idx];
                var check = checks.FirstOrDefault(c => c.id == id);
                if (check != null && !check.done)
                {
                    idToUse = id;
                    nextActivateIndex = (idx + 1) % count;
                    break;
                }
            }
        }

        // Si todas estaban hechas (o no hay checklist para esos IDs), usa la primera como feedback
        if (string.IsNullOrEmpty(idToUse))
        {
            idToUse = activateCheckIds[0];
            nextActivateIndex = (nextActivateIndex + 1) % activateCheckIds.Count;
        }

        RunTest(idToUse, "Activate (gatillo mientras está agarrado)");
    }

    void ShowUI(bool on, Transform interactorTf = null)
    {
        if (!on) return;
        if (!inspectionUIPrefab || uiInstance) return;

        uiInstance = Instantiate(inspectionUIPrefab);
        var panel = uiInstance.GetComponent<InstrumentInspectionPanel>();
        var anchor = uiAnchor ? uiAnchor : transform;

        if (panel != null)
            panel.Bind(this, anchor, interactorTf);
    }

    public void NotifyInspectionPanelClosed()
    {
        uiInstance = null;
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

    public void RunTest(string checkId, string reason = null)
    {
        // Si ya fue aprobado, los sonidos/pruebas dejan de funcionar
        if (reported == ReportedState.Good)
        {
            Debug.Log($"[InspectableInstrument:{name}] RunTest ignorado porque el instrumento ya está APROBADO.");
            return;
        }

        Debug.Log($"[InspectableInstrument:{name}] RunTest '{checkId}'. Motivo: {reason}");

        if (string.IsNullOrEmpty(checkId))
            return;

        // 1. Buscar la configuración de sonido para este check
        InstrumentTestSound ts = null;
        if (testSounds != null)
            ts = testSounds.FirstOrDefault(t => t.checkId == checkId);

        if (ts == null)
        {
            Debug.LogWarning($"[InspectableInstrument:{name}] No hay InstrumentTestSound configurado para checkId '{checkId}'.");
            return;
        }

        bool defective = IsActuallyDefective();
        AudioClip clip = defective ? ts.defectiveClip : ts.goodClip;

        if (clip == null)
        {
            string tipo = defective ? "defectiveClip" : "goodClip";
            Debug.LogWarning($"[InspectableInstrument:{name}] Falta {tipo} para '{checkId}' con condición {(defective ? "Defective" : "Good")}.");
        }
        else
        {
            if (!testAudioSource)
            {
                Debug.LogWarning($"[InspectableInstrument:{name}] testAudioSource no asignado.");
            }
            else
            {
                testAudioSource.Stop();
                testAudioSource.clip = clip;
                testAudioSource.Play();
                Debug.Log($"[InspectableInstrument:{name}] Reproduciendo clip '{clip.name}' ({(defective ? "DEFECTIVE" : "GOOD")}) para prueba '{checkId}'.");
            }
        }

        // 3. Marcar el check correspondiente
        MarkCheckDone(checkId);
    }

    // ---------------- ESTADO / REPORTES ----------------
    public void ReportDamaged()
    {
        reported = ReportedState.ReportedDamaged;
        Debug.Log($"[InspectableInstrument:{name}] Reportado como dañado.");

        UpdateInspectionHighlight();
        OnStateChanged?.Invoke(this);
    }

    public void ConfirmGood()
    {
        // No permitir aprobar si ya se reportó como dañado
        if (reported == ReportedState.ReportedDamaged)
        {
            Debug.Log($"[InspectableInstrument:{name}] No se puede aprobar un instrumento que ya fue reportado como dañado.");
            return;
        }

        if (!AllRequiredChecksDone)
        {
            Debug.Log($"[InspectableInstrument:{name}] No se puede aprobar: faltan pruebas requeridas.");
            return;
        }

        reported = ReportedState.Good;
        Debug.Log($"[InspectableInstrument:{name}] Aprobado por el acomodador.");

        UpdateInspectionHighlight();
        OnStateChanged?.Invoke(this);

        InstrumentSnapTarget.HighlightFor(type, true);
    }

    public void ReplaceNow()
    {
        if (reported != ReportedState.ReportedDamaged)
        {
            Debug.Log($"[InspectableInstrument:{name}] ReplaceNow llamado pero el instrumento no está reportado como dañado.");
            return;
        }

        // Soltar si está agarrado
        if (grab != null && grab.isSelected && grab.interactionManager != null && grab.firstInteractorSelecting != null)
        {
            grab.interactionManager.SelectExit(grab.firstInteractorSelecting, grab);
        }

        // Obtener prefab de reemplazo
        var prefab = goodReplacementPrefab ?? ReplacementService.Instance?.GetGoodPrefab(type);
        if (!prefab)
        {
            Debug.LogWarning($"[InspectableInstrument:{name}] No hay prefab de reemplazo configurado para {type}.");
            return;
        }

        // Determinar padre y posición apoyada en el suelo
        var parent = fallbackParent ? fallbackParent : transform.parent;
        var worldBounds = GetWorldBounds(gameObject);
        var spawnPos = GetGroundedPos(transform.position, worldBounds, groundMask, dropClearance);
        var spawnRot = transform.rotation;

        // Instanciar nuevo instrumento
        var newGO = Instantiate(prefab, spawnPos, spawnRot, parent);
        var newInspectable = newGO.GetComponent<InspectableInstrument>();
        if (newInspectable)
        {
            // El nuevo instrumento ya llega en buen estado
            newInspectable.actualCondition = ActualCondition.Good;

            if (newInspectable.checks != null)
            {
                foreach (var c in newInspectable.checks)
                    c.done = true;
            }

            newInspectable.reported = ReportedState.Good;

            newInspectable.UpdateInspectionHighlight();
            newInspectable.OnStateChanged?.Invoke(newInspectable);
            newInspectable.OnChecklistChanged?.Invoke(newInspectable);
        }

        OnReplaced?.Invoke(this);

        // Destruir el instrumento dañado
        Destroy(gameObject);
    }

    // ---------------- RESALTADO DE INSTRUMENTOS OBLIGATORIOS ----------------
    void CacheHighlightColors()
    {
        if (highlightCached)
            return;

        if (highlightRenderers == null || highlightRenderers.Length == 0)
            return;

        var colors = new List<Color>();

        foreach (var r in highlightRenderers)
        {
            if (!r) continue;

            // Forzar instancia de materiales para no afectar a otros objetos
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;

                Color baseColor;
                if (m.HasProperty("_BaseColor"))
                    baseColor = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color"))
                    baseColor = m.GetColor("_Color");
                else
                    baseColor = m.color;

                colors.Add(baseColor);
            }
            r.materials = mats;
        }

        highlightBaseColors = colors.ToArray();
        highlightCached = true;
    }

    void SetInspectionHighlight(bool on)
    {
        if (highlightRenderers == null || highlightRenderers.Length == 0)
            return;

        CacheHighlightColors();
        if (highlightBaseColors == null || highlightBaseColors.Length == 0)
            return;

        int colorIndex = 0;

        foreach (var r in highlightRenderers)
        {
            if (!r) continue;

            var mats = r.materials;
            for (int i = 0; i < mats.Length && colorIndex < highlightBaseColors.Length; i++, colorIndex++)
            {
                var m = mats[i];
                if (!m) continue;

                Color targetColor = on ? requiredHighlightColor : highlightBaseColors[colorIndex];

                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", targetColor);
                else if (m.HasProperty("_Color"))
                    m.SetColor("_Color", targetColor);
                else
                    m.color = targetColor;
            }

            r.materials = mats;
        }
    }

    void UpdateInspectionHighlight()
    {
        // Solo resaltamos mientras el instrumento no ha sido aún clasificado (Reported == Unknown)
        bool shouldHighlight = mustBeInspected && reported == ReportedState.Unknown;
        SetInspectionHighlight(shouldHighlight);
    }

    // ---------------- HELPERS ----------------
    static Bounds GetWorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
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
