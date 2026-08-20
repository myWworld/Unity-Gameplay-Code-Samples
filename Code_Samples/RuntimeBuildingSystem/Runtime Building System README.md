# Runtime Building System

런타임에서 건축 자재를 선택하고, 자동·수동 스냅 또는 임시 자유 배치로 프리뷰 위치를 결정한 뒤, 자원·배치 범위·수면·예측 지지력을 검증해 설치하거나 철거하는 흐름의 일부입니다.

## 설계 포인트

- `BuildingSystem`은 외부 진입점, 상태 전환, 의존성 초기화와 런타임 서비스 호출 순서를 조정하는 Facade/Orchestrator입니다.
- `IBuildingState` 구현은 대기·홀딩·철거 상태의 입력 흐름을 분리합니다. 공개본에는 `BuildingIdleState`, `BuildingHoldingState`, `BuildingRemoveState`가 모두 포함되어 있습니다.
- `BuildingInputHandler`는 설치·보조 동작·스냅 모드 변경·Anchor 변경·회전 입력과 마우스 Raycast 결과를 한곳에서 관리합니다. State 구현은 `UnityEngine.Input`을 직접 호출하지 않습니다.
- `BuildingPreviewController`는 현재 홀딩 자재와 Object Pool 생명주기, Anchor/Pivot 선택, 스냅 모드, 프리뷰 위치·회전, 범위 표시와 Snap Indicator를 담당합니다.
- `BuildingPlacementService`는 최종 자원 검증, 구조 그래프 연결과 실패 시 Rollback, 지지력 확정·전파, 실제 설치, 자원·도구 내구도 소모와 Door 후처리를 하나의 Commit 흐름으로 처리합니다.
- `BuildingRemovalService`는 Raycast Collider에서 실제 자재 Root를 해석하고, 철거 후보 표시와 실제 철거 요청을 담당합니다. 도구 내구도는 철거가 성공한 경우에만 소모합니다.
- `SnapController`는 자동 스냅, 수동 기준점 스냅, 임시 자유 배치와 Anchor/Pivot Offset 보정을 처리합니다.
- `PlacementValidator`는 프리뷰 단계에서 자원·배치 범위·수면·예측 지지력을 확인하고 결과를 캐시합니다. 이 단계에서는 실제 `Parents`·`ConnectedChildren` 그래프를 수정하지 않습니다.
- `StructuralIntegritySolver`는 설치 시 부모-자식 연결을 갱신하고, 여러 지지 경로 중 더 높은 값을 남기는 Queue 기반 지지력 전파를 수행합니다. 철거 시에는 영향받는 연결 컴포넌트만 수집해 지지력을 다시 계산하고 붕괴 대상을 처리합니다.
- `PlayerInventoryAdapter`는 건축 로직에서 인벤토리 구현 세부사항을 분리하고, 요구 자원 조회·검증·소모를 공통 인터페이스로 제공합니다.
- 프리뷰 Root는 판정 위치로 즉시 이동하고 Visual 자식만 보간해 판정 정확도와 화면의 부드러움을 분리합니다.
- 새로 분리된 `BuildingPreviewController`, `BuildingPlacementService`, `BuildingRemovalService`는 `BuildingSystem`이 생성하는 일반 C# 런타임 객체이므로 씬에 별도 MonoBehaviour 컴포넌트를 추가할 필요하지 않았습니다

## 처리 흐름

### 설치

```text
BuildingInputHandler
→ BuildingHoldingState
→ BuildingPreviewController
→ PlacementValidator
→ BuildingPlacementService
→ StructuralIntegritySolver / BuildOrRemove / PlayerInventoryAdapter
→ 다음 프리뷰 또는 IdleState
```

프리뷰 중에는 위치·스냅·자원·수면·예상 지지력만 검사합니다. 사용자가 설치를 확정한 뒤에만 구조 그래프 연결, 지지력 확정, 자원·내구도 소모와 실제 월드 배치를 수행합니다.

### 철거

```text
BuildingInputHandler
→ BuildingRemoveState
→ BuildingRemovalService
→ BuildOrRemove
→ StructuralIntegritySolver
→ 영향 클러스터 재계산 및 지연 붕괴
```

철거 대상과 연결된 컴포넌트만 수집해 지지력을 초기화한 뒤, 지면에 닿은 자재를 시작점으로 다시 전파합니다. 최소 지지력보다 낮은 자재는 붕괴 Queue에 등록합니다.

## 주요 파일

| 파일 | 역할 |
|---|---|
| [`Source/Core/BuildingSystem.cs`](Source/Core/BuildingSystem.cs) | 외부 공개 API, 상태 전환, 의존성 초기화와 런타임 서비스 호출 순서 조정 |
| [`Source/States/IBuildingState.cs`](Source/States/IBuildingState.cs) | 건축 상태의 `Enter`·`Update`·`Exit` 계약 |
| [`Source/States/BuildingIdleState.cs`](Source/States/BuildingIdleState.cs) | 자재를 들지 않은 대기 상태와 철거 모드 진입 처리 |
| [`Source/States/BuildingHoldingState.cs`](Source/States/BuildingHoldingState.cs) | 프리뷰 갱신, 스냅 입력, 배치 검증과 설치 요청 처리 |
| [`Source/States/BuildingRemoveState.cs`](Source/States/BuildingRemoveState.cs) | 철거 대상 탐색과 이전 상태 복귀 흐름 처리 |
| [`Source/Placement/BuildingInputHandler.cs`](Source/Placement/BuildingInputHandler.cs) | 건축 입력과 마우스 월드 좌표·Raycast 결과 통합 |
| [`Source/Placement/BuildingPreviewController.cs`](Source/Placement/BuildingPreviewController.cs) | 홀딩 자재, Pool 생명주기, Anchor/Pivot, 스냅 모드, 프리뷰 위치·회전과 표시 관리 |
| [`Source/Placement/BuildingPlacementService.cs`](Source/Placement/BuildingPlacementService.cs) | 검증된 프리뷰의 그래프 Commit, Rollback, 설치 후처리와 다음 프리뷰 준비 |
| [`Source/Placement/BuildingRemovalService.cs`](Source/Placement/BuildingRemovalService.cs) | 철거 대상 Root 해석, 후보 표시, 실제 철거와 성공 후 내구도 소모 |
| [`Source/Placement/SnapController.cs`](Source/Placement/SnapController.cs) | Anchor/Pivot 탐색, 자동·수동·자유 스냅과 위치 Offset 보정 |
| [`Source/Placement/PlacementValidator.cs`](Source/Placement/PlacementValidator.cs) | 프리뷰 배치 가능 여부, 자원·수면·예측 지지력 검증과 결과 캐시 |
| [`Source/Placement/BuildOrRemove.cs`](Source/Placement/BuildOrRemove.cs) | 월드 배치 확정, Highlight 피드백, 철거 실행과 부분 NavMesh 갱신 |
| [`Source/StructuralIntegrity/StructuralIntegritySolver.cs`](Source/StructuralIntegrity/StructuralIntegritySolver.cs) | 연결 그래프 구축, 지지력 예측·전파, 영향 클러스터 재계산과 지연 붕괴 처리 |
| [`Source/BuildingMaterials/IMaterial.cs`](Source/BuildingMaterials/IMaterial.cs) | 건축 자재의 연결 관계·지지력·Anchor·Visual 계약 |
| [`Source/Inventory/PlayerInventoryAdapter.cs`](Source/Inventory/PlayerInventoryAdapter.cs) | 새 인벤토리와 레거시 인벤토리 사이의 호환 경계 및 요구 자원 처리 |

프로젝트 전용 타입과 외부 패키지 등 공개본에 포함되지 않은 의존성은 [`../../docs/DEPENDENCIES.md`](../../docs/DEPENDENCIES.md)를 확인해 주세요.
