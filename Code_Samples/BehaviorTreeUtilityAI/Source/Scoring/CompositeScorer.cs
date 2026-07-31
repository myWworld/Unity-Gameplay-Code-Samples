using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "BT/Utility/CompositeScorer")]
public class CompositeScorer : WeightScorer
{
    public enum CombineMode { Add, Multiply, Average, Max , Linear}
    public CombineMode mode;

    [System.Serializable]
    public struct ScorerWeight
    {
        public WeightScorer scorer; // 개별 스코어러 (Distance, HP 등)
        public float weight;        // 가중치 (w1, w2 등)
    }

    public List<ScorerWeight> elements;

    public override float GetScore(BlackBoard blackBoard)
    {
        if (elements == null || elements.Count == 0) return 0f;

        float finalScore = (mode == CombineMode.Multiply) ? 1f : 0f;

        foreach (var element in elements)
        {
            if (element.scorer == null) continue;

            float s = element.scorer.GetScore(blackBoard);

            switch (mode)
            {
                case CombineMode.Add: finalScore += s; break;
                case CombineMode.Multiply: finalScore *= s; break;
                case CombineMode.Max: finalScore = Mathf.Max(finalScore, s); break;
                case CombineMode.Average: finalScore += s; break;
                case CombineMode.Linear:
                    {
                        float weightedScore = s * element.weight;
                        finalScore += weightedScore;
                        break;
                    }
            }
        }

        if (mode == CombineMode.Average) finalScore /= elements.Count;
        finalScore = Mathf.Clamp01(finalScore); // 최종 점수를 0~1 범위로 제한
        finalScore = Mathf.Lerp(minValue, maxValue, finalScore); // minValue와 maxValue 사이로 스케일링
        return finalScore;
    }
}
