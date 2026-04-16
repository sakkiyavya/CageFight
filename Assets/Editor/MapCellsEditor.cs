using UnityEditor;
using UnityEngine;

public class MapCellsEditor
{
    /// <summary>
    /// 为所有场景中的 MapCells 绘制网格 Gizmos
    /// </summary>
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
    public static void DrawMapGrid(MapCells mapCells, GizmoType gizmoType)
    {
        // 如果在编辑器模式下，实时刷新占用数据
        if (!Application.isPlaying)
        {
            mapCells.RefreshEditorOccupancy();
        }

        int width = mapCells.width;
        int height = mapCells.height;

        // 绘制网格线 (使用用户要求的绿色)
        Gizmos.color = new Color(0, 0.8f, 0f, 0.7f);

        // 绘制纵向线
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = new Vector3(x, 0, 0);
            Vector3 end = new Vector3(x, height, 0);
            Gizmos.DrawLine(mapCells.transform.TransformPoint(start), mapCells.transform.TransformPoint(end));
        }

        // 绘制横向线
        for (int y = 0; y <= height; y++)
        {
            Vector3 start = new Vector3(0, y, 0);
            Vector3 end = new Vector3(width, y, 0);
            Gizmos.DrawLine(mapCells.transform.TransformPoint(start), mapCells.transform.TransformPoint(end));
        }

        // 绘制外边框（红色强调）
        Gizmos.color = Color.red;
        Vector3 size = new Vector3(width, height, 0);
        Vector3 center = new Vector3(width / 2f, height / 2f, 0);
        Gizmos.DrawWireCube(mapCells.transform.TransformPoint(center), size);

        // 绘制占用情况（红色小圆点）
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (mapCells.IsUse(new Vector2Int(x, y)))
                {
                    Gizmos.color = new Color(1f, 0.5f, 0, 0.8f);
                    Vector3 cellCenter = new Vector3(x + 0.5f, y + 0.5f, 0);
                    Gizmos.DrawSphere(mapCells.transform.TransformPoint(cellCenter), 0.35f);
                }
            }
        }
    }
}
