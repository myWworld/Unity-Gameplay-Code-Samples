using FS_CombatSystem;
using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.Utilities;
using PixPlays.ElementalVFX;
using System.Collections;
using UnityEngine;

public abstract class TenTacleChild : MonoBehaviour
{
    protected TenTacleManager tentacleManager;
    public Animator animator;
    private EffectManager effectManager;

    public MAnimal mAnimal;
    public StatID hpID;
    public Stats stats;

    [Header("Projectile Vars")]
    public Transform projectileSocket;

    [Header("Effect Prefabs")]
    public GameObject poisonGas;
    public GameObject poisonProjectilePrefab;


    private Stat hpStat;
    public Vector3 initScale;

    protected bool isAttacking = false;
    public bool IsBusy => isAttacking;
    protected bool IsDead => mAnimal.ActiveState.ID == StateEnum.Death;
    protected float turnSpeed = 3f;

    protected Coroutine turnAndAttackCoroutine;

    private void Awake()
    {
        stats = GetComponent<Stats>();
        animator = GetComponent<Animator>();
        mAnimal = GetComponent<MAnimal>();
        hpStat = stats.Stat_Get(hpID);

    }

    private void Start()
    {

        if (effectManager == null)
            effectManager = EffectManager.Instance;
    }

    private void OnEnable()
    {
        if (mAnimal != null)
        {
            mAnimal.OnModeEnd.AddListener(OnMalbersModeEnded);
            mAnimal.OnStateChange.AddListener((stateID) =>
            {
                UnityEngine.Debug.Log($"[{gameObject.name}] 상태 변경됨! 새로운 State ID: {stateID}");
            });
        }
        StartCoroutine(DelayedReset());
    }

    private void OnDisable()
    {
        UnityEngine.Debug.Log($"[{gameObject.name}] 풀로 돌아감 - 논리 상태 초기화");
        if (mAnimal != null) mAnimal.OnModeEnd.RemoveListener(OnMalbersModeEnded);
        StopAllCoroutines();

        isAttacking = false;

        if (TryGetComponent<AutonomousTentacle>(out var brain))
        {
            brain.isSpawnComplete = false;
        }
    }
    public virtual void Init(TenTacleManager manager)
    {
        this.tentacleManager = manager;

        initScale = this.transform.localScale;
    }


    public void ForceClearBusyState()
    {
        isAttacking = false;
    }

    private void OnMalbersModeEnded(int modeID, int abilityID)
    {
        ForceClearBusyState();
    }
    public void AE_TenTacleOnDeath()
    {
        if (!IsValidDeath()) return;

        if (turnAndAttackCoroutine != null)
        {
            StopCoroutine(turnAndAttackCoroutine);
        }

        ReturnToPool();
    }
    protected virtual void ResetTentacle()
    {
        UnityEngine.Debug.Log("Tentacle removed");

        if (hpStat != null)
        {
            hpStat.SetActive(true);
            hpStat.Reset_to_Max();
        }


        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }


        if (mAnimal != null)
        {

            mAnimal.ResetController();
            mAnimal.State_Force(StateEnum.Idle);
        }


    }

    public abstract void ReturnToPool();
    public abstract void ExecuteAttack(Vector3 targetPos, ModeID modeID, int abilityID);
    public void PoisonTelegraph(Vector3 scale)
    {
        effectManager.PlayEffect(poisonGas, projectileSocket.position, Quaternion.identity, scale);
    }
    public void PoisonProjectile(Transform playerTransform)
    {

        GameObject obj = effectManager.SpawnFromPool(poisonProjectilePrefab, projectileSocket.position, Quaternion.identity, 6f);

        if (obj.TryGetComponent<AdvancedProjectileVFX>(out var projectile))
        {

            projectile.SetTarget(playerTransform);

            VfxData data = new VfxData(projectileSocket.position, playerTransform.position, 5f,1f);

            projectile.Play(data);
        }
    }
    protected IEnumerator TurnAndAttackRoutine(Vector3 targetPos, ModeID modeID, int abilityID)
    {
        isAttacking = true;
        Vector3 dirToTarget = targetPos - transform.position;
        dirToTarget.y = 0f;

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;

        if (dirToTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dirToTarget);

            float timeout = 3.0f;

            while (Vector3.Angle(flatForward, dirToTarget) > 2f && timeout > 0f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
                timeout -= Time.deltaTime;

                flatForward = transform.forward;
                flatForward.y = 0f;

                yield return null;
            }

            transform.rotation = targetRotation;
        }

        if (mAnimal != null)
        {
            bool isSuccess = mAnimal.Mode_TryActivate(modeID, abilityID);

            UnityEngine.Debug.LogWarning($"<color=yellow>[촉수 검사]</color> " +
                $"공격 활성화 결과: <b>{isSuccess}</b> | " +
                $"말버스가 인지한 땅 착지 여부(Grounded): <b>{mAnimal.Grounded}</b> | " +
                $"현재 상태 ID: <b>{mAnimal.ActiveStateID}</b>");
        }
    }

    public void AE_TentacleAttackFinished()
    {
        isAttacking = false;
       // UnityEngine.Debug.Log($"[{gameObject.name}] 공격 완료, 다시 명령 대기 중!");
    }
    private bool IsValidDeath()
    {
        if (hpStat != null && hpStat.Value > 0f)
        {
            return false;
        }

        return true;
    }

    private IEnumerator DelayedReset()
    {

        yield return null;

        ResetTentacle();
    }




}
