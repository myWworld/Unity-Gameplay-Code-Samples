# Custom Behavior Tree + Utility AI

보스 AI에서 **조건과 순서를 표현하는 Behavior Tree**와 **상황별 적합도를 비교하는 Utility 선택**을 함께 사용하기 위해 만든 런타임 노드 구조의 일부입니다.

## 설계 포인트

- `Node.Evaluate()`가 `OnStart → OnUpdate → OnStop` 생명주기를 보장합니다.
- `Stop()`은 실행 중인 노드에 `OnAbort`를 먼저 전달해 이동·공격·코루틴 같은 외부 상태를 정리할 수 있게 합니다.
- `Sequence`는 실행 중인 자식의 인덱스를 유지하고, `ReactiveSequenceNode`는 매 Tick 앞 조건부터 다시 확인합니다.
- `UtilitySelectorNode`는 점수가 가장 높은 행동을 선택하고, 재평가 주기·`CanInterrupt()`·관성 보너스를 통해 과도한 행동 전환을 억제합니다.
- `BTNodeData` 계열 ScriptableObject가 에디터 데이터에서 런타임 노드 그래프를 생성합니다.
- `BlackBoard`는 문자열 해시와 타입별 Dictionary를 사용해 행동 간 데이터를 공유합니다.

## 주요 파일

| 파일 | 역할 |
|---|---|
| [`Source/Core/Node.cs`](Source/Core/Node.cs) | 공통 노드 생명주기와 중단 계약 |
| [`Source/Core/BTRunner.cs`](Source/Core/BTRunner.cs) | 루트 노드 생성과 매 프레임 평가 |
| [`Source/Core/BlackBoard.cs`](Source/Core/BlackBoard.cs) | 타입별 Blackboard 데이터 저장 |
| [`Source/Composite/ReactiveSequenceNode.cs`](Source/Composite/ReactiveSequenceNode.cs) | 우선 조건을 매 Tick 재확인하는 Reactive 흐름 |
| [`Source/Composite/UtilitySelectorNode.cs`](Source/Composite/UtilitySelectorNode.cs) | 점수 기반 선택, 재평가, 관성 처리 |
| [`Source/Scoring/CompositeScorer.cs`](Source/Scoring/CompositeScorer.cs) | 여러 점수의 합·곱·평균·최댓값·선형 결합 |
| [`Source/ExampleActions/`](Source/ExampleActions/) | 이동·공격·예측 점프 공격의 프로젝트 연동 예시 |

행동 데이터 클래스와 일부 전용 Base Action/Scorer는 전체 프로젝트에 남아 있으며 공개본에는 포함하지 않았습니다. 자세한 경계는 [`../../docs/DEPENDENCIES.md`](../../docs/DEPENDENCIES.md)를 확인해 주세요.
