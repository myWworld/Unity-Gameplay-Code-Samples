public class BuildingRemoveState : IBuildingState
{
    public void Enter(BuildingSystem context)
    {
        context.ClearPreviewSnapState();
        context.UpdateHighlightTarget(null);
        context.HideHeldPreviewForRemoval();
    }

    public void Update(BuildingSystem context)
    {
        BuildingInputHandler input = context.InputHandler;
        if (input == null)
        {
            return;
        }

        if (input.WasSecondaryActionPressed)
        {
            if (context.prevBuildingState == context.HoldingState && context.IsHoldingMaterial())
            {
                context.ChangeState(context.HoldingState);
            }
            else
            {
                context.GetBackToOtherMode();
                context.ChangeState(context.IdleState);
            }

            return;
        }

        context.ProcessRemoveMaterial();
    }

    public void Exit(BuildingSystem context)
    {
        context.ClearRemoveCandidate();
    }
}
