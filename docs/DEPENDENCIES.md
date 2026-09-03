# Dependency Map

[← 문서 목록](./README.md) · [저장소 홈](../README.md) · [시스템 목록](../Code_Samples/README.md) · [Review Guide](./REVIEW_GUIDE.md) · [Architecture](./ARCHITECTURE.md)

이 저장소는 전체 Unity Project가 아니라 선별 Source Snapshot이므로, 공개 코드의 참조 대상 중 일부는 포함되지 않습니다. 아래 목록은 검토자가 Compile 경계와 외부 책임을 빠르게 파악하기 위한 것입니다.

## Unity 기능 및 Package

| 의존성 | 사용 위치 | 목적 | 공개본 |
|---|---|---|---|
| UnityEngine | 전체 | MonoBehaviour, Transform, Physics, Coroutine, ScriptableObject | Engine API 참조 |
| Unity AI / NavMesh | Building, AI Action | 이동 Agent, 착지 위치 검사, 부분 NavMesh 갱신 | Project/Package 미포함 |
| Universal Render Pipeline | Building | `DecalProjector` 기반 범위 표시 | Package 미포함 |
| Unity Editor API | Boss Hit Detection | Custom Inspector | `#if UNITY_EDITOR` 코드 포함 |

정확한 Unity Editor Version, `Packages/manifest.json`, Scene과 Prefab 구성은 전체 Project 정보에 해당하므로 공개본에 포함하지 않았습니다.

## 외부 또는 License Package

| Package / Namespace | 공개 코드에서의 역할 | 비고 |
|---|---|---|
| Malbers Animations | Character/Boss Controller, Mode, Stat, Event, Inventory, Attack Trigger | Package Source와 Asset 재배포 제외 |
| KWS | 수면 높이와 수면 배치 판정 | Package 미포함 |
| PixPlays Elemental VFX | 독 Projectile과 Effect 실행 | Package 미포함 |
| FS_CombatSystem | Grab과 Combat 연동 | Package/Project Module 미포함 |

Package명과 상표는 의존성 식별 목적으로만 기재했습니다.

## 프로젝트 전용 의존성

### Runtime Building System

[시스템 README](../Code_Samples/RuntimeBuildingSystem/README.md)

- `BuildingMaterialManagement`: Material Object Pool, Collider/Layer 활성화, Ground 검사, 파괴 처리
- `BuildingColliderUtility`: Proxy Collider에서 실제 `IMaterial` Root 해석
- `PartialNavMeshBuilder`: 배치·철거 위치 주변 NavMesh 국소 갱신
- `RuntimePlacedBuildingMarker`: Runtime 설치 Object 식별
- `RuntimeCursorController`: 건축 상태별 Cursor 표시 요청
- `LayerAndTagConstants`: Building/Highlight/Snap Layer와 Tag 상수
- `Mouse3D`, `PlayerBuildingController`: World Mouse와 외부 Input Routing
- `UIManager`: Inventory 열림 상태와 Building UI 연동
- `Door`, `Boat`: Material별 특수 배치
- `ItemDurabilityUtility`: 건축 Tool 내구도
- Project Inventory Store와 Item Database

### Behavior Tree + Utility AI

[시스템 README](../Code_Samples/BehaviorTreeUtilityAI/README.md)

- `BossMotor`: 이동과 행동 요청을 처리하는 Project Controller
- `ActionPlayMode`: Malbers Mode 실행을 공통 처리하는 Action Base
- 행동별 ScriptableObject Data 일부
- 거리·HP·Cooldown 등 Project 전용 Scorer
- Boss Blackboard Sensor와 실제 Tree Asset

### Boss Combat Framework

[시스템 README](../Code_Samples/BossCombatFramework/README.md)

- `PhaseManager`: 공통 Boss Phase 기반
- `BossSkill`, `TentacleSkillBase`: Skill Init/Cancel과 공통 참조
- `BossAnimEventBridge`, `YeogChunAnimEvent`: Animation Event와 Skill 연결
- `TenTacleManager`: Normal/Grab Tentacle Pool과 활성 목록
- `AutonomousTentacle`: 촉수 개별 AI
- `EffectManager`, Projectile VFX Data
- `BossAttackUtility`: Ground 탐색과 범위 Damage
- 실제 Boss Prefab, Animator Controller, Stat/Mode Asset

## 공개본만으로 Compile되지 않는 이유

공개본에는 다음이 포함되지 않습니다.

- 외부 Package Assembly와 Asset
- Scene/Prefab/Animator/ScriptableObject Asset
- Project 전용 Manager와 Integration Module
- 일부 Base Class와 Data Type

따라서 공개본의 Compile 실패 가능성은 Snapshot 범위상 예상되는 결과입니다. 검토 대상은 다음입니다.

- 클래스 간 책임과 호출 방향
- State/Node/Skill/Pool Lifecycle
- Graph·Queue·HashSet 알고리즘
- Preview Query와 실제 Commit의 분리
- Adapter/Base/Manager 경계
- NonAlloc Physics Query와 Buffer 재사용

실행 가능한 전체 Project나 유료 Package 전달은 공개 범위에 포함되지 않습니다.

[← 문서 목록](./README.md)
