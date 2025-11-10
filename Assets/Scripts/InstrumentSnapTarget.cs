// InstrumentSnapTarget.cs (reemplaza)
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InstrumentSnapTarget : MonoBehaviour
{
    public InstrumentType expectedType = InstrumentType.Otro;
    public Transform snapPoint;
    public bool lockAfterSnap = true;

    [Header("Feedback opcional")]
    public AudioSource audioOk;
    public AudioSource audioError;
    public MeshRenderer highlightRenderer;
    public Color okColor = Color.green;
    public Color errorColor = Color.red;

    bool occupied;
    float nextErrorAt = 0f;

    void OnTriggerStay(Collider other)
    {
        if (occupied) return;

        var grab = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (!grab || grab.isSelected) return;

        var inst = other.GetComponentInParent<InspectableInstrument>();
        if (!inst) return;

        if (inst.type != expectedType || !inst.IsApproved)
        {
            ErrorFlash();
            return;
        }

        // Snap correcto
        var t = inst.transform;
        var p = snapPoint ? snapPoint : transform;
        t.SetPositionAndRotation(p.position, p.rotation);

        var rb = t.GetComponent<Rigidbody>();
        if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        if (lockAfterSnap && grab) grab.enabled = false;

        occupied = true;
        if (audioOk) audioOk.Play();
        if (highlightRenderer) highlightRenderer.material.color = okColor;

        SendMessage("OnInstrumentSnapped", this, SendMessageOptions.DontRequireReceiver);
    }

    void ErrorFlash()
    {
        if (Time.time < nextErrorAt) return;
        nextErrorAt = Time.time + 0.35f;

        if (audioError) audioError.Play();
        if (highlightRenderer) highlightRenderer.material.color = errorColor;
        CancelInvoke(nameof(ResetHighlight));
        Invoke(nameof(ResetHighlight), 0.3f);
    }

    void ResetHighlight()
    {
        if (highlightRenderer) highlightRenderer.material.color = Color.white;
    }
}
