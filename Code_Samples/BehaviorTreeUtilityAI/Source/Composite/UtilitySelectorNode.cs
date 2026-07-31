using System.Collections.Generic;
using UnityEngine;


public class UtilitySelectorNode : CompositeNode
{
    private UtilitySelectorData data;
    private Node activeNode;

    private float reEvaluationTimer;



    public UtilitySelectorNode(BlackBoard blackBoard, UtilitySelectorData data, List<Node> children) : base(blackBoard, children)
    {
        this.data = data;
    }

    protected override void OnStart()
    {



    }

    protected override NodeState OnUpdate()
    {


        // 실행 중인 노드가 있을 때 재평가 타이머 체크
        if (activeNode != null)
        {

            if (activeNode.CanInterrupt())
            {
                reEvaluationTimer += Time.deltaTime;
            }


            if (reEvaluationTimer >= data.reEvaluationInterval)
            {
                reEvaluationTimer = 0f;
                Node newNode = SelectBestChild();

                // 현재 실행 중인 노드와 다른 노드가 선택되었다면 교체
                if (newNode != null && newNode != activeNode)
                {
                    UnityEngine.Debug.Log($"[UtilitySelectorNode] Switching from {activeNode} to {newNode}");
                    activeNode.Stop(); // 기존 노드 중단
                    activeNode = newNode; // 새 노드로 교체
                                          // 새 노드는 아래 Evaluate에서 자동으로 OnStart가 호출
                }
            }

            var state = activeNode.Evaluate();
            if (state != NodeState.RUNNING) activeNode = null;
            return state;
        }

        // 실행 중인 노드가 없을 때
        reEvaluationTimer = 0f;
        activeNode = SelectBestChild();
        if (activeNode == null) return NodeState.FAILURE;

        return activeNode.Evaluate();

    }

    protected override void OnStop()
    {
        activeNode = null;
    }

    protected override void OnAbort()
    {
        if (activeNode != null)
        {
            activeNode.Stop();
        }
    }


    private Node SelectBestChild()
    {
        float totalWeight = 0f;
        List<float> calculatedWeights = new List<float>();
        Node selectedChild = null;

        for (int i = 0; i < data.entries.Count; i++)
        {
            float score = Mathf.Max(0f, data.entries[i].scorer.GetScore(blackBoard));

            // 현재 실행 중인 노드라면 가산점을 줘서 쉽게 안 바뀌게
            if (children[i] == activeNode)
            {
                score += data.inertiaBonus;
            }

            calculatedWeights.Add(score);
            totalWeight += score;
        }

        if (totalWeight <= 0f) return null;

        float max_val = -1f;
        int selected_idx = 0;
        for(int i =0; i < calculatedWeights.Count; i++)
        {
            if(max_val < calculatedWeights[i])
            {
                max_val = calculatedWeights[i];
                selected_idx = i;
            }
        }

        selectedChild = children[selected_idx];

        //float roll = UnityEngine.Random.Range(0, totalWeight);
        //float cumulativeWeight = 0f;

        //for (int i = 0; i < children.Count; i++) //룰렛 휠 알고리즘
        //{
        //    cumulativeWeight += calculatedWeights[i];
        //    if (roll < cumulativeWeight)
        //    {
        //        selectedChild = children[i];
        //        UnityEngine.Debug.Log($"[UtilitySelectorNode] Selected child node with idx: {i}");
        //        break;
        //    }
        //}

        return selectedChild;
    }

}
