// AcomodadorChecklist.cs
using UnityEngine;
using UnityEngine.Events;

public class AcomodadorChecklist : MonoBehaviour
{
    public InstrumentSnapTarget[] targets;
    public UnityEvent OnAllPlaced;

    int done;

    void Awake()
    {
        if (targets == null || targets.Length == 0)
            targets = GetComponentsInChildren<InstrumentSnapTarget>(true);
    }

    // llamado por InstrumentSnapTarget con SendMessage
    void OnInstrumentSnapped(InstrumentSnapTarget _)
    {
        done++;
        if (done >= targets.Length) OnAllPlaced?.Invoke();
    }
}
