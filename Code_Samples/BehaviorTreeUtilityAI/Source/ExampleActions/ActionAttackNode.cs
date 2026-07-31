using MalbersAnimations;
using MalbersAnimations.Controller;
using UnityEngine;

public class ActionAttackNode : ActionPlayMode
{
    private ActionAttackData data;
    private Transform target;

    private float currentMultiplier = 1f;

    public ActionAttackNode(BlackBoard blackBoard, ActionAttackData data) : base(blackBoard, data)
    {
        this.data = data;
        name = data.nodeName;
        target = blackBoard.GetObject<Transform>(data.targetKey);

    }


    protected override void OnStart()
    {
        if(target == null)
            target = blackBoard.GetObject<Transform>(data.targetKey);

        base.OnStart();
        mAnimal.PostStateMovement += ApplyRootMotionScaling;
    }

    protected override NodeState OnUpdate()
    {
        if (data.needMultiplier)
            currentMultiplier = CalculateDynamicMultiplier();
        else
            currentMultiplier = 1f;

          //  UnityEngine.Debug.Log($"[ActionAttackNode] currentMultiplier is {currentMultiplier}");
        return base.OnUpdate();
    }

    protected override void OnStop()
    {
        currentMultiplier = 1f;
        mAnimal.PostStateMovement -= ApplyRootMotionScaling;

        base.OnStop();
    }

    private void ApplyRootMotionScaling(MAnimal animal)
    {
        if (animal.ActiveMode?.ID != data.modeID || !animal.IsPlayingMode)
        {
            return;
        }

        Vector3 warpedDelta = animal.AdditivePosition;

        // 수평 이동거리만 조정
        warpedDelta.x *= currentMultiplier;
        warpedDelta.z *= currentMultiplier;

        animal.AdditivePosition = warpedDelta;
    }

    private float CalculateDynamicMultiplier()
    {
    //    UnityEngine.Debug.Log($"[ActionAttackNode] target is {target} mAnimal is {mAnimal}");

        if (target == null || mAnimal == null) return 1f;

        float distance = Vector3.Distance(mAnimal.transform.position, target.position);

        float t = Mathf.InverseLerp(data.minReach, data.maxReach, distance);

        float multiplier = Mathf.Lerp(data.minMultiplier, data.maxMultiplier, t);

        return multiplier;
    }

}
