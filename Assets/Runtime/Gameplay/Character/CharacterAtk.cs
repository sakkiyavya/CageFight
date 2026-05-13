using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterAtk : MonoBehaviour
{
    private GameObjectProperty _prop;

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }

    public int Atk => _prop.atk;
    public int MagicAtk => _prop.magicAtk;
    public Vector2Int AtkRange => _prop.atkRange;
}
