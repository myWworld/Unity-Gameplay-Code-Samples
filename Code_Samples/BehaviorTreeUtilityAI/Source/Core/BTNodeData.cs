using UnityEngine;

public abstract class BTNodeData : ScriptableObject
{
    public string nodeName = "Node Name";
    public abstract Node CreateNode(BlackBoard blackBoarc);
}
