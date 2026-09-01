using System;
using MalbersAnimations.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GrabType { SyncToPivot, FrontOffset }


[System.Serializable]
public struct GrabHitbox
{
    public Collider collider;
    public GrabType grabType;
}
public class GrabManager : MonoBehaviour
{

    [Header("Grab Settings")]
    public Transform grabPivot;

    [Header("Offset Settings")]
    public Vector3 positionOffset = new Vector3(0, -1f, 0); // 예: Y축으로 1만큼 내려서 잡히게
    public Vector3 rotationOffset = Vector3.zero;

    public Vector3 PositionOffset
    {
        get => positionOffset;
        set => positionOffset = value;
    }

    public Vector3 RotationOffset
    {
        get => rotationOffset;
        set => rotationOffset = value;
    }

    [Header("Hitboxes")]
    public List<GrabHitbox> grabHitboxes = new List<GrabHitbox>();

    public bool IsGrabbing { get; private set; }


    public event Action<GameObject> OnGrabSuccess;
    public event Action OnGrabReleased;

    private IGrabbable currentTarget;

    public IGrabbable CurrentTarget => currentTarget;

    void Awake()
    {
        InitializeGrabColliders();
    }

    private void InitializeGrabColliders()
    {
        for (int i = 0; i < grabHitboxes.Count; i++)
        {
            Collider col = grabHitboxes[i].collider;
            GrabType type = grabHitboxes[i].grabType;

            if (col == null) continue;

            col.enabled = false;

            var proxy = col.gameObject.GetComponent<TriggerProxy>() ?? col.gameObject.AddComponent<TriggerProxy>();

            int currentIndex = i; 
            proxy.EnterTriggerInteraction += (root, other) => OnGrabHit(other.gameObject, currentIndex, type);
        }
    }

    public void SetGrabWindowActive(bool active, int grabColliderIndex = 0)
    {
        if (grabColliderIndex >= 0 && grabColliderIndex < grabHitboxes.Count)
        {

            if (grabHitboxes[grabColliderIndex].collider != null)
            {
               // UnityEngine.Debug.Log("Grab WindowActived");
                grabHitboxes[grabColliderIndex].collider.enabled = active;
            }
        }
    }

    public void OnGrabHit(GameObject target, int index, GrabType type)
    {

        UnityEngine.Debug.Log("On Grab hit Called!");
        if (IsGrabbing) return;

        IGrabbable grabbable = target.GetComponentInParent<IGrabbable>();

        if (grabbable != null && grabbable.CanBeGrabbed)
        {
            IsGrabbing = true;
            currentTarget = grabbable;


            grabbable.OnGrabbed(grabPivot);
            OnGrabSuccess?.Invoke(target);


            if (type == GrabType.SyncToPivot) StartCoroutine(SyncPlayerPosition(target.transform));
            // else if (type == GrabType.FrontOffset) ...
        }
    }

    private IEnumerator SyncPlayerPosition(Transform targetTransform)
    {
        float transitionTime = 0.2f; 
        float elapsedTime = 0f;

        Vector3 startPos = targetTransform.position;
        Quaternion startRot = targetTransform.rotation;


        while (elapsedTime < transitionTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / transitionTime;

            Vector3 targetPos = grabPivot.TransformPoint(PositionOffset);
            Quaternion targetRot = grabPivot.rotation * Quaternion.Euler(rotationOffset);

            targetTransform.position = Vector3.Lerp(startPos, targetPos, t);
            targetTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        while (IsGrabbing)
        {

            targetTransform.position = grabPivot.TransformPoint(PositionOffset);
            targetTransform.rotation = grabPivot.rotation * Quaternion.Euler(rotationOffset);
            yield return null;
        }

        ReleaseGrab();
    }


    public void ReleaseGrab()
    {
        if (!IsGrabbing) return;
        //UnityEngine.Debug.Log("<color=magenta>[범인 색출] 누군가 ReleaseGrab을 호출했습니다!</color>\n" + StackTraceUtility.ExtractStackTrace());
        IsGrabbing = false;

        if (currentTarget != null)
        {
            currentTarget.OnReleased();
            currentTarget = null;
        }

        OnGrabReleased?.Invoke();
    }
}