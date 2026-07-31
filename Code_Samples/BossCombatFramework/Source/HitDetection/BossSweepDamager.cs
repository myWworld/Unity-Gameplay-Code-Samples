using MalbersAnimations;
using MalbersAnimations.Controller;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class BossSweepDamager : MAttackTrigger
{
    [Header("Sweep Settings")]
    public Transform hitboxBase;
    public Transform hitboxMiddle;
    public Transform hitboxTip;
    public float hitRadius = 0.5f;
    [Range(3, 20)] public int segments = 5;
    private int CenterSegmentIndex => segments / 2; // Index of the center sample segment.
    public Transform scaleUpBone;

    private Vector3[] lastPositions;

    private Collider[] overlapResults = new Collider[64];
    private RaycastHit[] castResults = new RaycastHit[64];

    private HashSet<Collider> alreadyHit = new HashSet<Collider>();
    float timeSinceLastHit = 0f;

    protected override void Awake()
    {
        signalOnly = true;
        lastPositions = new Vector3[segments];

        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        alreadyHit.Clear();
        timeSinceLastHit = Time.time;
     //   UnityEngine.Debug.Log($"<color=green>[Sweep] SweepDamager Enabled: {gameObject.name}</color>");

        if (hitboxBase != null && hitboxTip != null)
        {
            if (lastPositions == null || lastPositions.Length != segments)
                lastPositions = new Vector3[segments];

            for (int i = 0; i < segments; i++)
            {
                lastPositions[i] = GetSegmentPosition(i);
            }
        }
    }

    protected override void OnDisable()
    {
        timeSinceLastHit -= Time.time;
    //    UnityEngine.Debug.Log($"<color=green>[Sweep] SweepDamager Disnabled: {timeSinceLastHit}</color>");
    // The damage window is controlled by animation events through DoDamage().
        timeSinceLastHit = 0f;
        base.OnDisable();
    }

    public override void DoDamage(bool value, int profile)
    {
        // Preserve the base trigger behavior while exposing animation-event diagnostics.
        if (value == false)
        {
            UnityEngine.Debug.Log("[Sweep] Damage window disabled.");
        }
        else
        {
            UnityEngine.Debug.Log($"[Sweep] Damage window enabled. Profile ID: {ID}");
        }

        base.DoDamage(value, profile);
    }

    void LateUpdate()
    {
        if (!enabled) return;

        float currentThicknessMultiplier = scaleUpBone != null ? Mathf.Max(scaleUpBone.lossyScale.x, scaleUpBone.lossyScale.z) : 1f;
        float currentHitRadius = hitRadius * currentThicknessMultiplier;

        for (int i = 0; i < segments; i++)
        {
            Vector3 currentPos = GetSegmentPosition(i);
            Vector3 lastPos = lastPositions[i];

            // ---------------------------------------------------------
            // Temporal sweep: cast each segment from its previous position to its current position.
            // This closes gaps caused by fast bone motion between frames.
            // ---------------------------------------------------------
            Vector3 moveDirection = currentPos - lastPos;
            float moveDistance = moveDirection.magnitude;

            if (moveDistance > 0.001f)
            {
                int castCount = Physics.SphereCastNonAlloc(lastPos, currentHitRadius, moveDirection.normalized, castResults, moveDistance, Layer, TriggerInteraction);
                for (int j = 0; j < castCount; j++)
                {
                    ProcessSingleHit(castResults[j].collider, castResults[j].point);
                }
            }

            // ---------------------------------------------------------
            // Spatial fill: overlap capsules between adjacent segments.
            // This closes gaps along the current curved hitbox.
            // ---------------------------------------------------------
            if (i < segments - 1)
            {
                Vector3 nextPos = GetSegmentPosition(i + 1);
                int overlapCount = Physics.OverlapCapsuleNonAlloc(currentPos, nextPos, currentHitRadius, overlapResults, Layer, TriggerInteraction);

                for (int j = 0; j < overlapCount; j++)
                {
                    // Use the midpoint of the adjacent segment pair as an approximate contact point.
                    ProcessSingleHit(overlapResults[j], Vector3.Lerp(currentPos, nextPos, 0.5f));
                }
            }

            // Store the current sample position for the next frame.
            lastPositions[i] = currentPos;
        }
    }


    private void ProcessSingleHit(Collider other, Vector3 hitPoint)
    {
     //   Debug.Log($"<color=orange>[Sweep] Hit Detected: {other.name}</color>");

        if (other == null) return;
        if (alreadyHit.Contains(other)) return;
        if (dontHitOwner && Owner != null && other.transform.IsChildOf(Owner.transform)) return;

        if (Tags != null && Tags.Length > 0)
        {
            if (!other.gameObject.HasMalbersTagInParent(Tags)) return;
        }

        alreadyHit.Add(other);

        if (!AttackDirection) Direction = Owner.transform.forward;
        else Direction = (other.bounds.center - hitboxBase.position).normalized;

      //  if (MissAttack()) return;

        TryInteract(other.gameObject);

        DamagePacket packet = BuildDamagePacket();
        TryPhysics(other.attachedRigidbody, other, hitPoint, packet.ImpactForce);
        TryStopAnimator();

        IMDamage damagee = other.GetComponentInParent<IMDamage>();

       //if (damagee == null)
       //{
       //    Debug.Log($"[Sweep] No IMDamage implementation found on {other.name}.");
       //}
       //else
       //{
       //    Debug.Log($"[Sweep] Damage target resolved: {other.name}.");
       //}

        if (damagee != null) damagee.LastForceMode = ForceMode.Impulse;



        bool canPlayHitEffect = TryPrepareHitEffect(other, hitPoint);
        TryDamage(damagee, packet);
        if (canPlayHitEffect) PlayPreparedHitEffect(other, damagee, packet);

        if (damagee != null) damagee.HitCollider = other;
    }

    private Vector3 GetSegmentPosition(int index)
    {
        // Without a middle point, interpolate directly from base to tip.
        if (hitboxMiddle == null)
        {
            float t = (segments > 1) ? (float)index / (segments - 1) : 0f;
            return Vector3.Lerp(hitboxBase.position, hitboxTip.position, t);
        }
        else
        {
            int midIndex = (segments - 1) / 2;

            // First half: base to middle.
            if (index <= midIndex)
            {
                float t = (midIndex > 0) ? (float)index / midIndex : 0f;
                return Vector3.Lerp(hitboxBase.position, hitboxMiddle.position, t);
            }
            // Second half: middle to tip.
            else
            {
                int remainingSegments = (segments - 1) - midIndex;
                float t = (float)(index - midIndex) / remainingSegments;
                return Vector3.Lerp(hitboxMiddle.position, hitboxTip.position, t);
            }
        }
    }
    void OnDrawGizmosSelected()
    {
        if (hitboxTip != null && hitboxBase != null)
        {

            float currentThicknessMultiplier = scaleUpBone != null ? Mathf.Max(scaleUpBone.lossyScale.x, scaleUpBone.lossyScale.z) : 1f;
            float currentHitRadius = hitRadius * currentThicknessMultiplier;

            Gizmos.color = new Color(1, 0, 0, 0.4f);
            for (int i = 0; i < segments - 1; i++)
            {
                var pos = GetSegmentPosition(i);
                var nextPos = GetSegmentPosition(i + 1);
                Gizmos.DrawSphere(pos, currentHitRadius);
            }
            Gizmos.DrawLine(hitboxBase.position, hitboxTip.position);
        }
    }
}

#if UNITY_EDITOR

namespace MalbersAnimations
{
    [CustomEditor(typeof(BossSweepDamager)), CanEditMultipleObjects]
    public class BossSweepDamagerEd : MAttackTriggerEd
    {
        // Custom inspector fields for the segmented sweep hitbox.
        SerializedProperty hitboxBase, hitboxMiddle, hitboxTip, hitRadius, segments, scaleUpBone;

        protected override void OnEnable()
        {

            base.OnEnable();

            hitboxBase = serializedObject.FindProperty("hitboxBase");
            hitboxMiddle = serializedObject.FindProperty("hitboxMiddle");
            hitboxTip = serializedObject.FindProperty("hitboxTip");
            hitRadius = serializedObject.FindProperty("hitRadius");
            segments = serializedObject.FindProperty("segments");
            scaleUpBone = serializedObject.FindProperty("scaleUpBone");
        }

        protected override void DrawGeneral(bool drawbox = true)
        {

            base.DrawGeneral(drawbox);


            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Sweep Settings (Custom)", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(hitboxBase);
                EditorGUILayout.PropertyField(hitboxMiddle);
                EditorGUILayout.PropertyField(hitboxTip);
                EditorGUILayout.PropertyField(scaleUpBone, new GUIContent("Scale Up Bone", "Bone used to scale hitbox thickness."));

                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(hitRadius, new GUIContent("Hit Radius"));
                    EditorGUILayout.PropertyField(segments, new GUIContent("Segments"));
                }
            }
        }
    }
}
#endif
