
public interface IBuildingState
{
    // 상태에 진입할 때 1회 호출
    void Enter(BuildingSystem context);

    // 매 프레임 호출 (기존 Update 역할)
    void Update(BuildingSystem context);

    // 상태를 빠져나갈 때 1회 호출
    void Exit(BuildingSystem context);
}
