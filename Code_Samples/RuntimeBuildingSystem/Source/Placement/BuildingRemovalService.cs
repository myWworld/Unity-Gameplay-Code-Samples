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

        inputHandler.UpdateInputData();
        GameObject removeTarget = ResolveMaterialRoot(inputHandler.RayCastedObject);

        if (removeTarget == null || !placementValidator.IsRemovableLayer(removeTarget.layer))
        {
            buildOrRemove.ResetRemoveCandidate();
            return;
        }

        buildOrRemove.RemoveCandidateColorChange(removeTarget, Color.blue);
        if (inputHandler.WasPlacePressed)
        {
            TryRemove(removeTarget, owner);
        }
    }

    public bool TryRemove(GameObject target, BuildingSystem owner)
    {
        if (target == null || owner == null || buildOrRemove == null)
        {
            return false;
        }

        bool removed = buildOrRemove.TryRemoveMaterial(target);
        if (!removed)
        {
            return false;
        }

        placementValidator?.ResetCache();
        ItemDurabilityUtility.TryConsumeEquipped(ItemDurabilityReason.BuildAction, owner);
        return true;
    }

    public void ClearCandidate()
    {
        buildOrRemove?.ResetRemoveCandidate();
    }

    private static GameObject ResolveMaterialRoot(GameObject candidate)
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
