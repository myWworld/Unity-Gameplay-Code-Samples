# Runtime Building System — Source Map

[← 시스템 설명](../README.md) · [시스템 목록](../../README.md) · [저장소 홈](../../../README.md) · [Review Guide](../../../docs/REVIEW_GUIDE.md) · [Dependencies](../../../docs/DEPENDENCIES.md)

이 문서는 `Source` 폴더의 모든 파일을 순서대로 읽지 않고도 역할과 호출 흐름을 찾을 수 있도록 만든 코드 지도입니다.

## 추천 검토 경로

```text
Core/BuildingSystem.cs
→ States/BuildingHoldingState.cs
→ Placement/PlacementValidator.cs
→ Placement/BuildingPlacementService.cs
→ StructuralIntegrity/StructuralIntegritySolver.cs
```

철거 흐름을 확인할 때는 다음 두 파일을 추가합니다.

```text
States/BuildingRemoveState.cs
→ Placement/BuildingRemovalService.cs
→ Placement/BuildOrRemove.cs
```

## 폴더와 파일

### Core

| 파일 | 역할 |
|---|---|
| [`Core/BuildingSystem.cs`](./Core/BuildingSystem.cs) | Facade/Orchestrator, State 전환, 의존성 해결, Runtime Service 생성, UI Event |

### States

| 파일 | 역할 |
|---|---|
| [`States/IBuildingState.cs`](./States/IBuildingState.cs) | `Enter / Update / Exit` 상태 계약 |
| [`States/BuildingIdleState.cs`](./States/BuildingIdleState.cs) | 대기와 철거 Mode 진입 |
| [`States/BuildingHoldingState.cs`](./States/BuildingHoldingState.cs) | Preview 갱신, Snap 입력, 검증, Place 요청 |
| [`States/BuildingRemoveState.cs`](./States/BuildingRemoveState.cs) | 철거 후보 처리와 이전 상태 복귀 |

### Placement

| 파일 | 역할 |
|---|---|
| [`Placement/BuildingInputHandler.cs`](./Placement/BuildingInputHandler.cs) | 입력 상태와 Mouse Raycast 결과 통합 |
| [`Placement/BuildingPreviewController.cs`](./Placement/BuildingPreviewController.cs) | Pool Preview, Anchor/Pivot, Snap Mode, 위치·회전, Indicator |
| [`Placement/SnapController.cs`](./Placement/SnapController.cs) | 자동·수동·임시 자유 Snap과 Offset 계산 |
| [`Placement/PlacementValidator.cs`](./Placement/PlacementValidator.cs) | 거리·수면·자원·예측 Support Query와 Cache |
| [`Placement/BuildingPlacementService.cs`](./Placement/BuildingPlacementService.cs) | 최종 검증, Graph/Inventory Transaction, Rollback, Commit |
| [`Placement/BuildingRemovalService.cs`](./Placement/BuildingRemovalService.cs) | Material Root 해석, 후보 표시, 철거 성공 후처리 |
| [`Placement/BuildOrRemove.cs`](./Placement/BuildOrRemove.cs) | Highlight, 실제 World 배치·삭제, Solver·NavMesh 연동 |

### Structural Integrity

| 파일 | 역할 |
|---|---|
| [`StructuralIntegrity/StructuralIntegritySolver.cs`](./StructuralIntegrity/StructuralIntegritySolver.cs) | Support 예측, Link 생성, Cluster 수집, BFS 재전파, 지연 붕괴 |

### Material

| 파일 | 역할 |
|---|---|
| [`BuildingMaterials/IMaterial.cs`](./BuildingMaterials/IMaterial.cs) | Anchor, Graph 관계, Support, Data, Visual 계약 |
| [`BuildingMaterials/MaterialsBase.cs`](./BuildingMaterials/MaterialsBase.cs) | 공통 Material Component 구현 |

### Inventory

| 파일 | 역할 |
|---|---|
| [`Inventory/IInventoryAdapter.cs`](./Inventory/IInventoryAdapter.cs) | 요구 자원 Query·Consume 계약 |
| [`Inventory/PlayerInventoryAdapter.cs`](./Inventory/PlayerInventoryAdapter.cs) | Project Inventory와 건축 System 사이 Adapter |

### Data

| 파일 | 역할 |
|---|---|
| [`Data/BuildingDataSO.cs`](./Data/BuildingDataSO.cs) | 개별 건축 자재 Prefab·요구 자원 Data |
| [`Data/BuildingDataBaseSO.cs`](./Data/BuildingDataBaseSO.cs) | 건축 Data Collection |

## 호출과 상태 변경 경계

```text
조회만 수행
BuildingPreviewController
→ PlacementValidator
→ StructuralIntegritySolver.PredictSupportValue

실제 상태 변경
BuildingPlacementService.TryCommit
→ Graph Link
→ Inventory Consume
→ Support Propagation
→ BuildOrRemove.PlaceMaterial
```

```text
철거 상태 변경
BuildingRemovalService
→ BuildOrRemove.TryRemoveMaterial
→ StructuralIntegritySolver.HandleMaterialPropagate
→ 영향 Cluster 재계산
→ BuildingMaterialManagement.DestroyProcess
```

## 파일을 세 개만 읽는다면

1. [`Core/BuildingSystem.cs`](./Core/BuildingSystem.cs)
2. [`Placement/BuildingPlacementService.cs`](./Placement/BuildingPlacementService.cs)
3. [`StructuralIntegrity/StructuralIntegritySolver.cs`](./StructuralIntegrity/StructuralIntegritySolver.cs)

[← 시스템 설명으로 돌아가기](../README.md)
