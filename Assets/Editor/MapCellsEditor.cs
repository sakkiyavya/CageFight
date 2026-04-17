using UnityEditor;
using UnityEngine;

public class MapCellsEditor
{
    private const float CellPadding = 0.16f;
    private const float RadiusScale = 0.8f;

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
    public static void DrawMapGrid(MapCells mapCells, GizmoType gizmoType)
    {
        int width = mapCells.width;
        int height = mapCells.height;

        Gizmos.color = new Color(0, 0.8f, 0f, 0.7f);
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = new Vector3(x, 0, 0);
            Vector3 end = new Vector3(x, height, 0);
            Gizmos.DrawLine(mapCells.transform.TransformPoint(start), mapCells.transform.TransformPoint(end));
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 start = new Vector3(0, y, 0);
            Vector3 end = new Vector3(width, y, 0);
            Gizmos.DrawLine(mapCells.transform.TransformPoint(start), mapCells.transform.TransformPoint(end));
        }

        Gizmos.color = Color.red;
        Vector3 size = new Vector3(width, height, 0);
        Vector3 center = new Vector3(width / 2f, height / 2f, 0);
        Gizmos.DrawWireCube(mapCells.transform.TransformPoint(center), size);

        Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int occupierCount = mapCells.GetOccupierCount(x, y);
                if (occupierCount == 0)
                {
                    continue;
                }

                DrawOccupancySpheres(mapCells, x, y, occupierCount);
            }
        }
    }

    private static void DrawOccupancySpheres(MapCells mapCells, int cellX, int cellY, int count)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt(count / (float)columns);

        float usableSize = 1f - CellPadding * 2f;
        float slotWidth = usableSize / columns;
        float slotHeight = usableSize / rows;
        float radius = Mathf.Min(slotWidth, slotHeight) * 0.5f * RadiusScale;

        float contentWidth = slotWidth * columns;
        float contentHeight = slotHeight * rows;
        float startX = cellX + 0.5f - contentWidth * 0.5f + slotWidth * 0.5f;
        float startY = cellY + 0.5f - contentHeight * 0.5f + slotHeight * 0.5f;

        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int column = i % columns;

            Vector3 localPosition = new Vector3(
                startX + column * slotWidth,
                startY + row * slotHeight,
                0f);

            Gizmos.DrawSphere(mapCells.transform.TransformPoint(localPosition), radius);
        }
    }
}
