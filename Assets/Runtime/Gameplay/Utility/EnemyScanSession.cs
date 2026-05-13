using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存储分帧索敌的状态会话。
/// </summary>
public class EnemyScanSession
{
    public int lastX = 0;
    public int lastY = 0;
    public HashSet<GameObject> processed = new HashSet<GameObject>();
    public List<GameObjectProperty> foundEnemies = new List<GameObjectProperty>();
    public bool isFinished = false;

    public bool Scan(int maxSteps)
    {
        if (isFinished) return false;

        MapCells map = MapCells.Instance;
        int steps = 0;
        
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
                        GameObjectProperty otherProp = obj.GetComponent<GameObjectProperty>();
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
}
