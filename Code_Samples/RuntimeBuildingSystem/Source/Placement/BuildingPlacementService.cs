using Project.Gameplay.Items;
using UnityEngine;

public struct BuildingPlacementResult
{
    public bool Succeeded;
    public bool HasNextPreview;
    public BuildingDataSO NextPreviewData;
    public Quaternion PreservedPreviewRotation;

    public static BuildingPlacementResult Failed()
    {
        return new BuildingPlacementResult { Succeeded = false };
    }
}

/// <summary>
/// Commits a validated preview into the runtime building graph and world.
/// Preview movement remains in BuildingPreviewController; this service owns the irreversible commit order.
/// </summary>
public sealed class BuildingPlacementService
{
    private readonly PlacementValidator placementValidator;
    private readonly StructuralIntegritySolver integritySolver;
    private readonly BuildingMaterialManagement materialManagement;
    private readonly BuildOrRemove buildOrRemove;
    private readonly PlayerInventoryAdapter inventoryAdapter;
    private readonly SnapController snapController;

    public BuildingPlacementService(
        PlacementValidator placementValidator,
        StructuralIntegritySolver integritySolver,
        BuildingMaterialManagement materialManagement,
        BuildOrRemove buildOrRemove,
        PlayerInventoryAdapter inventoryAdapter,
        SnapController snapController)
    {
        this.placementValidator = placementValidator;
        this.integritySolver = integritySolver;
        this.materialManagement = materialManagement;
        this.buildOrRemove = buildOrRemove;
        this.inventoryAdapter = inventoryAdapter;
        this.snapController = snapController;
    }

    public bool CanPlace(BuildingPreviewController preview)
    {
        if (preview == null || !preview.HasMaterial || placementValidator == null)
        {
            return false;
        }

        GameObject materialObject = preview.CurrentGameObject;
        GameObject snappedTargetRoot = preview.ResolveSnappedTargetRoot();
        return placementValidator.IsPossibleToPlace(
            materialObject,
            snappedTargetRoot,
            preview.MousePosition,
            preview.PivotPosition);
    }

    public BuildingPlacementResult TryCommit(BuildingPreviewController preview, BuildingSystem owner)
    {
        if (preview == null || !preview.HasMaterial || owner == null ||
            placementValidator == null || integritySolver == null ||
            materialManagement == null || buildOrRemove == null || inventoryAdapter == null)
        {
            return BuildingPlacementResult.Failed();
        }

        IMaterial material = preview.CurrentMaterial;
        GameObject materialObject = preview.CurrentGameObject;
        Transform materialTransform = preview.CurrentTransform;
        if (material == null || materialObject == null || materialTransform == null)
        {
            return BuildingPlacementResult.Failed();
        }

        if (!placementValidator.CheckIfMeetRequirement(materialObject))
        {
            return BuildingPlacementResult.Failed();
        }

        bool requiresStructuralSupport = BuildingPlacementRules.RequiresStructuralSupport(material);
        float finalSupport = placementValidator.GetCachedSupportValue();

        if (requiresStructuralSupport && finalSupport < integritySolver.MinimumSupportValue)
        {
            placementValidator.ResetCache();
            return BuildingPlacementResult.Failed();
        }

        bool graphLinksCreated = false;
        if (requiresStructuralSupport)
        {
            integritySolver.UpdateParentsAndChildren(material);
            graphLinksCreated = true;
        }

        if (!placementValidator.bConstructionMode &&
            !inventoryAdapter.TryConsumeRequirements(material.RequirementsForMat))
        {
            if (graphLinksCreated)
            {
                integritySolver.ClearParentAndChildren(material);
            }

            placementValidator.ResetCache();
            return BuildingPlacementResult.Failed();
        }

        if (requiresStructuralSupport)
        {
            material.SupportValue = finalSupport;
            integritySolver.HandleMaterialPlacement(material);
        }

        Quaternion preservedRotation = materialTransform.rotation;
        BuildingDataSO nextData = materialManagement.GetCurBuildingDataSO();

        owner.OnBuildAction();
        materialManagement.ActivateColliderAndLayer(materialObject);
        buildOrRemove.PlaceMaterial(materialObject, preview.PivotPosition);
        material.ResetVisualTransform();
        ItemDurabilityUtility.TryConsumeEquipped(ItemDurabilityReason.BuildAction, owner);

        ApplyDoorPlacement(material, materialTransform, preservedRotation);
        placementValidator.ResetCache();

        return new BuildingPlacementResult
        {
            Succeeded = true,
            HasNextPreview = materialManagement.GetCurrentPoolCount() > 0 && nextData != null,
            NextPreviewData = nextData,
            PreservedPreviewRotation = preservedRotation,
        };
    }

    private void ApplyDoorPlacement(
        IMaterial material,
        Transform materialTransform,
        Quaternion unsnappedRotation)
    {
        if (material == null ||
            material.GetBuildingMaterialType() != eBuildingMaterial.Door ||
            !(material is Door door))
        {
            return;
        }

        if (snapController != null &&
            snapController.isSnapped &&
            snapController.bestWorldSnap != null)
        {
            materialTransform.rotation =
                snapController.bestWorldSnap.transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            door.SetLocalEulerWhenPlaced(materialTransform.localEulerAngles);
            return;
        }

        door.SetLocalEulerWhenPlaced(unsnappedRotation.eulerAngles);
    }
}

public static class BuildingPlacementRules
{
    public static bool RequiresStructuralSupport(IMaterial material)
    {
        if (material == null)
        {
            return false;
        }

        eBuildingMaterial type = material.GetBuildingMaterialType();
        return type != eBuildingMaterial.Torch && type != eBuildingMaterial.Boat;
    }
}
