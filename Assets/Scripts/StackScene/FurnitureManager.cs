using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.IO;
using TMPro; // TextMeshPro 사용
using TransformGizmos; // ⚠️ GizmoController가 속한 네임스페이스 (없으면 지우세요)

public class FurnitureManager : MonoBehaviour
{
    public enum ManagerState { CameraMode, PlacementMode, EditMode }
    private ManagerState currentState = ManagerState.CameraMode;
    
    public enum InteractionMode { None, Move, Rotate }
    private InteractionMode currentInteractionMode = InteractionMode.None;

    [Header("UI References")]
    public GameObject interactionModePanel;
    public Button moveButton;
    public Button rotateButton;
    // public Slider rotationSlider; // (기즈모 사용으로 제거됨)
    public Color selectedButtonColor = new Color(1f, 0.9f, 0.4f);
    public Color normalButtonColor = Color.white;

[Header("External Gizmo Package")]
    [Tooltip("씬에 배치한 'Gizmo' 프리팹의 루트 오브젝트")]
    public GameObject gizmoRootObject; 
    
    [Tooltip("Gizmo 프리팹에 붙어있는 메인 컨트롤러 스크립트")]
    public GizmoController gizmoController; 

    // ▼▼▼ 이 줄이 꼭 있어야 합니다! ▼▼▼
    [Tooltip("Gizmo 하위의 'Rotation' 자식 오브젝트")]
    public GameObject rotationGizmoChild;

    [Header("Furniture Prefabs")]
    [Tooltip("미리 등록해둔 기본 가구 프리팹")]
    public GameObject[] furniturePrefabs;
    private GameObject furniturePrefabToPlace; 

    [Header("Reconstructed Objects")]
    public Transform reconstructedObjectsParent;
    
    [Header("Dynamic UI for Reconstructed Objects")]
    public Transform reconstructedObjectsUIParent;
    public GameObject reconstructedObjectButtonPrefab;
    
    private List<GameObject> reconstructedObjects = new List<GameObject>();
    private List<Button> dynamicButtons = new List<Button>();

    [Header("Scene References")]
    [SerializeField] private LayerMask raycastLayerMask; // Floor | Furniture
    [SerializeField] private LayerMask gizmoLayerMask;   // Gizmo (새로 추가!)

    private FurnitureDragger selectedFurniture;
    private OrbitCamera orbitCamera;

    void Start()
    {
        orbitCamera = Camera.main.GetComponent<OrbitCamera>();
        ChangeState(ManagerState.CameraMode);
        
        // 레이어 마스크 설정 (이름으로 가져오기)
        raycastLayerMask = LayerMask.GetMask("Floor", "Furniture");
        gizmoLayerMask = LayerMask.GetMask("Gizmo");

        // 시작할 때 기즈모 숨기기
        if(gizmoRootObject != null) gizmoRootObject.SetActive(false);

        // --- JobDataHolder 확인 및 로드 ---
        if (!string.IsNullOrEmpty(JobDataHolder.LatestJobID))
        {
            Debug.Log($"[FurnitureManager] 새 Job ID 로드: {JobDataHolder.LatestJobID}");
            LoadReconstructedObjects(JobDataHolder.LatestJobID);
            JobDataHolder.LatestJobID = null;
        }
        else
        {
            LoadLatestReconstructedObjects(); // 테스트용 최신 로드
        }
    }

    void Update()
    {
        // --- 1. 모바일 터치 입력 처리 ---
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            // UI 터치 중이면 무시
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return;
            
            // 💡 기즈모를 조작 중이라면 가구 선택/이동 로직 무시
            if (IsPointerOverGizmo(touch.position)) return;

            if (touch.phase == TouchPhase.Began)
            {
                HandleTouchDown(touch.position);
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                HandleTouchDrag(touch.position);
            }
        }
        
        // --- 2. 에디터 마우스 입력 처리 ---
        #if UNITY_EDITOR
        else if (Input.touchCount == 0) 
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if (IsPointerOverGizmo(Input.mousePosition)) return; // 기즈모 클릭 시 무시

            if (Input.GetMouseButtonDown(0))
            {
                HandleTouchDown(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0))
            {
                HandleTouchDrag(Input.mousePosition);
            }
        }
        #endif
    }

    /// <summary>
    /// 기즈모 레이어를 터치했는지 확인
    /// </summary>
    private bool IsPointerOverGizmo(Vector2 screenPos)
    {
        if (gizmoRootObject != null && gizmoRootObject.activeSelf)
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, gizmoLayerMask)) 
            {
                return true; 
            }
        }
        return false;
    }

    // 기즈모 위치 동기화
    void LateUpdate()
    {
        if (selectedFurniture != null && gizmoRootObject != null && gizmoRootObject.activeSelf)
        {
            gizmoRootObject.transform.position = selectedFurniture.transform.position;
        }
    }

    // 터치 시작 (선택 및 배치)
    private void HandleTouchDown(Vector2 touchPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(touchPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, raycastLayerMask))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Furniture"))
            {
                SelectExistingFurniture(hit.collider.GetComponent<FurnitureDragger>());
            }
            else if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Floor"))
            {
                if (currentState == ManagerState.PlacementMode && furniturePrefabToPlace != null)
                {
                    PlaceNewFurniture(hit.point);
                }
                else if (currentState == ManagerState.EditMode)
                {
                    ChangeState(ManagerState.CameraMode);
                }
            }
        }
        else
        {
            ChangeState(ManagerState.CameraMode);
        }
    }

    // 터치 드래그 (이동 모드일 때만)
    private void HandleTouchDrag(Vector2 touchPosition)
    {
        if (selectedFurniture == null) return;
        
        if (currentState == ManagerState.EditMode && currentInteractionMode == InteractionMode.Move)
        {
            Ray ray = Camera.main.ScreenPointToRay(touchPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Floor")))
            {
                selectedFurniture.MoveTo(hit.point);
            }
        }
    }

    // 모드 설정 (이동 vs 회전)
// 모드 설정 (이동 vs 회전)// 모드 설정 (이동 vs 회전)
    public void SetInteractionMode(string mode)
    {
        if (mode == "Move") 
        {
            currentInteractionMode = InteractionMode.Move;
            // 이동 모드: 기즈모 끄기
            if(gizmoRootObject != null) gizmoRootObject.SetActive(false);
        }
        else if (mode == "Rotate") 
        {
            currentInteractionMode = InteractionMode.Rotate;
            // 회전 모드: 기즈모 켜기
            if(selectedFurniture != null && gizmoRootObject != null)
            {
                gizmoRootObject.SetActive(true);
                
                // GizmoController 설정
                if(gizmoController != null)
                {
                    gizmoController.SetTarget(selectedFurniture.gameObject);
                    
                    // 💡 [수정됨] ToggleRotation() -> EnableRotation()
                    // 여러 번 눌러도 꺼지지 않고 계속 켜져 있게 함
                    gizmoController.EnableRotation(); 
                }
            }
        }

        moveButton.GetComponent<Image>().color = (currentInteractionMode == InteractionMode.Move) ? selectedButtonColor : normalButtonColor;
        rotateButton.GetComponent<Image>().color = (currentInteractionMode == InteractionMode.Rotate) ? selectedButtonColor : normalButtonColor;
    }

    private void SelectExistingFurniture(FurnitureDragger furniture)
    {
        if (selectedFurniture != null) selectedFurniture.Deselect();
        selectedFurniture = furniture;
        selectedFurniture.Select();
        ChangeState(ManagerState.EditMode);
    }

    private void ChangeState(ManagerState newState)
    {
        currentState = newState;
        
        // 상태 변경 시 기즈모 숨김
        if(gizmoRootObject != null) gizmoRootObject.SetActive(false);

        switch (currentState)
        {
            case ManagerState.CameraMode:
                if (selectedFurniture != null) selectedFurniture.Deselect();
                selectedFurniture = null;
                furniturePrefabToPlace = null;
                interactionModePanel.SetActive(false);
                orbitCamera.enabled = true;
                break;
            case ManagerState.PlacementMode:
                if (selectedFurniture != null) selectedFurniture.Deselect();
                selectedFurniture = null;
                interactionModePanel.SetActive(false);
                orbitCamera.enabled = false;
                break;
            case ManagerState.EditMode:
                furniturePrefabToPlace = null;
                interactionModePanel.SetActive(true);
                orbitCamera.enabled = false;
                SetInteractionMode("Move");
                break;
        }
    }

    // 가구 배치 (Y좌표 보정 포함)
    private void PlaceNewFurniture(Vector3 position)
    {
        GameObject newObj;
        Vector3 finalPosition = position;

        if (reconstructedObjects.Contains(furniturePrefabToPlace))
        {
            newObj = furniturePrefabToPlace;
            
            // Y좌표 보정
            MeshFilter mf = newObj.GetComponent<MeshFilter>();
            if (mf != null && mf.mesh != null)
                finalPosition.y = position.y - mf.mesh.bounds.min.y;

            newObj.SetActive(true);
            newObj.transform.position = finalPosition;
        }
        else
        {
            MeshFilter mf = furniturePrefabToPlace.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                finalPosition.y = position.y - mf.sharedMesh.bounds.min.y;
            
            newObj = Instantiate(furniturePrefabToPlace, finalPosition, Quaternion.identity);
        }
        
        SelectExistingFurniture(newObj.GetComponent<FurnitureDragger>());
    }

    // --- PLY 로딩 및 UI 생성 ---
    public void LoadReconstructedObjects(string jobId)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, "ReconstructedFiles", jobId);
        LoadReconstructedObjectsFromFolder(folderPath);
    }

    public void LoadReconstructedObjectsFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        ClearReconstructedObjects();
        List<GameObject> loadedObjects = PLYLoader.LoadAllPLYFromFolder(folderPath, reconstructedObjectsParent);
        if (loadedObjects.Count > 0)
        {
            reconstructedObjects.AddRange(loadedObjects);
            CreateDynamicUIButtons();
        }
    }

    public void LoadLatestReconstructedObjects()
    {
        string reconstructedFolder = Path.Combine(Application.persistentDataPath, "ReconstructedFiles");
        if (!Directory.Exists(reconstructedFolder)) return;
        string[] jobFolders = Directory.GetDirectories(reconstructedFolder);
        if (jobFolders.Length == 0) return;

        string latestFolder = jobFolders[0];
        System.DateTime latestTime = Directory.GetLastWriteTime(latestFolder);
        foreach (string folder in jobFolders)
        {
            if (Directory.GetLastWriteTime(folder) > latestTime)
            {
                latestTime = Directory.GetLastWriteTime(folder);
                latestFolder = folder;
            }
        }
        LoadReconstructedObjectsFromFolder(latestFolder);
    }

    public void ClearReconstructedObjects()
    {
        foreach (GameObject obj in reconstructedObjects) if (obj != null) Destroy(obj);
        reconstructedObjects.Clear();
        ClearDynamicUIButtons();
    }

    // --- UI 버튼 생성 (이름 파싱 적용) ---
    private void CreateDynamicUIButtons()
    {
        if (reconstructedObjectsUIParent == null) return;
        ClearDynamicUIButtons();

        for (int i = 0; i < reconstructedObjects.Count; i++)
        {
            GameObject obj = reconstructedObjects[i];
            if (obj == null) continue;

            Button newButton;
            if (reconstructedObjectButtonPrefab != null)
            {
                GameObject buttonObj = Instantiate(reconstructedObjectButtonPrefab, reconstructedObjectsUIParent);
                newButton = buttonObj.GetComponent<Button>();
            }
            else
            {
                GameObject buttonObj = new GameObject($"Btn_{obj.name}");
                buttonObj.transform.SetParent(reconstructedObjectsUIParent);
                buttonObj.AddComponent<Image>();
                newButton = buttonObj.AddComponent<Button>();
            }

            // 텍스트 파싱
            string objectName = obj.name; 
            string[] nameParts = objectName.Split(new string[] { "__" }, System.StringSplitOptions.None);
            string className = (nameParts.Length > 0) ? nameParts[0] : objectName;

            Text btnText = newButton.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = className;
            else
            {
                TextMeshProUGUI tmpText = newButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null) tmpText.text = className;
            }

            int index = i;
            newButton.onClick.AddListener(() => OnSelectReconstructedObject(index));
            dynamicButtons.Add(newButton);
            obj.SetActive(false);
        }
    }

    private void ClearDynamicUIButtons()
    {
        foreach (Button btn in dynamicButtons) if (btn != null) Destroy(btn.gameObject);
        dynamicButtons.Clear();
    }

    public void OnSelectReconstructedObject(int index)
    {
        if (index < 0 || index >= reconstructedObjects.Count) return;
        furniturePrefabToPlace = reconstructedObjects[index];
        ChangeState(ManagerState.PlacementMode);
    }
    
    public void OnSelectFurniturePrefab(int index)
    {
        if (index < 0 || index >= furniturePrefabs.Length) return;
        furniturePrefabToPlace = furniturePrefabs[index];
        ChangeState(ManagerState.PlacementMode);
    }
}