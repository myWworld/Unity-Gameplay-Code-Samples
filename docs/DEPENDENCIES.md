# Dependency Map

이 저장소는 전체 Unity 프로젝트가 아니라 선별 소스 스냅샷이므로, 공개 코드의 참조 대상 중 일부는 포함되지 않습니다. 아래 목록은 검토자가 어떤 코드가 외부 경계에 있는지 빠르게 파악하기 위한 것입니다.

## Unity 기능 및 패키지

| 의존성 | 사용 위치 | 목적 | 저장소 포함 여부 |
|---|---|---|---|
| UnityEngine | 전체 | MonoBehaviour, Transform, Physics, Coroutine, ScriptableObject | 엔진 API만 참조 |
| Unity AI / NavMesh | 건축, AI Action | 이동 Agent와 부분 NavMesh 갱신 연동 | 프로젝트/패키지 미포함 |
| Universal Render Pipeline | 건축 | `DecalProjector` 기반 범위 표시 | 패키지 미포함 |
| Unity Editor API | Boss hit detection | 커스텀 Inspector | `#if UNITY_EDITOR` 코드만 포함 |

정확한 Unity Editor 버전과 `Packages/manifest.json`은 전체 프로젝트 정보에 해당하므로 공개본에 포함하지 않았습니다.

## 외부 또는 라이선스 패키지

| 패키지/Namespace | 공개 코드에서의 역할 | 비고 |
|---|---|---|
| Malbers Animations | 캐릭터/보스 Controller, Mode, Stat, Event, Inventory, 공격 Trigger | 패키지 소스와 에셋은 재배포하지 않음 |
| KWS | 수면 높이와 수면 배치 판정 | 패키지 미포함 |
| PixPlays Elemental VFX | 독 투사체 VFX 실행 | 패키지 미포함 |
| FS_CombatSystem | 잡기/전투 연동 | 패키지 또는 프로젝트 모듈 미포함 |

패키지명과 상표는 의존성 식별 목적으로만 기재했으며 각 권리는 원 소유자에게 있습니다.

## 프로젝트 전용 의존성

다음은 공개 소스가 참조하지만 원본 게임 프로젝트에 남아 있는 대표 타입입니다. 목록은 역할 이해를 위한 것이며 완전한 API 명세가 아닙니다.

### Runtime Building System

- `BuildingMaterialManagement`: 자재 Object Pool, Collider/Layer 활성화, 지면 접촉 검사, 파괴 처리
- `BuildingColliderUtility`: Proxy Collider에서 실제 `IMaterial` Root 탐색
- `BuildingRemoveState`: 철거 입력 상태
- `PartialNavMeshBuilder`: 설치 위치 주변 NavMesh의 국소 갱신
- `RuntimePlacedBuildingMarker`: 런타임 설치 오브젝트 표시
- `RuntimeCursorController`: 건축 상태별 Cursor 표시 요청 관리
- `LayerAndTagConstants`: Building/Highlight/Snap Layer와 Tag 상수
- `Mouse3D`, `PlayerBuildingController`: 월드 마우스 좌표와 입력 Routing
- `UIManager`: 인벤토리 열림 상태와 건축 UI 연동
- `Door`, `Boat`: 자재별 특수 배치 처리
- `ItemDurabilityUtility`, `ItemDurabilityReason`: 건축 도구 내구도 소모
- `PlayerInventoryStore`, `ItemDatabase`, `PlayerUnifiedInventoryController`, `PlayerBuildingCatalogSettings`: 통합 인벤토리 계층

### Behavior Tree + Utility AI

- `BossMotor`: Blackboard에서 참조하는 보스 이동/행동 제어기
- `ActionPlayMode`: Malbers Mode 실행을 공통 처리하는 Action Base
- `ActionAttackData`, `ActionJumpAttackData`, `ActionMoveData`: 행동별 ScriptableObject 데이터
- 거리·HP·Cooldown 등 구체 `WeightScorer` 구현: 공개본에는 Composite 계약과 조합기만 포함

### Boss Combat Framework

- `PhaseManager`: 공통 보스 Phase 전환 기반 클래스
- `BossSkill`, `TentacleSkillBase`: 스킬 취소·초기화·공통 참조 기반 클래스
- `BossAnimEventBridge`, `YeogChunAnimEvent`: Animation Event와 Gameplay Skill 연결
- `TenTacleManager`: 일반/잡기 촉수 Pool과 활성 목록 관리
- `AutonomousTentacle`: 촉수 개별 AI 상태
- `EffectManager`, `AdvancedProjectileVFX`, `VfxData`: VFX Pool 및 투사체 실행
- `GrabManager`: 잡기 대상 보관·해제·던지기 연동
- `BossAttackUtility`: 지면 탐색, 무작위 선택, 범위 피해 유틸리티

## 컴파일에 대한 의미

위 의존성이 없기 때문에 공개본만으로의 컴파일 실패는 예상 가능한 상태입니다. 이 저장소에서 검토해야 하는 대상은 다음과 같습니다.

- 클래스 간 책임과 호출 방향
- 그래프/Queue/HashSet 기반 알고리즘
- 상태 및 수명주기 처리
- 외부 시스템을 직접 퍼뜨리지 않고 Adapter/Base/Manager 경계로 연결한 방식
- NonAlloc 물리 쿼리와 버퍼 재사용 같은 성능 선택

실행 가능한 전체 프로젝트나 외부 패키지 전달은 이 저장소의 공개 범위에 포함되지 않습니다.
