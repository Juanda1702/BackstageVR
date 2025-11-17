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
    public GameObject checklistItemPrefab;      // Prefab con Toggle + TMP_Text
    public TextMeshProUGUI checklistTitleText;  // "Pruebas pendientes / completas"

    [Header("Colocación en el mundo")]
    [Tooltip("Desplazamiento lateral respecto al instrumento (hacia la derecha desde el punto de vista de la cámara).")]
    public float lateralOffset = 0.35f;

    [Tooltip("Desplazamiento extra por encima de la parte alta del instrumento.")]
    public float extraVerticalOffset = 0.05f;

    InspectableInstrument target;

    // Pose anclada en el mundo
    Vector3 anchoredPosition;
    Quaternion anchoredRotation;
    bool hasAnchorPose;

    readonly Dictionary<string, Toggle> uiChecks = new();

    // --------------------------------------------------------------------
    // Llamado por InspectableInstrument.ShowUI(...)
    public void Bind(InspectableInstrument instrument, Transform anchor, Transform interactor)
    {
        target = instrument;

        if (target != null)
        {
            target.OnStateChanged += OnTargetStateChanged;
            target.OnChecklistChanged += OnTargetChecklistChanged;
        }

        WireButtons();
        BuildChecklistUI();
        Refresh();

        // Calcula la posición fija del panel al lado del instrumento
        SetupWorldAnchor(anchor);
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
            // Avisar al instrumento que este panel ya no existe
            target.NotifyInspectionPanelClosed();
        }
    }

    // --------------------------------------------------------------------
    // CHECKLIST
    void BuildChecklistUI()
    {
        // Limpia items anteriores
        foreach (Transform child in checklistContainer)
            Destroy(child.gameObject);
        uiChecks.Clear();

        if (target == null || target.checks == null || target.checks.Count == 0)
        {
            if (checklistTitleText) checklistTitleText.text = "";
            return;
        }

        foreach (var check in target.checks)
        {
            var go = Instantiate(checklistItemPrefab, checklistContainer);

            // 1) Texto: usamos TMP_Text para cubrir TextMeshPro/TextMeshProUGUI
            var tmpLabel = go.GetComponentInChildren<TMP_Text>();
            if (tmpLabel != null)
            {
                tmpLabel.text = check.displayName;
                tmpLabel.enableCulling = false;
                // Pequeño offset en Z para evitar z-fighting
                var t = tmpLabel.transform;
                t.localPosition += new Vector3(0f, 0f, -0.001f);
            }
            else
            {
                // Fallback a Text normal si el prefab no usa TMP
                var legacyLabel = go.GetComponentInChildren<Text>();
                if (legacyLabel != null)
                    legacyLabel.text = check.displayName;
            }

            // 2) Toggle
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

    // --------------------------------------------------------------------
    // ESTADO / TEXTOS
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

        // ⬇⬇⬇ LÓGICA DE BOTONES EXCLUYENTE ⬇⬇⬇

        // Solo se puede reportar cuando aún no hay decisión
        if (reportButton)
            reportButton.interactable = target.Reported == ReportedState.Unknown;

        // Solo se puede aprobar si:
        // - Sigue en Unknown (no se ha reportado ni aprobado)
        // - Todas las pruebas requeridas están hechas
        if (approveButton)
            approveButton.interactable =
                target.Reported == ReportedState.Unknown &&
                target.AllRequiredChecksDone;

        // Reemplazar solo si ya fue reportado como dañado
        if (replaceButton)
            replaceButton.interactable = target.Reported == ReportedState.ReportedDamaged;

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

    // --------------------------------------------------------------------
    // COLOCACIÓN EN EL MUNDO
    void SetupWorldAnchor(Transform anchor)
    {
        var camTransform = GetCameraTransform();

        // Punto base: parte alta del instrumento, centrada en XZ
        Vector3 top = GetInstrumentTopCenter(anchor);

        if (!camTransform)
        {
            anchoredPosition = top + Vector3.right * lateralOffset + Vector3.up * extraVerticalOffset;
            anchoredRotation = Quaternion.identity;
        }
        else
        {
            // Dirección cámara -> instrumento en el plano horizontal
            Vector3 camToInstrument = top - camTransform.position;
            camToInstrument.y = 0f;
            if (camToInstrument.sqrMagnitude < 0.001f)
                camToInstrument = camTransform.forward;
            camToInstrument.Normalize();

            // Derecha desde el punto de vista de la cámara
            Vector3 right = Vector3.Cross(Vector3.up, camToInstrument);
            right.Normalize();

            // Posición final: al lado y un poco por encima de la parte alta
            anchoredPosition = top + right * lateralOffset + Vector3.up * extraVerticalOffset;

            // Dirección cámara -> panel solo en horizontal
            Vector3 camToPanel = anchoredPosition - camTransform.position;
            camToPanel.y = 0f;
            if (camToPanel.sqrMagnitude < 0.001f)
                camToPanel = camTransform.forward;
            camToPanel.Normalize();

            anchoredRotation = Quaternion.LookRotation(camToPanel, Vector3.up);
        }

        hasAnchorPose = true;

        transform.position = anchoredPosition;
        transform.rotation = anchoredRotation;
    }

    Vector3 GetInstrumentTopCenter(Transform fallbackAnchor)
    {
        if (target != null)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }

            return target.transform.position;
        }

        if (fallbackAnchor != null)
            return fallbackAnchor.position;

        return transform.position;
    }

    void LateUpdate()
    {
        if (!hasAnchorPose) return;

        // Mantiene el panel quieto en la pose calculada
        transform.position = anchoredPosition;
        transform.rotation = anchoredRotation;
    }

    Transform GetCameraTransform()
    {
        if (Camera.main) return Camera.main.transform;

        var origin = FindFirstObjectByType<XROrigin>();
        if (origin && origin.Camera) return origin.Camera.transform;

        return null;
    }

    // --------------------------------------------------------------------
    // BOTONES
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
