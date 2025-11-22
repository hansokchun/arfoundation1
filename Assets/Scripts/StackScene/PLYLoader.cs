using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

/// <summary>
/// ASCII 형식의 PLY 파일을 파싱하여 Unity Mesh(GameObject)로 변환하는 로더 클래스입니다.
/// 헤더를 동적으로 분석하여 vertex 속성(x,y,z,r,g,b)의 순서가 바뀌어도 대응할 수 있습니다.
/// </summary>
public class PLYLoader
{
    /// <summary>
    /// 지정된 폴더(하위 폴더 포함) 내의 모든 .ply 파일을 찾아 로드하고 GameObject 리스트를 반환합니다.
    /// </summary>
    public static List<GameObject> LoadAllPLYFromFolder(string folderPath, Transform parentTransform = null)
    {
        List<GameObject> loadedObjects = new List<GameObject>();
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"폴더를 찾을 수 없습니다: {folderPath}");
            return loadedObjects;
        }

        string[] plyFiles = Directory.GetFiles(folderPath, "*.ply", SearchOption.AllDirectories);
        Debug.Log($"📂 {plyFiles.Length}개의 PLY 파일 발견");

        foreach (string filePath in plyFiles)
        {
            GameObject obj = LoadPLYFile(filePath, parentTransform);
            if (obj != null)
            {
                loadedObjects.Add(obj);
            }
        }
        return loadedObjects;
    }

    // --------------------------------------------------------------------------
    // 내부 데이터 구조 (헤더 파싱용)
    // --------------------------------------------------------------------------
    private class PLYHeader
    {
        public int vertexCount = 0;
        public int faceCount = 0;
        public int dataStartIndex = 0;
        public bool hasColors = false;

        // 각 속성이 데이터 라인에서 몇 번째에 위치하는지 저장하는 인덱스
        public int x_idx = -1;
        public int y_idx = -1;
        public int z_idx = -1;
        public int r_idx = -1;
        public int g_idx = -1;
        public int b_idx = -1;
    }

    // --------------------------------------------------------------------------
    // 메인 로딩 로직
    // --------------------------------------------------------------------------

    /// <summary>
    /// 단일 PLY 파일을 읽어 GameObject(MeshFilter + Renderer + Collider)를 생성합니다.
    /// </summary>
    public static GameObject LoadPLYFile(string filePath, Transform parentTransform = null)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"PLY 파일을 찾을 수 없습니다: {filePath}");
            return null;
        }

        try
        {
            string[] lines = File.ReadAllLines(filePath);
            PLYHeader header = ParseHeader(lines);

            if (header == null)
            {
                Debug.LogError($"[PLYLoader] 헤더 파싱 실패: {filePath}");
                return null;
            }
            
            List<Vector3> vertices = new List<Vector3>(header.vertexCount);
            List<Color32> colors = new List<Color32>(header.vertexCount);
            List<int> triangles = new List<int>(header.faceCount * 3);

            // 1. Vertex 데이터 파싱
            for (int i = 0; i < header.vertexCount; i++)
            {
                string[] values = lines[header.dataStartIndex + i].Split(' ');
                
                vertices.Add(new Vector3(
                    float.Parse(values[header.x_idx], CultureInfo.InvariantCulture),
                    float.Parse(values[header.y_idx], CultureInfo.InvariantCulture),
                    float.Parse(values[header.z_idx], CultureInfo.InvariantCulture)
                ));

                if (header.hasColors)
                {
                    colors.Add(new Color32(
                        byte.Parse(values[header.r_idx]),
                        byte.Parse(values[header.g_idx]),
                        byte.Parse(values[header.b_idx]),
                        255
                    ));
                }
            }

            // 2. Face 데이터 파싱 (Triangles)
            for (int i = 0; i < header.faceCount; i++)
            {
                string[] values = lines[header.dataStartIndex + header.vertexCount + i].Split(' ');
                if (values.Length > 0 && values[0] == "3") // 삼각형만 지원
                {
                    // Unity(CW)와 PLY(CCW)의 권선 방향 차이로 순서 변경 (0, 1, 2 -> 0, 2, 1)
                    triangles.Add(int.Parse(values[1]));
                    triangles.Add(int.Parse(values[3])); 
                    triangles.Add(int.Parse(values[2])); 
                }
            }
            
            // 3. GameObject 및 컴포넌트 구성
            GameObject obj = new GameObject(Path.GetFileNameWithoutExtension(filePath));
            if (parentTransform != null) obj.transform.SetParent(parentTransform);

            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
            
            Mesh mesh = new Mesh();
            // 정점 개수가 많을 경우를 대비해 IndexFormat 변경
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            
            if (header.faceCount > 0)
                mesh.SetTriangles(triangles, 0);
            else 
                mesh.SetIndices(CreatePointIndices(vertices.Count), MeshTopology.Points, 0); 

            if (header.hasColors)
                mesh.SetColors(colors);
            
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
            
            // 4. 머티리얼 설정 (Standard -> Specular Fallback)
            Material material = new Material(Shader.Find("Standard"));
            if (material == null || material.shader == null)
            {
                material = new Material(Shader.Find("Standard (Specular setup)"));
                Debug.LogWarning("Standard 셰이더 대체: Specular setup 사용");
            }
            meshRenderer.material = material;

            // 5. 물리 및 상호작용 컴포넌트 추가
            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh; 

            obj.AddComponent<FurnitureDragger>();
            obj.layer = LayerMask.NameToLayer("Furniture");

            return obj;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"PLY 로드 오류 ({filePath}): {e.Message}");
            return null;
        }
    }

    // --------------------------------------------------------------------------
    // 헤더 파싱 로직
    // --------------------------------------------------------------------------

    private static PLYHeader ParseHeader(string[] lines)
    {
        PLYHeader header = new PLYHeader();
        int propertyIndex = 0;
        bool readingVertexProperties = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            string[] parts = line.Split(' ');

            if (readingVertexProperties && parts[0] != "property")
            {
                readingVertexProperties = false;
            }

            if (parts[0] == "element" && parts[1] == "vertex")
            {
                header.vertexCount = int.Parse(parts[2]);
                readingVertexProperties = true;
                propertyIndex = 0;
            }
            else if (parts[0] == "element" && parts[1] == "face")
            {
                header.faceCount = int.Parse(parts[2]);
            }
            else if (parts[0] == "property" && readingVertexProperties)
            {
                string propName = parts[parts.Length - 1]; 
                switch (propName)
                {
                    case "x": header.x_idx = propertyIndex; break;
                    case "y": header.y_idx = propertyIndex; break;
                    case "z": header.z_idx = propertyIndex; break;
                    case "red": header.r_idx = propertyIndex; header.hasColors = true; break;
                    case "green": header.g_idx = propertyIndex; break;
                    case "blue": header.b_idx = propertyIndex; break;
                }
                propertyIndex++;
            }
            else if (parts[0] == "end_header")
            {
                header.dataStartIndex = i + 1;
                if (header.vertexCount > 0 && header.x_idx != -1)
                {
                    return header;
                }
                else
                {
                    Debug.LogError("유효하지 않은 PLY 헤더입니다.");
                    return null;
                }
            }
        }
        return null;
    }

    private static int[] CreatePointIndices(int vertexCount)
    {
        int[] indices = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++) indices[i] = i;
        return indices;
    }
}