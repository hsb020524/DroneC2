using UnityEngine;

namespace DroneC2.View
{
    /// <summary>
    /// 목표 위치·회전을 받아 부드럽게 따라가는 표시 컴포넌트. 위경도나 데이터 소스를 알지 못한다.
    /// </summary>
    public class DroneView : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] float positionSmoothTime = 0.1f;
        [SerializeField, Min(0.1f)] float rotationSharpness = 8f;

        Vector3 targetPosition;
        Quaternion targetRotation;
        Vector3 velocity;
        bool hasTarget;
        Renderer[] hiddenRenderers;

        /// <summary>첫 목표를 받아 배치가 끝났는지 여부. 배치 전의 Transform 위치는 의미가 없다.</summary>
        public bool HasTarget => hasTarget;

        void Awake()
        {
            HideUntilPlaced();
        }

        // 첫 목표 전에는 씬의 초기 위치가 의미 없으므로 모델·마커·경로선 등 자식 표시를 모두 숨긴다.
        // 이미 꺼져 있던 렌더러는 그대로 둔다(첫 목표 때 켜지 않는다).
        void HideUntilPlaced()
        {
            if (hasTarget || hiddenRenderers != null)
                return;

            var list = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled)
                {
                    r.enabled = false;
                    list.Add(r);
                }
            }
            hiddenRenderers = list.ToArray();
        }

        public void SetTarget(Vector3 position, Quaternion rotation)
        {
            targetPosition = position;
            targetRotation = rotation;

            if (!hasTarget)
            {
                // 첫 샘플은 보간 없이 바로 배치
                transform.SetPositionAndRotation(position, rotation);
                hasTarget = true;

                if (hiddenRenderers != null)
                {
                    foreach (Renderer r in hiddenRenderers)
                    {
                        if (r != null)
                            r.enabled = true;
                    }
                    hiddenRenderers = null;
                }
            }
        }

        void Update()
        {
            if (!hasTarget)
                return;

            transform.position = Vector3.SmoothDamp(
                transform.position, targetPosition, ref velocity, positionSmoothTime);

            float t = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }
    }
}
