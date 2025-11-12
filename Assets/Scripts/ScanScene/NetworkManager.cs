using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;
using System.Text; // --- [2번 완성을 위해 추가된 부분] ---
using System.Globalization; // --- [2번 완성을 위해 추가된 부분] ---
using System; // --- [2번 완성을 위해 추가된 부분] ---

/// <summary>
/// AI 서버와 통신(업로드/다운로드)하는 기능을 관리합니다.
/// PointCloudCollector와 연동하여 스캔 데이터를 .ply로 저장하고 업로드합니다.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    // ⚠️ 중요: 이 주소는 AI 개발자가 처음에 알려준 주소입니다.
    private string serverUrl = "https://hojoon.ddns.net:8889/segment-and-reconstruct";
    [Header("UI for Testing")]
    public Text statusText;

    [Header("For Sample File Test")]
    public string sampleFileName = "scene0000_00_vh_clean_2.ply";

    [Header("Network Settings")]
    [Tooltip("서버 응답 대기 시간(초)입니다. AI 처리 시간이 길어지면 이 값을 늘려주세요.")]
    public int requestTimeout = 180; // 기본 대기 시간을 180초(3분)로 늘립니다.

    // --- [2번 완성을 위해 추가된 부분] ---
    [Header("Scan Logic")]
    [Tooltip("스캔 데이터를 가져올 PointCloudCollector 스크립트를 연결하세요.")]
    [SerializeField] private PointCloudCollector pointCloudCollector;
    // --- [추가 끝] ---


    /// <summary>
    /// (기존 함수) 테스트용 샘플 파일을 업로드합니다.
    /// </summary>
    public void StartTestWithBedSample()
    {
        StartCoroutine(TestWithSampleFileCoroutine("bed"));
    }

    // --- [2번 완성을 위해 추가된 부분] ---

    /// <summary>
    /// [전송] 버튼이 눌렸을 때 호출될 메인 함수입니다.
    /// 스캔 중이면 중지시키고, 완료되면 업로드를 시작합니다.
    /// </summary>
    public void OnUploadButtonPressed()
    {
        if (pointCloudCollector == null)
        {
            SetStatus("오류: PointCloudCollector가 연결되지 않았습니다.", true);
            return;
        }

        if (pointCloudCollector.IsScanning)
        {
            SetStatus("스캔을 중지합니다... 완료 후 자동 업로드됩니다.");
            
            // 스캔을 중지하고, '완료 콜백'으로 OnScanFinished 함수를 실행시킵니다.
            pointCloudCollector.StopScan(OnScanFinished); 
        }
        else
        {
            // 스캔이 이미 중지된 상태(데이터가 있는 상태)라면, 바로 업로드를 시도합니다.
            OnScanFinished();
        }
    }

    /// <summary>
    /// PointCloudCollector.StopScan()에 의해 호출되는 콜백(Callback) 함수입니다.
    /// 이 함수가 실제 .ply 저장 및 업로드를 트리거합니다.
    /// </summary>
    private void OnScanFinished()
    {
        if (pointCloudCollector.Points == null || pointCloudCollector.Points.Count == 0)
        {
            SetStatus("업로드할 포인트가 없습니다. 먼저 스캔을 해주세요.", true);
            return;
        }

        SetStatus(".ply 파일 저장 중...");
        
        try
        {
            // 1. PLY 파일로 저장
            string filePath = SavePointCloudToPly(
                pointCloudCollector.Points, 
                pointCloudCollector.Colors, 
                "my_scan" // 파일 이름
            );

            SetStatus("파일 저장 완료. 업로드 시작...");

            // 2. 저장된 파일을 업로드 (기존 로직 재활용)
            // '리방' 앱의 주 기능은 방 전체를 분석하는 것이므로, targetClass를 "None"으로 설정합니다.
            StartCoroutine(UploadRequestCoroutine(filePath, "None"));
        }
        catch (Exception e)
        {
            SetStatus($"파일 저장/업로드 오류:\n{e.Message}", true);
        }
    }

    /// <summary>
    /// 수집된 포인트 클라우드 데이터를 .ply 파일 형식으로 저장합니다.
    /// </summary>
    /// <returns>저장된 .ply 파일의 전체 경로</returns>
    private string SavePointCloudToPly(List<Vector3> points, List<Color32> colors, string filename)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string uniqueFilename = $"{filename}_{timestamp}.ply";
        string filePath = Path.Combine(Application.persistentDataPath, uniqueFilename);

        StringBuilder sb = new StringBuilder();

        // --- PLY Header ---
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

        // --- PLY Data ---
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];
            Color32 c = colors[i];
            
            // CultureInfo.InvariantCulture를 사용하여 소수점이 ','가 아닌 '.'으로 찍히도록 보장
            sb.Append(p.x.ToString("F6", CultureInfo.InvariantCulture)).Append(" ");
            sb.Append(p.y.ToString("F6", CultureInfo.InvariantCulture)).Append(" ");
            sb.Append(p.z.ToString("F6", CultureInfo.InvariantCulture)).Append(" ");
            sb.Append(c.r).Append(" ");
            sb.Append(c.g).Append(" ");
            sb.Append(c.b).AppendLine();
        }

        // 파일 쓰기
        File.WriteAllText(filePath, sb.ToString());
        Debug.Log($".ply 파일 저장 완료: {filePath} (포인트 {points.Count}개)");
        
        return filePath;
    }
    // --- [추가 끝] ---


    /// <summary>
    /// 샘플 파일을 준비하고 업로드 코루틴을 시작합니다. (기존 함수)
    /// </summary>
    private IEnumerator TestWithSampleFileCoroutine(string targetClass)
    {
        string sourcePath = Path.Combine(Application.streamingAssetsPath, sampleFileName);
        string destPath = Path.Combine(Application.persistentDataPath, sampleFileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        UnityWebRequest www = UnityWebRequest.Get(sourcePath);
        www.timeout = requestTimeout;
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success) {
            SetStatus("샘플 파일 로드 실패!", true);
            yield break;
        }
        File.WriteAllBytes(destPath, www.downloadHandler.data);
#else
        if (!File.Exists(sourcePath))
        {
             SetStatus("샘플 파일 없음!", true);
             yield break;
        }
        File.Copy(sourcePath, destPath, true);
#endif
        
        Debug.Log($"샘플 파일이 다음 경로로 복사되었습니다: {destPath}");
        yield return StartCoroutine(UploadRequestCoroutine(destPath, targetClass));
    }

    /// <summary>
    /// 파일을 서버에 업로드하고 .tar 응답을 받는 실제 로직이 담긴 코루틴입니다. (기존 함수)
    /// </summary>
    private IEnumerator UploadRequestCoroutine(string filePath, string targetClass)
    {
        if (!File.Exists(filePath))
        {
            SetStatus($"업로드할 파일 없음:\n{filePath}", true);
            yield break;
        }

        SetStatus($"파일 업로드 중... (Target: {targetClass})");

        // 1. 전송할 데이터를 'multipart/form-data' 형식으로 구성합니다.
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

        byte[] fileData = File.ReadAllBytes(filePath);
        formData.Add(new MultipartFormFileSection("file", fileData, Path.GetFileName(filePath), "application/octet-stream"));
        formData.Add(new MultipartFormDataSection("target_class", targetClass));

        // 2. UnityWebRequest를 사용하여 POST 요청을 생성하고 타임아웃을 설정합니다.
        UnityWebRequest request = UnityWebRequest.Post(serverUrl, formData);
        request.timeout = requestTimeout;

        // 3. 요청을 보내고 서버의 응답을 기다립니다.
        yield return request.SendWebRequest();

        // 4. 응답 결과를 처리합니다.
        if (request.result == UnityWebRequest.Result.Success)
        {
            SetStatus("응답 수신 완료! 데이터 처리 중...");
            byte[] tarData = request.downloadHandler.data;
            ProcessTarData(tarData);
        }
        else
        {
            SetStatus($"서버 통신 오류:\n{request.error}", true);
        }
    }

    /// <summary>
    /// 서버로부터 받은 .tar 압축 파일 데이터를 처리합니다. (기존 함수)
    /// </summary>
    private void ProcessTarData(byte[] tarData)
    {
        // 중요: .tar 파일의 압축을 해제하려면 별도의 C# 라이브러리가 필요합니다.
        // (예: SharpZipLib, Tar.cs 등)
        Debug.Log($"{tarData.Length} bytes 크기의 .tar 파일을 받았습니다. 이제 압축을 해제하고 처리해야 합니다.");

        string outputPath = Path.Combine(Application.persistentDataPath, "reconstruction_result.tar");
        File.WriteAllBytes(outputPath, tarData);
        Debug.Log($"서버 응답이 다음 경로에 저장되었습니다: {outputPath}");

        SetStatus($"처리 완료!\n결과 파일이 저장되었습니다.");
    }

    /// <summary>
    /// 상태 메시지를 UI 텍스트와 디버그 로그에 동시에 표시합니다.
    /// </summary>
    private void SetStatus(string message, bool isError = false)
    {
        if (isError)
        {
            Debug.LogError(message);
        }
        else
        {
            Debug.Log(message);
        }

        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}