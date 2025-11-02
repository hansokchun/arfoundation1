using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using System;

/// <summary>
/// ARKit의 포인트 클라우드 데이터를 수집하고 관리합니다. (시작/중지 토글 방식)
/// </summary>
public class PointCloudCollector : MonoBehaviour
{
    [Header("AR Managers")]
    [SerializeField] private ARPointCloudManager pointCloudManager;
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private Camera arCamera;

    // --- 수집된 데이터 ---
    public List<Vector3> Points { get; private set; } = new List<Vector3>();
    public List<Color32> Colors { get; private set; } = new List<Color32>();

    // ARKit 데이터 덩어리 관리 (중복 방지)
    private Dictionary<TrackableId, List<Vector3>> trackedPoints = new Dictionary<TrackableId, List<Vector3>>();
    private Texture2D cameraTexture;

    // --- 스캔 상태 ---
    private bool isScanning = false;
    public bool IsScanning => isScanning; // 외부에서 현재 스캔 중인지 확인할 수 있도록 속성 추가
    private Action onScanComplete;

    void OnEnable()
    {
        if (pointCloudManager != null)
            pointCloudManager.pointCloudsChanged += OnPointCloudsChanged;
    }

    void OnDisable()
    {
        if (pointCloudManager != null)
            pointCloudManager.pointCloudsChanged -= OnPointCloudsChanged;
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

        isScanning = true;
        onScanComplete = onComplete;
        Debug.Log("스캔 시작");
    }

    /// <summary>
    /// 스캔을 중지하고 데이터를 최종 정리합니다.
    /// </summary>
    public void StopScan()
    {
        if (!isScanning) return; // 스캔 중이 아니면 무시

        isScanning = false;
        FinalizePointCloud(); // 최종 데이터 정리
        Debug.Log($"스캔 중지됨. 최종 수집된 포인트 수: {Points.Count}");
        onScanComplete?.Invoke(); // 완료 콜백 호출
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

    private void FinalizePointCloud()
    {
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) return;

        // 카메라 이미지 준비 (이전과 동일)
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

        // 모든 점 합치고 색상 찾기 (이전과 동일)
        foreach (var pair in trackedPoints)
        {
            foreach (var worldPos in pair.Value)
            {
                Points.Add(worldPos);
                Vector3 screenPoint = arCamera.WorldToScreenPoint(worldPos);
                if (screenPoint.z < 0 || screenPoint.x < 0 || screenPoint.x >= textureWidth || screenPoint.y < 0 || screenPoint.y >= textureHeight)
                {
                    Colors.Add(new Color32(127, 127, 127, 255));
                    continue;
                }
                int x = (int)screenPoint.x;
                int y = (int)screenPoint.y;
                int colorIndex = y * textureWidth + x;
                Colors.Add(textureColors[colorIndex]);
            }
        }
    }
}

