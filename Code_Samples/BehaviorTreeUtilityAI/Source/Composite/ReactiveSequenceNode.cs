using System.Collections.Generic;

public class ReactiveSequenceNode : CompositeNode
{

    public ReactiveSequenceNode(BlackBoard blackBoard, List<Node> children) : base(blackBoard, children)
    {

    }

    protected override void OnStart() { }
    protected override NodeState OnUpdate()
    {
        for (int i = 0; i < children.Count; i++)
        {

            var state = children[i].Evaluate();
            if (state == NodeState.RUNNING || state == NodeState.FAILURE)
                return state;
        }

        return NodeState.SUCCESS;
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
