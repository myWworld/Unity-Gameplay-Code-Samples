using MalbersAnimations;
using MalbersAnimations.Controller;
using UnityEngine;

public class NormalTentacleChild : TenTacleChild
{
    public override void Init(TenTacleManager manager)
    {
        base.Init(manager);
        turnSpeed = 6f;
    }

    public override void ExecuteAttack(Vector3 targetPos, ModeID modeID, int abilityID)
    {
        if (isAttacking) return;
        if (IsDead) return;
        if (mAnimal != null)
        {
            if (turnAndAttackCoroutine != null) StopCoroutine(turnAndAttackCoroutine);
            turnAndAttackCoroutine = StartCoroutine(TurnAndAttackRoutine(targetPos, modeID, abilityID));
        }
    }
    public override void ReturnToPool()
    {
        tentacleManager.BackToNormalTentaclePool(this.gameObject, 2f);
    }
}
