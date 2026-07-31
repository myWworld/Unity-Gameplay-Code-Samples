using KWS;
using MalbersAnimations.Events;
using UnityEngine;

public class PlacementValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BuildingMaterialManagement buildingMaterialManagement;
    [SerializeField] private StructuralIntegritySolver integritySolver;
    [SerializeField] private SnapController snapController;
    [SerializeField] private BuildOrRemove buildOrRemove;
    [SerializeField] private PlayerInventoryAdapter inventoryAdapter;

    [Header("Settings")]
    public bool bConstructionMode = false;
    [SerializeField] private bool showLegacyLackOfRequirementNotification = false;
    public MEvent OnReqIsNotEnough;

    [Header("Cache")]
    private Vector3 lastCheckedPivotPos = Vector3.negativeInfinity;
    private Quaternion lastCheckedRot = Quaternion.identity;
    private bool lastCheckedConstructionMode = false;
    private bool cachedIsPossibleToPlace = false;
    private float cachedSupportValue = 1.0f;

    public void InitializeDependencies(BuildingMaterialManagement bmm, StructuralIntegritySolver sis, SnapController sc, BuildOrRemove bor, PlayerInventoryAdapter pda)
    {
        buildingMaterialManagement = bmm;
        integritySolver = sis;
        snapController = sc;
        buildOrRemove = bor;
        inventoryAdapter = pda;
    }


    public bool CheckIfMeetRequirement(BuildingDataSO data)
    {
        if (bConstructionMode) return true;

        IMaterial imat = buildingMaterialManagement.GetMaterialFromPool(data);
         if (imat == null) return false;

        GameObject curMat = imat.GetGameObject();
        if (curMat == null) return false;

        bool isMet = CheckMaterialRequirements(imat, notifyIfMissing: true);
        buildingMaterialManagement.BackToMaterialPool(curMat);
        return isMet;
    }

    // Validate the resource requirements of the currently held material.
    public bool CheckIfMeetRequirement(GameObject curMaterial)
    {
        if (bConstructionMode) return true;
        if (curMaterial == null) return false;

        return CheckMaterialRequirements(curMaterial.GetComponent<IMaterial>(), notifyIfMissing: true);
    }

    private bool CheckMaterialRequirements(IMaterial imat, bool notifyIfMissing)
    {
        if (bConstructionMode) return true;
        if (imat == null) return false;

        EnsureInventoryAdapter();
        if (inventoryAdapter == null)
        {
            NotifyMissingRequirement(notifyIfMissing);
            return false;
        }

        foreach (var req in imat.RequirementsForMat)
        {
            UnityEngine.Debug.Log($"Item Name : {req.Key} / cur count : {inventoryAdapter.GetItemCount(req.Key)} / need count : {req.Value}");
            if (inventoryAdapter.GetItemCount(req.Key) < req.Value)
            {
                NotifyMissingRequirement(notifyIfMissing);
                return false;
            }
        }
        return true;
    }

    private void NotifyMissingRequirement(bool notifyIfMissing)
    {
        if (notifyIfMissing && showLegacyLackOfRequirementNotification)
        {
            OnReqIsNotEnough?.Invoke();
        }
    }

    private void EnsureInventoryAdapter()
    {
        if (inventoryAdapter != null)
        {
            return;
        }

        inventoryAdapter = GetComponent<PlayerInventoryAdapter>();
        if (inventoryAdapter == null)
        {
            inventoryAdapter = GetComponentInParent<PlayerInventoryAdapter>();
        }

        if (inventoryAdapter == null)
        {
            inventoryAdapter = FindFirstObjectByType<PlayerInventoryAdapter>(FindObjectsInactive.Include);
        }
    }

    public bool IsPossibleToPlace(GameObject curMaterial, GameObject snappedTarget, Vector3 mousePos, Vector3 pivotPos)
    {
        if (curMaterial == null || !curMaterial.activeSelf) return false;

        buildOrRemove.UpdatePreview(curMaterial, snappedTarget);
        bool constructionMode = bConstructionMode;

        if ((pivotPos - lastCheckedPivotPos).sqrMagnitude < 0.0005f &&
            Quaternion.Angle(curMaterial.transform.rotation, lastCheckedRot) < 0.1f &&
            lastCheckedConstructionMode == constructionMode)
        {
            return buildOrRemove.UpdatePreviewHighlight(cachedIsPossibleToPlace, cachedSupportValue);
        }

        lastCheckedPivotPos = pivotPos;
        lastCheckedRot = curMaterial.transform.rotation;
        lastCheckedConstructionMode = constructionMode;

        IMaterial matComponent = curMaterial.GetComponent<IMaterial>();
        bool isBoat = (matComponent.GetBuildingMaterialType() == eBuildingMaterial.Boat);

        if(isBoat)
        {

            bool isOnWater = WaterSystem.IsPositionOnWaterSurface(mousePos);

            cachedIsPossibleToPlace = isOnWater && snapController.CanPlaceMaterial(mousePos, curMaterial);
            cachedSupportValue = 1.0f;

          //  UnityEngine.Debug.Log($"[보트테스트] {isOnWater}");
        }
        else
        {
            cachedSupportValue = integritySolver.PredictSupportValue(pivotPos, curMaterial, buildingMaterialManagement);
            cachedIsPossibleToPlace = snapController.CanPlaceMaterial(mousePos, curMaterial);


            if (cachedSupportValue < 0.25f)
            {
                cachedIsPossibleToPlace = false;
            }
        }

        if (!constructionMode && !CheckMaterialRequirements(curMaterial.GetComponent<IMaterial>(), notifyIfMissing: false))
        {
            cachedIsPossibleToPlace = false;
        }


        return buildOrRemove.UpdatePreviewHighlight( cachedIsPossibleToPlace, cachedSupportValue);
    }


    public float GetCachedSupportValue() => cachedSupportValue;

    public void ResetCache()
    {
        lastCheckedPivotPos = Vector3.negativeInfinity;
    }

    public bool IsRemovableLayer(int layer)
    {
        return (layer == LayerAndTagConstants.Layer_Building ||
                layer == LayerAndTagConstants.Layer_Door);
                //layer ==LayerAndTagConstants.Layer_Worktable ||
                //layer ==LayerAndTagConstants.Layer_Furnace ||
                //layer ==LayerAndTagConstants.Layer_Agungi);
    }
}
