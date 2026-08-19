using UnityEngine;
using MalbersAnimations.Controller;
using MalbersAnimations.Controller.AI;
using UnityEngine.AI;

public class ActionJumpAttackNode : ActionPlayMode
{
    private ActionJumpAttackData jumpData;
    private Vector3 startPos;
    private Vector3 targetLandingPos;
    private Vector3 lastPlayerPos;

    private float timer;
    private float jumpDuration = 0.6f;

    private Transform target;

    private Animator animator;

    private float originalAnimSpeed;

    private bool isLanded = false;
    private bool isInterrupted = false;

    public ActionJumpAttackNode(BlackBoard blackBoard, ActionJumpAttackData data) : base(blackBoard, data)
    {
        this.jumpData = data;
        name = data.nodeName;
        animator = mAnimal?.GetComponent<Animator>();


    }

    protected override void OnStart()
    {




        target = blackBoard.GetObject<Transform>(jumpData.targetTransformKey);
        if (target == null || mAnimal == null || animator == null) return;

        isInterrupted = false;
        blackBoard.OnActionCancel += JumpStop;

        timer = 0;

        mAnimal.ActiveState_Persisent(true);
        mAnimal.RootMotion = false;   // Y축 데이터 누적 방지
        mAnimal.Grounded = false;     // 지면 적응 로직 차단
        mAnimal.UseGravity = false;   // 엔진 중력 일시 정지
        mAnimal.DisablePosition = true; // 말버스가 위치를 건드리지 못하게 함

        lastPlayerPos = target.position;
        startPos = mAnimal.transform.position;
        float distance = Vector3.Distance(startPos, target.position);

        //  거리에 따른 물리적 체공 시간 계산 (t = d / v)
        float physicalDuration = distance / jumpData.baseSpeed;

        //  애니메이션 클립의 실제 체공 구간 길이
        float clipAirTime = jumpData.animationClip.length * jumpData.jumpRatio;

        // 최종 jumpDuration 결정 (너무 짧거나 길지 않게 Clamp)
        jumpDuration = Mathf.Clamp(physicalDuration, 1.0f, 2.0f);

        // 결정된 시간에 맞춰 애니메이터 속도 동기화
        animator.speed = clipAirTime / jumpDuration;

        //  업데이트된 jumpDuration으로 예측 지점 재계산
        Vector3 playerVelocity = Vector3.zero;
        var targetRB = target.GetComponent<Rigidbody>();
        if (targetRB != null) playerVelocity = targetRB.linearVelocity;
        playerVelocity.y = 0;


        targetLandingPos = GetTargetPos(playerVelocity);

        // 나비메쉬 유효성 체크 (맵 밖으로 튀는 것 방지)
        if (NavMesh.SamplePosition(targetLandingPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            targetLandingPos = hit.position;
        }
        else
        {
            // 3f 내에 바닥이 없으면, 최소한 타겟(플레이어)의 현재 위치로 착지 지점을 변경
            targetLandingPos = target.position;
        }
        targetLandingPos.y -= 0.5f;


        UnityEngine.Debug.Log($"[ActionJumpAttackNode] jump Durtaion : {jumpDuration}");

        isLanded = false;


        base.OnStart();    // 기본 애니메이션 실행 (ActionPlayMode의 로직 호출)
        blackBoard.SetBool(jumpData.isNearGroundKey, false); // 점프 시작 신호

    }

    protected override NodeState OnUpdate()
    {
        if (target == null || mAnimal == null) return NodeState.FAILURE;

        if(isInterrupted == true)
        {
            return NodeState.FAILURE;
        }

        if (!blackBoard.GetBool(jumpData.lanchKey))
        {
            mAnimal.transform.position = startPos;
            return NodeState.RUNNING;
        }



        timer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(timer / jumpDuration);

        if (normalizedTime < 0.6f) // 추적 허용 구간
        {
            Vector3 currentVelocity = (target.position - lastPlayerPos) / Time.deltaTime;
            lastPlayerPos = target.position;

            float remainingTime = jumpDuration - timer;
            Vector3 predictedPos = target.position + (currentVelocity * remainingTime);


            if (NavMesh.SamplePosition(predictedPos, out NavMeshHit hitUpdate, 6f, NavMesh.AllAreas))
            {
                predictedPos = hitUpdate.position;
            }
            else
            {
                // 맵 밖으로 뛸 것 같으면 타겟의 현재 위치로 제한
                predictedPos = target.position;
            }

            targetLandingPos = Vector3.Lerp(targetLandingPos, predictedPos, Time.deltaTime * 5f);
        }

        // 5. 궤적 계산
        float t = jumpData.speedCurve.Evaluate(normalizedTime);

        // 수평 위치 (Lerp)
        Vector3 currentPos = Vector3.Lerp(startPos, targetLandingPos, t);

        // 수직 높이 (포물선 공식: 4 * h * t * (1-t))
        float yOffset = 4 * jumpData.jumpHeight * t * (1 - t);
        currentPos.y += yOffset;

        // 위치 적용
        mAnimal.transform.position = currentPos;


        if (normalizedTime >= 1.0f)
        {
            if (!isLanded)
            {
                // 1. 강제 위치 보정: 계산된 최종 목적지로 순간이동
                mAnimal.transform.position = targetLandingPos;

                // 2. 물리적 지면 확인 (Raycast)
                // 캐릭터 발밑으로 레이를 쏴서 실제 지면 좌표를 가져옵니다.
                if (Physics.Raycast(targetLandingPos + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 2f, jumpData.groundLayer))
                {
                    mAnimal.transform.position = hit.point; // 실제 바닥 점으로 스냅
                }

                // 3. 말버스 엔진에 강제로 착지 알림
                mAnimal.Grounded = true;
                mAnimal.AlignPosition(); // 지면 각도에 맞게 정렬
                animator.speed = originalAnimSpeed;

                isLanded = true;
            }

            if (mAnimal.IsPlayingMode)
            {
                return NodeState.RUNNING; // 아직 착지 모션 중이니 계속 실행
            }

            return NodeState.SUCCESS;

        }

        lastPlayerPos = target.position;

        return NodeState.RUNNING;
    }

    protected override void OnStop()
    {
        if (blackBoard != null)
        {
            blackBoard.SetBool(jumpData.lanchKey, false); // 다음 점프를 위해  리셋
        }

        animator.speed = originalAnimSpeed; // 애니메이터 속도 복구
        base.OnStop();

        UnityEngine.Debug.Log($"[ActionJumpAttackNode] ActionJumpAttackNode Stop");
        blackBoard.OnActionCancel -= JumpStop;

        // 7. 상태 복구
        if (mAnimal != null)
        {
            mAnimal.ActiveState_Persisent(false);
            mAnimal.RootMotion = true;
            mAnimal.UseGravity = true;
            mAnimal.Grounded = true;
            mAnimal.DisablePosition = false;
            mAnimal.DisableRotation = false;

            // 착지 시 정확한 위치 고정을 위해 다시 한 번 바닥 정렬
            mAnimal.AlignPosition();
        }
    }

    private Vector3 GetTargetPos(Vector3 playerVelocity)
    {
        switch (jumpData.jumpType)
        {
            case ActionJumpAttackData.JumpType.Predictive:
                return target.position + (playerVelocity * jumpDuration * jumpData.leadMultiplier);
            case ActionJumpAttackData.JumpType.Offset:
                Vector3 dirToBoss = (target.position - mAnimal.transform.position).normalized;
                return target.position + (dirToBoss * jumpData.targetOffset);
        }

        return target.position;

    }

    private void JumpStop()
    {

        isInterrupted = true;
        mAnimal.Mode_Stop(true);

    }
}
