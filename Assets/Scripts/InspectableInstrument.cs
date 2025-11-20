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

    // Condición real (oculta al jugador) – YA NO SE RANDOMIZA
    [SerializeField] ActualCondition actualCondition = ActualCondition.Good;

    // Estado declarado por el jugador
    [SerializeField] ReportedState reported = ReportedState.Unknown;
    public ReportedState Reported => reported;

    // Checklist de pruebas
    public List<InstrumentCheck> checks = new List<InstrumentCheck>();

    public bool Inspected => reported != ReportedState.Unknown;

    public bool AllRequiredChecksDone
    {
        get
        {
            if (checks == null || checks.Count == 0)
                return true;

            foreach (var c in checks)
            {
                if (!c.active)    // solo cuentan las pruebas activas
                    continue;
                if (!c.required)
                    continue;
                if (!c.done)
                    return false;
            }

            return true;
        }
    }

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

    // NUEVO: para saber si ya se abrió el menú alguna vez
    bool hasOpenedMenu = false;

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

    [Header("Pruebas visuales (partes presentes/faltantes)")]
    [Tooltip("Cada entrada representa una parte del instrumento que se ve o desaparece según la condición real.")]
    public List<InstrumentTestVisual> testVisuals = new List<InstrumentTestVisual>();

    [Header("Pruebas con gatillo (Activate)")]
    [Tooltip("IDs de las pruebas que se pueden disparar con el gatillo mientras el instrumento está agarrado. Se intentará ejecutar siempre la primera que no se haya hecho.")]
    public List<string> activateCheckIds = new List<string>();
    int nextActivateIndex = 0;

    [Header("Randomización de pruebas")]
    [Tooltip("Si está activo, al iniciar se decidirá aleatoriamente qué pruebas aparecen (sonido / visual) en la checklist.")]
    public bool randomizeTestsOnStart = false;

    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que cada prueba de SONIDO aparezca en esta ejecución.")]
    public float soundTestAppearChance = 1f;

    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que cada prueba VISUAL (con isManualVisualCheck) aparezca en esta ejecución.")]
    public float visualTestAppearChance = 1f;

    XRGrabInteractable grab;

    // Eventos externos
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
        // Inicializar estado done/active de checks
        if (checks != null)
        {
            foreach (var c in checks)
            {
                c.done = false;
                c.active = true;
            }
        }

        // Randomizar qué pruebas estarán activas
        RandomizeTests();

        // Aplicar visibilidad de partes visuales según condición real
        ApplyVisualTests();

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

    // ---------------- RANDOMIZACIÓN DE TESTS ----------------
    void RandomizeTests()
    {
        if (!randomizeTestsOnStart || checks == null || checks.Count == 0)
            return;

        foreach (var c in checks)
        {
            // Visual/manual o de sonido
            bool isVisual = c.isManualVisualCheck;
            float chance = isVisual ? visualTestAppearChance : soundTestAppearChance;

            // Si la probabilidad es 1, no hace falta Random
            if (chance >= 1f)
            {
                c.active = true;
                continue;
            }

            if (chance <= 0f)
            {
                c.active = false;
                continue;
            }

            c.active = (Random.value <= chance);
        }

        // Log opcional
        foreach (var c in checks)
        {
            Debug.Log($"[InspectableInstrument:{name}] Check '{c.id}' activo={c.active}, visual={c.isManualVisualCheck}");
        }
    }

    // ---------------- GRAB / ACTIVATE ----------------
    void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Si ya está aprobado (Good), NO mostramos más el panel al volver a agarrarlo.
        if (reported == ReportedState.Good)
            return;

        // Marcamos que su menú ya fue abierto al menos una vez
        hasOpenedMenu = true;
        // Actualizamos highlight para apagarlo si era obligatorio
        UpdateInspectionHighlight();

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
        if (checks == null || checks.Count == 0)
            return;

        string idToUse = null;

        int count = activateCheckIds.Count;

        // Buscar la primera prueba de la lista que aún no esté hecha y esté activa
        for (int offset = 0; offset < count; offset++)
        {
            int idx = (nextActivateIndex + offset) % count;
            var id = activateCheckIds[idx];

            var check = checks.FirstOrDefault(c => c.id == id);
            if (check == null) continue;
            if (!check.active) continue;
            if (check.done) continue;

            idToUse = id;
            nextActivateIndex = (idx + 1) % count;
            break;
        }

        // Si no hay ninguna activa y pendiente, no hacemos nada
        if (string.IsNullOrEmpty(idToUse))
        {
            Debug.Log($"[InspectableInstrument:{name}] RunActivateTest: no hay pruebas activas/pedientes en activateCheckIds.");
            return;
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

    // ---------------- VISUALES (PARTES PRESENTES/FALTANTES) ----------------
    // Regla:
    // - Si el instrumento está BUENO → la parte asignada siempre se ve (SetActive(true)).
    // - Si está DEFECTUOSO:
    //     - hideIfDefective = true  → SetActive(false)
    //     - hideIfDefective = false → SetActive(true)
    // Además: si la prueba asociada (checkId) no está activa, tratamos esa parte como "normal" (visible),
    // para que solo se simulen faltantes de las pruebas que realmente aparecen en la checklist.
    void ApplyVisualTests()
    {
        if (testVisuals == null) return;

        bool defective = IsActuallyDefective();

        foreach (var vis in testVisuals)
        {
            if (!vis.partObject) continue;

            // Buscar el check asociado (si existe)
            InstrumentCheck check = null;
            if (checks != null)
                check = checks.FirstOrDefault(c => c.id == vis.checkId);

            // Si el check existe y está inactivo, no queremos simular el defecto → parte visible
            if (check != null && !check.active)
            {
                vis.partObject.SetActive(true);
                continue;
            }

            bool active;
            if (!defective)
            {
                // Instrumento bueno → la parte siempre está presente
                active = true;
            }
            else
            {
                // Instrumento defectuoso → depende del booleano
                active = !vis.hideIfDefective;
            }

            vis.partObject.SetActive(active);
        }
    }

    // ---------------- CHECKLIST ----------------
    public void MarkCheckDone(string id)
    {
        if (checks == null) return;
        var c = checks.Find(ch => ch.id == id);
        if (c == null) return;
        if (!c.active) return; // si la prueba no está activa en este escenario, no hacemos nada
        if (c.done) return;

        c.done = true;
        Debug.Log($"[InspectableInstrument:{name}] Check '{id}' marcado como done.");
        OnChecklistChanged?.Invoke(this);
    }

    public bool IsActuallyDefective() => actualCondition == ActualCondition.Defective;

    public void RunTest(string checkId, string reason = null)
    {
        // Si ya fue aprobado, las pruebas dejan de funcionar
        if (reported == ReportedState.Good)
        {
            Debug.Log($"[InspectableInstrument:{name}] RunTest ignorado porque el instrumento ya está APROBADO.");
            return;
        }

        if (string.IsNullOrEmpty(checkId))
            return;

        // Si existe un check asociado y está inactivo, ignoramos esta prueba
        InstrumentCheck checkRef = null;
        if (checks != null)
            checkRef = checks.FirstOrDefault(c => c.id == checkId);

        if (checkRef != null && !checkRef.active)
        {
            Debug.Log($"[InspectableInstrument:{name}] RunTest '{checkId}' ignorado: check está inactivo por randomización.");
            return;
        }

        Debug.Log($"[InspectableInstrument:{name}] RunTest '{checkId}'. Motivo: {reason}");

        bool defective = IsActuallyDefective();

        // ---------- 1. Feedback de audio según estado ----------
        InstrumentTestSound ts = null;
        if (testSounds != null && testSounds.Count > 0)
            ts = testSounds.FirstOrDefault(t => t.checkId == checkId);

        if (ts != null)
        {
            AudioClip clipToPlay = null;

            if (!defective)
            {
                // Instrumento BUENO → siempre usamos goodClip (si existe)
                clipToPlay = ts.goodClip;
            }
            else
            {
                // Instrumento DEFECTUOSO:
                // - Si defectiveSoundsGood == true → suena como bueno
                // - Si defectiveSoundsGood == false → suena el clip defectuoso
                if (ts.defectiveSoundsGood)
                    clipToPlay = ts.goodClip;
                else
                    clipToPlay = ts.defectiveClip;
            }

            if (clipToPlay != null)
            {
                if (!testAudioSource)
                {
                    Debug.LogWarning($"[InspectableInstrument:{name}] testAudioSource no asignado.");
                }
                else
                {
                    testAudioSource.Stop();
                    testAudioSource.clip = clipToPlay;
                    testAudioSource.Play();
                    Debug.Log($"[InspectableInstrument:{name}] Reproduciendo clip '{clipToPlay.name}' para '{checkId}' " +
                              $"({(defective ? "DEFECTIVE" : "GOOD")}, defectiveSoundsGood={ts.defectiveSoundsGood}).");
                }
            }
            else
            {
                Debug.Log($"[InspectableInstrument:{name}] No hay clip asignado para '{checkId}' en estado " +
                          $"{(defective ? "DEFECTIVE" : "GOOD")} (solo feedback visual o ninguno).");
            }
        }

        // ---------- 2. (Opcional) lógica extra visual al probar ----------
        // De momento, la visibilidad de partes depende de ApplyVisualTests en Start
        // + randomización de checks (si el check no está activo, la parte se ve normal).

        // ---------- 3. Marcar el check correspondiente ----------
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
                {
                    c.done = true;
                    c.active = true;
                }
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
        // Solo resaltamos mientras:
        // - es obligatorio
        // - no se ha clasificado (Reported == Unknown)
        // - y TODAVÍA no se ha abierto el menú
        bool shouldHighlight = mustBeInspected &&
                               reported == ReportedState.Unknown &&
                               !hasOpenedMenu;

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
