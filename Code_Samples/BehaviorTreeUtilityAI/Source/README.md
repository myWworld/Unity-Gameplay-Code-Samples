# Behavior Tree + Utility AI — Source Map

[← 시스템 설명](../README.md) · [시스템 목록](../../README.md) · [저장소 홈](../../../README.md) · [Review Guide](../../../docs/REVIEW_GUIDE.md) · [Dependencies](../../../docs/DEPENDENCIES.md)

## 추천 검토 경로

```text
Core/Node.cs
→ Core/BTRunner.cs
→ Composite/Sequence.cs + ReactiveSequenceNode.cs
→ Composite/UtilitySelectorNode.cs
→ ExampleActions/ActionJumpAttackNode.cs
```

## Core

| 파일 | 역할 |
|---|---|
| [`Core/Node.cs`](./Core/Node.cs) | Runtime Node와 Composite 공통 계약, Start/Update/Stop/Abort |
| [`Core/BTRunner.cs`](./Core/BTRunner.cs) | Root Data에서 Runtime Tree 생성, 매 Frame Evaluate, 전체 Abort |
| [`Core/BTNodeData.cs`](./Core/BTNodeData.cs) | ScriptableObject Node Data 기반 계약 |
| [`Core/BlackBoard.cs`](./Core/BlackBoard.cs) | 타입별 공유 Data와 Action Cancel Event |
| [`Core/BlackboardKey.cs`](./Core/BlackboardKey.cs) | 직렬화 가능한 Key와 Hash |

## Composite

| 파일 | 역할 |
|---|---|
| [`Composite/Selector.cs`](./Composite/Selector.cs) | 우선순위 Child 선택과 Active Node 전환 |
| [`Composite/Sequence.cs`](./Composite/Sequence.cs) | 현재 Child Index를 유지하는 Stateful Sequence |
| [`Composite/ReactiveSequenceNode.cs`](./Composite/ReactiveSequenceNode.cs) | 매 Tick 앞 조건부터 재검사 |
| [`Composite/UtilitySelectorNode.cs`](./Composite/UtilitySelectorNode.cs) | Score 재평가와 Interrupt 기반 Action 교체 |
| [`Composite/Parallel.cs`](./Composite/Parallel.cs) | 여러 Child 실행을 위한 보조 Composite 구현 |

## Data

| 파일 | 역할 |
|---|---|
| [`Data/SelectorData.cs`](./Data/SelectorData.cs) | Selector Runtime 생성 |
| [`Data/SequenceData.cs`](./Data/SequenceData.cs) | Sequence Runtime 생성 |
| [`Data/ReactiveSequenceData.cs`](./Data/ReactiveSequenceData.cs) | Reactive Sequence Runtime 생성 |
| [`Data/UtilitySelectorData.cs`](./Data/UtilitySelectorData.cs) | Utility Entry·재평가 주기·관성 설정 |

## Scoring

| 파일 | 역할 |
|---|---|
| [`Scoring/WeightScorer.cs`](./Scoring/WeightScorer.cs) | Score 계산 계약 |
| [`Scoring/GenericFloatScorer.cs`](./Scoring/GenericFloatScorer.cs) | Blackboard Float Score 예시 |
| [`Scoring/CompositeScorer.cs`](./Scoring/CompositeScorer.cs) | 여러 Scorer 조합 |

## Example Actions

| 파일 | 역할 |
|---|---|
| [`ExampleActions/ActionMoveNode.cs`](./ExampleActions/ActionMoveNode.cs) | 이동 요청 |
| [`ExampleActions/ActionAttackNode.cs`](./ExampleActions/ActionAttackNode.cs) | 공격 Mode 실행과 상태 확인 |
| [`ExampleActions/ActionJumpAttackNode.cs`](./ExampleActions/ActionJumpAttackNode.cs) | 예측 Jump Attack과 Cleanup |

## 데이터 생성과 실행

```text
SelectorData / SequenceData / UtilitySelectorData
→ CreateNode(BlackBoard)
→ Selector / Sequence / UtilitySelectorNode
→ Child Action Evaluate
→ BossMotor / Malbers Mode
```

## 파일을 세 개만 읽는다면

1. [`Core/Node.cs`](./Core/Node.cs)
2. [`Composite/UtilitySelectorNode.cs`](./Composite/UtilitySelectorNode.cs)
3. [`ExampleActions/ActionJumpAttackNode.cs`](./ExampleActions/ActionJumpAttackNode.cs)

[← 시스템 설명으로 돌아가기](../README.md)
