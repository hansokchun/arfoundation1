using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타겟을 중심으로 회전하며 핀치 줌을 지원하는 카메라 컨트롤러입니다.
/// 모바일 터치 제스처(드래그, 핀치)와 에디터 마우스 입력을 모두 지원합니다.
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
        if (!this.enabled || target == null) return;

        // 1. 모바일 터치 입력 처리
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
        {
            Touch touch = Input.GetTouch(0);
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return;

            Vector2 touchDeltaPosition = touch.deltaPosition;
            x += touchDeltaPosition.x * xSpeed * 0.002f;
            y -= touchDeltaPosition.y * ySpeed * 0.002f;
        }
        else if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;
            distance += deltaMagnitudeDiff * zoomSpeed * 0.01f;
        }

        // 2. 에디터 마우스 입력 처리 (테스트용)
#if UNITY_EDITOR
        if (Input.touchCount == 0) 
        {
            if (Input.GetMouseButton(0))
            {
                if (EventSystem.current.IsPointerOverGameObject()) return;
                x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
            }
            distance -= Input.GetAxis("Mouse ScrollWheel") * 2f;
        }
#endif

        UpdateCameraPosition();
    }

    /// <summary>
    /// 계산된 회전값(x, y)과 거리(distance)를 기반으로 카메라의 최종 위치와 회전을 갱신합니다.
    /// </summary>
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