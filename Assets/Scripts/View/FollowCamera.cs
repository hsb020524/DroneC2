using UnityEngine;

namespace DroneC2.View
{
    /// <summary>
    /// 대상 Transform의 뒤·위에서 따라가며 대상을 바라보는 카메라. 월드 좌표만 사용한다.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 24f, -52f);
        [SerializeField, Min(0.01f)] float positionSmoothTime = 0.15f;
        [SerializeField, Min(0.1f)] float yawSharpness = 2f;
        [SerializeField] float lookHeightOffset = 4f;

        Vector3 velocity;
        float yaw;
        bool initialized;

        void LateUpdate()
        {
            if (target == null)
                return;

            float targetYaw = target.eulerAngles.y;
            if (!initialized)
            {
                yaw = targetYaw;
                transform.position = DesiredPosition();
                initialized = true;
            }

            // 코너에서 드론 방위가 급변해도 카메라가 휙 돌지 않도록 방위를 따로 보간한다.
            float t = 1f - Mathf.Exp(-yawSharpness * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, targetYaw, t);

            transform.position = Vector3.SmoothDamp(
                transform.position, DesiredPosition(), ref velocity, positionSmoothTime);

            Vector3 lookPoint = target.position + Vector3.up * lookHeightOffset;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }

        Vector3 DesiredPosition()
        {
            return target.position + Quaternion.Euler(0f, yaw, 0f) * offset;
        }
    }
}
