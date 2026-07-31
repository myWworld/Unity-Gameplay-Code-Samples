# Boss Combat Framework

보스의 공격 판정과 다단계 스킬, 촉수 개체의 생성·공격·사망·풀 복귀를 연결하는 코드 일부입니다.

## 설계 포인트

- `BossSweepDamager`는 각 세그먼트의 이전/현재 위치 사이를 `SphereCastNonAlloc`로 검사해 프레임 사이 이동 누락을 줄입니다.
- 같은 프레임의 인접 세그먼트 사이에는 `OverlapCapsuleNonAlloc`를 사용해 길이 방향 빈 공간을 메웁니다.
- 활성화된 공격 창 안에서는 `HashSet<Collider>`로 동일 Collider의 중복 피해를 방지합니다.
- 커스텀 Inspector와 Gizmo로 Base/Middle/Tip 기반 세그먼트 히트박스를 시각적으로 조정할 수 있습니다.
- 촉수는 풀에서 재사용되므로 HP·Animator·Controller·논리 플래그를 활성화 시점에 초기화합니다.
- 스킬 실행은 애니메이션 이벤트와 코루틴으로 Telegraph, 생성, 장착, 피해, 정리를 단계적으로 연결합니다.

## 주요 파일

| 파일 | 역할 |
|---|---|
| [`Source/HitDetection/BossSweepDamager.cs`](Source/HitDetection/BossSweepDamager.cs) | 빠른 골격 공격을 위한 연속 Sweep 판정 |
| [`Source/Tentacle/TenTacleChild.cs`](Source/Tentacle/TenTacleChild.cs) | 촉수 공통 생명주기, 회전 후 공격, 투사체, 풀 초기화 |
| [`Source/Tentacle/NormalTentacleChild.cs`](Source/Tentacle/NormalTentacleChild.cs) | 일반 공격 촉수 구현 |
| [`Source/Tentacle/GrabTentacleChild.cs`](Source/Tentacle/GrabTentacleChild.cs) | 잡기·던지기 촉수 구현 |
| [`Source/Skills/TentacleSpawnSkill.cs`](Source/Skills/TentacleSpawnSkill.cs) | 위치 제약을 고려한 순차 촉수 생성 |
| [`Source/Skills/TentaclePoisonProjectileSkill.cs`](Source/Skills/TentaclePoisonProjectileSkill.cs) | Telegraph 이후 다수 촉수의 독 투사체 발사 |
| [`Source/Skills/TreeStrikeYC.cs`](Source/Skills/TreeStrikeYC.cs) | 생성·장착·공격·정리 단계가 있는 무기형 스킬 |
| [`Source/Phase/YeogChunPhaseManager.cs`](Source/Phase/YeogChunPhaseManager.cs) | 공통 PhaseManager를 확장한 전환 처리 예시 |

공통 보스 기반 클래스, Manager, VFX/Combat 패키지는 포함하지 않았습니다. 자세한 내용은 [`../../docs/DEPENDENCIES.md`](../../docs/DEPENDENCIES.md)를 확인해 주세요.
