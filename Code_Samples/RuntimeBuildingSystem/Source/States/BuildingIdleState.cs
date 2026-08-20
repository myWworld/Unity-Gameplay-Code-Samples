public class BuildingIdleState : IBuildingState
{
    public void Enter(BuildingSystem context)
    {
        context.GetBackToOtherMode();
    }

    public void Update(BuildingSystem context)
    {
        BuildingInputHandler input = context.InputHandler;
        if (context.IsBuildToolEquipped && input != null && input.WasSecondaryActionPressed)
        {
            context.ChangeState(context.RemoveState);
        }
    }

    public void Exit(BuildingSystem context)
    {
    }
}
