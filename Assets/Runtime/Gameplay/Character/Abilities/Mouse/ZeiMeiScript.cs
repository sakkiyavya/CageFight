using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class ZeiMeiScript : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int killReward = 100;

    private GameObjectProperty prop;

    private readonly Dictionary<GameObjectProperty, Action<Damage>>
        watchedTargets =
            new Dictionary<GameObjectProperty, Action<Damage>>();

    private readonly HashSet<GameObjectProperty> rewardedTargets =
        new HashSet<GameObjectProperty>();

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    private void OnEnable()
    {
        prop.OnAtt += WatchCurrentTarget;
    }

    private void OnDisable()
    {
        prop.OnAtt -= WatchCurrentTarget;

        foreach (var pair in watchedTargets)
        {
            if (pair.Key != null)
                pair.Key.OnHitted -= pair.Value;
        }

        watchedTargets.Clear();
        rewardedTargets.Clear();
    }

    private void Update()
    {
        // 提前监听当前锁定目标。
        WatchCurrentTarget();

        /*
         * 对象池中的敌人复活后，
         * 允许再次提供击杀奖励。
         */
        foreach (GameObjectProperty target in watchedTargets.Keys)
        {
            if (target != null && !target.isDead)
                rewardedTargets.Remove(target);
        }
    }

    private void WatchCurrentTarget()
    {
        WatchTarget(prop.target);
    }

    private void WatchTarget(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        GameObjectProperty target =
            targetObject.GetComponent<GameObjectProperty>();

        if (target == null ||
            target.side == prop.side ||
            watchedTargets.ContainsKey(target))
        {
            return;
        }

        Action<Damage> listener = null;

        listener = damage =>
        {
            CheckKill(target, damage);
        };

        watchedTargets.Add(target, listener);
        target.OnHitted += listener;
    }

    private void CheckKill(
        GameObjectProperty target,
        Damage damage)
    {
        if (target == null || target.isDead)
            return;

        // 必须是贼眉本人造成的伤害。
        if (damage.source != gameObject)
            return;

        Damage calculated =
            DamageComputor.DamageCompute(damage);

        int finalDamage =
            Mathf.Max(0, calculated.finalDamage);

        // 本次伤害不足以击杀。
        if (target.currentHp - finalDamage > 0)
            return;

        // 防止同一个死亡事件重复获得金币。
        if (!rewardedTargets.Add(target))
            return;

        if (Coins.Instance == null)
        {
            Debug.LogWarning(
                "场景中没有找到 Coins 组件，无法发放击杀金币。",
                this
            );

            return;
        }

        Coins.Instance.GainCoins(killReward);
    }
}