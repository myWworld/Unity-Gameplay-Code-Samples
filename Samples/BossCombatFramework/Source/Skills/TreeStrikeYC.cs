using MalbersAnimations.Utilities;
using UnityEngine;
using System.Collections;

public class TreeStrikeYC : BossSkill
{
    private YeogChunAnimEvent yeogChunAnimEvent;

    [Header("Effect Prefabs")]
    public GameObject EarthSlamYC;
    public GameObject SmokePopUp;

    public GameObject treeBatPrefab;
    public Transform TreeSocket;
    public float damageRadius = 4f;
    public float damageAmount = 50f;

    private TreeAttackData treeForStrike = null;
    private Coroutine treeStrikeCoroutine = null;

    private Collider[] hitColliders = new Collider[10];
    public override void Init(BossAnimEventBridge brain)
    {
        base.Init(brain);

        yeogChunAnimEvent = brain as YeogChunAnimEvent;


        if (treeBatPrefab)
        {
            treeForStrike = new TreeAttackData();
            treeForStrike.Init(treeBatPrefab, Vector3.zero);
            treeForStrike.trigger.HasDealtDamage = true;

           bossAnimBrain.SetupWeaponTrailEffect(bossAnimBrain.weaponTrailEffectForWeapon, treeForStrike.transform, new Vector3(0, -0.55f, 0), new Vector3(0, 1.45f, 0));
        }
    }
    public override void CancelSkill()
    {
        CancelTreeStrike();

    }

    public void Execute_GenerateAndEquip()
    {
        Vector3 spawnPos = bossAnimBrain.transform.position + bossAnimBrain.transform.forward * 1.4f;

        spawnPos = BossAttackUtility.GetGroundPos(spawnPos, groundLayer,2f , 20f);


        if (treeForStrike != null)
        {
            if (treeStrikeCoroutine != null) StopCoroutine(treeStrikeCoroutine);
            treeStrikeCoroutine = StartCoroutine(EquipTreeRoutine(spawnPos));
        }

    }

    private IEnumerator EquipTreeRoutine(Vector3 spawnPos)
    {
        treeForStrike.transform.localRotation = Quaternion.identity;
        treeForStrike.Activate(spawnPos);

        float elapsedTime = 0f;
        float duration = 0.75f;
        Vector3 targetScale = new Vector3(2.0f, 3.0f, 2.0f);
        Vector3 startScale = treeForStrike.transform.localScale;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            treeForStrike.transform.localScale = Vector3.Lerp(startScale, targetScale, elapsedTime / duration);
            yield return null;
        }

        treeForStrike.rootObj.transform.SetParent(TreeSocket, true);
        treeForStrike.rootObj.transform.localPosition = Vector3.zero;
        treeForStrike.rootObj.transform.localRotation = Quaternion.identity;

        bossAnimBrain.animator.SetTrigger("DoStrike");
    }

    public void Execute_StrikeEffectAndDamage()
    {
        Vector3 pos = treeForStrike.transform.position + Vector3.up * 2f;
        pos = BossAttackUtility.GetGroundPos(pos, groundLayer, 0f, 20f);


        bossAnimBrain.effectManger.PlayEffect(EarthSlamYC, pos, Quaternion.identity, new Vector3(4f, 3f, 4f));

        BossAttackUtility.ApplySphereRangeDamage(pos, damageRadius, damageAmount, targetLayer, hpID, playerTag, hitColliders);
    }

    public void Execute_TreeStrikeTriggerToggle(int on)
    {
        if (treeForStrike != null)
        {
            treeForStrike.trigger.HasDealtDamage = on == 1 ? false : true;
        }
    }

    public void Execute_TreeStrikeEnd()
    {
        if (treeForStrike != null)
        {
            treeForStrike.rootObj.SetActive(false);
            bossAnimBrain.WeaponFireEffectOff(0.01f);
            treeForStrike.trigger.HasDealtDamage = true;
            bossAnimBrain.effectManger.PlayEffect(SmokePopUp, treeForStrike.transform.position, Quaternion.identity);
            treeForStrike.rootObj.transform.SetParent(null, true);
        }
    }

    private void CancelTreeStrike()
    {
     //  if (bossAnimBrain.isAttackSuccess)
     //  {
     //      return;
     //  }

        if (treeStrikeCoroutine != null)
        {
            StopCoroutine(treeStrikeCoroutine);
            treeStrikeCoroutine = null;
        }

        if (treeForStrike != null && treeForStrike.rootObj.activeInHierarchy)
        {
            Execute_TreeStrikeEnd();
        }
    }
}
