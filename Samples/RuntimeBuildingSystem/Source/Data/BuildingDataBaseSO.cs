using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Building System/BuildingDataBaseSO")]
public class BuildingDataBaseSO : ScriptableObject
{
    [Header("Building Data SOs")]
    public List<BuildingDataSO> BuildingDatas;

    private Dictionary<eBuildingMaterial, BuildingDataSO> buildingDataCache;

    private void InitializeCache()
    {
        if (buildingDataCache != null) return;

        buildingDataCache = new Dictionary<eBuildingMaterial, BuildingDataSO>();
        foreach (var data in BuildingDatas)
        {
            if (data != null && !buildingDataCache.ContainsKey(data.buildingMaterial))
            {
                buildingDataCache.Add(data.buildingMaterial, data);
            }
        }
    }



    public BuildingDataSO GetBuildingData(eBuildingMaterial buildingMaterial)
    {
        InitializeCache();

        if (buildingDataCache.TryGetValue(buildingMaterial, out BuildingDataSO data))
        {
            return data;
        }

        UnityEngine.Debug.LogWarning($"[BuildingDataBaseSO] 해당 자재({buildingMaterial})가 데이터베이스에 등록되어 있지 않습니다");
        return null;
    }

}
