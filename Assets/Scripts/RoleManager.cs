using UnityEngine;
using UnityEngine.Events;

public enum Rol { Acomodador, Seguridad }

public class RoleManager : MonoBehaviour
{
    public Rol rolInicial = Rol.Acomodador;
    public GameObject[] habilitarAcomodador;   // objetos UI / sockets instrumentos
    public GameObject[] habilitarSeguridad;    // sockets vallas, etc.

    public UnityEvent OnAcomodadorCompletado;
    public UnityEvent OnSeguridadCompletado;

    Rol rolActual;

    void Start() => SetRol(rolInicial);

    public void SetRol(Rol r)
    {
        rolActual = r;
        Toggle(habilitarAcomodador, r == Rol.Acomodador);
        Toggle(habilitarSeguridad, r == Rol.Seguridad);
    }

    void Toggle(GameObject[] arr, bool on)
    {
        foreach (var go in arr) if (go) go.SetActive(on);
    }

    // Llaman estos métodos los “checklists” al terminar
    public void AcomodadorListo() { OnAcomodadorCompletado?.Invoke(); SetRol(Rol.Seguridad); }
    public void SeguridadListo() { OnSeguridadCompletado?.Invoke(); }
}
