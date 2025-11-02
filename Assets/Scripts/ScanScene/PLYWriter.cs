using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 포인트 클라우드 데이터를 .ply 파일로 저장하는 독립적인 유틸리티 클래스입니다.
/// </summary>
public class PLYWriter
{
    /// <summary>
    /// 지정된 경로에 포인트 클라우드 데이터를 .ply 파일로 저장합니다.
    /// </summary>
    /// <param name="filePath">저장할 전체 파일 경로 (예: C:/.../scan.ply)</param>
    /// <param name="points">저장할 3D 점들의 리스트</param>
    /// <param name="colors">저장할 색상 정보 리스트</param>
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

            // PLY 헤더 작성
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

            // 데이터 작성
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

