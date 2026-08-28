
using MalbersAnimations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TentacleSpawnSkill : TentacleSkillBase
{

    [Header("Spawn vars")]
    public float maxSpawnRadius = 6f;
    public float minSpawnRadius = 1.0f;
    public int maxAttempts = 20;
    public float minDistance = 1.5f;

    private const float Tentacle_UNDERGROUND_DEPTH = 5.5f;
    private Coroutine tentacleGenerateCoroutine = null;

    [Header("VFX vars")]
    public GameObject greenPortal;

    [Header("Blackboard")]
    public BlackBoard blackBoard;
    public BlackboardKey tentacleSpawnLastTimeKey;
    public float spawnCooldown = 10f;

    private bool isUninterruptibleSpawn = false; //오프닝일땐 여러개 한번에 스폰해서 이게 끝나기도 전에 다른 쪽에서 스킬 취소시 다 사라져버리는 거 방지용 1회용 플래그

    private void Awake()
    {
        if (blackBoard == null)
            blackBoard = GetComponentInParent<BlackBoard>();
        blackBoard.SetFloat(tentacleSpawnLastTimeKey, -spawnCooldown);
    }

    public override void CancelSkill()
    {
        if (isUninterruptibleSpawn) return;//오프닝 생성때는 취소 안함

        //for (int i = tenTacleManager.AllActivatedTentacles.Count - 1; i >= 0; i--)
        //{
        //    var tentacle = tenTacleManager.AllActivatedTentacles[i];
        //    if (tentacle.TryGetComponent<AutonomousTentacle>(out var brain))
        //    {
        //        if (!brain.isSpawnComplete)// || Vector3.Distance(tentacle.transform.localScale, child.initScale) > 0.1f)
        //        {
        //            if (tentacle.TryGetComponent<TenTacleChild>(out TenTacleChild child))
        //            {
        //                if (Vector3.Distance(tentacle.transform.localScale, child.initScale) > 1.0f)
        //                {
        //                    child.ReturnToPool();
        //                }

        //            }
        //        }

        //    }
        //}

        if (bossAnimBrain.isAttackSuccess) return;

        if (tentacleGenerateCoroutine != null) StopCoroutine(tentacleGenerateCoroutine);



    }

    public void Execute_TetnaclesGenerate(int num)
    {
        if (tentacleGenerateCoroutine != null) StopCoroutine(tentacleGenerateCoroutine);
        tentacleGenerateCoroutine = StartCoroutine(TentacleSequentialGenerateProcess(num));
    }

    private IEnumerator TentacleSequentialGenerateProcess(int num)
    {
        isUninterruptibleSpawn = true;
        for (int i = 0; i < num; i++)
        {
            Vector3 playerPos = bossAnimBrain.GetPlayerPos();
            Vector3 validPos = BossAttackUtility.GetValidSpawnPosition(playerPos, minSpawnRadius, maxSpawnRadius, groundLayer, minDistance, tenTacleManager.AllActivatedTentacles, null, maxAttempts);

            Vector3 directionToPlayer = playerPos - validPos;
            directionToPlayer.y = 0f;
            Quaternion lookRot = directionToPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(directionToPlayer) : Quaternion.identity;

            GameObject tentacle = tenTacleManager.SpawnRandomAvailableTentacle(validPos, lookRot);

            if (tentacle != null)
            {

                StartCoroutine(SingleTentacleSpawnProcess(tentacle));
            }

            yield return new WaitForSeconds(0.3f);
        }

        isUninterruptibleSpawn = false;
    }

    public void Execute_TentacleGenerate()
    {
        if(Time.time - blackBoard.GetFloat(tentacleSpawnLastTimeKey) < spawnCooldown) return;

        Vector3 playerPos = bossAnimBrain.GetPlayerPos();
        Vector3 validPos = BossAttackUtility.GetGroundPos(playerPos, groundLayer,2f,20f);

        Vector3 flatPlayerPos = new Vector3(playerPos.x, 0f, playerPos.z);
        Vector3 flatValidPos = new Vector3(validPos.x, 0f, validPos.z);

        if ((flatPlayerPos - flatValidPos).sqrMagnitude > 1f)
        {
            return;
        }

        if (Mathf.Abs(playerPos.y - validPos.y) > 3f)
        {
            return;
        }

        validPos = BossAttackUtility.GetValidSpawnPosition(playerPos, minSpawnRadius, maxSpawnRadius, groundLayer, minDistance, tenTacleManager.AllActivatedTentacles, null, maxAttempts);


        Vector3 directionToPlayer = playerPos - validPos;
        directionToPlayer.y = 0f;

        Quaternion lookRot = Quaternion.identity;

        if (directionToPlayer.sqrMagnitude > 0.01f)
        {
            lookRot = Quaternion.LookRotation(directionToPlayer);
        }

        GameObject tentacle = tenTacleManager.SpawnRandomAvailableTentacle(validPos, lookRot);

        if (tentacle == null) return;

        blackBoard.SetFloat(tentacleSpawnLastTimeKey,Time.time);
        StartCoroutine(SingleTentacleSpawnProcess(tentacle));
    }

    private IEnumerator SingleTentacleSpawnProcess(GameObject tentacle)
    {
        var tentacleChild = tentacle.GetComponent<TenTacleChild>();
        Vector3 groundPos = tentacle.transform.position;
        groundPos.y += 0.02f;

        var effect = bossAnimBrain.effectManger.PlayEffect(greenPortal, groundPos);

        yield return new WaitForSeconds(0.8f);

        yield return StartCoroutine(BossAttackUtility.ScaleTargetsRoutine(new Transform[] { tentacle.transform }, 1f, tentacleChild.initScale));

        tentacle.transform.position = groundPos;

        if (tentacle.TryGetComponent<AutonomousTentacle>(out var brain))
        {
            brain.isSpawnComplete = true;
        }

        if (effect.TryGetComponent<UnityEngine.VFX.VisualEffect>(out UnityEngine.VFX.VisualEffect vfx))
            vfx.SendEvent("OnStop");
        else
            effect.SetActive(false);


    }

}
