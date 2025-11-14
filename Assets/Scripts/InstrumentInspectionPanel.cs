using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.XR.CoreUtils;

public class InstrumentInspectionPanel : MonoBehaviour
{
    [Header("UI principal")]
    public TextMeshProUGUI statusText;
    public Button reportButton;
    public Button replaceButton;
    public Button closeButton;
    public Button approveButton;

    [Header("Checklist UI")]
    public Transform checklistContainer;        // Vertical Layout Group
    public GameObject checklistItemPrefab;      // prefab con Toggle + Texto (TMP o Text)
    public TextMeshProUGUI checklistTitleText;  // título "Pruebas requeridas (...)"

    [Header("Colocación en el mundo")]
    public float preferredDistance = 0.55f;
    public float verticalOffset = 0.12f;
    public float lateralOffset = 0.10f;
    public float followSmoothing = 12f;

    InspectableInstrument target;
    Transform followTarget;         // suele ser el instrumento
    Transform interactorTarget;     // mano/rayo que agarró

    readonly Dictionary<string, Toggle> uiChecks = new();

    // ------------------------------------------------------
    public void Bind(InspectableInstrument instrument, Transform anchor, Transform interactor)
    {
        target = instrument;
        followTarget = anchor;
        interactorTarget = interactor;

        if (target != null)
        {
            target.OnStateChanged += OnTargetStateChanged;
            target.OnChecklistChanged += OnTargetChecklistChanged;
        }

        WireButtons();
        BuildChecklistUI();
        Refresh();
    }

    void WireButtons()
    {
        if (reportButton)
        {
            reportButton.onClick.RemoveAllListeners();
            reportButton.onClick.AddListener(OnReport);
        }

        if (replaceButton)
        {
            replaceButton.onClick.RemoveAllListeners();
            replaceButton.onClick.AddListener(OnReplace);
        }

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClose);
        }

        if (approveButton)
        {
            approveButton.onClick.RemoveAllListeners();
            approveButton.onClick.AddListener(OnApprove);
        }
    }

    void OnDestroy()
    {
        if (target != null)
        {
            target.OnStateChanged -= OnTargetStateChanged;
            target.OnChecklistChanged -= OnTargetChecklistChanged;
        }
    }

    // ------------------------------------------------------
    void BuildChecklistUI()
    {
        // Limpia items anteriores
        foreach (Transform child in checklistContainer)
            Destroy(child.gameObject);
        uiChecks.Clear();

        if (target == null || target.checks == null)
        {
            UpdateChecklistTitle();
            return;
        }

        foreach (var check in target.checks)
        {
            var go = Instantiate(checklistItemPrefab, checklistContainer);

            // 1) Texto: primero intentamos con TextMeshProUGUI
            var tmpLabel = go.GetComponentInChildren<TextMeshPro>();
            if (tmpLabel != null)
            {
                tmpLabel.text = check.displayName;
            }
            else
            {
                // 2) Fallback a Text normal si el prefab no usa TMP
                var legacyLabel = go.GetComponentInChildren<Text>();
                if (legacyLabel != null)
                    legacyLabel.text = check.displayName;
            }

            // 3) Toggle
            var toggle = go.GetComponentInChildren<Toggle>();
            if (toggle)
            {
                toggle.isOn = check.done;
                toggle.interactable = false; // los marca la simulación, no el jugador
                uiChecks[check.id] = toggle;
            }
        }

        UpdateChecklistTitle();
    }

    void OnTargetChecklistChanged(InspectableInstrument inst)
    {
        if (target == null || target.checks == null) return;

        foreach (var check in target.checks)
        {
            if (uiChecks.TryGetValue(check.id, out var t))
                t.isOn = check.done;
        }

        UpdateChecklistTitle();
        Refresh();
    }

    void OnTargetStateChanged(InspectableInstrument inst)
    {
        Refresh();
    }

    // ------------------------------------------------------
    void Refresh()
    {
        if (!statusText || target == null) return;

        if (!target.Inspected)
        {
            statusText.text =
                $"{target.type}\n" +
                $"No inspeccionado";
        }
        else
        {
            statusText.text =
                $"{target.type}\n" +
                $"Inspeccionado";
        }

        // Botones
        if (reportButton)
            reportButton.interactable = target.Reported != ReportedState.Good;

        if (replaceButton)
            replaceButton.interactable = target.Reported == ReportedState.ReportedDamaged;

        if (approveButton)
            approveButton.interactable =
                target.Reported != ReportedState.Good &&
                target.AllRequiredChecksDone;

        UpdateChecklistTitle();
    }

    void UpdateChecklistTitle()
    {
        if (!checklistTitleText || target == null || target.checks == null || target.checks.Count == 0)
        {
            if (checklistTitleText) checklistTitleText.text = "";
            return;
        }

        string estadoChecks = target.AllRequiredChecksDone ? "completas" : "pendientes";
        checklistTitleText.text = $"Pruebas {estadoChecks}";
    }

    // ------------------------------------------------------
    void LateUpdate()
    {
        if (!followTarget) return;

        var camTransform = GetCameraTransform();
        if (!camTransform) return;

        // base: frente a la cámara
        Vector3 lookPos = camTransform.position + camTransform.forward * preferredDistance;

        if (interactorTarget)
        {
            // desplazamiento lateral hacia la mano/rayo
            Vector3 side = Vector3.ProjectOnPlane(interactorTarget.right, Vector3.up).normalized;
            lookPos += side * lateralOffset;
        }

        lookPos += Vector3.up * verticalOffset;

        transform.position = Vector3.Lerp(transform.position, lookPos, Time.deltaTime * followSmoothing);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(transform.position - camTransform.position, Vector3.up),
            Time.deltaTime * followSmoothing);
    }

    Transform GetCameraTransform()
    {
        if (Camera.main) return Camera.main.transform;

        var origin = FindFirstObjectByType<XROrigin>();
        if (origin && origin.Camera) return origin.Camera.transform;

        return null;
    }

    // ------------------------------------------------------
    void OnReport()
    {
        if (target == null) return;
        target.ReportDamaged();
        Refresh();
    }

    void OnReplace()
    {
        if (target == null) return;
        target.ReplaceNow();
        Destroy(gameObject); // este panel pertenece al objeto viejo
    }

    void OnClose()
    {
        Destroy(gameObject);
    }

    void OnApprove()
    {
        if (target == null) return;
        target.ConfirmGood();
        Refresh();
    }
}
