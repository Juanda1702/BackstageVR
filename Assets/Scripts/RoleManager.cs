using UnityEngine;
using UnityEngine.Events;

// Ya no hay varios roles; este manager solo coordina el escenario del acomodador.
public class RoleManager : MonoBehaviour
{
    [Tooltip("Objetos que deben estar activos durante todo el ejercicio del acomodador")]
    public GameObject[] objetosAcomodador;

    [Tooltip("Se dispara cuando el acomodador termina todas sus tareas (por ejemplo, todos los instrumentos colocados).")]
    public UnityEvent OnAcomodadorCompletado;

    void Start()
    {
        // Nos aseguramos de que todo lo necesario esté activo al iniciar
        Toggle(objetosAcomodador, true);
    }

    void Toggle(GameObject[] arr, bool on)
    {
        if (arr == null) return;
        foreach (var go in arr)
            if (go) go.SetActive(on);
    }

    // Llama a este método el AcomodadorChecklist cuando todos los instrumentos están en su sitio.
    public void AcomodadorListo()
    {
        OnAcomodadorCompletado?.Invoke();
        // Aquí puedes enganchar desde el inspector:
        // - Mostrar un panel de "Escenario listo"
        // - Guardar resultados
        // - Volver al menú, etc.
    }
}
