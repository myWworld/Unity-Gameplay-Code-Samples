using UnityEngine;

public class BuildingHoldingState : IBuildingState
{
    public void Enter(BuildingSystem context)
    {
        UnityEngine.Debug.Log("[State] 자재 홀딩(건축) 모드 진입");
        context.ShowMaterial(); // 자재 화면에 띄우기
    }

    // 매 프레임 호출 (기존 Update 역할)
    public void Update(BuildingSystem context)
    {
        if (Input.GetMouseButtonDown(1))
        {
            context.ChangeState(context.RemoveState);
            return;
        }

        if (context.curBuildingMode == BuildingSystem.eBuildingMode.ManualSnap && Input.GetKeyDown(KeyCode.E))
        {
            context.ChangeSnapPoint(1);
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            context.ToggleSnapMode();
        }

        context.PosUpdate();
        context.MakeRotate();

        if (context.IsPossibleToPlace())
        {
            // 배치가 가능할 때 좌클릭을 누르면 설치

            if (Input.GetMouseButtonDown(0))
            {

                context.PlaceMaterial();

                // 설치 후 자재가 소진되었다면 Idle 상태로 돌아감
                if (!context.IsHoldingMaterial())
                {
                    context.ChangeState(context.IdleState);
                    return;
                }
            }
        }
    }

    // 상태를 빠져나갈 때 1회 호출
    public void Exit(BuildingSystem context)
    {
        context.ResetHighlitedObject();
    }

}
