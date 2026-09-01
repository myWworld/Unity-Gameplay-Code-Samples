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

        if (input.WasSecondaryActionPressed)//철거모드 진입
        {
            context.ChangeState(context.RemoveState);
            return;
        }

        if (context.curBuildingMode == BuildingSystem.eBuildingMode.ManualSnap &&
            input.WasCycleSnapPointPressed)//스냅 포인트 교체
        {
            context.ChangeSnapPoint(1);
        }

        if (input.WasToggleSnapModePressed)
        {
            context.ToggleSnapMode();//스냅모드 변환
        }

        context.PosUpdate();//위치
        context.MakeRotate();//회전 업데이트

        if (!context.IsPossibleToPlace() || !input.WasPlacePressed)//배치 검증 + 클릭 여부
        {
            return;
        }

        context.PlaceMaterial();//배치
        if (!context.IsHoldingMaterial())//자재 없다면 Idle 상태로
        {
            context.ChangeState(context.IdleState);
        }
    }

    public void Exit(BuildingSystem context)
    {
        context.ResetHighlitedObject();
    }
}
