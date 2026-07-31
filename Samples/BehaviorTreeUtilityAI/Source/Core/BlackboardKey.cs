using System.ComponentModel;
using UnityEngine;

[CreateAssetMenu(fileName = "New BB Key", menuName = "BT/Blackboard Key")]
public class BlackboardKey : ScriptableObject
{
    public string keyName;
    [ReadOnly(true)] public int keyHash; // 에디터에서 수정 불가능하게 (Custom Property Drawer 필요)

    // 인스펙터에서 값이 바뀔 때마다 해시값을 미리 계산. (C++의 constexpr 해시와 유사)
    private void OnValidate()
    {
        keyHash = Animator.StringToHash(keyName);
    }

    // 암시적 형변환을 통해 string으로 사용할 수 있게 함
    public static implicit operator string(BlackboardKey key) => key.keyName;
}
