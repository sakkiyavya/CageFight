using UnityEngine;

public class CharacterAtk : MonoBehaviour, ILevelComponent
{
    #region ILevelComponent实现
    public System.Type DataType => typeof(CharacterAtkData);

    public ComponentData ExtractData()
    {
        return new CharacterAtkData
        {
            atk = this.atk,
            magicAtk = this.magicAtk,
            atkRange = this.atkRange
        };
    }

    public void ApplyData(ComponentData data)
    {
        if (data is CharacterAtkData aData)
        {
            this.atk = aData.atk;
            this.magicAtk = aData.magicAtk;
            this.atkRange = aData.atkRange;
        }
    }
    #endregion

    [SerializeField]
    [Header("攻击力")]
    private int atk = 10; public int Atk => atk;

    [SerializeField]
    [Header("魔法攻击力")]
    private int magicAtk = 5; public int MagicAtk => magicAtk;

    [SerializeField]
    [Header("攻击范围")]
    private float atkRange = 1.5f; public float AtkRange => atkRange;
}
