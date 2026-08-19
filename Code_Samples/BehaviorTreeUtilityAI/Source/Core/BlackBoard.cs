using System;
using System.Collections.Generic;
using UnityEngine;

public class BlackBoard : MonoBehaviour
{
    public BossMotor motor;
    public Action OnActionCancel;

    private readonly Dictionary<int, int> intData = new();
    private readonly Dictionary<int, float> floatData = new();
    private readonly Dictionary<int, bool> boolData = new();
    private readonly Dictionary<int, Vector3> vectorData    = new();
    private readonly Dictionary<int, object> objectData = new();

    void Start()
    {
        motor = GetComponent<BossMotor>();
    }

    // 문자열 키를 정수 해시로 변환
    public int GetKeyHash(string key) => Animator.StringToHash(key);

    #region Float
    public void SetFloat(string key, float value) => floatData[GetKeyHash(key)] = value;

    public void SetFloat(BlackboardKey key, float value)
    {
        floatData[key.keyHash] = value;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        return floatData.TryGetValue(GetKeyHash(key), out float value) ? value : defaultValue;
    }

    public float GetFloat(BlackboardKey key, float defaultValue = 0f)
    {
        return floatData.TryGetValue(key.keyHash, out float value) ? value : defaultValue;
    }
    #endregion

    #region Int
    public void SetInt(string key, int value) => intData[GetKeyHash(key)] = value;
    public void SetInt(BlackboardKey key, int value)
    {
        intData[key.keyHash] = value;
    }
    public int GetInt(string key, int defaultValue = 0)
    {
        return intData.TryGetValue(GetKeyHash(key), out int value) ? value : defaultValue;
    }

    public int GetInt(BlackboardKey key, int defaultValue = 0)
    {
        return intData.TryGetValue(key.keyHash, out int value) ? value : defaultValue;
    }
    #endregion

    #region Bool
    public void SetBool(string key, bool value) => boolData[GetKeyHash(key)] = value;
    public void SetBool(BlackboardKey key, bool value)
    {
        boolData[key.keyHash] = value;
    }
    public bool GetBool(string key, bool defaultValue = false)
    {
        return boolData.TryGetValue(GetKeyHash(key), out bool value) ? value : defaultValue;
    }

    public bool GetBool(BlackboardKey key, bool defaultValue = false)
    {
        return boolData.TryGetValue(key.keyHash, out bool value) ? value : defaultValue;
    }
    #endregion

    #region Vector3
    public void SetVector3(string key, Vector3 value) => vectorData[GetKeyHash(key)] = value;

    public void SetVector3(BlackboardKey key, Vector3 value)
    {
        vectorData[key.keyHash] = value;
    }

    public Vector3 GetVector3(string key)
    {
        Vector3 defaultValue = Vector3.zero;
        return vectorData.TryGetValue(GetKeyHash(key), out Vector3 value) ? value : defaultValue;
    }

    public Vector3 GetVector3(BlackboardKey key)
    {
        return vectorData.TryGetValue(key.keyHash, out Vector3 value) ? value : Vector3.zero;
    }

    #endregion

    #region Object

    public void SetObject<T>(string key, T value) where T : class
    {
        objectData[GetKeyHash(key)] = value;
    }
    public void SetObject<T>(BlackboardKey key, T value) where T : class
    {
        objectData[key.keyHash] = value;
    }


    public T GetObject<T>(string key) where T : class
    {
        if (objectData.TryGetValue(GetKeyHash(key), out object value))
        {
            return value as T;
        }
        return null;
    }

    public T GetObject<T>(BlackboardKey key) where T : class
    {
        if (objectData.TryGetValue(key.keyHash, out object value))
        {
            return value as T;
        }
        return null;
    }
    #endregion

    public BossMotor GetBossMotor() => motor;
}
