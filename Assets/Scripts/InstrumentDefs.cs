using System;
using System.Collections.Generic;
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

// Lo que ve/declara el jugador
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

    [NonSerialized] public bool done; // se marca solo en runtime
}

[Serializable]
public class InstrumentTestSound
{
    public string checkId;           // debe coincidir con InstrumentCheck.id
    public AudioClip goodClip;       // sonido si el instrumento está bueno
    public AudioClip defectiveClip;  // sonido si el instrumento está dañado
}
