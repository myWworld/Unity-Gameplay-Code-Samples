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


    public void AE_ThrowPlayer(int DamageOn)
    {
        var grabbedTarget = grabManager.CurrentTarget;//그랩 매니져가 IGrabble만 반환함, 촉수는 관련해서 알 필요 없고 잡힌 쪽 로직만 호출해 주면됨
        bool damage = DamageOn == 1 ? true : false;

        if (grabbedTarget != null)
        {
            Vector3 throwDir = (transform.forward + (Vector3.up * 0.5f)).normalized;
            grabbedTarget.OnThrown(throwDir, currentThrowPower, damage);
        }

        grabManager.ReleaseGrab();
    }

    public  void Die()
    {
        if (grabManager != null) grabManager.ReleaseGrab();

    }

    public override void ReturnToPool()
    {
        tentacleManager.BackToGrabTentaclePool(this.gameObject, 2f);//풀로반환 그랩용 촉수 관리용
    }
}
