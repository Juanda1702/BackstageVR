using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DisallowMultipleComponent]
public class PlayerActivityTracker : MonoBehaviour
{
    public static PlayerActivityTracker Instance { get; private set; }

    [Header("Grabación")]
    public bool autoStartRecording = true;
    public bool recordMovement = true;
    [Tooltip("Segundos entre muestras de posición/rotación")]
    public float positionSampleInterval = 0.2f;
    public bool recordRotation = true;

    [Header("Guardado")]
    [Tooltip("Si true, guarda automáticamente al salir de la aplicación o al detener la grabación.")]
    public bool autoSaveOnStop = true;
    [Tooltip("Nombre base del archivo; se añadirá timestamp y .json")]
    public string outputFileBaseName = "player_activity";

    [Header("Límites")]
    [Tooltip("Máximo de entradas de movimiento a mantener en memoria. 0 = ilimitado.")]
    public int maxMovementEntries = 0;

    [Header("Auto-hook Interactables")]
    [Tooltip("Si está activo, al iniciar el tracker buscará XRGrabInteractable en la escena y se suscribirá a sus eventos para loggear grabs/releases/activates.")]
    public bool autoHookXRGrabInteractables = true;

    // Lista de interactables a los que nos hemos suscrito para evitar subscripciones duplicadas
    List<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable> hookedInteractables = new List<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

    // Entradas en runtime
    [Serializable]
    public class MovementEntry
    {
        public float time; // segundos desde el inicio de la sesión
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class ActionEntry
    {
        public float time; // segundos desde el inicio de la sesión
        public string actionType; // ej. "Grab", "Release", "PressButton"
        public string details; // texto libre
        public Vector3 position; // posición del jugador al momento
    }

    [Serializable]
    class ActivityLog
    {
        public string sessionId;
        public string sceneName;
        public double startedAtUnixUtc;
        public float duration;
        public MovementEntry[] movements;
        public ActionEntry[] actions;
        public InstrumentInspectionEntry[] inspections;
    }

    [Serializable]
    public class InstrumentInspectionEntry
    {
        public string instrumentId;
        public float startedAt; // segundos desde el inicio de la sesión
        public float duration;  // duración en segundos
    }

    List<MovementEntry> movementEntries = new List<MovementEntry>();
    List<ActionEntry> actionEntries = new List<ActionEntry>();
    List<InstrumentInspectionEntry> inspectionEntries = new List<InstrumentInspectionEntry>();
    Dictionary<string, float> inspectionStartTimes = new Dictionary<string, float>();

    bool recording = false;
    float startTime = 0f;
    float sessionDuration = 0f;
    string currentSessionId;
    double sessionStartedAtUnix = 0.0;

    /// <summary>
    /// Marca el inicio de la inspección de un instrumento (por id). Se puede llamar múltiples veces para distintos instrumentos.
    /// </summary>
    public void BeginInstrumentInspection(string instrumentId)
    {
        if (string.IsNullOrEmpty(instrumentId)) return;
        if (!recording)
            Debug.LogWarning("BeginInstrumentInspection called while not recording.");

        if (inspectionStartTimes.ContainsKey(instrumentId))
        {
            // ya estaba inspeccionándose
            return;
        }

        inspectionStartTimes[instrumentId] = Time.time - startTime;
        LogAction("InspectionStart", instrumentId);
    }

    /// <summary>
    /// Sobrecarga: marcar inicio de inspección usando el enum InstrumentType.
    /// </summary>
    public void BeginInstrumentInspection(InstrumentType instrumentType)
    {
        BeginInstrumentInspection(instrumentType.ToString());
    }

    /// <summary>
    /// Marca el fin de la inspección de un instrumento y registra la duración.
    /// </summary>
    public void EndInstrumentInspection(string instrumentId)
    {
        if (string.IsNullOrEmpty(instrumentId)) return;

        if (!inspectionStartTimes.TryGetValue(instrumentId, out var startedAt))
        {
            // No había inicio registrado; aún así podemos loggear un evento de end
            LogAction("InspectionEnd", instrumentId);
            return;
        }

        float endTime = Time.time - startTime;
        float duration = Mathf.Max(0f, endTime - startedAt);

        var entry = new InstrumentInspectionEntry()
        {
            instrumentId = instrumentId,
            startedAt = startedAt,
            duration = duration
        };

        inspectionEntries.Add(entry);
        inspectionStartTimes.Remove(instrumentId);

        LogAction("InspectionEnd", instrumentId + $" (duration={duration:F2}s)");
    }

    /// <summary>
    /// Sobrecarga: marcar fin de inspección usando el enum InstrumentType.
    /// </summary>
    public void EndInstrumentInspection(InstrumentType instrumentType)
    {
        EndInstrumentInspection(instrumentType.ToString());
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple PlayerActivityTracker instances detected — destroying duplicate.");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (autoStartRecording)
            StartRecording();
        
        if (autoHookXRGrabInteractables)
            HookAllGrabInteractables();
    }

    void OnDestroy()
    {
        UnhookAllInteractables();

        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit()
    {
        // Guardar si corresponde
        if (recording)
        {
            StopRecording();
        }
    }

    public void StartRecording()
    {
        if (recording) return;
        recording = true;
        movementEntries.Clear();
        actionEntries.Clear();
        inspectionEntries.Clear();
        inspectionStartTimes.Clear();
        startTime = Time.time;
        sessionDuration = 0f;
        currentSessionId = Guid.NewGuid().ToString();
        sessionStartedAtUnix = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        if (recordMovement)
            StartCoroutine(SampleMovementCoroutine());

        Debug.Log("PlayerActivityTracker: started recording.");
    }

    public void StopRecording()
    {
        if (!recording) return;
        recording = false;
        StopAllCoroutines();

        // cerrar inspecciones abiertas
        var keys = new List<string>(inspectionStartTimes.Keys);
        foreach (var k in keys)
            EndInstrumentInspection(k);

        // calcular duración de la sesión
        sessionDuration = Time.time - startTime;

        if (autoSaveOnStop)
            SaveToFile();

        Debug.Log("PlayerActivityTracker: stopped recording.");
    }

    // ----------------- Interactable hooking -----------------
    void HookAllGrabInteractables()
    {
        var all = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        foreach (var g in all)
            RegisterInteractable(g);
    }

    public void RegisterInteractable(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable g)
    {
        if (g == null) return;
        if (hookedInteractables.Contains(g)) return;

        g.selectEntered.AddListener(OnHookedSelectEntered);
        g.selectExited.AddListener(OnHookedSelectExited);
        g.activated.AddListener(OnHookedActivated);

        hookedInteractables.Add(g);
    }

    public void UnregisterInteractable(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable g)
    {
        if (g == null) return;
        if (!hookedInteractables.Contains(g)) return;

        g.selectEntered.RemoveListener(OnHookedSelectEntered);
        g.selectExited.RemoveListener(OnHookedSelectExited);
        g.activated.RemoveListener(OnHookedActivated);

        hookedInteractables.Remove(g);
    }

    void UnhookAllInteractables()
    {
        foreach (var g in hookedInteractables)
        {
            if (g == null) continue;
            g.selectEntered.RemoveListener(OnHookedSelectEntered);
            g.selectExited.RemoveListener(OnHookedSelectExited);
            g.activated.RemoveListener(OnHookedActivated);
        }
        hookedInteractables.Clear();
    }

    void OnHookedSelectEntered(SelectEnterEventArgs args)
    {
        var interactorName = (args.interactorObject as Component)?.name ?? "UnknownInteractor";
        var interactableName = (args.interactableObject as Component)?.name ?? "UnknownObject";
        LogAction("Grab", $"{interactableName} by {interactorName}");
    }

    void OnHookedSelectExited(SelectExitEventArgs args)
    {
        var interactorName = (args.interactorObject as Component)?.name ?? "UnknownInteractor";
        var interactableName = (args.interactableObject as Component)?.name ?? "UnknownObject";
        LogAction("Release", $"{interactableName} by {interactorName}");
    }

    void OnHookedActivated(ActivateEventArgs args)
    {
        var interactorName = (args.interactorObject as Component)?.name ?? "UnknownInteractor";
        var interactableName = (args.interactableObject as Component)?.name ?? "UnknownObject";
        LogAction("Activate", $"{interactableName} by {interactorName}");
    }

    IEnumerator SampleMovementCoroutine()
    {
        while (recording)
        {
            SampleMovement();
            yield return new WaitForSeconds(positionSampleInterval);
        }
    }

    void SampleMovement()
    {
        if (!recordMovement) return;

        var e = new MovementEntry();
        e.time = Time.time - startTime;
        e.position = transform.position;
        e.rotation = recordRotation ? transform.rotation : Quaternion.identity;

        movementEntries.Add(e);

        if (maxMovementEntries > 0 && movementEntries.Count > maxMovementEntries)
            movementEntries.RemoveRange(0, movementEntries.Count - maxMovementEntries);
    }

    /// <summary>
    /// Registrar una acción del jugador. Llamar desde otros scripts cuando ocurra una acción relevante.
    /// </summary>
    public void LogAction(string actionType, string details = null)
    {
        if (!recording)
            Debug.LogWarning($"PlayerActivityTracker.LogAction('{actionType}') called while not recording.");

        var a = new ActionEntry();
        a.time = Time.time - startTime;
        a.actionType = actionType;
        a.details = details;
        a.position = transform.position;

        actionEntries.Add(a);
    }

    /// <summary>
    /// Guarda el log en JSON en Application.persistentDataPath. Devuelve la ruta completa del archivo.
    /// </summary>
    public string SaveToFile(string fileName = null)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            // timestamp con milisegundos para evitar colisiones en saves rápidos
            string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            fileName = $"{outputFileBaseName}_{currentSessionId}_{ts}.json";
        }

        string path = Path.Combine(Application.persistentDataPath, fileName);

        var log = new ActivityLog();
        log.sessionId = string.IsNullOrEmpty(currentSessionId) ? Guid.NewGuid().ToString() : currentSessionId;
        log.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        log.startedAtUnixUtc = sessionStartedAtUnix > 0.0 ? sessionStartedAtUnix : DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        log.duration = recording ? (Time.time - startTime) : sessionDuration;
        log.movements = movementEntries.ToArray();
        log.actions = actionEntries.ToArray();
        log.inspections = inspectionEntries.ToArray();

        try
        {
            string json = JsonUtility.ToJson(log, true);
            File.WriteAllText(path, json);
            Debug.Log($"PlayerActivityTracker: saved activity log to {path}");
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogError($"PlayerActivityTracker: failed to save activity log: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Borrar todos los datos grabados en memoria (no borra archivos en disco).
    /// </summary>
    public void Clear()
    {
        movementEntries.Clear();
        actionEntries.Clear();
        inspectionEntries.Clear();
        inspectionStartTimes.Clear();
    }

    // Opcional: exposición de estado y contadores
    public bool IsRecording => recording;
    public int MovementCount => movementEntries.Count;
    public int ActionCount => actionEntries.Count;
}
