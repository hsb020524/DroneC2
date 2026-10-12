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

        /// <summary>첫 목표를 받아 배치가 끝났는지 여부. 배치 전의 Transform 위치는 의미가 없다.</summary>
        public bool HasTarget => hasTarget;

        public void SetTarget(Vector3 position, Quaternion rotation)
        {
            targetPosition = position;
            targetRotation = rotation;

            if (!hasTarget)
            {
                // 첫 샘플은 보간 없이 바로 배치
                transform.SetPositionAndRotation(position, rotation);
                hasTarget = true;
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
