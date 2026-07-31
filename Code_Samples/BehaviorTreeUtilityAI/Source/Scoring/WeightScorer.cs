using UnityEngine;

public abstract class WeightScorer : ScriptableObject
{
    public float minValue = 0f;
    public float maxValue = 100f;

    public abstract float GetScore(BlackBoard blackBoard);
}
