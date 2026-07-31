# Code Review Guide

## 5분 검토 경로

1. 루트 [`README.md`](../README.md)의 세 시스템 요약을 확인합니다.
2. [`StructuralIntegritySolver.cs`](../Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs)에서 `PredictSupportValue`, `HandleMaterialPlacement`, `HandleMaterialPropagate`, `UpdateParentsAndChildren`을 확인합니다.
3. [`UtilitySelectorNode.cs`](../Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs)에서 재평가 Timer, `CanInterrupt()`, `inertiaBonus`, 최댓값 선택을 확인합니다.
4. [`BossSweepDamager.cs`](../Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs)에서 이전-현재 위치 SphereCast와 인접 세그먼트 Capsule Overlap을 확인합니다.

## 15~30분 상세 검토 경로

### Runtime Building System

| 순서 | 파일/메서드 | 확인할 내용 |
|---|---|---|
| 1 | [`IMaterial.cs`](../Samples/RuntimeBuildingSystem/Source/BuildingMaterials/IMaterial.cs) | 연결 그래프와 자재 계약이 어떤 데이터로 구성되는지 |
| 2 | [`BuildingSystem.PlaceMaterial`](../Samples/RuntimeBuildingSystem/Source/Core/BuildingSystem.cs) | 검증 → 연결 갱신 → 지지력 확정 → 자원 소모 → Pool 교체 순서 |
| 3 | [`PlacementValidator.IsPossibleToPlace`](../Samples/RuntimeBuildingSystem/Source/Placement/PlacementValidator.cs) | 프리뷰 캐시와 실제 상태 변경 없는 예측 |
| 4 | [`SnapController`](../Samples/RuntimeBuildingSystem/Source/Placement/SnapController.cs) | 자동/수동/자유 스냅과 Anchor/Pivot Offset 처리 |
| 5 | [`StructuralIntegritySolver`](../Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs) | 국소 클러스터 수집, 지면 Seed, 최대 지지 경로 전파, 붕괴 Queue |
| 6 | [`PlayerInventoryAdapter`](../Samples/RuntimeBuildingSystem/Source/Inventory/PlayerInventoryAdapter.cs) | 새/레거시 인벤토리 구현을 건축 로직과 분리한 방식 |

### Behavior Tree + Utility AI

| 순서 | 파일/메서드 | 확인할 내용 |
|---|---|---|
| 1 | [`Node.Evaluate/Stop`](../Samples/BehaviorTreeUtilityAI/Source/Core/Node.cs) | 정상 종료와 강제 중단의 차이 |
| 2 | [`Sequence`](../Samples/BehaviorTreeUtilityAI/Source/Composite/Sequence.cs)와 [`ReactiveSequenceNode`](../Samples/BehaviorTreeUtilityAI/Source/Composite/ReactiveSequenceNode.cs) | Stateful 진행과 매 Tick 재검사의 차이 |
| 3 | [`Selector`](../Samples/BehaviorTreeUtilityAI/Source/Composite/Selector.cs) | 제어권 변경 시 기존 노드 Abort |
| 4 | [`UtilitySelectorNode`](../Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs) | 행동 전환 안정화와 인터럽트 정책 |
| 5 | [`CompositeScorer`](../Samples/BehaviorTreeUtilityAI/Source/Scoring/CompositeScorer.cs) | 여러 평가값을 조합하고 범위를 정규화하는 방식 |
| 6 | [`ActionJumpAttackNode`](../Samples/BehaviorTreeUtilityAI/Source/ExampleActions/ActionJumpAttackNode.cs) | 예측 위치, 이동/애니메이션 상태, 중단 Cleanup의 실제 연동 예시 |

### Boss Combat Framework

| 순서 | 파일/메서드 | 확인할 내용 |
|---|---|---|
| 1 | [`BossSweepDamager.LateUpdate`](../Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs) | 시간 방향과 공간 방향을 함께 채우는 충돌 판정 |
| 2 | `ProcessSingleHit` | Owner/Tag 필터, 중복 방지, 기존 Combat Pipeline 재사용 |
| 3 | [`TenTacleChild`](../Samples/BossCombatFramework/Source/Tentacle/TenTacleChild.cs) | OnEnable/OnDisable, HP·Animator·Controller 초기화, 공격 종료 Event |
| 4 | [`TentacleSpawnSkill`](../Samples/BossCombatFramework/Source/Skills/TentacleSpawnSkill.cs) | Coroutine 취소와 연속 생성 수명주기 |
| 5 | [`TreeStrikeYC`](../Samples/BossCombatFramework/Source/Skills/TreeStrikeYC.cs) | 생성 → 확대 → 장착 → 타격 → 정리 단계 분리 |


