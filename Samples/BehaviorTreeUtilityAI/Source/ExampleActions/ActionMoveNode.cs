using UnityEngine;
using MalbersAnimations.Controller.AI;
using MalbersAnimations.Controller;
using MalbersAnimations;

public class ActionMoveNode : Node
{
    private ActionMoveData data;
    private MAnimal mAnimal;
    private MAnimalAIControl animalAIControl;

    private Transform target;

    public ActionMoveNode(BlackBoard blackBoard, ActionMoveData data) : base(blackBoard)
    {
        this.data = data;
        name = data.nodeName;
        var btRunner = blackBoard.GetComponent<BTRunner>();

        mAnimal = btRunner.GetMAnimal();
        animalAIControl = btRunner.GetMAnimalAIControl();
    }

    protected override void OnStart()
    {
        target = blackBoard.GetObject<Transform>(data.targetTransformKey);

        if(target == null) return;

        UnityEngine.Debug.Log($"[ActionMoveNode] Moving towards target: {target.name}");
        if (animalAIControl.Target != target)
        {
            animalAIControl.SetTarget(target, data.moveToTarget);
        }
        animalAIControl.UpdateDestinationPosition = true;

        if (data.useDynamicStoppingDistance)
        {
            animalAIControl.StoppingDistance = blackBoard.GetFloat(data.distanceKey);
        }
        else
            animalAIControl.StoppingDistance = data.stoppingDistance;

        mAnimal.SpeedSet_Set_Active(data.speedSet, data.speedIndex);
        mAnimal.SetSprint(data.sprint);

        if (data.recordChaseTime == true)
        {
            blackBoard.SetFloat(data.chaseStartTimeKey, Time.time);
        }
    }

    protected override NodeState OnUpdate()
    {

        if (target == null) return NodeState.FAILURE;

        //(단순 타겟 설정용)
        if (data.moveToTarget == false)
        {
            // 타겟 설정은 OnStart에서 이미 수행했으므로, 즉시 완료 처리
            UnityEngine.Debug.Log($"[ActionMoveNode] Target set to {target.name}, skipping movement.");
            return NodeState.SUCCESS;
        }


        float chaseStartTime = blackBoard.GetFloat(data.chaseStartTimeKey);
        if (data.recordChaseTime == true && chaseStartTime >= 0f) //버그 발생시
        {
            float elapsedTime = Time.time - chaseStartTime;
            if (elapsedTime >= data.maxChaseTime)
            {
                UnityEngine.Debug.Log($"[ActionMoveNode] Max chase time exceeded ({elapsedTime} seconds).");
                return NodeState.FAILURE;
            }
        }

        if(data.isForTurning == true) //자연스러운 회전용
        {
            Vector3 direction = (target.position - mAnimal.transform.position).normalized;
            direction.y = 0;

            float dot = Vector3.Dot(direction, mAnimal.transform.forward);

            if (dot > 0.90f)
            {
                return NodeState.SUCCESS;
            }

            return NodeState.RUNNING;
        }

        bool arrived = animalAIControl.HasArrived;

        float distance = Vector3.Distance(mAnimal.transform.position, target.position);
        bool closeEnough = distance <= animalAIControl.StoppingDistance + 1.0f;

        if (arrived || closeEnough)
        {
            return NodeState.SUCCESS; //진짜 이동용
        }

        return NodeState.RUNNING;
    }

    protected override void OnStop()
    {
        if (animalAIControl != null)
        {
            animalAIControl.ClearTarget();
            // animalAIControl.ClearTarget();
        }

        if (data.recordChaseTime == true)
        {
            blackBoard.SetFloat(data.chaseStartTimeKey, -1f);
        }
    }

    protected override void OnAbort()
    {
        if (data.stopAIOnAbort && animalAIControl != null)
        {
            animalAIControl.Stop();
            animalAIControl.UpdateDestinationPosition = false;
        }
    }

    public override bool CanInterrupt() => true;
}
