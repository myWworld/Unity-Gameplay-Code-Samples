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
/// 배치가능 자재를 실제로 배치하고 그래프 등록 배치 가능 여부 재검사후 실패시 롤백 성공 시 커밋 자원 내구도 소모
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

    public bool CanPlace(BuildingPreviewController preview)//배치 가능여부 반환
    {
        if (preview == null || !preview.HasMaterial || placementValidator == null)
        {
            return false;
        }

        GameObject materialObject = preview.CurrentGameObject;
        GameObject snappedTargetRoot = preview.ResolveSnappedTargetRoot();

        return placementValidator.IsPossibleToPlace(materialObject, snappedTargetRoot, preview.MousePosition, preview.PivotPosition);
    }

    public BuildingPlacementResult TryCommit(BuildingPreviewController preview, BuildingSystem owner)//배치 단계 실제 포지션 결정은 BuildOrRemove에서
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

        if (!placementValidator.CheckIfMeetRequirement(materialObject))//자재 있는지 체크
        {
            return BuildingPlacementResult.Failed();
        }

        bool requiresStructuralSupport = BuildingPlacementRules.RequiresStructuralSupport(material);//지지력이 필요한 자재인지 확인(횃불, 배는 필요 없음)
        float finalSupport = placementValidator.GetCachedSupportValue();//최종 예측 지지력

        if (requiresStructuralSupport && finalSupport < integritySolver.MinimumSupportValue)//기준 미달 시 실패
        {
            placementValidator.ResetCache();
            return BuildingPlacementResult.Failed();
        }

        bool graphLinksCreated = false;
        if (requiresStructuralSupport)//지지력 그래프에 현재 자재 등록
        {
            integritySolver.UpdateParentsAndChildren(material);
            graphLinksCreated = true;
        }

        if (!placementValidator.bConstructionMode &&
            !inventoryAdapter.TryConsumeRequirements(material.RequirementsForMat))//실제 자재 소모 가능한지
        {
            if (graphLinksCreated)
            {
                integritySolver.ClearParentAndChildren(material);//연결구조 롤백
            }

            placementValidator.ResetCache();
            return BuildingPlacementResult.Failed();
        }

        if (requiresStructuralSupport)
        {
            material.SupportValue = finalSupport;//예측 지지력 반영
            integritySolver.HandleMaterialPlacement(material);//지지력이 반영됐으므로 지지력 재전파과정
        }

        Quaternion preservedRotation = materialTransform.rotation;
        BuildingDataSO nextData = materialManagement.GetCurBuildingDataSO();

        owner.OnBuildAction();

        materialManagement.ActivateColliderAndLayer(materialObject);//프리뷰 자재 콜라이더 모두 재활성화
        buildOrRemove.PlaceMaterial(materialObject, preview.PivotPosition);//실제로 목표 위치에 배치
        material.ResetVisualTransform();//비쥬얼 트랜스폼을 원래 자리로
        ItemDurabilityUtility.TryConsumeEquipped(ItemDurabilityReason.BuildAction, owner);//내구도 소모

        ApplyDoorPlacement(material, materialTransform, preservedRotation);//문일 경우 각도 조절
        placementValidator.ResetCache();

        return new BuildingPlacementResult//배치 상태 반환
        {
            Succeeded = true,
            HasNextPreview = materialManagement.GetCurrentPoolCount() > 0 && nextData != null, //자재 계속해서 사용가능한지
            NextPreviewData = nextData,
            PreservedPreviewRotation = preservedRotation,
        };
    }

    private void ApplyDoorPlacement(
        IMaterial material,
        Transform materialTransform,
        Quaternion unsnappedRotation) ///문일경우 항상 닫힌 각도로 배치
    {
        if (material == null ||
            material.GetBuildingMaterialType() != eBuildingMaterial.Door ||
            !(material is Door door))
        {
            return;
        }

        if (snapController != null &&
            snapController.isSnapped && snapController.bestWorldSnap != null)
        {
            materialTransform.rotation =
                snapController.bestWorldSnap.transform.rotation * Quaternion.Euler(0f, 90f, 0f);

            door.SetLocalEulerWhenPlaced(materialTransform.localEulerAngles);
            return;
        }

        door.SetLocalEulerWhenPlaced(unsnappedRotation.eulerAngles);
    }
}

public static class BuildingPlacementRules//배치 관련 규칙
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
