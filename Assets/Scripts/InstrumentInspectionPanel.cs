// InstrumentInspectionPanel.cs (reemplaza)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.XR.CoreUtils; // para hallar la cámara del XROrigin si no hay Camera.main

public class InstrumentInspectionPanel : MonoBehaviour
{
    [Header("Refs UI")]
    public TextMeshProUGUI statusText;
    public UnityEngine.UI.Button reportButton, replaceButton, closeButton, approveButton;

    [Header("Colocación cómoda")]
    public float preferredDistance = 0.55f; // 50–60 cm delante de la vista
    public float verticalOffset = 0.12f;    // un poquito arriba del centro
    public float lateralOffset = 0.10f;     // a la derecha de la mano/rayo
    public float followSmoothing = 12f;     // Lerp

    Transform followTarget;        // el instrumento
    Transform interactorTarget;    // mano/rayo que agarró
    InspectableInstrument target;

    public void Bind(InspectableInstrument instrument, Transform anchor, Transform interactor)
    {
        target = instrument;
        followTarget = anchor;
        interactorTarget = interactor;
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
        if (!cam) return;

        // base: frente a la cámara a distancia fija
        Vector3 lookPos = cam.position + cam.forward * preferredDistance;
        // desplazamiento lateral hacia la mano/rayo si lo tenemos
        if (interactorTarget)
        {
            Vector3 side = Vector3.ProjectOnPlane(interactorTarget.right, Vector3.up).normalized;
            lookPos += side * lateralOffset;
        }
        lookPos += Vector3.up * verticalOffset;

        // suavizado
        transform.position = Vector3.Lerp(transform.position, lookPos, Time.deltaTime * followSmoothing);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(transform.position - cam.position, Vector3.up),
            Time.deltaTime * followSmoothing);
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

        string estadoTxt = target.Reported switch
        {
            ReportedState.Unknown => "Por inspeccionar",
            ReportedState.Good => "Aprobado",
            ReportedState.ReportedDamaged => "Reportado (dañado)",
            _ => "Por inspeccionar"
        };

        statusText.text =
            $"Estado: <b>{estadoTxt}</b>\n" +
            $"Instrumento: {target.type}\n" +
            $"Inspeccionado: <b>{(target.Inspected ? "Sí" : "No")}</b>";

        // Habilitaciones:
        // - Reportar: si aún no aprobó (si ya aprobó no tiene sentido reportar)
        reportButton.interactable = target.Reported != ReportedState.Good;

        // - Reemplazar: SOLO si fue reportado dañado
        replaceButton.interactable = target.Reported == ReportedState.ReportedDamaged;

        // - Aprobar: si aún no aprobó
        if (approveButton) approveButton.interactable = target.Reported != ReportedState.Good;
    }

    void OnReport() { target.ReportDamaged(); }
    void OnReplace() { target.ReplaceNow(); }
    void OnClose() { Destroy(gameObject); }
    void OnApprove() { target.ConfirmGood(); }

    void OnTargetStateChanged(InspectableInstrument _) { Refresh(); }
    void OnTargetReplaced(InspectableInstrument _) { Destroy(gameObject); }
}
