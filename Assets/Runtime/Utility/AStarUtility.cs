using System.Collections.Generic;
using UnityEngine;

public static class AStarUtility
{
    public class Node
    {
        public Vector2Int pos;                                                                // 节点所在的地图坐标。
        public int g;                                                                         // 从起点移动到当前节点的实际代价。
        public int h;                                                                         // 当前节点到终点的启发式估算代价。
        public int f => g + h;                                                                // A* 排序使用的总代价。
        public Node parent;                                                                   // 最优路径上的前驱节点。

        #region 公开接口
        /// <summary>
        /// 创建一个 A* 搜索节点并记录其位置、代价与前驱节点。
        /// </summary>
        /// <param name="pos">节点对应的地图坐标。</param>
        /// <param name="g">从起点到当前节点的实际移动代价。</param>
        /// <param name="h">从当前节点到终点的估算代价。</param>
        /// <param name="parent">当前最优路径上的前驱节点，起点可传入 <see langword="null"/>。</param>
        public Node(Vector2Int pos, int g, int h, Node parent)
        {
            this.pos = pos;
            this.g = g;
            this.h = h;
            this.parent = parent;
        }
        #endregion
    }

    public class PathSearchSession
    {
        public Vector2Int start;                                                              // 搜索起点。
        public Vector2Int end;                                                                // 搜索终点。
        public PriorityQueue<Node> openList = new PriorityQueue<Node>();                      // 等待扩展的候选节点。
        public Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>();    // 已创建节点的坐标索引。
        public HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();                    // 已完成扩展的节点坐标。
        public List<Vector2Int> resultPath;                                                   // 搜索成功后生成的路径，不包含起点。
        public bool isFinished;                                                               // 搜索是否已经得到成功或失败结论。
        public bool isSuccess;                                                                // 搜索结束时是否找到可达路径。

        #region 游戏逻辑
        /// <summary>
        /// 创建可分帧推进的 A* 搜索会话，并将起点加入开放列表。
        /// </summary>
        /// <param name="start">路径搜索的起点坐标。</param>
        /// <param name="end">路径搜索的终点坐标。</param>
        public PathSearchSession(Vector2Int start, Vector2Int end)
        {
            this.start = start;
            this.end = end;
            Node startNode = new Node(start, 0, GetDistance(start, end), null);               // 搜索起点节点。
            openList.Enqueue(startNode, startNode.f);
            allNodes.Add(start, startNode);
        }

        /// <summary>
        /// 继续推进当前 A* 会话，在指定步数预算内扩展候选节点。
        /// 遇到终点时生成路径，开放列表耗尽时标记搜索失败。
        /// </summary>
        /// <param name="maxSteps">本次调用最多扩展的节点数量。</param>
        /// <returns>本次是否实际扩展了节点；会话已经结束且没有继续工作时返回 <see langword="false"/>。</returns>
        public bool Search(int maxSteps)
        {
            if (isFinished) return false;

            MapCells map = MapCells.Instance;                                                 // 用于边界和占用判断的地图。
            int steps = 0;                                                                    // 本次已经扩展的节点数。
            while (openList.Count > 0 && steps < maxSteps)
            {
                steps++;
                Node current = openList.Dequeue();                                            // 当前总代价最低的候选节点。
                
                // 如果当前节点已经在关闭列表中（因为重复入队），则跳过
                if (closedList.Contains(current.pos)) continue;

                if (current.pos == end)
                {
                    resultPath = RetracePath(current);
                    isFinished = true;
                    isSuccess = true;
                    return true;
                }

                closedList.Add(current.pos);

                foreach (Vector2Int neighborPos in GetNeighbors(current.pos))
                {
                    if (!map.IsInRange(neighborPos.x, neighborPos.y)) continue;
                    if (closedList.Contains(neighborPos)) continue;
                    if (map.IsPathBlocked(neighborPos) && neighborPos != end) continue;

                    int moveCost = GetDistance(current.pos, neighborPos);                     // 从当前节点移动到相邻节点的代价。
                    int newG = current.g + moveCost;                                          // 经当前节点到达相邻节点的新实际代价。
                    
                    if (!allNodes.TryGetValue(neighborPos, out Node neighborNode) || newG < neighborNode.g)
                    {
                        if (neighborNode == null)
                        {
                            neighborNode = new Node(neighborPos, newG, GetDistance(neighborPos, end), current);
                            allNodes.Add(neighborPos, neighborNode);
                            openList.Enqueue(neighborNode, neighborNode.f);
                        }
                        else
                        {
                            neighborNode.g = newG;
                            neighborNode.parent = current;
                            // 即使节点已在 openList 中，由于 priority queue 不支持 Update，我们选择重复入队
                            // 后续 Dequeue 时会通过 closedList 检查过滤掉旧节点
                            openList.Enqueue(neighborNode, neighborNode.f);
                        }
                    }
                }
            }

            if (openList.Count == 0)
            {
                isFinished = true;
                isSuccess = false;
            }

            return steps > 0;
        }
        #endregion
    }

    #region 路径查询
    /// <summary>
    /// 同步执行完整的 A* 搜索，直到找到路径或确认终点不可达。
    /// </summary>
    /// <param name="start">路径起点坐标。</param>
    /// <param name="end">路径终点坐标。</param>
    /// <returns>从起点之后的第一个格子到终点的坐标列表；不可达时返回 <see langword="null"/>。</returns>
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
    {
        PathSearchSession session = new PathSearchSession(start, end);                        // 本次同步搜索会话。
        while (!session.isFinished)
        {
            session.Search(100); 
        }
        return session.resultPath;
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 按直线移动代价 10、对角移动代价 14 计算两个网格坐标之间的八方向距离。
    /// </summary>
    /// <param name="a">第一个网格坐标。</param>
    /// <param name="b">第二个网格坐标。</param>
    /// <returns>用于 A* 的整数移动代价。</returns>
    private static int GetDistance(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);                                                        // 横向距离。
        int dy = Mathf.Abs(a.y - b.y);                                                        // 纵向距离。
        
        // 八向移动：直线 10，对角线 14 (1.414 * 10)
        if (dx > dy) return 14 * dy + 10 * (dx - dy);
        return 14 * dx + 10 * (dy - dx);
    }

    /// <summary>
    /// 获取指定网格周围的八个相邻坐标，不在此处过滤地图边界或占用状态。
    /// </summary>
    /// <param name="pos">中心网格坐标。</param>
    /// <returns>包含横向、纵向和对角方向的相邻坐标列表。</returns>
    private static List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();                                  // 相邻坐标集合。
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                neighbors.Add(new Vector2Int(pos.x + x, pos.y + y));
            }
        }
        return neighbors;
    }

    /// <summary>
    /// 从终点节点沿父节点回溯到起点，并反转为正向移动路径。
    /// </summary>
    /// <param name="node">已经到达终点的搜索节点。</param>
    /// <returns>不包含起点、按移动顺序排列的路径坐标。</returns>
    private static List<Vector2Int> RetracePath(Node node)
    {
        List<Vector2Int> path = new List<Vector2Int>();                                       // 反向收集后再翻转的路径。
        Node curr = node;                                                                     // 当前回溯节点。
        while (curr.parent != null)
        {
            path.Add(curr.pos);
            curr = curr.parent;
        }
        path.Reverse();
        return path;
    }
    #endregion
}

// Simple Priority Queue implementation
public class PriorityQueue<T>
{
    private List<(T item, int priority)> elements = new List<(T, int)>();                     // 队列元素及其优先级。

    public int Count => elements.Count;                                                       // 当前队列中的元素数量。

    #region 公开接口
    /// <summary>
    /// 将元素及其优先级加入队列；数值越小，出队顺序越靠前。
    /// </summary>
    /// <param name="item">需要加入队列的元素。</param>
    /// <param name="priority">元素的排序优先级。</param>
    public void Enqueue(T item, int priority)
    {
        elements.Add((item, priority));
    }

    /// <summary>
    /// 线性查找并移除优先级数值最小的元素。
    /// </summary>
    /// <returns>当前优先级最高的元素。</returns>
    public T Dequeue()
    {
        int bestIndex = 0;                                                                    // 当前找到的最高优先级元素索引。
        for (int i = 1; i < elements.Count; i++)
        {
            if (elements[i].priority < elements[bestIndex].priority)
            {
                bestIndex = i;
            }
        }
        T item = elements[bestIndex].item;                                                    // 即将返回的元素。
        elements.RemoveAt(bestIndex);
        return item;
    }
    #endregion
}
