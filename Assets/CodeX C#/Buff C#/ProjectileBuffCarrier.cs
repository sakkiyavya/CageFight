using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DamageSource))]
public class ProjectileBuffCarrier : MonoBehaviour
{
    [Header("可携带的Buff")]
    [SerializeField]
    private List<BuffBase> buffOptions =
        new List<BuffBase>();

    [Header("每种Buff施加层数")]
    [SerializeField, Min(1)]
    private int amount = 1;

    [Header("随机设置")]
    [SerializeField]
    private bool randomBuff = false;

    private DamageSource damageSource;

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
    }

    private void OnEnable()
    {
        ConfigureBuffs();
    }

    private void ConfigureBuffs()
    {
        if (damageSource == null)
            damageSource = GetComponent<DamageSource>();

        List<BuffBase> validBuffs =
            new List<BuffBase>();

        foreach (BuffBase buff in buffOptions)
        {
            if (buff != null)
                validBuffs.Add(buff);
        }

        List<BuffBase> result =
            new List<BuffBase>();

        if (randomBuff && validBuffs.Count > 0)
        {
            BuffBase selected =
                validBuffs[
                    Random.Range(0, validBuffs.Count)
                ];

            AddLayers(result, selected);
        }
        else
        {
            foreach (BuffBase buff in validBuffs)
                AddLayers(result, buff);
        }

        Damage damage = damageSource.damage;
        damage.buffs = result.ToArray();
        damageSource.damage = damage;
    }

    private void AddLayers(
        List<BuffBase> result,
        BuffBase buff)
    {
        for (int i = 0; i < amount; i++)
            result.Add(buff);
    }
}
