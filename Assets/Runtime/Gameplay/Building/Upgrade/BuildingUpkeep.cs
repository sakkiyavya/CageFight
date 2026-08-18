using UnityEngine;

/// <summary>
/// 建筑维护费（哨塔用）：每秒从金币（Coins）中扣除当前等级的维护费用，
/// 即从每秒获取的资源中支出维护费；维护费随建筑等级变化，
/// upkeepPerLevel 依次配置 1/2/3 级的每秒消耗。
/// 等级读取自 BuildUP.CurrentLevel；金币不足时本次跳过（不欠费、不扣负）。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(BuildUP))]
public class BuildingUpkeep : MonoBehaviour
{
    [Header("维护费（每秒消耗金币，按等级 1/2/3 配置）")]
    [SerializeField, Min(0)]
    private int[] upkeepPerLevel = new int[] { 10, 20, 30 };

    private BuildUP buildUp;
    private float timer;

    private void Awake()
    {
        buildUp = GetComponent<BuildUP>();
    }

    private void OnEnable()
    {
        timer = 0f;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < 1f)
            return;

        timer = 0f;
        if (Coins.Instance == null || buildUp == null)
            return;

        int level = Mathf.Clamp(buildUp.CurrentLevel, 0,
            Mathf.Max(0, upkeepPerLevel.Length - 1));
        int upkeep = upkeepPerLevel[level];
        if (upkeep > 0)
            Coins.Instance.ConsumeCoins(upkeep);
    }
}
