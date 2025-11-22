using UnityEngine;
using System;
using System.IO;
using TMPro;

/// <summary>
/// 스캔 시작/중지 버튼의 UI 상호작용을 처리하고, 스캔 완료 시 데이터 저장 및 후처리를 담당하는 클래스입니다.
/// </summary>
public class ScanButton : MonoBehaviour
{
    [Header("Core Components")]
    [SerializeField] private PointCloudCollector collector;
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private GameObject scanningIndicator;
    [SerializeField] private GameObject sendButton; 

    private PLYWriter writer;

    void Start()
    {
        writer = new PLYWriter();
        
        if (buttonText != null)
            buttonText.text = "START";
        
        if (scanningIndicator != null)
            scanningIndicator.SetActive(false);
            
        if (sendButton != null)
            sendButton.SetActive(false);
    }

    /// <summary>
    /// UI 버튼 클릭 시 호출됩니다. 현재 상태에 따라 스캔을 시작하거나 중지합니다.
    /// </summary>
    public void OnClickToggleScan()
    {
        if (collector == null)
        {
            Debug.LogError("PointCloudCollector가 연결되지 않았습니다.");
            return;
        }

        if (!collector.IsScanning)
        {
            // 스캔 시작
            collector.StartScan(HandleScanComplete); 
            
            if (buttonText != null) buttonText.text = "STOP";
            if (scanningIndicator != null) scanningIndicator.SetActive(true);
            if (sendButton != null) sendButton.SetActive(false);
        }
        else
        {
            // 스캔 중지 요청 (비동기적으로 HandleScanComplete가 호출됨)
            collector.StopScan(); 
            
            if (scanningIndicator != null) scanningIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// PointCloudCollector가 스캔을 완전히 종료하고 데이터 수집을 마쳤을 때 호출되는 콜백입니다.
    /// 파일을 저장하고 UI를 초기 상태로 되돌립니다.
    /// </summary>
    private void HandleScanComplete()
    {
        Debug.Log("스캔 완료. 파일 저장을 시작합니다.");
        string savedFilePath = null;

        if (writer != null)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"scan_{timestamp}.ply";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            
            writer.SaveToPLY(path, collector.Points, collector.Colors);
            savedFilePath = path;
        }

        if (buttonText != null)
        {
            buttonText.text = "START";
        }

        if (!string.IsNullOrEmpty(savedFilePath))
        {
            Debug.Log($"파일 저장 완료. Send 버튼 활성화: {savedFilePath}");
            
            if (sendButton != null)
                sendButton.SetActive(true);
        }
    }
}