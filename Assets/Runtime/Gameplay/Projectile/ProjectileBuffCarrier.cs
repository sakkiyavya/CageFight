using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DamageSource))]
public class ProjectileBuffCarrier : MonoBehaviour
{
    [Header("可携带的 Buff")]
    [SerializeField] private List<BuffBase> buffOptions =
        new List<BuffBase>();

    [Header("每种 Buff 施加层数")]
    [SerializeField, Min(1)] private int amount = 1;

    [Header("随机设置")]
    [SerializeField] private bool randomBuff;

    private DamageSource damageSource;
    private BuffBase[] validBuffs = Array.Empty<BuffBase>();
    private BuffBase[] configuredBuffs = Array.Empty<BuffBase>();
    private BuffBase[] randomBuffs = Array.Empty<BuffBase>();

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
        RebuildBuffCache();
    }

    private void OnEnable()
    {
        if (damageSource == null)
            damageSource = GetComponent<DamageSource>();

        ConfigureBuffs();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildBuffCache();
    }
#endif

    private void RebuildBuffCache()
    {
        int validCount = 0;

        if (buffOptions != null)
        {
            for (int i = 0; i < buffOptions.Count; i++)
            {
                if (buffOptions[i] != null)
                    validCount++;
            }
        }

        validBuffs = new BuffBase[validCount];

        int index = 0;
        if (buffOptions != null)
        {
            for (int i = 0; i < buffOptions.Count; i++)
            {
                BuffBase buff = buffOptions[i];
                if (buff != null)
                    validBuffs[index++] = buff;
            }
        }

        int layerCount = Mathf.Max(1, amount);
        configuredBuffs = new BuffBase[validCount * layerCount];
        randomBuffs = new BuffBase[layerCount];

        index = 0;
        for (int i = 0; i < validBuffs.Length; i++)
        {
            for (int layer = 0; layer < layerCount; layer++)
                configuredBuffs[index++] = validBuffs[i];
        }
    }

    private void ConfigureBuffs()
    {
        if (damageSource == null)
            return;

        Damage damage = damageSource.damage;

        if (validBuffs.Length == 0)
        {
            damage.buffs = null;
        }
        else if (randomBuff)
        {
            BuffBase selected =
                validBuffs[UnityEngine.Random.Range(0, validBuffs.Length)];

            for (int i = 0; i < randomBuffs.Length; i++)
                randomBuffs[i] = selected;

            damage.buffs = randomBuffs;
        }
        else
        {
            damage.buffs = configuredBuffs;
        }

        damageSource.damage = damage;
    }
}
