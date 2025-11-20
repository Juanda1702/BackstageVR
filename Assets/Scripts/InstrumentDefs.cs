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
    Unknown,        // a�n no se ha tomado decisi�n
    Good,           // aprobado
    ReportedDamaged // reportado como da�ado
}

// Condici�n real (no se muestra en la UI)
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

    [Tooltip("Si est� activo, esta tarea se marca manualmente desde el toggle del panel (normalmente visual).")]
    public bool isManualVisualCheck = false;

    [NonSerialized] public bool done;   // se marca solo en runtime
    [NonSerialized] public bool active = true; // mantiene la prueba en la checklist (siempre true ahora)

    [NonSerialized]
    [Tooltip("En runtime: si true, y el instrumento real está DEFECTUOSO, esta prueba manifestará el defecto (sonará/a visiblemente fallará). Si false, la prueba se comportará como si el instrumento estuviera bueno.)")]
    public bool simulatedDefective = false; // si el defecto se manifiesta para esta prueba
}

[Serializable]
public class InstrumentTestSound
{
    [Tooltip("Debe coincidir con InstrumentCheck.id")]
    public string checkId;           // debe coincidir con InstrumentCheck.id

    [Tooltip("Sonido cuando el instrumento est� en BUEN estado.")]
    public AudioClip goodClip;       // sonido si el instrumento est� bueno

    [Tooltip("Sonido cuando el instrumento est� DEFECTUOSO.")]
    public AudioClip defectiveClip;  // sonido si el instrumento est� da�ado

    [Tooltip("Si el instrumento est� DEFECTUOSO, �esta prueba suena como si estuviera BIEN?")]
    public bool defectiveSoundsGood = false;
}

[Serializable]
public class InstrumentTestVisual
{
    [Tooltip("Debe coincidir con InstrumentCheck.id de la tarea visual asociada.")]
    public string checkId;

    [Tooltip("Parte del instrumento (GameObject hijo) que se ver� o desaparecer� seg�n la condici�n real.")]
    public GameObject partObject;

    [Tooltip("Si el instrumento est� DEFECTUOSO, �esta parte desaparece?")]
    public bool hideIfDefective = true;
}
