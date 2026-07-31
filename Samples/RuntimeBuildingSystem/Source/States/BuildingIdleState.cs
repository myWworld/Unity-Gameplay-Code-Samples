using UnityEngine;

public class BuildingIdleState : IBuildingState
{
    public void Enter(BuildingSystem context)
    {
        UnityEngine.Debug.Log("[State] 건축 대기 모드 진입");

        context.GetBackToOtherMode(); // 혹시 들고 있던 게 있다면 초기화
    }

    public void Update(BuildingSystem context)
    {
        // 대기 상태에서 우클릭 시 철거 모드로 전환
        if (context.IsBuildToolEquipped && Input.GetMouseButtonDown(1))
        {
            context.ChangeState(context.RemoveState);
        }
    }

    public void Exit(BuildingSystem context)
    {

    }
}
