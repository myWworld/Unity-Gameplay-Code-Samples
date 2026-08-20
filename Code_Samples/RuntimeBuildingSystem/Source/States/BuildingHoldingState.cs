public class BuildingHoldingState : IBuildingState
{
    public void Enter(BuildingSystem context)
    {
        context.ShowMaterial();
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
            context.ChangeState(context.RemoveState);
            return;
        }

        if (context.curBuildingMode == BuildingSystem.eBuildingMode.ManualSnap &&
            input.WasCycleSnapPointPressed)
        {
            context.ChangeSnapPoint(1);
        }

        if (input.WasToggleSnapModePressed)
        {
            context.ToggleSnapMode();
        }

        context.PosUpdate();
        context.MakeRotate();

        if (!context.IsPossibleToPlace() || !input.WasPlacePressed)
        {
            return;
        }

        context.PlaceMaterial();
        if (!context.IsHoldingMaterial())
        {
            context.ChangeState(context.IdleState);
        }
    }

    public void Exit(BuildingSystem context)
    {
        context.ResetHighlitedObject();
    }
}
