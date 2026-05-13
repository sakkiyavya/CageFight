using System.Collections.Generic;
using UnityEngine;

public static class AStarUtility
{
    public class Node
    {
        public Vector2Int pos;
        public int g;
        public int h;
        public int f => g + h;
        public Node parent;

        public Node(Vector2Int pos, int g, int h, Node parent)
        {
            this.pos = pos;
            this.g = g;
            this.h = h;
            this.parent = parent;
        }
    }

    public class PathSearchSession
    {
        public Vector2Int start;
        public Vector2Int end;
        public PriorityQueue<Node> openList = new PriorityQueue<Node>();
        public Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>();
        public HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();
        public List<Vector2Int> resultPath;
        public bool isFinished;
        public bool isSuccess;

        public PathSearchSession(Vector2Int start, Vector2Int end)
        {
            this.start = start;
            this.end = end;
            Node startNode = new Node(start, 0, GetDistance(start, end), null);
            openList.Enqueue(startNode, startNode.f);
            allNodes.Add(start, startNode);
        }

        public bool Search(int maxSteps)
        {
            if (isFinished) return false;

            MapCells map = MapCells.Instance;
            int steps = 0;
            while (openList.Count > 0 && steps < maxSteps)
            {
                steps++;
                Node current = openList.Dequeue();
                
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
                    if (map.IsUse(neighborPos) && neighborPos != end) continue;

                    int moveCost = GetDistance(current.pos, neighborPos);
                    int newG = current.g + moveCost;
                    
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
    }

    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
    {
        PathSearchSession session = new PathSearchSession(start, end);
        while (!session.isFinished)
        {
            session.Search(100); 
        }
        return session.resultPath;
    }

    private static int GetDistance(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        
        // 八向移动：直线 10，对角线 14 (1.414 * 10)
        if (dx > dy) return 14 * dy + 10 * (dx - dy);
        return 14 * dx + 10 * (dy - dx);
    }

    private static List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
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

    private static List<Vector2Int> RetracePath(Node node)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Node curr = node;
        while (curr.parent != null)
        {
            path.Add(curr.pos);
            curr = curr.parent;
        }
        path.Reverse();
        return path;
    }
}

// Simple Priority Queue implementation
public class PriorityQueue<T>
{
    private List<(T item, int priority)> elements = new List<(T, int)>();

    public int Count => elements.Count;

    public void Enqueue(T item, int priority)
    {
        elements.Add((item, priority));
    }

    public T Dequeue()
    {
        int bestIndex = 0;
        for (int i = 1; i < elements.Count; i++)
        {
            if (elements[i].priority < elements[bestIndex].priority)
            {
                bestIndex = i;
            }
        }
        T item = elements[bestIndex].item;
        elements.RemoveAt(bestIndex);
        return item;
    }
}
