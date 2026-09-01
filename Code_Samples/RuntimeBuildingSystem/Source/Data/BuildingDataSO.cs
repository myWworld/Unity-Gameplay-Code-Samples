using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Building System/Building Data")]
public class BuildingDataSO : ScriptableObject
{
    [Header("Basic Information")]
    public string materialName;
    public MaterialType materialType;//자재 타입(나무, 흙 등..)
    public eBuildingMaterial buildingMaterial;//자재 종류
    public GameObject prefab;

    [Header("Support Settings")]
    public float maxSupportWeight;
    public float baseSupportValue = 1.2f; // 지면 접촉 시 기본 지지력

    [Header("Requirements")]
    public List<ResourceRequirement> requirements;//설치 시 필요 조건

}

[System.Serializable]
public struct ResourceRequirement
{
    public string itemName;
    public int count;
}
