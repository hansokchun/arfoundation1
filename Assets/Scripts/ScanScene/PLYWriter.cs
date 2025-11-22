using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 포인트 클라우드 데이터를 .ply 파일로 저장하는 독립적인 유틸리티 클래스입니다.
/// </summary>
public class PLYWriter
{

    public void SaveToPLY(string filePath, List<Vector3> points, List<Color32> colors)
    {
        if (points == null || points.Count == 0)
        {
            Debug.LogWarning("저장할 포인트 데이터가 없습니다.");
            return;
        }

        if (colors == null || points.Count != colors.Count)
        {
            Debug.LogError("포인트와 색상 데이터의 개수가 일치하지 않아 저장할 수 없습니다.");
            return;
        }

        try
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("ply");
            sb.AppendLine("format ascii 1.0");
            sb.AppendLine($"element vertex {points.Count}");
            sb.AppendLine("property float x");
            sb.AppendLine("property float y");
            sb.AppendLine("property float z");
            sb.AppendLine("property uchar red");
            sb.AppendLine("property uchar green");
            sb.AppendLine("property uchar blue");
            sb.AppendLine("end_header");

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                Color32 c = colors[i];
                sb.AppendLine($"{p.x:F6} {p.y:F6} {p.z:F6} {c.r} {c.g} {c.b}");
            }

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"성공적으로 저장되었습니다: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"파일 저장 중 오류 발생: {e.Message}");
        }
    }
}