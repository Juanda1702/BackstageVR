using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
    [Tooltip("Desplazamiento lateral respecto a la posición base del panel (hacia la derecha desde el punto de vista de la cámara).")]
    public float lateralOffset = 0.0f;

    [Tooltip("Desplazamiento extra por encima de la posición base del panel.")]
    public float extraVerticalOffset = 0.0f;

    [Header("Movimiento manual del panel")]
    [Tooltip("Si está activo, el usuario puede agarrar y reposicionar el panel libremente.")]
    public bool allowUserReposition = true;

    InspectableInstrument target;

    // Pose anclada en el mundo
    Vector3 anchoredPosition;
    Quaternion anchoredRotation;
    bool hasAnchorPose;

    // Movimiento manual
    XRGrabInteractable grabInteractable;
    bool userHasRepositioned = false;

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

        // Buscar XRGrabInteractable si existe en el panel
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
        }

        WireButtons();
        BuildChecklistUI();
        Refresh();

        // Calcula la posición fija inicial del panel al lado del instrumento
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

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }
    }

    // --------------------------------------------------------------------
    // EVENTO CUANDO EL USUARIO AGARRA EL PANEL
    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (!allowUserReposition)
            return;

        // Desde este momento dejamos de forzar la posición/rotación anclada
        userHasRepositioned = true;
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

        // LÓGICA DE BOTONES EXCLUYENTE
        if (reportButton)
            reportButton.interactable = target.Reported == ReportedState.Unknown;

        if (approveButton)
            approveButton.interactable =
                target.Reported == ReportedState.Unknown &&
                target.AllRequiredChecksDone;

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
    /// <summary>
    /// Calcula una pose inicial en el mundo para el panel:
    /// - Usa el uiAnchor (o el instrumento) como base.
    /// - Le aplica el offset local definido en el InspectableInstrument.
    /// - Opcionalmente aplica un desplazamiento lateral y vertical en función de la cámara.
    /// </summary>
    void SetupWorldAnchor(Transform anchor)
    {
        var camTransform = GetCameraTransform();

        // 1) Posición base: uiAnchor (si existe) o instrumento
        Vector3 basePos;
        Transform offsetRef = null;

        if (anchor != null)
        {
            basePos = anchor.position;
            offsetRef = anchor;
        }
        else if (target != null)
        {
            basePos = target.transform.position;
            offsetRef = target.transform;
        }
        else
        {
            basePos = transform.position;
        }

        // 2) Offset local por instrumento
        Vector3 customOffsetWorld = Vector3.zero;
        if (target != null && offsetRef != null)
        {
            customOffsetWorld = offsetRef.TransformVector(target.inspectionPanelLocalOffset);
        }

        Vector3 rawPos = basePos + customOffsetWorld;

        // 3) Ajuste en función de la cámara
        if (!camTransform)
        {
            anchoredPosition = rawPos + Vector3.right * lateralOffset + Vector3.up * extraVerticalOffset;
            anchoredRotation = Quaternion.identity;
        }
        else
        {
            // Dirección cámara -> punto base en el plano horizontal
            Vector3 camToRaw = rawPos - camTransform.position;
            camToRaw.y = 0f;
            if (camToRaw.sqrMagnitude < 0.001f)
                camToRaw = camTransform.forward;
            camToRaw.Normalize();

            // Derecha desde el punto de vista de la cámara
            Vector3 right = Vector3.Cross(Vector3.up, camToRaw);
            right.Normalize();

            // Posición final: rawPos + offset lateral / vertical
            anchoredPosition = rawPos + right * lateralOffset + Vector3.up * extraVerticalOffset;

            // Orientar el panel hacia la cámara en horizontal
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

    void LateUpdate()
    {
        if (!hasAnchorPose)
            return;

        // Si no permitimos reposicionamiento manual, mantener anclado siempre
        if (!allowUserReposition)
        {
            transform.position = anchoredPosition;
            transform.rotation = anchoredRotation;
            return;
        }

        // Si permitimos reposicionamiento pero el usuario todavía no lo ha movido,
        // mantenemos el panel en la posición inicial para que no se desplace solo.
        if (!userHasRepositioned)
        {
            transform.position = anchoredPosition;
            transform.rotation = anchoredRotation;
        }

        // Si userHasRepositioned == true, no tocamos posición/rotación:
        // el panel se queda donde el usuario lo dejó.
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
