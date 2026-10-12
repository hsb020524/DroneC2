using UnityEngine;

namespace DroneC2.View
{
    /// <summary>
    /// 대상 Transform의 뒤·위에서 따라가며 대상을 바라보는 카메라. 월드 좌표만 사용한다.
    /// 대상이 없으면 고정된 전체 보기 포즈로 이동한다.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 24f, -52f);
        [SerializeField, Min(0.01f)] float positionSmoothTime = 0.15f;
        [SerializeField, Min(0.1f)] float yawSharpness = 2f;
        [SerializeField, Min(0.1f)] float rotationSharpness = 6f;
        [SerializeField] float lookHeightOffset = 4f;

        [Header("전체 보기 (대상이 없을 때)")]
        [SerializeField] Vector3 overviewPosition = new Vector3(500f, 1500f, -1800f);
        [SerializeField] Vector3 overviewLookAt = new Vector3(500f, -100f, 200f);
        [SerializeField, Min(0.01f)] float overviewSmoothTime = 0.8f;

        Vector3 velocity;
        float yaw;
        bool positioned;
        bool yawPending = true;

        public Transform Target => target;

        /// <summary>추적 대상을 바꾼다. null이면 전체 보기로 돌아간다. 위치는 부드럽게 이동한다.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            yawPending = true;
        }

        void LateUpdate()
        {
            if (target == null)
            {
                MoveTo(overviewPosition, overviewLookAt, overviewSmoothTime);
                return;
            }

            float targetYaw = target.eulerAngles.y;
            if (yawPending)
            {
                yaw = targetYaw;
                yawPending = false;
            }

            // 코너에서 드론 방위가 급변해도 카메라가 휙 돌지 않도록 방위를 따로 보간한다.
            float t = 1f - Mathf.Exp(-yawSharpness * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, targetYaw, t);

            Vector3 lookPoint = target.position + Vector3.up * lookHeightOffset;
            MoveTo(DesiredPosition(), lookPoint, positionSmoothTime);
        }

        void MoveTo(Vector3 desiredPosition, Vector3 lookPoint, float smoothTime)
        {
            bool firstFrame = !positioned;
            if (firstFrame)
            {
                transform.position = desiredPosition;
                positioned = true;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, desiredPosition, ref velocity, smoothTime);
            }

            Vector3 forward = lookPoint - transform.position;
            if (forward.sqrMagnitude < 1e-4f)
                return;

            Quaternion desired = Quaternion.LookRotation(forward, Vector3.up);
            if (firstFrame)
            {
                transform.rotation = desired;
                return;
            }

            float r = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, r);
        }

        Vector3 DesiredPosition()
        {
            return target.position + Quaternion.Euler(0f, yaw, 0f) * offset;
        }
    }
}
