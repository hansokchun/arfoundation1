using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;
using System.Text;
using System.Globalization;
using System;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    [Header("Server URLs")]
    private string uploadUrl = "http://hojoon.ddns.net:8889/segment-and-reconstruct";
    private string jobBaseUrl = "http://hojoon.ddns.net:8889/jobs";

    [Header("UI for Testing")]
    public Text statusText;

    [Header("Loading UI")] // ▼▼▼ 새로 추가된 UI 변수들 ▼▼▼
    public GameObject loadingPanel; // 로딩 배경 패널
    public Slider loadingSlider;    // 로딩바 슬라이더
    public Text loadingText;        // 진행 상황 텍스트

    [Header("For Sample File Test")]
    public string sampleFileName = "scene0000_00_vh_clean_2.ply";

    [Header("Network Settings")]
    public int requestTimeout = 180;
    private string downloadFolder = "ReconstructedFiles";

    [Header("Scan Logic")]
    [SerializeField] private PointCloudCollector pointCloudCollector;

    [Header("Scene Navigation")]
    [SerializeField] private GameObject goToPlacementSceneButton;

    void Start()
    {
        if (goToPlacementSceneButton != null) goToPlacementSceneButton.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false); // 시작 시 로딩창 숨김
    }

    public void OnUploadButtonPressed()
    {
        if (pointCloudCollector == null)
        {
            SetStatus("오류: PointCloudCollector 연결 안됨", true);
            return;
        }

        // 로딩 UI 켜기
        ShowLoading(true);

        if (pointCloudCollector.IsScanning)
        {
            SetStatus("스캔 중지 중...", false);
            UpdateLoadingBar(0f, "스캔 정리 중...");
            pointCloudCollector.StopScan(OnScanFinished);
        }
        else
        {
            OnScanFinished();
        }
    }

    private void OnScanFinished()
    {
        if (pointCloudCollector.Points == null || pointCloudCollector.Points.Count == 0)
        {
            SetStatus("업로드할 포인트가 없습니다.", true);
            ShowLoading(false); // 실패 시 로딩 끄기
            return;
        }

        UpdateLoadingBar(0.05f, "파일 저장 중...");
        SetStatus(".ply 파일 저장 중...", false);

        try
        {
            string filePath = SavePointCloudToPly(pointCloudCollector.Points, pointCloudCollector.Colors, "my_scan");
            SetStatus("파일 저장 완료. 업로드 시작...", false);
            
            StartCoroutine(UploadAndDownloadCoroutine(filePath, "None"));
        }
        catch (Exception e)
        {
            SetStatus($"오류: {e.Message}", true);
            ShowLoading(false);
        }
    }

    private IEnumerator UploadAndDownloadCoroutine(string filePath, string targetClass)
    {
        string jobId = null;
        
        // 1단계: 업로드 (진행률 10% ~ 40%)
        yield return StartCoroutine(UploadAndGetJobIdCoroutine(filePath, targetClass, (id) => jobId = id));

        if (string.IsNullOrEmpty(jobId))
        {
            SetStatus("업로드 실패!", true);
            ShowLoading(false);
            yield break;
        }

        JobDataHolder.LatestJobID = jobId;
        SetStatus($"Job ID: {jobId}\n서버 처리 대기 중...", false);
        
        // 2단계: 다운로드 (진행률 50% ~ 100%)
        yield return StartCoroutine(DownloadAllFilesCoroutine(jobId));
    }

    private IEnumerator UploadAndGetJobIdCoroutine(string filePath, string targetClass, System.Action<string> onJobIdReceived)
    {
        if (!File.Exists(filePath)) yield break;

        UpdateLoadingBar(0.1f, "서버로 파일 전송 중...");

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        byte[] fileData = File.ReadAllBytes(filePath);
        formData.Add(new MultipartFormFileSection("file", fileData, Path.GetFileName(filePath), "application/octet-stream"));
        formData.Add(new MultipartFormDataSection("target_class", targetClass));

        UnityWebRequest request = UnityWebRequest.Post(uploadUrl, formData);
        request.timeout = requestTimeout;

        // 비동기 전송 시작
        var operation = request.SendWebRequest();

        // 전송 진행률 표시 loop
        while (!operation.isDone)
        {
            // uploadProgress는 0~1 사이 값. 이를 전체 공정의 10%~40% 구간에 매핑
            float progress = 0.1f + (operation.progress * 0.3f); 
            UpdateLoadingBar(progress, $"파일 업로드 중... {(int)(operation.progress * 100)}%");
            yield return null;
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            UpdateLoadingBar(0.4f, "응답 확인 중...");
            string responseText = request.downloadHandler.text;
            Debug.Log($"서버 응답: {responseText}");

            try
            {
                JobIdResponse response = JsonUtility.FromJson<JobIdResponse>(responseText);
                if (!string.IsNullOrEmpty(response.job_id))
                {
                    onJobIdReceived?.Invoke(response.job_id);
                }
            }
            catch (Exception e) { Debug.LogError(e); }
        }
        else
        {
            SetStatus($"업로드 오류: {request.error}", true);
            ShowLoading(false);
        }
    }

    private IEnumerator DownloadAllFilesCoroutine(string jobId)
    {
        // 폴링 (AI 처리 대기) - 40% ~ 50% 구간
        string jobDetailUrl = $"{jobBaseUrl}/{jobId}";
        bool isCompleted = false;
        JobDetailResponse jobDetail = null;

        UpdateLoadingBar(0.45f, "AI 분석 중... (잠시 대기)");

        while (!isCompleted)
        {
            UnityWebRequest checkRequest = UnityWebRequest.Get(jobDetailUrl);
            checkRequest.timeout = requestTimeout;
            yield return checkRequest.SendWebRequest();

            if (checkRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = checkRequest.downloadHandler.text;
                jobDetail = JsonUtility.FromJson<JobDetailResponse>(jsonResponse);

                if (jobDetail.status == "completed") isCompleted = true;
                else if (jobDetail.status == "failed")
                {
                    SetStatus("서버 처리 실패", true);
                    ShowLoading(false);
                    yield break;
                }
            }
            // 2초 대기 후 다시 확인
            yield return new WaitForSeconds(2f);
        }

        // 다운로드 시작 - 50% ~ 100% 구간
        if (jobDetail.files == null || jobDetail.files.Length == 0)
        {
            SetStatus("다운로드할 파일 없음", false);
            ShowLoading(false);
            yield break;
        }

        string savePath = Path.Combine(Application.persistentDataPath, downloadFolder, jobId);
        if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

        int totalFiles = jobDetail.files.Length;
        int successCount = 0;

        for (int i = 0; i < totalFiles; i++)
        {
            FileInfo fileInfo = jobDetail.files[i];
            
            // 진행률 계산: 기본 50% + (파일순서/전체 * 50%)
            float currentProgress = 0.5f + ((float)i / totalFiles * 0.5f);
            UpdateLoadingBar(currentProgress, $"결과 다운로드 중 ({i + 1}/{totalFiles})...");

            string downloadUrl = $"{jobBaseUrl}/{jobId}/files/{fileInfo.id}";
            UnityWebRequest fileRequest = UnityWebRequest.Get(downloadUrl);
            fileRequest.timeout = requestTimeout;

            yield return fileRequest.SendWebRequest();

            if (fileRequest.result == UnityWebRequest.Result.Success)
            {
                string classFolder = savePath;
                if (!string.IsNullOrEmpty(fileInfo.class_name) && fileInfo.class_name != "unknown")
                {
                    classFolder = Path.Combine(savePath, fileInfo.class_name);
                    if (!Directory.Exists(classFolder)) Directory.CreateDirectory(classFolder);
                }

                string fileSavePath = Path.Combine(classFolder, fileInfo.filename);
                File.WriteAllBytes(fileSavePath, fileRequest.downloadHandler.data);
                successCount++;
            }
        }

        // 완료
        UpdateLoadingBar(1.0f, "완료!");
        SetStatus($"모든 작업 완료! 배치 씬으로 이동하세요.", false);
        
        // 잠시 후 로딩창 끄기
        yield return new WaitForSeconds(0.5f);
        ShowLoading(false);

        if (goToPlacementSceneButton != null) goToPlacementSceneButton.SetActive(true);
    }

    // --- UI 제어 함수 ---
    private void ShowLoading(bool show)
    {
        if (loadingPanel != null) loadingPanel.SetActive(show);
    }

    private void UpdateLoadingBar(float value, string msg)
    {
        if (loadingSlider != null) loadingSlider.value = value;
        if (loadingText != null) loadingText.text = msg;
    }

    private void SetStatus(string message, bool isError)
    {
        if (isError) Debug.LogError(message);
        else Debug.Log(message);
        if (statusText != null) statusText.text = message;
    }

    // ... (SavePointCloudToPly 및 JSON 구조체는 기존과 동일) ...
    // 기존 SavePointCloudToPly 함수와 JSON 클래스들은 그대로 아래에 두시면 됩니다.
    private string SavePointCloudToPly(List<Vector3> points, List<Color32> colors, string filename)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string uniqueFilename = $"{filename}_{timestamp}.ply";
        string filePath = Path.Combine(Application.persistentDataPath, uniqueFilename);
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
            sb.Append(p.x.ToString("F6", CultureInfo.InvariantCulture)).Append(" ");
            sb.Append(p.y.ToString("F6", CultureInfo.InvariantCulture)).Append(" ");
            sb.Append(p.z.ToString("F6", CultureInfo.InvariantCulture)).Append(" ");
            sb.Append(c.r).Append(" ");
            sb.Append(c.g).Append(" ");
            sb.Append(c.b).AppendLine();
        }
        File.WriteAllText(filePath, sb.ToString());
        return filePath;
    }

    [Serializable] private class JobIdResponse { public string job_id; }
    [Serializable] private class JobDetailResponse { public string job_id; public string status; public FileInfo[] files; }
    [Serializable] private class FileInfo { public string id; public string filename; public string class_name; }
}