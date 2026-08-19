using UnityEngine;

[CreateAssetMenu(menuName = "BT/Utility/GenericFloatScorer")]
public class GenericFloatScorer : WeightScorer
{
    public BlackboardKey targetKey; // HP, Distance, Stamina 등 아무거나 가능
    public AnimationCurve responseCurve; // 수치를 0~1 점수로 변환할 그래프


    public override float GetScore(BlackBoard blackBoard)
    {
        float rawValue = blackBoard.GetFloat(targetKey);
        // 정규화
        float normalizedValue = Mathf.Clamp01((rawValue - minValue) / (maxValue - minValue));

        return responseCurve.Evaluate(normalizedValue);
    }
}
