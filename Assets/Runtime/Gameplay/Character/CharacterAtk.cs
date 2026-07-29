using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterAtk : MonoBehaviour
{
    private GameObjectProperty _prop;                // 提供攻击数值和范围的角色属性组件。

    #region 生命周期与回调
    /// <summary>
    /// 缓存同一对象上的角色属性组件，供只读攻击属性访问器使用。
    /// </summary>
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }
    #endregion

    public int Atk => _prop.atk;                     // 当前物理攻击力。
    public int MagicAtk => _prop.magicAtk;           // 当前魔法攻击力。
    public Vector2Int AtkRange => _prop.atkRange;    // 当前攻击范围的网格尺寸。
}
