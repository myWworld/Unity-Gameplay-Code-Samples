# Code Review Guide

## 5분 검토 경로

1. 루트 [`README.md`](../README.md)의 세 시스템 요약을 확인합니다.
2. [`StructuralIntegritySolver.cs`](../Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs)에서 `PredictSupportValue`, `HandleMaterialPlacement`, `HandleMaterialPropagate`, `UpdateParentsAndChildren`을 확인합니다.
3. [`UtilitySelectorNode.cs`](../Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs)에서 재평가 Timer, `CanInterrupt()`, `inertiaBonus`, 최댓값 선택을 확인합니다.
4. [`BossSweepDamager.cs`](../Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs)에서 이전-현재 위치 SphereCast와 인접 세그먼트 Capsule Overlap을 확인합니다.
5. 실행 환경이 필요한 경우가 아니라면 의존성 설치를 시도하지 말고 [`DEPENDENCIES.md`](DEPENDENCIES.md)의 공개 경계를 확인합니다.

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

## 면접에서 설명하기 좋은 핵심 질문

### 왜 구조 안정성에 BFS를 사용했는가?

건축물은 트리라고 보장되지 않고 여러 부모·자식 경로가 생길 수 있습니다. Queue 기반 전파는 지면 Seed에서 연결 그래프 전체로 값을 확장하기 쉽고, 더 큰 지지값을 발견했을 때만 재방문하면 여러 경로 중 강한 경로를 남길 수 있습니다. 철거 시에는 전체 건축물을 다시 계산하지 않고 제거 대상과 연결된 컴포넌트만 수집합니다.

### 왜 프리뷰 단계에서 그래프를 수정하지 않는가?

마우스를 움직일 때마다 임시 연결을 추가·해제하면 실제 설치되지 않은 자재가 그래프에 남거나, 철거 재계산 대상이 불필요하게 확장될 수 있습니다. 프리뷰는 위치 기반 예측만 수행하고 클릭 후에만 Commit합니다.

### 왜 Behavior Tree와 Utility AI를 같이 사용했는가?

Behavior Tree는 조건·순서·Fallback을 읽기 쉽게 표현하지만, 여러 유효 행동 중 상황에 가장 적합한 하나를 고르는 문제는 점수 모델이 더 자연스럽습니다. 상위 구조는 BT로 제약하고, 선택 지점에서 Utility를 사용해 역할을 나눴습니다.

### 왜 Utility 선택에 관성 보너스와 재평가 주기를 두었는가?

점수가 비슷한 행동이 매 프레임 앞뒤로 바뀌는 현상을 막기 위해서입니다. 현재 행동에 작은 보너스를 주고 인터럽트 가능한 행동만 일정 주기로 재평가해 안정성과 반응성을 절충했습니다.

### 왜 단일 Trigger Collider 대신 Sweep를 사용했는가?

빠르게 회전하거나 길게 휘는 골격 공격은 한 프레임의 Collider 위치만 검사하면 프레임 사이 이동 경로 또는 세그먼트 사이가 비어 피격이 누락될 수 있습니다. 이전-현재 위치 Cast와 현재 세그먼트 간 Overlap을 함께 사용해 두 종류의 빈 공간을 줄였습니다.

## 공개본에서 의도적으로 보이지 않는 것

씬 연결, Inspector 설정값, 애니메이션 클립 Event, Prefab 계층, 실제 외부 패키지 구현은 포함되지 않습니다. 코드의 호출 관계를 검토할 때는 이를 결함으로 추정하기보다 [`PUBLIC_SNAPSHOT.md`](PUBLIC_SNAPSHOT.md)의 공개 정책과 [`DEPENDENCIES.md`](DEPENDENCIES.md)의 경계를 함께 확인해 주세요.
