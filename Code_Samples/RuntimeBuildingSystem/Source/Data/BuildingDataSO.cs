using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Building System/Building Data")]
public class BuildingDataSO : ScriptableObject
{
    [Header("Basic Information")]
    public string materialName;
    public MaterialType materialType;
    public eBuildingMaterial buildingMaterial;
    public GameObject prefab;

    [Header("Support Settings")]
    public float maxSupportWeight;
    public float baseSupportValue = 1.2f; // 지면 접촉 시 기본 지지력

    [Header("Requirements")]
    public List<ResourceRequirement> requirements;

}

[System.Serializable]
public struct ResourceRequirement
{
    public string itemName;
    public int count;
}
