// ReplacementService.cs
using System.Collections.Generic;
using UnityEngine;

public class ReplacementService : MonoBehaviour
{
    public static ReplacementService Instance { get; private set; }

    [System.Serializable]
    public struct Entry { public InstrumentType type; public GameObject goodPrefab; }

    public List<Entry> catalog = new();

    Dictionary<InstrumentType, GameObject> map;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        map = new Dictionary<InstrumentType, GameObject>();
        foreach (var e in catalog) if (e.goodPrefab) map[e.type] = e.goodPrefab;
    }

    public GameObject GetGoodPrefab(InstrumentType t) => map != null && map.TryGetValue(t, out var p) ? p : null;
}
