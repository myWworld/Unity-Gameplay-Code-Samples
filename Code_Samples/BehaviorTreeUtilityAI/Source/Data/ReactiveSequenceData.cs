using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Reactive Sequence", menuName = "BT/Composite/Reactive Sequence")]
public class ReactiveSequenceData : BTNodeData
{
    public List<BTNodeData> childrenData = new List<BTNodeData>();

    public override Node CreateNode(BlackBoard blackBoard)
    {
        List<Node> nodes = new List<Node>();
        foreach (var childData in childrenData)
        {
            nodes.Add(childData.CreateNode(blackBoard));
        }


        return new ReactiveSequenceNode (blackBoard, nodes);
    }
}
