using System.Collections.Generic;

public class Selector : CompositeNode //If at least one of its children succeeds, the selector succeeds.
{

    private Node activeNode;
    public Selector(BlackBoard blackBoard, List<Node> children ) : base(blackBoard, children)
    {

    }

    protected override NodeState OnUpdate()
    {
        //  UnityEngine.Debug.Log($"[Selector] Evaluating child nodes.");
        foreach (var node in children)
        {
            var currentState = node.Evaluate();

            if (currentState == NodeState.RUNNING)
            {

                if (activeNode != null && activeNode != node)
                {
                    UnityEngine.Debug.Log($"[Selector] 제어권 변경! {activeNode.name} 강제 종료 -> {node.name} 실행");
                    activeNode.Stop();
                }

                activeNode = node; // 현재 실행 중인 노드 갱신
                return NodeState.RUNNING;
            }
            else if (currentState == NodeState.SUCCESS)
            {

                if (activeNode != null && activeNode != node)
                {
                    activeNode.Stop();
                }
                activeNode = null;
                return NodeState.SUCCESS;
            }


        }

        if (activeNode != null)
        {
            activeNode.Stop();
            activeNode = null;
        }

        return NodeState.FAILURE;
    }

    protected override void OnStop()
    {
        if (activeNode != null)
        {
            activeNode.Stop();
            activeNode = null;
        }
    }

    protected override void OnAbort()
    {
        if (activeNode != null)
        {
            activeNode.Stop();
            activeNode = null;
        }
    }


}
