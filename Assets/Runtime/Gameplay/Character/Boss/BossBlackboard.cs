using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS 轻量黑板：标志位与计数容器，供被动机制/特殊逻辑写入、攻击槽条件读取。
/// 不存放阵营/目标/生命等基础状态（那些归 GameObjectProperty 所有）。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossBlackboard : MonoBehaviour
{
    private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
    private readonly Dictionary<string, float> _counters = new Dictionary<string, float>();

    /// <summary>写入一个标志位。</summary>
    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrEmpty(key))
            return;
        _flags[key] = value;
    }

    /// <summary>读取标志位（不存在时返回 false）。</summary>
    public bool GetFlag(string key)
    {
        return !string.IsNullOrEmpty(key) && _flags.TryGetValue(key, out bool value) && value;
    }

    /// <summary>写入一个计数器。</summary>
    public void SetCounter(string key, float value)
    {
        if (string.IsNullOrEmpty(key))
            return;
        _counters[key] = value;
    }

    /// <summary>读取计数器（不存在时返回 0）。</summary>
    public float GetCounter(string key)
    {
        return !string.IsNullOrEmpty(key) && _counters.TryGetValue(key, out float value) ? value : 0f;
    }

    /// <summary>给计数器累加一个增量。</summary>
    public void AddCounter(string key, float delta)
    {
        if (string.IsNullOrEmpty(key))
            return;
        SetCounter(key, GetCounter(key) + delta);
    }
}
