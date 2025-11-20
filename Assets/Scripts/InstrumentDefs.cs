using System;
using UnityEngine;

public enum InstrumentType
{
    Piano,
    Violin,
    Guitar,
    ElectricGuitar,
    Harp,
    Bass,
    DrumSet,
    Microphone,
    Sax,
    Otro
}

// Lo que ve / declara el jugador
public enum ReportedState
{
    Unknown,        // aún no se ha tomado decisión
    Good,           // aprobado
    ReportedDamaged // reportado como dañado
}

// Condición real (no se muestra en la UI)
public enum ActualCondition
{
    Good,
    Defective
}

[Serializable]
public class InstrumentCheck
{
    public string id;           // ej. "mic_hit", "piano_keys"
    public string displayName;  // texto en el panel
    public bool required = true;

    [Tooltip("Si está activo, esta tarea se marca manualmente desde el toggle del panel (normalmente visual).")]
    public bool isManualVisualCheck = false;

    [NonSerialized] public bool done;   // se marca solo en runtime
    [NonSerialized] public bool active = true; // randomizador decide si esta prueba aparece o no
}

[Serializable]
public class InstrumentTestSound
{
    [Tooltip("Debe coincidir con InstrumentCheck.id")]
    public string checkId;           // debe coincidir con InstrumentCheck.id

    [Tooltip("Sonido cuando el instrumento está en BUEN estado.")]
    public AudioClip goodClip;       // sonido si el instrumento está bueno

    [Tooltip("Sonido cuando el instrumento está DEFECTUOSO.")]
    public AudioClip defectiveClip;  // sonido si el instrumento está dañado

    [Tooltip("Si el instrumento está DEFECTUOSO, ¿esta prueba suena como si estuviera BIEN?")]
    public bool defectiveSoundsGood = false;
}

[Serializable]
public class InstrumentTestVisual
{
    [Tooltip("Debe coincidir con InstrumentCheck.id de la tarea visual asociada.")]
    public string checkId;

    [Tooltip("Parte del instrumento (GameObject hijo) que se verá o desaparecerá según la condición real.")]
    public GameObject partObject;

    [Tooltip("Si el instrumento está DEFECTUOSO, ¿esta parte desaparece?")]
    public bool hideIfDefective = true;
}
