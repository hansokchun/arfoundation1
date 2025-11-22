using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using System;

/// <summary>
/// AR Feature Points, Plane Mesh, Depth(LiDAR) 데이터를 통합하여 
/// 컬러 포인트 클라우드를 수집하고 생성하는 매니저 클래스입니다.
/// </summary>
public class PointCloudCollector : MonoBehaviour
{
    [Header("AR Managers")]
    [SerializeField] private ARPointCloudManager pointCloudManager;
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private AROcclusionManager occlusionManager;
    [SerializeField] private Camera arCamera;
    
    [Header("Scan Settings")]
    [SerializeField] private bool useFeaturePoints = true;
    [SerializeField] private bool usePlaneMesh = true;
    [SerializeField] private bool useDepthData = true;
    [SerializeField] private int depthSamplingStep = 4;
    [SerializeField] private float planeMeshResolution = 0.05f;

    public List<Vector3> Points { get; private set; } = new List<Vector3>();
    public List<Color32> Colors { get; private set; } = new List<Color32>();

    private Dictionary<TrackableId, List<Vector3>> trackedPoints = new Dictionary<TrackableId, List<Vector3>>();
    private HashSet<Vector3> addedPoints = new HashSet<Vector3>(); 
    private Texture2D cameraTexture;

    private bool isScanning = false;
    public bool IsScanning => isScanning; 
    private Action onScanComplete;

    // --------------------------------------------------------------------------
    // 라이프사이클 및 초기화
    // --------------------------------------------------------------------------
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

    // --------------------------------------------------------------------------
    // 공개 API (스캔 제어)
    // --------------------------------------------------------------------------

    /// <summary>
    /// 스캔을 시작하고 이전 데이터를 초기화합니다. Depth 모드가 활성화된 경우 최적 설정을 요청합니다.
    /// </summary>
    public void StartScan(Action onComplete = null)
    {
        if (isScanning) return; 

        Points.Clear();
        Colors.Clear();
        trackedPoints.Clear();
        addedPoints.Clear();

        isScanning = true;
        onScanComplete = onComplete;
        
        if (occlusionManager != null && useDepthData)
        {
            occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
        }
        
        Debug.Log($"Scan Started - Feature: {useFeaturePoints}, Plane: {usePlaneMesh}, Depth: {useDepthData}");
    }

    /// <summary>
    /// 스캔을 중지하고, 최종적으로 수집된 좌표들에 색상을 입히는 후처리 작업을 수행합니다.
    /// </summary>
    public void StopScan(Action onCompleteCallback = null) 
    {
        if (!isScanning) return; 

        isScanning = false;
        FinalizePointCloud(); 
        
        Debug.Log($"Scan Stopped. Total Points: {Points.Count}");

        onScanComplete?.Invoke();     
        onCompleteCallback?.Invoke(); 
    }

    // --------------------------------------------------------------------------
    // AR 이벤트 핸들러 (실시간 데이터 수집)
    // --------------------------------------------------------------------------
    private void OnPointCloudsChanged(ARPointCloudChangedEventArgs eventArgs)
    {
        if (!isScanning) return; 

        foreach (var pointCloud in eventArgs.added) UpdateTrackedPoints(pointCloud);
        foreach (var pointCloud in eventArgs.updated) UpdateTrackedPoints(pointCloud);
        foreach (var pointCloud in eventArgs.removed) trackedPoints.Remove(pointCloud.trackableId);
    }

    private void UpdateTrackedPoints(ARPointCloud pointCloud)
    {
        if (pointCloud.positions == null) return;
        trackedPoints[pointCloud.trackableId] = new List<Vector3>(pointCloud.positions);
    }

    private void OnPlanesChanged(ARPlanesChangedEventArgs eventArgs)
    {
        if (!isScanning || !usePlaneMesh) return;

        foreach (var plane in eventArgs.added) CollectPlanePoints(plane);
        foreach (var plane in eventArgs.updated) CollectPlanePoints(plane);
    }

    // --------------------------------------------------------------------------
    // 지오메트리 처리 로직
    // --------------------------------------------------------------------------

    /// <summary>
    /// 감지된 평면을 격자(Grid) 형태로 샘플링하여 포인트 데이터로 변환합니다.
    /// </summary>
    private void CollectPlanePoints(ARPlane plane)
    {
        if (plane.boundary.Length < 3) return;

        Vector3 center = plane.center;
        Vector2 size = plane.size;
        Quaternion rotation = plane.transform.rotation;
        Vector3 position = plane.transform.position;

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
                
                // 평면의 실제 경계 안에 점이 있는지 확인
                if (IsPointInPolygon(plane.transform.InverseTransformPoint(worldPoint), plane.boundary.ToArray()))
                {
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

    // --------------------------------------------------------------------------
    // 최종 데이터 병합 및 색상 매핑
    // --------------------------------------------------------------------------

    /// <summary>
    /// 현재 카메라 프레임을 캡처하고, 수집된 모든 위치 좌표(Feature, Plane, Depth)에 색상 정보를 매핑하여 리스트를 완성합니다.
    /// </summary>
    private void FinalizePointCloud()
    {
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            return;
        }

        // 카메라 이미지 텍스처 변환
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

        // 1. Feature Points 처리
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
        }

        // 2. Plane Mesh Points 처리
        if (usePlaneMesh && addedPoints.Count > 0)
        {
            foreach (var point in addedPoints)
            {
                Points.Add(point);
                Colors.Add(GetColorForPoint(point, textureColors, textureWidth, textureHeight));
            }
        }

        // 3. Depth Data 처리 (빈 공간 채우기)
        if (useDepthData && occlusionManager != null)
        {
            AddDepthPoints(textureColors, textureWidth, textureHeight);
        }
    }

    private Color32 GetColorForPoint(Vector3 worldPos, Color32[] textureColors, int textureWidth, int textureHeight)
    {
        Vector3 screenPoint = arCamera.WorldToScreenPoint(worldPos);
        
        if (screenPoint.z < 0 || screenPoint.x < 0 || screenPoint.x >= textureWidth || 
            screenPoint.y < 0 || screenPoint.y >= textureHeight)
        {
            return new Color32(127, 127, 127, 255);
        }
        
        int x = Mathf.Clamp((int)screenPoint.x, 0, textureWidth - 1);
        int y = Mathf.Clamp((int)screenPoint.y, 0, textureHeight - 1);
        int colorIndex = y * textureWidth + x;
        
        return textureColors[colorIndex];
    }

    /// <summary>
    /// 화면 전체의 Depth 맵을 샘플링하여 포인트 클라우드에 추가합니다. (LiDAR 활용)
    /// </summary>
    private int AddDepthPoints(Color32[] textureColors, int textureWidth, int textureHeight)
    {
        if (occlusionManager == null) return 0;

        var depthTexture = occlusionManager.environmentDepthTexture;
        if (depthTexture == null) return 0;

        int depthWidth = depthTexture.width;
        int depthHeight = depthTexture.height;
        int addedCount = 0;
        
        var raycastManager = FindObjectOfType<ARRaycastManager>();

        for (int y = 0; y < depthHeight; y += depthSamplingStep)
        {
            for (int x = 0; x < depthWidth; x += depthSamplingStep)
            {
                float normalizedX = x / (float)depthWidth;
                float normalizedY = y / (float)depthHeight;

                if (TryGetDepthAt(raycastManager, normalizedX, normalizedY, out float depth) && depth > 0 && depth < 20f)
                {
                    Vector2 screenPos = new Vector2(normalizedX * Screen.width, normalizedY * Screen.height);
                    Ray ray = arCamera.ScreenPointToRay(screenPos);
                    Vector3 worldPoint = arCamera.transform.position + ray.direction * depth;
                    
                    // 그리드 정렬 및 중복 검사
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

    private bool TryGetDepthAt(ARRaycastManager raycastManager, float normalizedX, float normalizedY, out float depth)
    {
        depth = 0f;
        if (raycastManager == null) return false;

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

        return false;
    }
}