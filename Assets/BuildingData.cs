using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBuilding", menuName = "Modular/Building Data")]
public class BuildingData : ScriptableObject
{
    [Serializable]
    public struct ModuleEntry
    {
        public GameObject prefab;
        public Vector3 position;
        public Quaternion rotation;
    }

    public List<ModuleEntry> modules = new();
}
