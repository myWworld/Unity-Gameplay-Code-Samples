# Unity Gameplay Code Samples

> **상태:** 개발 진행 중 · 부분 공개 소스 스냅샷 · 코드 리뷰용 저장소

이 저장소는 개발 중인 **3D 오픈월드 액션 RPG**에서 직접 설계하고 구현한 Unity C# 코드 중, 기술 검토에 필요한 일부를 선별하여 공개한 포트폴리오 저장소입니다.

전체 게임 프로젝트를 배포하기 위한 저장소가 아니며, 씬·프리팹·아트 및 사운드 리소스·ScriptableObject 에셋·프로젝트 설정·라이선스가 필요한 외부 패키지·일부 프로젝트 전용 통합 코드는 포함하지 않습니다.

따라서 이 저장소만으로 Unity 프로젝트를 독립적으로 컴파일하거나 실행할 수는 없습니다. 대신 GitHub 웹 또는 로컬 저장소에서 **설계 구조, 알고리즘, 책임 분리, 성능을 고려한 구현 방식**을 바로 검토할 수 있도록 구성했습니다.

## 공개한 핵심 시스템

| 시스템 | 해결하려는 문제 | 핵심 구현 | 시작 파일 |
| --- | --- | --- | --- |
| Runtime Building System | 런타임 건축물의 배치·스냅·철거·구조 안정성 처리 | 상태 패턴, 자동·수동·자유 스냅, 프리뷰 검증과 실제 설치 분리, 부모-자식 그래프, BFS 지지력 전파, 국소 클러스터 재계산 | [`BuildingSystem.cs`](Code_Samples/RuntimeBuildingSystem/Source/Core/BuildingSystem.cs), [`StructuralIntegritySolver.cs`](Code_Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs) |
| Custom Behavior Tree + Utility AI | 행동 우선순위와 상황별 점수 선택을 함께 운용 | 명시적 Start·Update·Stop·Abort 생명주기, Stateful·Reactive Composite, ScriptableObject 기반 트리 데이터, 주기적 Utility 재평가와 관성 보너스 | [`Node.cs`](Code_Samples/BehaviorTreeUtilityAI/Source/Core/Node.cs), [`UtilitySelectorNode.cs`](Code_Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs) |
| Boss Combat Framework | 빠르게 움직이는 보스 공격의 피격 누락과 복합 스킬 수명주기 관리 | 프레임 간 SphereCast, 세그먼트 사이 Capsule Overlap, 공격당 1회 타격 집합, 다단계 스킬 실행·취소·정리, Pool 재사용 시 촉수 상태 초기화 | [`BossSweepDamager.cs`](Code_Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs), [`TenTacleChild.cs`](Code_Samples/BossCombatFramework/Source/Tentacle/TenTacleChild.cs) |

## 주요 저장소 구조

```text
.
├─ README.md
├─ LICENSE
├─ NOTICE.md
├─ Code_Samples/
│  ├─ RuntimeBuildingSystem/
│  │  ├─ README.md
│  │  └─ Source/
│  ├─ BehaviorTreeUtilityAI/
│  │  ├─ README.md
│  │  └─ Source/
│  └─ BossCombatFramework/
│     ├─ README.md
│     └─ Source/
└─ docs/
   ├─ ARCHITECTURE.md
   ├─ DEPENDENCIES.md
   └─ REVIEW_GUIDE.md
