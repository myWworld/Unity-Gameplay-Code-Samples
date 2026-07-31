using System.Collections.Generic;


public enum NodeState
{
    RUNNING,
    SUCCESS,
    FAILURE
}

public abstract class Node
{
    public string name;
    protected bool started = false;

    protected NodeState state;
    protected BlackBoard blackBoard;

    public Node(BlackBoard blackBoard)
    {
        this.blackBoard = blackBoard;
    }

    public NodeState Evaluate()
    {
        if (!started)
        {
           /// UnityEngine.Debug.Log($"[BTRunner] Evaluating Behavior Tree , Node name is : {name}");
            OnStart();
            started = true;
        }

        NodeState nodeState = OnUpdate();

        if(nodeState != NodeState.RUNNING)
        {
            OnStop();
            started = false;
        }

        return nodeState;

    }

    public void Stop()
    {
        if (!started) return;
        OnAbort();
        OnStop();
        started = false;
    }

    protected virtual void OnStart() { }
    protected abstract NodeState OnUpdate();
    protected virtual void OnStop() { }

    protected virtual void OnAbort() { }
    public virtual bool CanInterrupt() => false;
}

public abstract class CompositeNode : Node
{

    public List<Node> children = new List<Node>();

    protected CompositeNode(BlackBoard blackBoard) : base(blackBoard) { }
    protected CompositeNode(BlackBoard blackBoard, List<Node> children) : base(blackBoard)
    {
        this.children = children;
    }
}
