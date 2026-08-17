using UnityEngine;

/// <summary>
/// 召唤单位创造者注入接口。
/// CharacterAI.ShootProjectile 在从对象池生成对象后，若该对象实现了本接口，
/// 会调用 <see cref="SetCreator"/> 传入攻击者，使召唤单位能够回引自己的创造者。
/// 现有投射物（未实现本接口）不受任何影响。
/// </summary>
public interface ISummonedUnit
{
    /// <summary>
    /// 设置创造并生成当前单位的源对象。
    /// </summary>
    /// <param name="creator">创造者对象；池化复用时会随每次生成重新注入。</param>
    void SetCreator(GameObject creator);
}
