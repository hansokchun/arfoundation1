using UnityEngine;

/// <summary>
/// 개별 가구에 부착되어 FurnitureManager의 명령을 받아 실제 이동, 회전, 충돌 검사 및 외곽선 하이라이트를 수행합니다.
/// </summary>
public class FurnitureDragger : MonoBehaviour
{
    [Header("Settings")]
    public Vector3 overlapBoxSize = new Vector3(1f, 1f, 1f);

    private float yOffset;
    private LayerMask collisionLayerMask;

    [Header("Visual Feedback (Outline)")]
    private Renderer objectRenderer;
    public Color outlineColor = Color.yellow; // 외곽선 색상
    [Range(0f, 0.1f)]
    public float outlineWidth = 0.02f; // 외곽선 두께


    void Awake()
    {
        collisionLayerMask = LayerMask.GetMask("Wall", "Furniture");
        objectRenderer = GetComponent<Renderer>();
        // 시작할 때 외곽선이 보이지 않도록 초기화
        Deselect();
    }

    // --- FurnitureManager가 호출할 함수들 ---

    /// <summary>
    /// 가구가 선택되었을 때 호출됩니다. 외곽선을 활성화합니다.
    /// </summary>
    public void Select()
    {
        yOffset = transform.position.y;
        if (objectRenderer != null)
        {
            // Material의 프로퍼티를 변경하여 외곽선을 켭니다.
            // _OutlineWidth 와 _OutlineColor 는 셰이더에 정의된 이름이어야 합니다.
            objectRenderer.material.SetFloat("_OutlineWidth", outlineWidth);
            objectRenderer.material.SetColor("_OutlineColor", outlineColor);
        }
    }

    /// <summary>
    /// 가구 선택이 해제되었을 때 호출됩니다. 외곽선을 비활성화합니다.
    /// </summary>
    public void Deselect()
    {
        if (objectRenderer != null)
        {
            // 외곽선 두께를 0으로 만들어 보이지 않게 합니다.
            objectRenderer.material.SetFloat("_OutlineWidth", 0f);
        }
    }

    public void MoveTo(Vector3 targetPoint)
    {
        Vector3 targetPos = targetPoint;
        targetPos.y = yOffset;
        if (!IsCollidingAt(targetPos))
        {
            transform.position = targetPos;
        }
    }

    public void SetRotation(float yAngle)
    {
        Vector3 currentEulerAngles = transform.eulerAngles;
        transform.eulerAngles = new Vector3(currentEulerAngles.x, yAngle, currentEulerAngles.z);
    }

    // --- 충돌 감지 ---
    bool IsCollidingAt(Vector3 position)
    {
        Collider[] hitColliders = Physics.OverlapBox(
            position,
            overlapBoxSize * 0.5f,
            transform.rotation,
            collisionLayerMask
        );

        foreach (var col in hitColliders)
        {
            if (col.gameObject != this.gameObject)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 에디터에서 충돌 박스 크기를 시각적으로 보여줍니다.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, overlapBoxSize);
    }
}

