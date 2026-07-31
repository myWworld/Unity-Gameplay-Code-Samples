using UnityEngine;
using MalbersAnimations;
public class GrabTentacleChild : TenTacleChild
{
    [Header("Grab Components")]
    public GrabManager grabManager;
    public Transform partForMafnifying;
    public Vector3 partScale = Vector3.one;

    public float currentThrowPower = 15f;


    public override void Init(TenTacleManager manager)
    {
        base.Init(manager);
        turnSpeed = 3f;
    }

    private void LateUpdate()
    {
        if(partForMafnifying != null)
        {
            partForMafnifying.localScale = partScale;
        }
    }

    public override void ExecuteAttack(Vector3 targetPos, ModeID modeID, int abilityID)
    {
        if (isAttacking) return;
        if (IsDead) return;
        if (mAnimal != null)
        {
            if(turnAndAttackCoroutine != null) StopCoroutine(turnAndAttackCoroutine);
            turnAndAttackCoroutine =  StartCoroutine(TurnAndAttackRoutine(targetPos, modeID, abilityID));
        }

    }


    public void AE_ThrowPlayer()
    {
        var grabbedTarget = grabManager.CurrentTarget;

        if (grabbedTarget != null)
        {
            Vector3 throwDir = (transform.forward + (Vector3.up * 0.5f)).normalized;
            grabbedTarget.OnThrown(throwDir, currentThrowPower, true);
        }

        grabManager.ReleaseGrab();
    }

    public void AE_ThrowPlayerNoDamage()
    {
        var grabbedTarget = grabManager.CurrentTarget;

        if (grabbedTarget != null)
        {
            Vector3 throwDir = (transform.forward + (Vector3.up * 0.5f)).normalized;
            grabbedTarget.OnThrown(throwDir, currentThrowPower, false);
        }

        grabManager.ReleaseGrab();
    }

    public  void Die()
    {
        if (grabManager != null) grabManager.ReleaseGrab();

    }

    public override void ReturnToPool()
    {
        tentacleManager.BackToGrabTentaclePool(this.gameObject, 2f);
    }
}
