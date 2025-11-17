using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class InstrumentSnapTarget : MonoBehaviour
{
    [Header("Configuración")]
    public InstrumentType expectedType = InstrumentType.Otro;
    public Transform snapPoint;          // punto/pose final del instrumento
    public bool lockAfterSnap = true;    // deshabilitar XRGrabInteractable tras encajar

    [Header("Feedback opcional")]
    public AudioSource audioOk;
    public AudioSource audioError;
    public MeshRenderer highlightRenderer;   // malla que se ilumina como guía
    public Color okColor = Color.green;
    public Color errorColor = Color.red;
    public Color guideColor = new Color(1f, 0.85f, 0.1f); // amarillo guía

    // Registro global: InstrumentType -> Socket correspondiente
    static readonly Dictionary<InstrumentType, InstrumentSnapTarget> registry = new();

    Material matInst;
    Color baseColor = Color.white;
    Coroutine pulseCo;
    bool guiding;
    bool occupied;
    float nextErrorAt;

    // ---------------------------------------------------------
    void OnEnable()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // usamos OnTriggerStay para detectar el instrumento

        registry[expectedType] = this;
        CacheMat();

        occupied = false;

        // Al inicio el área NO es visible
        if (highlightRenderer)
            highlightRenderer.enabled = false;
    }

    void OnDisable()
    {
        if (registry.TryGetValue(expectedType, out var me) && me == this)
            registry.Remove(expectedType);

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

        if (matInst.HasProperty("_BaseColor"))
            baseColor = matInst.GetColor("_BaseColor");
        else
            baseColor = matInst.color;
    }

    void SetColor(Color c)
    {
        if (!matInst) return;

        if (matInst.HasProperty("_BaseColor"))
            matInst.SetColor("_BaseColor", c);
        else
            matInst.color = c;
    }

    // ---------------------------------------------------------
    // API pública: encender/apagar guía desde InspectableInstrument
    public static void HighlightFor(InstrumentType type, bool on)
    {
        if (!registry.TryGetValue(type, out var s) || !s) return;
        if (on) s.StartGuide(); else s.StopGuide();
    }

    void StartGuide()
    {
        if (!highlightRenderer || guiding) return;

        CacheMat();
        guiding = true;

        // al guiar, hacemos visible el área
        highlightRenderer.enabled = true;

        pulseCo = StartCoroutine(Pulse());
    }

    void StopGuide()
    {
        if (!highlightRenderer || !guiding) return;

        guiding = false;
        if (pulseCo != null) StopCoroutine(pulseCo);

        if (!occupied)
        {
            // si aún no hay instrumento colocado, ocultamos de nuevo el área
            highlightRenderer.enabled = false;
        }
        else
        {
            // si ya está ocupado, dejamos el color OK
            SetColor(okColor);
        }
    }

    IEnumerator Pulse()
    {
        while (guiding)
        {
            float t = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            Color c = Color.Lerp(baseColor, guideColor, t);
            SetColor(c);
            yield return null;
        }
    }

    // ---------------------------------------------------------
    void OnTriggerStay(Collider other)
    {
        if (occupied) return;

        var grab = other.GetComponentInParent<XRGrabInteractable>();
        if (!grab || grab.isSelected) return; // solo cuando lo sueltan encima

        var inst = other.GetComponentInParent<InspectableInstrument>();
        if (!inst) return;

        // tipo incorrecto o instrumento no aprobado -> error
        if (inst.type != expectedType || !inst.IsApproved)
        {
            ErrorFlash();
            return;
        }

        // SNAP correcto: mover y orientar al punto de snap
        var t = inst.transform;
        var p = snapPoint ? snapPoint : transform;
        t.SetPositionAndRotation(p.position, p.rotation);

        var rb = t.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (lockAfterSnap && grab)
            grab.enabled = false;

        occupied = true;

        // detenemos la guía, pero mantenemos visible el área en color OK
        StopGuide();
        if (audioOk) audioOk.Play();
        SetColor(okColor);

        // notificar a otros scripts (ej. AcomodadorChecklist)
        SendMessageUpwards("OnInstrumentSnapped", this, SendMessageOptions.DontRequireReceiver);
    }

    void ErrorFlash()
    {
        if (Time.time < nextErrorAt) return;
        nextErrorAt = Time.time + 0.35f;

        if (audioError) audioError.Play();
        if (highlightRenderer) highlightRenderer.enabled = true; // aseguramos visibilidad
        SetColor(errorColor);

        CancelInvoke(nameof(ResetHighlight));
        Invoke(nameof(ResetHighlight), 0.25f);
    }

    void ResetHighlight()
    {
        if (!highlightRenderer) return;

        if (guiding)
        {
            // seguimos en modo guía, el pulso volverá a tomar el control del color
            SetColor(guideColor);
        }
        else if (occupied)
        {
            SetColor(okColor);
        }
        else
        {
            // sin guía ni instrumento → oculto
            highlightRenderer.enabled = false;
            SetColor(baseColor);
        }
    }
}
