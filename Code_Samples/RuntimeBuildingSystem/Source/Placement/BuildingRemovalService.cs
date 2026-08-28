using Project.Gameplay.Items;
using UnityEngine;

/// <summary>
/// Resolves, highlights, and removes the current raycast target.
/// </summary>
public sealed class BuildingRemovalService
{
    private readonly BuildingInputHandler inputHandler;
    private readonly PlacementValidator placementValidator;
    private readonly BuildOrRemove buildOrRemove;

    public BuildingRemovalService(
        BuildingInputHandler inputHandler,
        PlacementValidator placementValidator,
        BuildOrRemove buildOrRemove)
    {
        this.inputHandler = inputHandler;
        this.placementValidator = placementValidator;
        this.buildOrRemove = buildOrRemove;
    }

    public void Tick(BuildingSystem owner)
    {
        if (owner == null || inputHandler == null ||
            placementValidator == null || buildOrRemove == null)
        {
            return;
        }

        inputHandler.UpdateInputData();//raycast 정보 업데이트
        GameObject removeTarget = ResolveMaterialRoot(inputHandler.RayCastedObject);//철거 대상의 root가져옴

        if (removeTarget == null || !placementValidator.IsRemovableLayer(removeTarget.layer))//레이어가 철거 가능한 레이어인지 체크
        {
            buildOrRemove.ResetRemoveCandidate();
            return;
        }

        buildOrRemove.RemoveCandidateColorChange(removeTarget, Color.blue);//철거 대상 색깔 파란색으로 변경
        if (inputHandler.WasPlacePressed)//클릭시
        {
            TryRemove(removeTarget, owner);//철거 진행
        }
    }

    public bool TryRemove(GameObject target, BuildingSystem owner)//철거 진행 로직
    {
        if (target == null || owner == null || buildOrRemove == null)
        {
            return false;
        }

        bool removed = buildOrRemove.TryRemoveMaterial(target);//실제로 지우는 과정
        if (!removed)//실패 했을 시
        {
            return false;
        }

        placementValidator?.ResetCache();//캐시 초기화
        ItemDurabilityUtility.TryConsumeEquipped(ItemDurabilityReason.BuildAction, owner);//철거 시 아이템 내구도 소모
        return true;
    }

    public void ClearCandidate()
    {
        buildOrRemove?.ResetRemoveCandidate();//파란색 표시가 있던 철거 대상 초기화
    }

    private static GameObject ResolveMaterialRoot(GameObject candidate)//자식 오브젝트가 들어왔어도 IMaterial이 있는 최상위 루트를 반환
    {
        if (candidate == null)
        {
            return null;
        }

        if (BuildingColliderUtility.TryResolveMaterialRoot(candidate, out GameObject materialRoot, out _))
        {
            return materialRoot;
        }

        return candidate;
    }
}
