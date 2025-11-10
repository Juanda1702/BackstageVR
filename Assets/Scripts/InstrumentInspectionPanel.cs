// InstrumentInspectionPanel.cs (reemplaza)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.XR.CoreUtils; // para hallar la cámara del XROrigin si no hay Camera.main

public class InstrumentInspectionPanel : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public Button reportButton;
    public Button replaceButton;
    public Button closeButton;
    [Header("Opcional")]
    public Button approveButton; // si lo dejas null, no se usa

    Transform followTarget;
    InspectableInstrument target;

    public void Bind(InspectableInstrument instrument, Transform anchor)
    {
        target = instrument;
        followTarget = anchor;
        Refresh();

        reportButton.onClick.AddListener(OnReport);
        replaceButton.onClick.AddListener(OnReplace);
        closeButton.onClick.AddListener(OnClose);
        if (approveButton) approveButton.onClick.AddListener(OnApprove);

        target.OnStateChanged += OnTargetStateChanged;
        target.OnReplaced += OnTargetReplaced;
    }

    void LateUpdate()
    {
        if (!followTarget) return;
        var cam = Camera.main ? Camera.main.transform :
                  FindFirstObjectByType<XROrigin>()?.Camera.transform;
        transform.position = followTarget.position + Vector3.up * 0.1f;
        if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
    }

    void OnDestroy()
    {
        if (!target) return;
        target.OnStateChanged -= OnTargetStateChanged;
        target.OnReplaced -= OnTargetReplaced;

        reportButton.onClick.RemoveListener(OnReport);
        replaceButton.onClick.RemoveListener(OnReplace);
        closeButton.onClick.RemoveListener(OnClose);
        if (approveButton) approveButton.onClick.RemoveListener(OnApprove);
    }

    void Refresh()
    {
        if (!statusText || !target) return;
        statusText.text = $"Estado: <b>{target.state}</b>\nInstrumento: {target.type}" +
                          $"\nInspeccionado: <b>{(target.Inspected ? "Sí" : "No")}</b>";
        replaceButton.interactable = (target.state == InstrumentState.Damaged);
        if (approveButton) approveButton.interactable = (target.state == InstrumentState.Good);
    }

    void OnReport() { target.MarkDamaged(); }
    void OnReplace() { target.ReplaceNow(); }
    void OnClose() { Destroy(gameObject); }
    void OnApprove() { target.ConfirmGood(); }

    void OnTargetStateChanged(InspectableInstrument _) { Refresh(); }
    void OnTargetReplaced(InspectableInstrument _) { Destroy(gameObject); }
}
