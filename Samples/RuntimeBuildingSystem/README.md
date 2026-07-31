# Runtime Building System

런타임에서 건축 자재를 선택하고, 스냅 또는 자유 배치로 위치를 결정하고, 자원·충돌·수면·지지력 조건을 검증한 뒤 설치하거나 철거하는 흐름의 일부입니다.

## 설계 포인트

- `BuildingSystem`은 입력 상태와 통합 흐름을 조정하는 Orchestrator입니다.
- `IBuildingState` 구현은 대기·홀딩·철거 입력 흐름을 분리합니다. 공개본에는 프로젝트 전용 `BuildingRemoveState`가 포함되지 않습니다.
- `SnapController`는 자동 스냅, 수동 기준점 스냅, 임시 자유 배치를 처리합니다.
- `PlacementValidator`는 프리뷰 단계에서 자원·충돌·수면·예측 지지력을 확인하며, 이 단계에서는 실제 연결 그래프를 수정하지 않습니다.
- `StructuralIntegritySolver`는 설치 시 부모-자식 연결을 갱신하고, 철거 시 영향받는 연결 컴포넌트만 수집해 지지력을 다시 전파합니다.
- `PlayerInventoryAdapter`는 새 인벤토리와 레거시 인벤토리 사이의 호환 경계를 담당합니다.
- 프리뷰 Root는 즉시 스냅하고 Visual 자식만 보간해 판정 정확도와 화면 부드러움을 분리합니다.

## 주요 파일

| 파일 | 역할 |
|---|---|
| [`Source/Core/BuildingSystem.cs`](Source/Core/BuildingSystem.cs) | 상태 전환, 프리뷰 갱신, 설치/철거, 의존성 연결을 조정 |
| [`Source/Placement/SnapController.cs`](Source/Placement/SnapController.cs) | Anchor/Pivot 탐색과 스냅 위치 보정 |
| [`Source/Placement/PlacementValidator.cs`](Source/Placement/PlacementValidator.cs) | 배치 가능 여부와 예측 지지력 캐시 |
| [`Source/StructuralIntegrity/StructuralIntegritySolver.cs`](Source/StructuralIntegrity/StructuralIntegritySolver.cs) | 연결 그래프 구축, BFS 지지력 전파, 국소 붕괴 처리 |
| [`Source/BuildingMaterials/IMaterial.cs`](Source/BuildingMaterials/IMaterial.cs) | 건축 자재의 연결 관계·지지력·Anchor 계약 |
| [`Source/Inventory/PlayerInventoryAdapter.cs`](Source/Inventory/PlayerInventoryAdapter.cs) | 인벤토리 구현 세부사항을 배치 로직에서 분리 |

상세 흐름은 [`../../docs/ARCHITECTURE.md`](../../docs/ARCHITECTURE.md), 누락 의존성은 [`../../docs/DEPENDENCIES.md`](../../docs/DEPENDENCIES.md)를 확인해 주세요.
