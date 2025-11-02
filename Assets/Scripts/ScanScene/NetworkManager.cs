using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

/// <summary>
/// AI 서버와 1단계 프로세스(업로드 -> .tar 다운로드)로 통신하는 기능을 관리합니다.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    // ⚠️ 중요: 이 주소는 AI 개발자가 처음에 알려준 주소입니다.
    private string serverUrl = "http://hojoon.ddns.net:8889/segment-and-reconstruct";

    [Header("UI for Testing")]
    public Text statusText;

    [Header("For Sample File Test")]
    public string sampleFileName = "scene0000_00_vh_clean_2.ply";

    [Header("Network Settings")]
    [Tooltip("서버 응답 대기 시간(초)입니다. AI 처리 시간이 길어지면 이 값을 늘려주세요.")]
    public int requestTimeout = 180; // 기본 대기 시간을 180초(3분)로 늘립니다.

    /// <summary>
    /// </summary>
    public void StartReconstructionProcess(string plyFilePath)
    {
        // '리방' 앱의 주 기능은 방 전체를 분석하는 것이므로, targetClass를 "None"으로 설정합니다.
        StartCoroutine(UploadRequestCoroutine(plyFilePath, "None"));
    }

    /// <summary>
    /// </summary>
    public void StartTestWithBedSample()
    {
        StartCoroutine(TestWithSampleFileCoroutine("bed"));
    }

    /// <summary>
    /// 샘플 파일을 준비하고 업로드 코루틴을 시작합니다.
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
            Debug.LogError("StreamingAssets에서 샘플 파일을 로드하는 데 실패했습니다: " + www.error);
            if(statusText != null) statusText.text = "샘플 파일 로드 실패!";
            yield break;
        }
        File.WriteAllBytes(destPath, www.downloadHandler.data);
#else
        if (!File.Exists(sourcePath))
        {
             Debug.LogError($"샘플 파일을 찾을 수 없습니다: {sourcePath}. StreamingAssets 폴더에 파일을 넣었는지 확인하세요.");
             if(statusText != null) statusText.text = "샘플 파일 없음!";
             yield break;
        }
        File.Copy(sourcePath, destPath, true);
#endif
        
        Debug.Log($"샘플 파일이 다음 경로로 복사되었습니다: {destPath}");
        yield return StartCoroutine(UploadRequestCoroutine(destPath, targetClass));
    }

    /// <summary>
    /// 파일을 서버에 업로드하고 .tar 응답을 받는 실제 로직이 담긴 코루틴입니다.
    /// </summary>
    private IEnumerator UploadRequestCoroutine(string filePath, string targetClass)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"업로드할 파일을 찾을 수 없습니다: {filePath}");
            yield break;
        }

        Debug.Log($"서버로 파일 업로드를 시작합니다 (Target: {targetClass})... 경로: {filePath}");
        if (statusText != null) statusText.text = $"파일 업로드 중... (Target: {targetClass})";

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
            Debug.Log("서버로부터 성공적으로 응답을 받았습니다!");
            if (statusText != null) statusText.text = "응답 수신 완료! 데이터 처리 중...";

            byte[] tarData = request.downloadHandler.data;
            ProcessTarData(tarData);
        }
        else
        {
            Debug.LogError($"서버 통신 오류: {request.error}");
            if (statusText != null) statusText.text = $"서버 통신 오류:\n{request.error}";
        }
    }

    /// <summary>
    /// 서버로부터 받은 .tar 압축 파일 데이터를 처리합니다.
    /// </summary>
    private void ProcessTarData(byte[] tarData)
    {
        // 중요: .tar 파일의 압축을 해제하려면 별도의 C# 라이브러리가 필요합니다.
        // (예: SharpZipLib, Tar.cs 등)
        Debug.Log($"{tarData.Length} bytes 크기의 .tar 파일을 받았습니다. 이제 압축을 해제하고 처리해야 합니다.");

        string outputPath = Path.Combine(Application.persistentDataPath, "reconstruction_result.tar");
        File.WriteAllBytes(outputPath, tarData);
        Debug.Log($"서버 응답이 다음 경로에 저장되었습니다: {outputPath}");

        if (statusText != null) statusText.text = $"처리 완료!\n결과 파일이 저장되었습니다.";
    }
}

