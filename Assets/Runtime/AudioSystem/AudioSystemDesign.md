# 音频系统设计计划书（终稿）

## 一、系统概述

- **`AudioManager`**：全局单例，负责通道对象池管理、播放决策与音乐渐变。
- **`AudioPlayer`**：挂载在预制体上，持有 `AudioSource`，判断是否请求播放，对外暴露 `Play()`。

---

## 二、AudioManager 设计

### 2.1 通道模型

| 通道类型 | 默认数量 | 上限 | 说明 |
| :--- | :--- | :--- | :--- |
| 音乐通道 | 1（固定） | 1 | 全局唯一，切换时渐变 |
| 音效通道 | 8（默认） | 16（池上限） | 先到先得；占满 8 后按策略判断 |

### 2.2 核心字段

```
_musicSource     : AudioSource         // 当前音乐通道
_pool            : List<AudioSource>   // 对象池，持有最多 16 个通道
_activeChannels  : List<AudioSource>   // 当前活跃（正在播放）的通道子集
CHANNEL_DEFAULT  : int = 8
POOL_MAX         : int = 16
[fadeDuration    : float = 1f]         // Inspector 可配
```

优先级和距离**不在 AudioManager 中缓存**，决策时直接遍历 `_activeChannels`，读取 `source.priority`（Unity 内置字段，0 = 最高）进行即时比较。

### 2.3 对外方法

```csharp
bool PlayMusic(AudioSource source)
bool PlayEffect(AudioSource source, uint priority, float distance)
```

> `priority` 和 `distance` 由调用方（`AudioPlayer`）在请求时传入，AudioManager 不负责存储。

### 2.4 PlayEffect 决策流程

```
PlayEffect(source, priority, distance):
│
├─ [priority == 0] → 紧急抢占
│   └─ 找到 _activeChannels 中 source.priority 最大（最低优先级）的通道
│       → Stop() 该通道，将其重新分配给新请求 → 返回 true
│
├─ [_activeChannels.Count < 8] → 通道未满
│   └─ 从对象池取出通道，PlayOneShot / Play → 返回 true
│
└─ [_activeChannels.Count >= 8] → 通道已满
    ├─ [_activeChannels.Count >= 16] → 池耗尽 → 放弃 → 返回 false
    │
    ├─ [priority < min(_activeChannels.priority) 且 priority < 10]
    │   └─ 池中取新通道播放 → 返回 true
    │
    ├─ [priority 介于 最高与最低之间 且 distance < max(_activeChannels distance)]
    │   └─ 池中取新通道播放 → 返回 true
    │
    └─ 其余 → 放弃 → 返回 false
```

> 分支中的 distance 比较通过遍历 `_activeChannels`，对每个通道找到其对应 `AudioPlayer` 记录的距离（或直接用 `AudioSource.transform.position` 与 `Camera.main.transform.position` 计算）。

### 2.5 通道回收

Update 中遍历 `_activeChannels`，将 `isPlaying == false` 的通道移回对象池。

### 2.6 PlayMusic 渐变

```
新旧两个 AudioSource 同时启动 Coroutine：
  旧：volume 1 → 0 （FadeOut），完成后 Stop()
  新：volume 0 → 1 （FadeIn），同步开始
```

---

## 三、AudioPlayer 设计

### 3.1 字段

```csharp
[SerializeField] private AudioSource _audioSource;  // 预制体上的 AudioSource，priority 在此设置
```

> `AudioSource.priority` 即为优先级（0 最高），直接在 Inspector 配置，无需额外字段。

### 3.2 对外接口

```csharp
public bool Play()
{
    if (AudioManager.Instance == null) return false;

    // 1. 摄像机剔除：正交半宽 = orthographicSize × aspect
    float halfWidth = Camera.main.orthographicSize * Camera.main.aspect;
    if (Mathf.Abs(transform.position.x - Camera.main.transform.position.x) > halfWidth)
        return false;

    // 2. 动态计算距离
    float distance = Vector3.Distance(transform.position, Camera.main.transform.position);

    // 3. 转发请求，priority 从 AudioSource 组件读取
    return AudioManager.Instance.PlayEffect(_audioSource, (uint)_audioSource.priority, distance);
}
```

---

## 四、实施顺序

1. **`AudioManager.cs`**
   - Awake：预创建 8 个通道加入池
   - `PlayEffect`：实现五分支决策（priority==0 抢占、未满、满+优先级高、满+距离近、拒绝）
   - `PlayMusic`：Coroutine FadeIn/FadeOut
   - Update：通道回收

2. **`AudioPlayer.cs`**
   - 持有 `AudioSource`
   - 实现 `Play()`：剔除 → 计算 distance → 委托 AudioManager
