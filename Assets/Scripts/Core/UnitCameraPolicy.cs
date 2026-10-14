using DroneC2.DataSource;

namespace DroneC2.Core
{
    /// <summary>
    /// 유닛 카메라를 렌더할지 정하는 규칙. 렌더 비용을 줄이기 위해 선택되었거나 고정된(패널에 표시 중인) 유닛만 그리고,
    /// 두절된 유닛은 갱신되지 않으므로 그리지 않는다.
    /// </summary>
    public static class UnitCameraPolicy
    {
        public static bool ShouldRender(bool shown, UnitStatus status)
        {
            return shown && status != UnitStatus.Lost;
        }
    }
}
