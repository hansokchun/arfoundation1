using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using System;

/// <summary>
/// ARKit의 포인트 클라우드 데이터를 수집하고 관리합니다. (시작/중지 토글 방식)
/// Feature Points + Plane Mesh + Depth Data를 모두 활용하여 평면도 스캔합니다.
/// </summary>
public class PointCloudCollector : MonoBehaviour
{
    [Header("AR Managers")]
    [SerializeField] private ARPointCloudManager pointCloudManager;
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARPlaneManager planeManager; // 평면 감지 추가
    [SerializeField] private AROcclusionManager occlusionManager; // Depth 데이터 추가
    [SerializeField] private Camera arCamera;
    
    [Header("Scan Settings")]
    [SerializeField] private bool useFeaturePoints = true; // 특징점 사용
    [SerializeField] private bool usePlaneMesh = true; // 평면 메쉬 사용
    [SerializeField] private bool useDepthData = true; // LiDAR Depth 사용
    [SerializeField] private int depthSamplingStep = 4; // Depth 샘플링 간격 (성능 조절)
    [SerializeField] private float planeMeshResolution = 0.05f; // 평면 메쉬의 점 간격 (미터)

    // --- 수집된 데이터 ---
    public List<Vector3> Points { get; private set; } = new List<Vector3>();
    public List<Color32> Colors { get; private set; } = new List<Color32>();

    // ARKit 데이터 덩어리 관리 (중복 방지)
    private Dictionary<TrackableId, List<Vector3>> trackedPoints = new Dictionary<TrackableId, List<Vector3>>();
    private HashSet<Vector3> addedPoints = new HashSet<Vector3>(); // 중복 점 방지
    private Texture2D cameraTexture;

    // --- 스캔 상태 ---
    private bool isScanning = false;
    public bool IsScanning => isScanning; // 외부에서 현재 스캔 중인지 확인할 수 있도록 속성 추가
    private Action onScanComplete;

    void OnEnable()
    {
        if (pointCloudManager != null && useFeaturePoints)
            pointCloudManager.pointCloudsChanged += OnPointCloudsChanged;
        
        if (planeManager != null && usePlaneMesh)
            planeManager.planesChanged += OnPlanesChanged;
    }

    void OnDisable()
    {
        if (pointCloudManager != null)
            pointCloudManager.pointCloudsChanged -= OnPointCloudsChanged;
        
        if (planeManager != null)
            planeManager.planesChanged -= OnPlanesChanged;
    }

    /// <summary>
    /// 스캔을 시작합니다.
    /// </summary>
    public void StartScan(Action onComplete = null)
    {
        if (isScanning) return; // 이미 스캔 중이면 무시

        Points.Clear();
        Colors.Clear();
        trackedPoints.Clear();
        addedPoints.Clear();

        isScanning = true;
        onScanComplete = onComplete;
        
        // Depth 데이터 활성화
        if (occlusionManager != null && useDepthData)
        {
            occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
            Debug.Log("Depth 모드 활성화됨");
        }
        
        Debug.Log($"스캔 시작 - Feature Points: {useFeaturePoints}, Plane Mesh: {usePlaneMesh}, Depth: {useDepthData}");
    }

    /// <summary>
    /// 스캔을 중지하고 데이터를 최종 정리합니다.
    /// </summary>
// (100번째 줄 근처) 이 코드로 덮어쓰세요:
    public void StopScan(Action onCompleteCallback = null) // 1. = null 을 추가하여 '선택적' 인수로 변경
        {
            if (!isScanning) return; // 스캔 중이 아니면 무시

            isScanning = false;
            FinalizePointCloud(); // 최종 데이터 정리
            Debug.Log($"스캔 중지됨. 최종 수집된 포인트 수: {Points.Count}");

            // 2. 기존 onScanComplete와 NetworkManager의 콜백을 모두 호출
            onScanComplete?.Invoke();     // (기존 코드)
            onCompleteCallback?.Invoke(); // (NetworkManager가 보낸 콜백)
        }

    // Update 함수에서 시간 체크 로직 제거
    // void Update() { ... }

    private void OnPointCloudsChanged(ARPointCloudChangedEventArgs eventArgs)
    {
        if (!isScanning) return; // 스캔 중일 때만 데이터 처리

        foreach (var pointCloud in eventArgs.added) { UpdateTrackedPoints(pointCloud); }
        foreach (var pointCloud in eventArgs.updated) { UpdateTrackedPoints(pointCloud); }
        foreach (var pointCloud in eventArgs.removed) { trackedPoints.Remove(pointCloud.trackableId); }
    }

    private void UpdateTrackedPoints(ARPointCloud pointCloud)
    {
        if (pointCloud.positions == null) return;
        trackedPoints[pointCloud.trackableId] = new List<Vector3>(pointCloud.positions);
    }

    // 평면 변경 이벤트 처리
    private void OnPlanesChanged(ARPlanesChangedEventArgs eventArgs)
    {
        if (!isScanning || !usePlaneMesh) return;

        // 추가되거나 업데이트된 평면의 메쉬 데이터 수집
        foreach (var plane in eventArgs.added) { CollectPlanePoints(plane); }
        foreach (var plane in eventArgs.updated) { CollectPlanePoints(plane); }
    }

    // 평면의 메쉬를 점으로 변환
    private void CollectPlanePoints(ARPlane plane)
    {
        if (plane.boundary.Length < 3) return;

        // 평면의 경계 내부를 샘플링하여 점들 생성
        Vector3 center = plane.center;
        Vector2 size = plane.size;
        Quaternion rotation = plane.transform.rotation;
        Vector3 position = plane.transform.position;

        // 평면을 그리드로 샘플링
        int stepsX = Mathf.Max(1, (int)(size.x / planeMeshResolution));
        int stepsY = Mathf.Max(1, (int)(size.y / planeMeshResolution));

        for (int x = 0; x <= stepsX; x++)
        {
            for (int y = 0; y <= stepsY; y++)
            {
                float localX = (x / (float)stepsX - 0.5f) * size.x;
                float localY = (y / (float)stepsY - 0.5f) * size.y;
                
                Vector3 localPoint = new Vector3(localX, 0, localY);
                Vector3 worldPoint = position + rotation * (center + localPoint);
                
                // 평면 경계 내부인지 확인
                if (IsPointInPolygon(plane.transform.InverseTransformPoint(worldPoint), plane.boundary.ToArray())) // .ToArray() 추가
                {
                    // 중복 방지를 위해 그리드 정렬된 위치 사용
                    Vector3 gridAlignedPoint = new Vector3(
                        Mathf.Round(worldPoint.x / planeMeshResolution) * planeMeshResolution,
                        Mathf.Round(worldPoint.y / planeMeshResolution) * planeMeshResolution,
                        Mathf.Round(worldPoint.z / planeMeshResolution) * planeMeshResolution
                    );
                    
                    addedPoints.Add(gridAlignedPoint);
                }
            }
        }
    }

    // 점이 다각형 내부에 있는지 확인 (2D)
    private bool IsPointInPolygon(Vector3 point, Vector2[] polygon)
    {
        Vector2 p = new Vector2(point.x, point.z);
        bool inside = false;
        int j = polygon.Length - 1;
        
        for (int i = 0; i < polygon.Length; i++)
        {
            if (((polygon[i].y > p.y) != (polygon[j].y > p.y)) &&
                (p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                inside = !inside;
            }
            j = i;
        }
        
        return inside;
    }

    private void FinalizePointCloud()
    {
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            Debug.LogWarning("카메라 이미지를 가져올 수 없습니다.");
            return;
        }

        // 카메라 이미지 준비
        var conversionParams = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32, XRCpuImage.Transformation.MirrorY);
        if (cameraTexture == null || cameraTexture.width != conversionParams.outputDimensions.x || cameraTexture.height != conversionParams.outputDimensions.y)
        {
            cameraTexture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, TextureFormat.RGBA32, false);
        }
        var rawTextureData = cameraTexture.GetRawTextureData<byte>();
        image.Convert(conversionParams, rawTextureData);
        cameraTexture.Apply();
        image.Dispose();

        Color32[] textureColors = cameraTexture.GetPixels32();
        int textureWidth = cameraTexture.width;
        int textureHeight = cameraTexture.height;

        // 1. Feature Points 추가
        if (useFeaturePoints)
        {
            foreach (var pair in trackedPoints)
            {
                foreach (var worldPos in pair.Value)
                {
                    Points.Add(worldPos);
                    Colors.Add(GetColorForPoint(worldPos, textureColors, textureWidth, textureHeight));
                }
            }
            Debug.Log($"Feature Points 추가됨: {Points.Count}개");
        }

        int featurePointCount = Points.Count;

        // 2. Plane Mesh Points 추가
        if (usePlaneMesh && addedPoints.Count > 0)
        {
            foreach (var point in addedPoints)
            {
                Points.Add(point);
                Colors.Add(GetColorForPoint(point, textureColors, textureWidth, textureHeight));
            }
            Debug.Log($"Plane Mesh Points 추가됨: {addedPoints.Count}개");
        }

        int planeMeshCount = Points.Count - featurePointCount;

        // 3. Depth Data 추가 (LiDAR)
        if (useDepthData && occlusionManager != null)
        {
            int depthPointCount = AddDepthPoints(textureColors, textureWidth, textureHeight);
            Debug.Log($"Depth Points 추가됨: {depthPointCount}개");
        }

        Debug.Log($"총 수집된 포인트: {Points.Count}개 (Feature: {featurePointCount}, Plane: {planeMeshCount}, Depth: {Points.Count - featurePointCount - planeMeshCount})");
    }

    // 점의 색상 가져오기
    private Color32 GetColorForPoint(Vector3 worldPos, Color32[] textureColors, int textureWidth, int textureHeight)
    {
        Vector3 screenPoint = arCamera.WorldToScreenPoint(worldPos);
        
        if (screenPoint.z < 0 || screenPoint.x < 0 || screenPoint.x >= textureWidth || 
            screenPoint.y < 0 || screenPoint.y >= textureHeight)
        {
            return new Color32(127, 127, 127, 255); // 회색 (화면 밖)
        }
        
        int x = Mathf.Clamp((int)screenPoint.x, 0, textureWidth - 1);
        int y = Mathf.Clamp((int)screenPoint.y, 0, textureHeight - 1);
        int colorIndex = y * textureWidth + x;
        
        return textureColors[colorIndex];
    }

    // Depth 데이터에서 점 추가
    private int AddDepthPoints(Color32[] textureColors, int textureWidth, int textureHeight)
    {
        if (occlusionManager == null) return 0;

        // Environment Depth 텍스처 가져오기
        var depthTexture = occlusionManager.environmentDepthTexture;
        if (depthTexture == null)
        {
            Debug.LogWarning("Depth 텍스처를 사용할 수 없습니다. LiDAR가 지원되는 기기인지 확인하세요.");
            return 0;
        }

        int depthWidth = depthTexture.width;
        int depthHeight = depthTexture.height;
        int addedCount = 0;

        // Depth 텍스처를 샘플링하여 3D 점 생성
        for (int y = 0; y < depthHeight; y += depthSamplingStep)
        {
            for (int x = 0; x < depthWidth; x += depthSamplingStep)
            {
                // 정규화된 화면 좌표
                float normalizedX = x / (float)depthWidth;
                float normalizedY = y / (float)depthHeight;

                // Depth 값 가져오기 (셰이더를 통해 읽어야 함)
                // Unity의 Depth 텍스처는 셰이더에서만 직접 접근 가능
                // 대신 카메라의 역투영을 사용
                
                Vector2 screenPos = new Vector2(normalizedX * Screen.width, normalizedY * Screen.height);
                Ray ray = arCamera.ScreenPointToRay(screenPos);
                
                // ARFoundation의 Raycast를 사용하여 실제 depth 가져오기
                List<ARRaycastHit> hits = new List<ARRaycastHit>();
                if (TryGetDepthAt(normalizedX, normalizedY, out float depth) && depth > 0 && depth < 20f)
                {
                    Vector3 worldPoint = arCamera.transform.position + ray.direction * depth;
                    
                    // 중복 방지
                    Vector3 gridAlignedPoint = new Vector3(
                        Mathf.Round(worldPoint.x / (planeMeshResolution * 2)) * (planeMeshResolution * 2),
                        Mathf.Round(worldPoint.y / (planeMeshResolution * 2)) * (planeMeshResolution * 2),
                        Mathf.Round(worldPoint.z / (planeMeshResolution * 2)) * (planeMeshResolution * 2)
                    );
                    
                    if (!addedPoints.Contains(gridAlignedPoint))
                    {
                        Points.Add(worldPoint);
                        Colors.Add(GetColorForPoint(worldPoint, textureColors, textureWidth, textureHeight));
                        addedPoints.Add(gridAlignedPoint);
                        addedCount++;
                    }
                }
            }
        }

        return addedCount;
    }

    // Depth 값 가져오기 (근사치)
    private bool TryGetDepthAt(float normalizedX, float normalizedY, out float depth)
    {
        depth = 0f;
        
        if (occlusionManager == null || occlusionManager.environmentDepthTexture == null)
            return false;

        // ARRaycastManager를 사용한 실제 depth 측정
        var raycastManager = FindObjectOfType<ARRaycastManager>();
        if (raycastManager != null)
        {
            Vector2 screenPoint = new Vector2(normalizedX * Screen.width, normalizedY * Screen.height);
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            
            if (raycastManager.Raycast(screenPoint, hits, TrackableType.Depth))
            {
                if (hits.Count > 0)
                {
                    depth = hits[0].distance;
                    return true;
                }
            }
        }

        return false;
    }
}

