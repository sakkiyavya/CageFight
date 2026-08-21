using UnityEngine;

public class MinerRatScript : BehaviourBase
{
    /// <summary>经 CharacterAI 调度接入：本被动无每帧行为，返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    // 供动画事件调用。
    public void GainCoin()
    {
        if (Coins.Instance == null)
        {
            Debug.LogWarning(
                "场景中没有找到 Coins 组件。",
                this
            );

            return;
        }

        Coins.Instance.GainCoins(1);
    }
}