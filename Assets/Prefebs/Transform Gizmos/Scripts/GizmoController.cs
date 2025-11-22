using UnityEngine;

namespace TransformGizmos
{
    public class GizmoController : MonoBehaviour
    {
        [SerializeField] Rotation m_rotation;
        [SerializeField] Translation m_translation;
        [SerializeField] Scaling m_scaling;
        [SerializeField] GameObject m_rotationAppendix;

        [SerializeField] Material m_clickedMaterial;
        [SerializeField] Material m_transparentMaterial;
        [SerializeField] GameObject m_objectWithMeshes;
        [SerializeField] GameObject m_degreesText;

        [Header("Adjustable Variables")]
        [SerializeField] GameObject m_targetObject;
        [SerializeField] float m_gizmoSize = 1;

        Transformation m_transformation = Transformation.None;

        // 현재 초기화된 타겟을 기억하는 변수
        private GameObject _currentTarget = null;

        enum Transformation
        {
            None,
            Rotation,
            Translation,
            Scale
        }

        void Start()
        {
            if (m_targetObject == null) return;
            // Start에서는 초기화하지 않고 SetTarget 호출을 기다림
        }

        // 내부 초기화 로직
        void InitializeGizmo()
        {
            if (m_targetObject == null) return;

            // 💡 [핵심 1] 타겟이 같으면 재초기화(Initialization)를 수행하지 않음
            // (버튼을 여러 번 눌러도 내부 로직이 꼬이는 것을 방지)
            if (_currentTarget == m_targetObject) 
            {
                // 위치와 회전만 다시 맞추고 끝냄
                SyncTransform();
                // 모드만 확실하게 다시 켬
                if (m_transformation == Transformation.None) ChangeTransformationState(Transformation.Rotation);
                return;
            }

            _currentTarget = m_targetObject; // 새 타겟 기억

            SyncTransform();
            
            // 💡 [핵심 2] 기즈모 자체의 크기는 무조건 (1,1,1)로 고정
            transform.localScale = Vector3.one;

            // 하위 모듈 초기화 (타겟이 바뀌었을 때만 실행됨)
            if(m_rotation != null) 
                m_rotation.Initialization(m_targetObject, m_clickedMaterial, m_transparentMaterial, m_objectWithMeshes, m_degreesText, m_rotationAppendix);
            if(m_translation != null)
                m_translation.Initialization(m_targetObject, m_clickedMaterial, m_transparentMaterial);
            if(m_scaling != null)
                m_scaling.Initialization(m_targetObject, m_clickedMaterial, m_transparentMaterial);

            // 초기 상태 설정 (None -> Rotation)
            ChangeTransformationState(Transformation.Rotation);
        }

        void SyncTransform()
        {
            if (m_targetObject != null)
            {
                transform.position = m_targetObject.transform.position;
                transform.rotation = m_targetObject.transform.rotation;
                // Scale은 절대 따라가지 않음 (Vector3.one 유지)
                transform.localScale = Vector3.one;
            }
        }

        void Update()
        {
            if (m_targetObject == null) return;

            SyncTransform();
            
            if(m_degreesText != null) m_degreesText.transform.position = m_targetObject.transform.position;
            if(m_objectWithMeshes != null) m_objectWithMeshes.transform.position = m_targetObject.transform.position;
            
            // 기즈모 사이즈 유지
            if(m_rotation != null) m_rotation.SetGizmoSize(m_gizmoSize);
            if(m_translation != null) m_translation.SetGizmoSize(m_gizmoSize);
            if(m_scaling != null) m_scaling.SetGizmoSize(m_gizmoSize);

            // 키보드 단축키
            if (Input.GetKeyDown(KeyCode.R)) ChangeTransformationState(Transformation.Rotation);
            if (Input.GetKeyDown(KeyCode.T)) ChangeTransformationState(Transformation.Translation);
            if (Input.GetKeyDown(KeyCode.Z)) ChangeTransformationState(Transformation.Scale);
        }

        // FurnitureManager에서 호출
        public void SetTarget(GameObject target)
        {
            m_targetObject = target;
            InitializeGizmo();
        }

        public void EnableRotation()
        {
            // 강제로 켜기
            if(m_rotation != null) m_rotation.gameObject.SetActive(true);
            if(m_translation != null) m_translation.gameObject.SetActive(false);
            if(m_scaling != null) m_scaling.gameObject.SetActive(false);
            
            m_transformation = Transformation.Rotation;
        }

        // (기존 함수 유지)
        public void ToggleRotation() { ChangeTransformationState(Transformation.Rotation); }
        public void ToggleMovement() { ChangeTransformationState(Transformation.Translation); }
        public void ToggleScale() { ChangeTransformationState(Transformation.Scale); }

        private void ChangeTransformationState(Transformation transformation)
        {
            if(m_rotation != null) m_rotation.gameObject.SetActive(false);
            if(m_translation != null) m_translation.gameObject.SetActive(false);
            if(m_scaling != null) m_scaling.gameObject.SetActive(false);

            switch (transformation)
            {
                case Transformation.None:
                    break;

                case Transformation.Rotation:
                    if (m_transformation == Transformation.Rotation)
                        m_transformation = Transformation.None;
                    else
                    {
                        if(m_rotation != null) m_rotation.gameObject.SetActive(true);
                        m_transformation = transformation;
                    }
                    break;

                case Transformation.Translation:
                    if (m_transformation == Transformation.Translation)
                        m_transformation = Transformation.None;
                    else
                    {
                        if(m_translation != null) m_translation.gameObject.SetActive(true);
                        m_transformation = transformation;
                    }
                    break;

                case Transformation.Scale:
                    if (m_transformation == Transformation.Scale)
                        m_transformation = Transformation.None;
                    else
                    {
                        if(m_scaling != null) m_scaling.gameObject.SetActive(true);
                        m_transformation = transformation;
                    }
                    break;
            }
        }
    }
}