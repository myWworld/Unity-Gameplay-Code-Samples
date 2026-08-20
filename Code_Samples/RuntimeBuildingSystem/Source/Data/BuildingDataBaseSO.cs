using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Building System/BuildingDataBaseSO")]
public class BuildingDataBaseSO : ScriptableObject
{
    [Header("Building Data SOs")]
    public List<BuildingDataSO> BuildingDatas;

    private Dictionary<eBuildingMaterial, BuildingDataSO> buildingDataCache;

    private void OnEnable()
    {
        buildingDataCache = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        buildingDataCache = null;
    }
#endif

    public BuildingDataSO GetBuildingData(eBuildingMaterial buildingMaterial)
    {
        EnsureCache();
        if (buildingDataCache.TryGetValue(buildingMaterial, out BuildingDataSO data))
        {
            return data;
        }

        Debug.LogWarning(
            $"[BuildingDataBaseSO] Building material ({buildingMaterial}) is not registered.");
        return null;
    }

    private void EnsureCache()
    {
        if (buildingDataCache != null)
        {
            return;
        }

        buildingDataCache = new Dictionary<eBuildingMaterial, BuildingDataSO>();
        if (BuildingDatas == null)
        {
            return;
        }

        for (int i = 0; i < BuildingDatas.Count; i++)
        {
            BuildingDataSO data = BuildingDatas[i];
            if (data != null && !buildingDataCache.ContainsKey(data.buildingMaterial))
            {
                buildingDataCache.Add(data.buildingMaterial, data);
            }
        }
    }
}
