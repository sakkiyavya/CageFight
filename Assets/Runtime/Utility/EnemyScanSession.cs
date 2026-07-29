using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存储分帧索敌的状态会话。
/// </summary>
public class EnemyScanSession
{
    public int lastX = 0;                                                                         // 下一次扫描的网格横坐标。
    public int lastY = 0;                                                                         // 下一次扫描的网格纵坐标。
    public HashSet<GameObject> processed = new HashSet<GameObject>();                             // 已处理对象集合，用于避免跨格重复收集同一对象。
    public List<GameObjectProperty> foundEnemies = new List<GameObjectProperty>();                // 扫描过程中找到的游戏对象属性组件。
    public bool isFinished = false;                                                               // 是否已经遍历完整张地图。

    #region 游戏逻辑
    /// <summary>
    /// 从上次停留的网格位置继续扫描地图，并收集尚未处理的对象。
    /// 每次调用最多检查指定数量的网格，以便将全图扫描分摊到多帧执行。
    /// </summary>
    /// <param name="maxSteps">本次允许检查的最大网格数量。</param>
    /// <returns>本次是否实际检查了至少一个网格；扫描已经结束时返回 <see langword="false"/>。</returns>
    public bool Scan(int maxSteps)
    {
        if (isFinished) return false;

        MapCells map = MapCells.Instance;                                                         // 当前地图网格。
        int steps = 0;                                                                            // 本次调用已经检查的网格数。
        
        while (lastX < map.width && steps < maxSteps)
        {
            while (lastY < map.height && steps < maxSteps)
            {
                steps++;
                foreach (var obj in map.GetOccupiers(lastX, lastY))
                {
                    if (obj != null && !processed.Contains(obj))
                    {
                        processed.Add(obj);
                        GameObjectProperty otherProp = obj.GetComponent<GameObjectProperty>();    // 当前对象的游戏属性组件。
                        if (otherProp != null) foundEnemies.Add(otherProp);
                    }
                }
                lastY++;
            }

            if (lastY >= map.height)
            {
                lastY = 0;
                lastX++;
            }
        }

        if (lastX >= map.width)
        {
            isFinished = true;
        }

        return steps > 0;
    }
    #endregion
}
