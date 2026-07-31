using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Selector", menuName = "BT/Composite/Selector")]
public class SelectorData : BTNodeData
{
    public List<BTNodeData> childrenData = new List<BTNodeData>();

    public override Node CreateNode(BlackBoard blackBoard)
    {
        name = nodeName;
        List<Node> nodes = new List<Node>();

        foreach (var childData in childrenData)
        {
            nodes.Add(childData.CreateNode(blackBoard));
        }

        return new Selector(blackBoard, nodes);
    }
}
