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
    private Quaternion lastCheckedRotation = Quaternion.identity;
    private bool lastCheckedConstructionMode;
    private int lastCheckedMaterialId;
    private int lastCheckedTargetId;
    private bool cachedGeometryAndSupportResult;
    private float cachedSupportValue = 1f;

    public void InitializeDependencies(
        BuildingMaterialManagement materialManagement,
        StructuralIntegritySolver structuralSolver,
        SnapController snap,
        BuildOrRemove buildFeedback,
        PlayerInventoryAdapter inventory)
    {
        buildingMaterialManagement = materialManagement;
        integritySolver = structuralSolver;
        snapController = snap;
        buildOrRemove = buildFeedback;
        inventoryAdapter = inventory;
        ResetCache();
    }

    public bool CheckIfMeetRequirement(BuildingDataSO data)
    {
        if (bConstructionMode)
        {
            return true;
        }

        if (data == null)
        {
            NotifyMissingRequirement(true);
            return false;
        }

        EnsureInventoryAdapter();
        bool hasRequirements = inventoryAdapter != null && inventoryAdapter.HasRequirements(data.requirements);
        if (!hasRequirements)
        {
            NotifyMissingRequirement(true);
        }

        return hasRequirements;
    }

    public bool CheckIfMeetRequirement(GameObject currentMaterial)
    {
        if (bConstructionMode)
        {
            return true;
        }

        if (currentMaterial == null || !currentMaterial.TryGetComponent(out IMaterial material))
        {
            NotifyMissingRequirement(true);
            return false;
        }

        return CheckMaterialRequirements(material, notifyIfMissing: true);
    }

    public bool IsPossibleToPlace(
        GameObject currentMaterial,
        GameObject snappedTarget,
        Vector3 mousePosition,
        Vector3 pivotPosition)
    {
        if (currentMaterial == null || !currentMaterial.activeSelf ||
            buildOrRemove == null || snapController == null || integritySolver == null ||
            buildingMaterialManagement == null)
        {
            return false;
        }

        if (!currentMaterial.TryGetComponent(out IMaterial material))
        {
            return false;
        }

        buildOrRemove.UpdatePreview(currentMaterial, snappedTarget);

        int materialId = currentMaterial.GetInstanceID();
        int targetId = snappedTarget != null ? snappedTarget.GetInstanceID() : 0;
        bool cacheHit =
            materialId == lastCheckedMaterialId &&
            targetId == lastCheckedTargetId &&
            (pivotPosition - lastCheckedPivotPos).sqrMagnitude < 0.0005f &&
            Quaternion.Angle(currentMaterial.transform.rotation, lastCheckedRotation) < 0.1f &&
            lastCheckedConstructionMode == bConstructionMode;

        if (!cacheHit)
        {
            lastCheckedMaterialId = materialId;
            lastCheckedTargetId = targetId;
            lastCheckedPivotPos = pivotPosition;
            lastCheckedRotation = currentMaterial.transform.rotation;
            lastCheckedConstructionMode = bConstructionMode;

            EvaluateGeometryAndSupport(material, currentMaterial, mousePosition, pivotPosition);
        }

        // Resources can change while the preview pose remains cached, so this check is intentionally not cached.
        bool hasResources = bConstructionMode || CheckMaterialRequirements(material, notifyIfMissing: false);
        bool canPlace = cachedGeometryAndSupportResult && hasResources;
        return buildOrRemove.UpdatePreviewHighlight(canPlace, cachedSupportValue);
    }

    public float GetCachedSupportValue()
    {
        return cachedSupportValue;
    }

    public void ResetCache()
    {
        lastCheckedPivotPos = Vector3.negativeInfinity;
        lastCheckedRotation = Quaternion.identity;
        lastCheckedConstructionMode = bConstructionMode;
        lastCheckedMaterialId = 0;
        lastCheckedTargetId = 0;
        cachedGeometryAndSupportResult = false;
        cachedSupportValue = integritySolver != null ? integritySolver.BaseSupportValue : 1f;
    }

    public bool IsRemovableLayer(int layer)
    {
        return layer == LayerAndTagConstants.Layer_Building ||
               layer == LayerAndTagConstants.Layer_Door;
    }

    private void EvaluateGeometryAndSupport(
        IMaterial material,
        GameObject materialObject,
        Vector3 mousePosition,
        Vector3 pivotPosition)
    {
        bool isBoat = material.GetBuildingMaterialType() == eBuildingMaterial.Boat;
        if (isBoat)
        {
            bool isOnWater = WaterSystem.IsPositionOnWaterSurface(mousePosition);
            cachedGeometryAndSupportResult =
                isOnWater && snapController.CanPlaceMaterial(mousePosition, materialObject);
            cachedSupportValue = integritySolver.BaseSupportValue;
            return;
        }

        cachedSupportValue = integritySolver.PredictSupportValue(
            pivotPosition,
            materialObject,
            buildingMaterialManagement);

        cachedGeometryAndSupportResult =
            snapController.CanPlaceMaterial(mousePosition, materialObject) &&
            cachedSupportValue >= integritySolver.MinimumSupportValue;
    }

    private bool CheckMaterialRequirements(IMaterial material, bool notifyIfMissing)
    {
        if (bConstructionMode)
        {
            return true;
        }

        if (material == null)
        {
            NotifyMissingRequirement(notifyIfMissing);
            return false;
        }

        EnsureInventoryAdapter();
        bool hasRequirements =
            inventoryAdapter != null && inventoryAdapter.HasRequirements(material.RequirementsForMat);

        if (!hasRequirements)
        {
            NotifyMissingRequirement(notifyIfMissing);
        }

        return hasRequirements;
    }

    private void NotifyMissingRequirement(bool shouldNotify)
    {
        if (shouldNotify && showLegacyLackOfRequirementNotification)
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
}
