# Unity Gameplay Code Samples

[English README](README.en.md)

> **상태:** 개발 진행 중 · 부분 공개 소스 스냅샷 · 코드 리뷰용 저장소

이 저장소는 개발 중인 **3D 오픈월드 액션 RPG**에서 직접 설계하고 구현한 Unity C# 코드 중, 기술 검토에 필요한 일부만 선별해 공개한 포트폴리오 저장소입니다.

전체 게임 프로젝트를 배포하기 위한 저장소가 아니며, 씬·프리팹·아트/사운드·ScriptableObject 에셋·프로젝트 설정·라이선스가 필요한 외부 패키지·일부 프로젝트 전용 통합 코드는 포함하지 않습니다. 따라서 공개본만으로 Unity에서 컴파일하거나 플레이할 수 있다고 가정하지 않습니다. 대신 저장소를 클론한 뒤 별도 설치 없이 **설계 구조, 알고리즘, 책임 분리, 성능을 고려한 구현 방식**을 바로 검토할 수 있도록 구성했습니다.

## 공개한 핵심 시스템

| 시스템 | 해결하려는 문제 | 핵심 구현 | 시작 파일 |
|---|---|---|---|
| Runtime Building System | 런타임 건축물의 배치·스냅·철거·구조 안정성 처리 | 상태 패턴, 자동/수동/자유 스냅, 프리뷰 검증과 실제 설치 분리, 부모-자식 그래프, BFS 지지력 전파, 국소 클러스터 재계산 | [`BuildingSystem.cs`](Samples/RuntimeBuildingSystem/Source/Core/BuildingSystem.cs), [`StructuralIntegritySolver.cs`](Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs) |
| Custom Behavior Tree + Utility AI | 행동 우선순위와 상황별 점수 선택을 함께 운용 | 명시적 Start/Update/Stop/Abort 생명주기, Stateful/Reactive Composite, ScriptableObject 기반 트리 데이터, 주기적 Utility 재평가와 관성 보너스 | [`Node.cs`](Samples/BehaviorTreeUtilityAI/Source/Core/Node.cs), [`UtilitySelectorNode.cs`](Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs) |
| Boss Combat Framework | 빠르게 움직이는 보스 공격의 피격 누락과 복합 스킬 수명주기 관리 | 프레임 간 SphereCast + 세그먼트 간 Capsule Overlap, 1회 타격 집합, 애니메이션 이벤트 기반 스킬 실행, 촉수 풀링·초기화·복귀 | [`BossSweepDamager.cs`](Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs), [`TenTacleChild.cs`](Samples/BossCombatFramework/Source/Tentacle/TenTacleChild.cs) |

## 저장소 구조

```text
.
├─ README.md / README.en.md
├─ Samples/
│  ├─ RuntimeBuildingSystem/
│  │  ├─ README.md
│  │  └─ Source/
│  ├─ BehaviorTreeUtilityAI/
│  │  ├─ README.md
│  │  └─ Source/
│  └─ BossCombatFramework/
│     ├─ README.md
│     └─ Source/
├─ docs/
│  ├─ ARCHITECTURE.md
│  ├─ DEPENDENCIES.md
│  ├─ PUBLIC_SNAPSHOT.md
│  └─ REVIEW_GUIDE.md
├─ tools/verify_repository.py
└─ .github/workflows/validate-public-snapshot.yml
```

## 권장 검토 순서

처음 보는 경우 [`docs/REVIEW_GUIDE.md`](docs/REVIEW_GUIDE.md)의 5분 검토 경로를 먼저 확인하는 것이 가장 빠릅니다. 시스템 간 흐름과 설계 의도는 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), 공개하지 않은 경계와 외부 의존성은 [`docs/DEPENDENCIES.md`](docs/DEPENDENCIES.md)에 정리했습니다.

특히 다음 세 파일이 각 시스템의 핵심 판단을 가장 잘 보여줍니다.

1. [`StructuralIntegritySolver.cs`](Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs) — 구조물 그래프 수집, 지지력 전파, 철거 후 국소 재계산과 붕괴 처리
2. [`UtilitySelectorNode.cs`](Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs) — 재평가 주기, 인터럽트 가능 여부, 관성 보너스를 이용한 행동 전환 안정화
3. [`BossSweepDamager.cs`](Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs) — 빠른 골격 애니메이션에서도 공격 궤적의 빈 공간을 줄이는 연속 충돌 판정

## 실행 및 컴파일 범위

이 저장소는 **소스 리뷰용 스냅샷**이며 독립 실행 가능한 Unity 프로젝트 또는 재사용 패키지가 아닙니다. 공개 코드가 참조하는 프로젝트 전용 타입과 유료/외부 패키지는 의도적으로 배포하지 않습니다. 어떤 요소가 빠져 있는지와 그 이유는 [`docs/PUBLIC_SNAPSHOT.md`](docs/PUBLIC_SNAPSHOT.md), 참조 관계는 [`docs/DEPENDENCIES.md`](docs/DEPENDENCIES.md)를 확인해 주세요.

## 공개본 정리 원칙

- 게임 로직의 핵심 설계는 유지하되 전체 프로젝트 구조와 콘텐츠는 공개하지 않습니다.
- 동일 파일 중복, 잘못된 폴더명, 혼합 인코딩, 깨진 주석처럼 코드 검토를 방해하는 요소만 정리했습니다.
- 외부 패키지의 소스·바이너리·에셋은 포함하지 않습니다.
- 개발 중인 코드이므로 실제 프로젝트에서는 이후 변경될 수 있습니다.

## 사용 권한

이 저장소는 포트폴리오 검토와 채용·기술 평가를 위해 공개됩니다. 클론 및 열람은 가능하지만, 별도 허가 없이 코드의 복제·수정·재배포·상업적 사용 또는 다른 프로젝트로의 편입은 허용하지 않습니다. 자세한 내용은 [`LICENSE`](LICENSE)를 확인해 주세요.
