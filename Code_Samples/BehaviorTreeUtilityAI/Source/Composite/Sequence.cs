using System.Collections.Generic;

public class Sequence : CompositeNode
{
    private int currentChildIndex = 0;

    public Sequence(BlackBoard blackBoard, List<Node> children) : base(blackBoard, children)
    {

    }

    protected override void OnStart()
    {
        currentChildIndex = 0;
    }

    protected override NodeState OnUpdate()
    {
        for (int i = currentChildIndex; i < children.Count; i++)
        {
            currentChildIndex = i;
            var state = children[currentChildIndex].Evaluate();

            // 자식이 실행 중이거나 실패하면 그 상태를 부모에게 보고
            if (state == NodeState.RUNNING || state == NodeState.FAILURE)
                return state;
        }

        return NodeState.SUCCESS;
    }

    protected override void OnStop()
    {

    }

    protected override void OnAbort()
    {
        currentChildIndex = 0;

        foreach (var child in children)
        {
            child.Stop();
        }
    }
}
