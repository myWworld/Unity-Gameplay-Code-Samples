using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Selector", menuName = "BT/Composite/Sequence")]
public class SequenceData : BTNodeData
{

    public List<BTNodeData> childrenData = new List<BTNodeData>();

    public override Node CreateNode(BlackBoard blackBoard)
    {
        List<Node> nodes = new List<Node>();

        foreach (var childData in childrenData)
        {
            nodes.Add(childData.CreateNode(blackBoard));
        }

        return new Sequence(blackBoard, nodes);
    }
}
