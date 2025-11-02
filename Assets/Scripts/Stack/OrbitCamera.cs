using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 배경을 드래그하여 시점을 회전시키고 핀치 줌을 지원하는 카메라 (모바일 터치 버전)
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    [Header("Camera Settings")]
    public float distance = 5.0f;
    public float xSpeed = 120.0f;
    public float ySpeed = 120.0f;
    public float zoomSpeed = 0.5f;

    [Header("Clamp Settings")]
    public float yMinLimit = 10f;
    public float yMaxLimit = 80f;
    public float distanceMin = 1f;
    public float distanceMax = 10f;

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        if (target == null)
        {
            this.enabled = false;
            return;
        }
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ▼▼▼ 마우스 대신 터치 입력으로 카메라 제어 ▼▼▼

        // 1. 회전 (한 손가락 드래그)
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
        {
            Touch touch = Input.GetTouch(0);
            // UI를 터치하고 있다면 카메라 회전 안함
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return;

            Vector2 touchDeltaPosition = touch.deltaPosition;
            x += touchDeltaPosition.x * xSpeed * 0.002f; // 터치 감도 조절
            y -= touchDeltaPosition.y * ySpeed * 0.002f;
        }

        // 2. 줌 (두 손가락 핀치 줌)
        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;
            distance += deltaMagnitudeDiff * zoomSpeed * 0.01f; // 터치 감도 조절
        }

        // ▼▼▼ PC 에디터 테스트를 위한 마우스 입력 (선택사항) ▼▼▼
#if UNITY_EDITOR
        if (Input.GetMouseButton(0))
        {
            if (Input.touchCount == 0) // 터치가 없을 때만 마우스 작동
            {
                if (EventSystem.current.IsPointerOverGameObject()) return;
                x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
            }
        }
        distance -= Input.GetAxis("Mouse ScrollWheel") * 2f;
#endif

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        y = ClampAngle(y, yMinLimit, yMaxLimit);
        distance = Mathf.Clamp(distance, distanceMin, distanceMax);

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + target.position;

        transform.rotation = rotation;
        transform.position = position;
    }

    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}

