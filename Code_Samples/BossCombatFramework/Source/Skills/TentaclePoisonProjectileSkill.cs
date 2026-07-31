using MalbersAnimations;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class TentaclePoisonProjectileSkill : TentacleSkillBase
{
    public BlackboardKey farAttackLastTimeKey;

    [Header("Projectile Setting vars")]
    public int projectileCount = 10;
    public float projectileDelay = 0.5f;
    private Coroutine posionProjectileCoroutine = null;

    private readonly Vector3 telegraphScale = new Vector3(0.02f, 0.03f, 0.02f);
    public override void CancelSkill()
    {
        if (bossAnimBrain.isAttackSuccess) return;

        if (posionProjectileCoroutine != null) StopCoroutine(posionProjectileCoroutine);
    }

    public void Execute_PosionProjectile()
    {
        var allActivedTentacle = tenTacleManager.AllActivatedTentacles;
        Transform playerTr = bossAnimBrain.GetPlayerTransform();

        BossAttackUtility.PickRandomElements(allActivedTentacle, projectileCount);

        if (posionProjectileCoroutine != null) StopCoroutine(posionProjectileCoroutine);
        posionProjectileCoroutine = StartCoroutine(PoisonProjectileProcess(allActivedTentacle, playerTr));
    }


    private IEnumerator PoisonProjectileProcess(List<GameObject> allActivedTentacle, Transform targetTr)
    {
        int projectileCnt = UnityEngine.Random.Range(1, allActivedTentacle.Count);

        List<TenTacleChild> validTentacles = new List<TenTacleChild>(projectileCnt);

        for (int i = 0; i < projectileCnt; i++)
        {

            GameObject obj = allActivedTentacle[i];

            if (obj != null && obj.TryGetComponent<TenTacleChild>(out var child))
            {
                if (child.IsBusy) continue;

                validTentacles.Add(child);
                child.PoisonTelegraph(telegraphScale);
            }
        }

        yield return new WaitForSeconds(projectileDelay);

        yeogChunAnimEvent.blackBoard.SetFloat(farAttackLastTimeKey, Time.time);

        foreach (var tentacle in validTentacles)
        {
            tentacle.PoisonProjectile(targetTr);
        }
    }

}
