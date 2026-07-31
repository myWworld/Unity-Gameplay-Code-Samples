using System.Collections.Generic;

public class Parallel : Node
{
    public enum Policy
    {
        MajorChild,
        Join,
    }

    private List<Node> children = new List<Node>();
    private int majorChildIndex = 0;

    public Policy policy;

    public Parallel(BlackBoard blackBoard, List<Node> children, int majorIdx) : base(blackBoard)
    {
        this.children = children;
        this.majorChildIndex = majorIdx;
    }

    protected override void OnStart()
    {

    }

    protected override NodeState OnUpdate()
    {
        NodeState majorState = NodeState.RUNNING;
        bool anyRunning = false;

        for (int i = 0; i < children.Count; i++)
        {
            var state = children[i].Evaluate();

            //  하나라도 실패하면 즉시 실패 반환
            if (policy == Policy.Join && state == NodeState.FAILURE)
            {
                return NodeState.FAILURE;
            }

            if(i == majorChildIndex)
            {
                majorState = state;
            }

            if (state == NodeState.RUNNING) anyRunning = true;

        }

        if (policy == Policy.MajorChild) return majorState;

        return anyRunning ? NodeState.RUNNING : NodeState.SUCCESS;
    }

    protected override void OnStop()
    {
        foreach (var child in children)
        {
            child.Stop();
        }
    }

    protected override void OnAbort()
    {
        foreach (var child in children)
        {
            child.Stop();
        }
    }
}
