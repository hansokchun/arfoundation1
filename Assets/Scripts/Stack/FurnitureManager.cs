using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 모든 가구 상호작용과 UI를 총괄하는 메인 컨트롤러 (모바일 터치 버전)
/// </summary>
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
    public Slider rotationSlider;
    public Color selectedButtonColor = new Color(1f, 0.9f, 0.4f);
    public Color normalButtonColor = Color.white;

    [Header("Furniture Prefabs")]
    public GameObject[] furniturePrefabs;
    private GameObject furniturePrefabToPlace;

    [Header("Scene References")]
    [SerializeField] private LayerMask raycastLayerMask;

    private FurnitureDragger selectedFurniture;
    private OrbitCamera orbitCamera;

    void Start()
    {
        orbitCamera = Camera.main.GetComponent<OrbitCamera>();
        ChangeState(ManagerState.CameraMode);
        raycastLayerMask = LayerMask.GetMask("Floor", "Furniture");
    }

    void Update()
    {
        // ▼▼▼ 마우스 입력 대신 터치 입력으로 전체 로직 변경 ▼▼▼
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // UI를 터치하고 있다면 3D 상호작용을 막음 (터치 ID 전달이 중요)
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                return;
            }

            // 터치를 시작하는 순간 (탭)
            if (touch.phase == TouchPhase.Began)
            {
                HandleTouchDown(touch.position);
            }
            // 터치하고 움직이는 순간 (드래그)
            else if (touch.phase == TouchPhase.Moved && currentState == ManagerState.EditMode && currentInteractionMode == InteractionMode.Move)
            {
                HandleTouchDrag(touch.position);
            }
        }
    }

    // 터치 시작 시 처리 (탭)
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
                else if (currentState != ManagerState.PlacementMode)
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

    // 터치 드래그 시 처리 (이동)
    private void HandleTouchDrag(Vector2 touchPosition)
    {
        if (selectedFurniture == null) return;
        Ray ray = Camera.main.ScreenPointToRay(touchPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Floor")))
        {
            selectedFurniture.MoveTo(hit.point);
        }
    }
    
    // --- 이하 나머지 함수들은 이전과 동일합니다 ---
    public void OnSelectFurniturePrefab(int index)
    {
        if (index < 0 || index >= furniturePrefabs.Length) return;
        furniturePrefabToPlace = furniturePrefabs[index];
        ChangeState(ManagerState.PlacementMode);
    }

    public void SetInteractionMode(string mode)
    {
        if (mode == "Move") currentInteractionMode = InteractionMode.Move;
        else if (mode == "Rotate") currentInteractionMode = InteractionMode.Rotate;

        rotationSlider.gameObject.SetActive(currentInteractionMode == InteractionMode.Rotate);

        moveButton.GetComponent<Image>().color = (currentInteractionMode == InteractionMode.Move) ? selectedButtonColor : normalButtonColor;
        rotateButton.GetComponent<Image>().color = (currentInteractionMode == InteractionMode.Rotate) ? selectedButtonColor : normalButtonColor;
    }

    public void OnRotationSliderChanged()
    {
        if (selectedFurniture != null)
        {
            selectedFurniture.SetRotation(rotationSlider.value);
        }
    }

    private void ChangeState(ManagerState newState)
    {
        currentState = newState;
        switch (currentState)
        {
            case ManagerState.CameraMode:
                if (selectedFurniture != null) selectedFurniture.Deselect();
                selectedFurniture = null;
                furniturePrefabToPlace = null;
                interactionModePanel.SetActive(false);
                rotationSlider.gameObject.SetActive(false);
                orbitCamera.enabled = true;
                break;
            case ManagerState.PlacementMode:
                if (selectedFurniture != null) selectedFurniture.Deselect();
                selectedFurniture = null;
                interactionModePanel.SetActive(false);
                rotationSlider.gameObject.SetActive(false);
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

    private void PlaceNewFurniture(Vector3 position)
    {
        GameObject newObj = Instantiate(furniturePrefabToPlace, position, Quaternion.identity);
        SelectExistingFurniture(newObj.GetComponent<FurnitureDragger>());
    }

    private void SelectExistingFurniture(FurnitureDragger furniture)
    {
        if (selectedFurniture != null) selectedFurniture.Deselect();
        selectedFurniture = furniture;
        selectedFurniture.Select();
        rotationSlider.value = furniture.transform.eulerAngles.y;
        ChangeState(ManagerState.EditMode);
    }
}

