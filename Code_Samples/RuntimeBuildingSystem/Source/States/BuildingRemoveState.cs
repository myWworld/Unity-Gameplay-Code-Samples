using UnityEngine;

public class BuildingRemoveState : IBuildingState
{
    public void Enter(BuildingSystem context)
    {
        UnityEngine.Debug.Log("[State] 철거 모드 진입");
       
        context.snapController.ClearSnapState();
        context.UpdateHighlightTarget(null);

        if (context.IsHoldingMaterial())
        {
            GameObject gameObject = context.GetCurMaterial().GetGameObject();
            gameObject.SetActive(false);
        }
    }

    // 매 프레임 호출 (기존 Update 역할) 
    public void Update(BuildingSystem context)
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (context.prevBuildingState == context.HoldingState)
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

    // 상태를 빠져나갈 때 1회 호출
    public void Exit(BuildingSystem context)
    {
        context.buildOrRemove.ResetRemoveCandidate();
    }
}
