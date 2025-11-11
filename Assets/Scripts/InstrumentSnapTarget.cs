using System.Collections;
using System.Collections.Generic;
using UnityEngine;
                 // IXRInteractor
using UnityEngine.XR.Interaction.Toolkit.Interactables;   // XRGrabInteractable

[RequireComponent(typeof(Collider))]
public class InstrumentSnapTarget : MonoBehaviour
{
    public InstrumentType expectedType = InstrumentType.Otro;
    public Transform snapPoint;                 // pose final exacta
    public bool lockAfterSnap = true;           // deshabilita el grab tras encajar

    [Header("Feedback opcional")]
    public AudioSource audioOk;
    public AudioSource audioError;
    public MeshRenderer highlightRenderer;      // malla a iluminar
    public Color okColor = Color.green;
    public Color errorColor = Color.red;
    public Color guideColor = new Color(1f, 0.85f, 0.1f); // amarillo

    // Registro global por tipo para encender/apagar gu�a desde el instrumento
    static readonly Dictionary<InstrumentType, InstrumentSnapTarget> registry = new();

    Material matInst;
    Color baseColor = Color.white;
    Coroutine pulseCo;
    bool guiding;
    bool occupied;
    float nextErrorAt;

    void OnEnable()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // usamos OnTriggerStay para detectar el instrumento
        registry[expectedType] = this;
        CacheMat();
    }

    void OnDisable()
    {
        if (registry.TryGetValue(expectedType, out var me) && me == this) registry.Remove(expectedType);
        StopGuide();
    }

    void CacheMat()
    {
        if (!highlightRenderer) return;
        if (!matInst)
        {
            matInst = new Material(highlightRenderer.material);
            highlightRenderer.material = matInst;
        }
        baseColor = matInst.HasProperty("_BaseColor") ? matInst.GetColor("_BaseColor") : matInst.color;
    }

    void SetColor(Color c)
    {
        if (!matInst) return;
        if (matInst.HasProperty("_BaseColor")) matInst.SetColor("_BaseColor", c);
        else matInst.color = c;
    }

    // API p�blica: enciende o apaga la gu�a para un tipo
    public static void HighlightFor(InstrumentType type, bool on)
    {
        if (!registry.TryGetValue(type, out var s) || !s) return;
        if (on) s.StartGuide(); else s.StopGuide();
    }

    void StartGuide()
    {
        if (!highlightRenderer || guiding) return;
        guiding = true;
        pulseCo = StartCoroutine(Pulse());
    }

    void StopGuide()
    {
        if (!guiding) return;
        guiding = false;
        if (pulseCo != null) StopCoroutine(pulseCo);
        SetColor(baseColor);
    }

    IEnumerator Pulse()
    {
        CacheMat();
        while (guiding)
        {
            float t = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            SetColor(Color.Lerp(baseColor, guideColor, t));
            yield return null;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (occupied) return;

        var grab = other.GetComponentInParent<XRGrabInteractable>();
        if (!grab || grab.isSelected) return; // solo cuando lo sueltan encima

        var inst = other.GetComponentInParent<InspectableInstrument>();
        if (!inst) return;

        if (inst.type != expectedType || !inst.IsApproved)
        {
            ErrorFlash();
            return;
        }

        // SNAP correcto (posici�n + rotaci�n)
        var t = inst.transform;
        var p = snapPoint ? snapPoint : transform;
        t.SetPositionAndRotation(p.position, p.rotation);

        var rb = t.GetComponent<Rigidbody>();
        if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        if (lockAfterSnap && grab) grab.enabled = false;

        occupied = true;
        StopGuide();
        if (audioOk) audioOk.Play();
        SetColor(okColor);

        // Notifica (Acomodador_Checklist puede escuchar esto)
        SendMessage("OnInstrumentSnapped", this, SendMessageOptions.DontRequireReceiver);
    }

    void ErrorFlash()
    {
        if (Time.time < nextErrorAt) return;
        nextErrorAt = Time.time + 0.35f;

        if (audioError) audioError.Play();
        SetColor(errorColor);
        CancelInvoke(nameof(ResetHighlight));
        Invoke(nameof(ResetHighlight), 0.25f);
    }

    void ResetHighlight() => SetColor(guiding ? guideColor : baseColor);
}
