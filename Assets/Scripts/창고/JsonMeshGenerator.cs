using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// JSON 데이터를 기반으로 3D 메쉬와 정점 색상(Vertex Colors)을 동적으로 생성하는 예제 스크립트입니다.
/// </summary>
public class JsonMeshGenerator : MonoBehaviour
{
    [Header("Test Data")]
    [Tooltip("테스트할 JSON 데이터를 여기에 붙여넣으세요.")]
    [TextArea(10, 20)]
    public string jsonRoomData;

    [Header("Materials")]
    [Tooltip("정점 색상을 표시할 수 있는 셰이더를 사용하는 머티리얼을 연결해야 합니다.")]
    public Material vertexColorMaterial; // 이제 하나의 머티리얼만 사용합니다.

    // --- JSON 구조와 일치하는 C# 클래스들 ---
    [System.Serializable]
    private class MeshData
    {
        public List<float> vertices;
        public List<int> triangles;
        public List<float> colors; // ▼▼▼ 색상 정보 리스트 추가 ▼▼▼
    }

    [System.Serializable]
    private class RoomData
    {
        public MeshData floor;
        public List<MeshData> walls;
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(jsonRoomData))
        {
            CreateRoomFromJson(jsonRoomData);
        }
    }

    public void CreateRoomFromJson(string jsonString)
    {
        RoomData room = JsonUtility.FromJson<RoomData>(jsonString);
        if (room == null)
        {
            Debug.LogError("JSON 파싱에 실패했습니다.");
            return;
        }

        CreateMeshObject("Floor", room.floor, "Floor");
        for (int i = 0; i < room.walls.Count; i++)
        {
            CreateMeshObject($"Wall_{i}", room.walls[i], "Wall");
        }
    }

    private void CreateMeshObject(string objectName, MeshData data, string layerName)
    {
        GameObject newObject = new GameObject(objectName);
        MeshFilter meshFilter = newObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = newObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = newObject.AddComponent<MeshCollider>();

        Mesh mesh = new Mesh();

        // 1. Vertices (정점) 설정
        List<Vector3> vertices = new List<Vector3>();
        for (int i = 0; i < data.vertices.Count; i += 3)
        {
            vertices.Add(new Vector3(data.vertices[i], data.vertices[i + 1], data.vertices[i + 2]));
        }
        mesh.vertices = vertices.ToArray();

        // 2. Triangles (면) 설정
        mesh.triangles = data.triangles.ToArray();

        // 3. Colors (색상) 설정 ▼▼▼ (핵심 추가 부분) ▼▼▼
        if (data.colors != null && data.colors.Count == data.vertices.Count)
        {
            List<Color> colors = new List<Color>();
            for (int i = 0; i < data.colors.Count; i += 3)
            {
                // JSON의 RGB 값(0~1)으로 Unity의 Color 객체를 생성
                colors.Add(new Color(data.colors[i], data.colors[i + 1], data.colors[i + 2]));
            }
            mesh.colors = colors.ToArray();
        }

        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;

        meshRenderer.material = vertexColorMaterial; // 정점 색상용 머티리얼 적용
        newObject.layer = LayerMask.NameToLayer(layerName);
    }
}

