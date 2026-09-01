using UnityEngine;

public interface IGrabbable 
{
    void OnGrabbed(Transform grabPivot);
    void OnReleased();
    void OnThrown(Vector3 direction, float force, bool thrownDamage);
    bool CanBeGrabbed { get; }
}
