using UnityEngine;
using System;
using System.IO;
using UnityEngine.UI; // Text 컴포넌트를 사용하기 위해 필요합니다.
using TMPro; // ▼▼▼ TextMeshPro 네임스페이스 추가 ▼▼▼

/// <summary>
/// 스캔 시작/중지 버튼의 UI 이벤트를 처리하고, 스캔 완료 후 데이터 저장을 요청합니다. (토글 방식, 시각화 제외)
/// </summary>
public class ScanButton : MonoBehaviour
{
    [Header("Core Components")]
    [SerializeField] private PointCloudCollector collector;
    
    [Header("UI")]
    // ▼▼▼ Text 대신 TextMeshProUGUI 타입으로 변경 ▼▼▼
    [SerializeField] private TextMeshProUGUI buttonText; // 버튼의 텍스트 (TextMeshPro)
    [SerializeField] private GameObject scanningIndicator; // "Scanning..." 텍스트 오브젝트
    [SerializeField] private GameObject sendButton;     // "Send" 버튼 오브젝트

    private PLYWriter writer;

    void Start()
    {
        writer = new PLYWriter();
        
        if (buttonText != null)
        {
            buttonText.text = "START"; // Korean: 스캔 시작
        }
        
        // UI 초기 상태 설정
        if (scanningIndicator != null)
        {
            scanningIndicator.SetActive(false);
        }
        if (sendButton != null)
        {
            sendButton.SetActive(false);
        }
    }

    /// <summary>
    /// UI 버튼의 OnClick 이벤트에 연결될 함수입니다. 스캔 시작/중지를 토글합니다.
    /// </summary>
    public void OnClickToggleScan()
    {
        if (collector == null)
        {
            Debug.LogError("PointCloudCollector가 ScanButton에 연결되지 않았습니다!"); // Korean: PointCloudCollector가 ScanButton에 연결되지 않았습니다!
            return;
        }

        if (!collector.IsScanning) // 스캔 시작
        {
            collector.StartScan(HandleScanComplete); 
            
            if (buttonText != null)
                buttonText.text = "STOP"; // Korean: 스캔 중지
            
            if (scanningIndicator != null)
                scanningIndicator.SetActive(true); // "Scanning..." 텍스트 켜기
            if (sendButton != null)
                sendButton.SetActive(false); // Send 버튼 숨기기
        }
        else // 스캔 중지
        {
            collector.StopScan(); 
            
            if (scanningIndicator != null)
                scanningIndicator.SetActive(false); // "Scanning..." 텍스트 끄기
        }
    }

    /// <summary>
    /// PointCloudCollector가 스캔을 완료(중지)했을 때 호출될 함수입니다.
    /// </summary>
    private void HandleScanComplete()
    {
        Debug.Log("스캔 완료 콜백 수신. 파일 저장을 시작합니다."); // Korean: 스캔 완료 콜백 수신. 파일 저장을 시작합니다.
        string savedFilePath = null;

        if (writer != null)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"scan_{timestamp}.ply";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            writer.SaveToPLY(path, collector.Points, collector.Colors);
            savedFilePath = path; // 저장된 경로 기억
        }

        if (buttonText != null)
        {
            buttonText.text = "START"; // Korean: 스캔 시작
        }

        // 파일 저장이 완료된 후, "Send" 버튼 표시
        if (!string.IsNullOrEmpty(savedFilePath))
        {
            Debug.Log($"파일 저장 완료. Send 버튼 활성화. 경로: {savedFilePath}"); // Korean: 파일 저장 완료. Send 버튼 활성화. 경로:
            
            if (sendButton != null)
            {
                sendButton.SetActive(true);
            }
        }
    }
}

