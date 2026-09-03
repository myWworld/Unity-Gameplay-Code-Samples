# Code Review Guide

[← 문서 목록](./README.md) · [저장소 홈](../README.md) · [시스템 목록](../Code_Samples/README.md) · [Architecture](./ARCHITECTURE.md) · [Dependencies](./DEPENDENCIES.md)

이 문서는 전체 코드를 읽지 않고도 검토 목적에 맞는 대표 구현으로 바로 이동하기 위한 안내서입니다.

## 3분 검토

1. [루트 README](../README.md)의 세 시스템 요약을 확인합니다.
2. 다음 세 파일의 역할만 비교합니다.
   - [`BuildingSystem.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Core/BuildingSystem.cs)
   - [`Node.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/Core/Node.cs)
   - [`BossSweepDamager.cs`](../Code_Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs)
3. 더 관심 있는 시스템의 README로 이동합니다.

## 10분 검토

| 시간 | 파일 | 핵심 질문 |
|---:|---|---|
| 2분 | [`BuildingSystem.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Core/BuildingSystem.cs) | State와 Runtime Service의 책임을 어떻게 조율하는가 |
| 2분 | [`BuildingPlacementService.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Placement/BuildingPlacementService.cs) | Query 이후 실제 Commit과 Rollback은 어디서 이루어지는가 |
| 2분 | [`StructuralIntegritySolver.cs`](../Code_Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs) | Graph 변경 후 파생값을 어떻게 무효화·재계산하는가 |
| 2분 | [`UtilitySelectorNode.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs) | 행동 전환 Thrashing을 어떻게 줄이는가 |
| 2분 | [`BossSweepDamager.cs`](../Code_Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs) | 빠른 공격의 시간·공간 누락을 어떻게 함께 채우는가 |

## 20~30분 상세 검토

### Runtime Building System

[시스템 설명](../Code_Samples/RuntimeBuildingSystem/README.md) · [Source Map](../Code_Samples/RuntimeBuildingSystem/Source/README.md)

| 순서 | 파일 | 확인할 내용 |
|---:|---|---|
| 1 | [`BuildingSystem.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Core/BuildingSystem.cs) | Facade, State, Dependency 초기화, Runtime Service 생성 |
| 2 | [`BuildingHoldingState.cs`](../Code_Samples/RuntimeBuildingSystem/Source/States/BuildingHoldingState.cs) | 입력 → Preview → 검증 → Place 요청 |
| 3 | [`BuildingPreviewController.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Placement/BuildingPreviewController.cs) | Pool Preview, Anchor/Pivot, Snap, 판정 Root와 Visual 보간 |
| 4 | [`PlacementValidator.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Placement/PlacementValidator.cs) | 실제 상태를 변경하지 않는 Preview Query와 Cache |
| 5 | [`BuildingPlacementService.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Placement/BuildingPlacementService.cs) | Graph Link, Inventory Consume, Rollback, World Commit |
| 6 | [`StructuralIntegritySolver.cs`](../Code_Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs) | 최대 Support 경로, Cluster 무효화, Ground Seed BFS |
| 7 | [`BuildingRemovalService.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Placement/BuildingRemovalService.cs) | Material Root 해석과 성공 후 후처리 |
| 8 | [`PlayerInventoryAdapter.cs`](../Code_Samples/RuntimeBuildingSystem/Source/Inventory/PlayerInventoryAdapter.cs) | 구체 Inventory 구현과 건축 로직 분리 |

### Behavior Tree + Utility AI

[시스템 설명](../Code_Samples/BehaviorTreeUtilityAI/README.md) · [Source Map](../Code_Samples/BehaviorTreeUtilityAI/Source/README.md)

| 순서 | 파일 | 확인할 내용 |
|---:|---|---|
| 1 | [`Node.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/Core/Node.cs) | 정상 종료와 Abort의 수명주기 차이 |
| 2 | [`BTRunner.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/Core/BTRunner.cs) | Data에서 Runtime Root 생성과 Tick |
| 3 | [`Sequence.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/Composite/Sequence.cs) | 실행 Index를 유지하는 Stateful 처리 |
| 4 | [`ReactiveSequenceNode.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/Composite/ReactiveSequenceNode.cs) | 앞 조건을 매 Tick 다시 검사하는 흐름 |
| 5 | [`UtilitySelectorNode.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs) | Timer, `CanInterrupt`, 관성 Bonus, Action 교체 |
| 6 | [`CompositeScorer.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/Scoring/CompositeScorer.cs) | 여러 평가값 조합 |
| 7 | [`ActionJumpAttackNode.cs`](../Code_Samples/BehaviorTreeUtilityAI/Source/ExampleActions/ActionJumpAttackNode.cs) | 예측 위치, NavMesh 보정, 외부 상태 Cleanup |

### Boss Combat Framework

[시스템 설명](../Code_Samples/BossCombatFramework/README.md) · [Source Map](../Code_Samples/BossCombatFramework/Source/README.md)

| 순서 | 파일 | 확인할 내용 |
|---:|---|---|
| 1 | [`BossSweepDamager.cs`](../Code_Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs) | Previous→Current SphereCast와 Segment Capsule |
| 2 | [`TenTacleChild.cs`](../Code_Samples/BossCombatFramework/Source/Tentacle/TenTacleChild.cs) | OnEnable/OnDisable, HP·Animator·Controller·Listener Reset |
| 3 | [`TentacleSpawnSkill.cs`](../Code_Samples/BossCombatFramework/Source/Skills/TentacleSpawnSkill.cs) | 순차 Spawn Coroutine과 취소 |
| 4 | [`TreeStrikeYC.cs`](../Code_Samples/BossCombatFramework/Source/Skills/TreeStrikeYC.cs) | 단계형 Skill과 생성 Object Cleanup |
| 5 | [`GrabManager.cs`](../Code_Samples/BossCombatFramework/Source/Grabs/GrabManager.cs) | Grab Window와 Target 수명주기 |
| 6 | [`YeogChunPhaseManager.cs`](../Code_Samples/BossCombatFramework/Source/Phase/YeogChunPhaseManager.cs) | Phase 전환 시 BT·Skill·Mode 중단 |

## 역량별 검토

| 관심 주제 | 추천 경로 |
|---|---|
| 상태 패턴과 Orchestration | `BuildingSystem` → `BuildingHoldingState` → `BuildingPlacementService` |
| Transaction과 Rollback | `PlacementValidator` → `BuildingPlacementService` |
| Graph와 BFS | `IMaterial` → `StructuralIntegritySolver` |
| Lifecycle와 Abort | `Node` → `Selector` → `ActionJumpAttackNode` |
| Utility AI | `UtilitySelectorData` → `CompositeScorer` → `UtilitySelectorNode` |
| Physics Query | `BossSweepDamager` |
| Pooling Cleanup | `TenTacleChild` → `NormalTentacleChild` / `GrabTentacleChild` |
| Animation Event Skill | `TreeStrikeYC` → `YeogChunPhaseManager` |

## 처음 검토할 때 생략해도 되는 코드

- 짧은 ScriptableObject Data 파생 클래스 전부
- 유사한 Material/Action 파생 구현 전부
- Unity `.meta` 파일
- 외부 Package API 구현
- 전체 게임 Project에 남아 있는 Manager와 Asset 구성

대신 각 시스템의 README와 Source Map에서 추천한 파일을 먼저 확인하면 전체 구조를 빠르게 파악할 수 있습니다.
