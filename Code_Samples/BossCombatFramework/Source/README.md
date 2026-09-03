# Boss Combat Framework — Source Map

[← 시스템 설명](../README.md) · [시스템 목록](../../README.md) · [저장소 홈](../../../README.md) · [Review Guide](../../../docs/REVIEW_GUIDE.md) · [Dependencies](../../../docs/DEPENDENCIES.md)

## 추천 검토 경로

```text
HitDetection/BossSweepDamager.cs
→ Tentacle/TenTacleChild.cs
→ Skills/TreeStrikeYC.cs
→ Phase/YeogChunPhaseManager.cs
```

Grab 흐름은 다음 경로를 추가합니다.

```text
Grabs/GrabManager.cs
→ Grabs/GrabTriggerBehaviour.cs
→ Grabs/IGrabbable.cs
```

## Hit Detection

| 파일 | 역할 |
|---|---|
| [`HitDetection/BossSweepDamager.cs`](./HitDetection/BossSweepDamager.cs) | Segment Sampling, Previous→Current SphereCast, 인접 Capsule, Damage 전달 |

## Skills

| 파일 | 역할 |
|---|---|
| [`Skills/TentacleSpawnSkill.cs`](./Skills/TentacleSpawnSkill.cs) | 위치 제약을 적용한 순차 촉수 Spawn |
| [`Skills/TentaclePoisonProjectileSkill.cs`](./Skills/TentaclePoisonProjectileSkill.cs) | Telegraph 후 다수 촉수 Projectile 실행 |
| [`Skills/TreeStrikeYC.cs`](./Skills/TreeStrikeYC.cs) | Tree 생성·확대·장착·Damage·정리와 Cancel |
| [`Skills/TreeAttackData.cs`](./Skills/TreeAttackData.cs) | Tree Skill Runtime Object 참조 묶음 |

## Tentacle

| 파일 | 역할 |
|---|---|
| [`Tentacle/TenTacleChild.cs`](./Tentacle/TenTacleChild.cs) | 공통 Pool Lifecycle, Reset, 회전, Mode 실행, Projectile |
| [`Tentacle/NormalTentacleChild.cs`](./Tentacle/NormalTentacleChild.cs) | 일반 촉수 공격과 Pool 반환 |
| [`Tentacle/GrabTentacleChild.cs`](./Tentacle/GrabTentacleChild.cs) | Grab/Throw 촉수 공격과 Pool 반환 |

## Grabs

| 파일 | 역할 |
|---|---|
| [`Grabs/GrabManager.cs`](./Grabs/GrabManager.cs) | Grab Collider Window, Target 유지, Pivot 동기화, Release |
| [`Grabs/GrabTriggerBehaviour.cs`](./Grabs/GrabTriggerBehaviour.cs) | Animation State와 Grab Window 연결 |
| [`Grabs/IGrabbable.cs`](./Grabs/IGrabbable.cs) | 잡힐 수 있는 대상 계약 |

## Phase

| 파일 | 역할 |
|---|---|
| [`Phase/YeogChunPhaseManager.cs`](./Phase/YeogChunPhaseManager.cs) | 공통 PhaseManager 확장, BT Abort와 Skill Cleanup |

## 핵심 실행 흐름

```text
Behavior Tree Action
→ Malbers Mode
→ Animation Event
→ Boss Skill Execute_*
→ Sweep / Spawn / Grab
→ AttackCleanUp / CancelSkill
```

```text
Pooled Tentacle
OnEnable → Reset
→ Turn / Attack
→ Mode End 또는 Death Event
→ ReturnToPool
→ OnDisable Cleanup
```

## 파일을 세 개만 읽는다면

1. [`HitDetection/BossSweepDamager.cs`](./HitDetection/BossSweepDamager.cs)
2. [`Tentacle/TenTacleChild.cs`](./Tentacle/TenTacleChild.cs)
3. [`Skills/TreeStrikeYC.cs`](./Skills/TreeStrikeYC.cs)

[← 시스템 설명으로 돌아가기](../README.md)
