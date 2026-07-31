using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct UtilityChildEntry
{
    public BTNodeData childData;    // 실행할 행동 (공격, 회피 등)
    public WeightScorer scorer;     // 이 행동의 점수를 계산할 스코어러 (CompositeScorer 등)
}

[CreateAssetMenu(menuName = "BT/Composite/UtilitySelector")]
public class UtilitySelectorData : BTNodeData
{
    public List<UtilityChildEntry> entries = new List<UtilityChildEntry>();
    public float inertiaBonus = 0.3f;
    public float reEvaluationInterval = 0.2f;

    public override Node CreateNode(BlackBoard blackBoard)
    {
        List<Node> nodes = new List<Node>();

        foreach(var entry in entries)
        {
            nodes.Add(entry.childData.CreateNode(blackBoard));
        }


        return new UtilitySelectorNode(blackBoard, this, nodes);
    }

}
