using System.Collections.Generic;
using UnityEngine;

namespace DroneC2.View
{
    /// <summary>
    /// 대상 Transform이 지나간 위치를 LineRenderer로 그린다. 월드 좌표만 사용하며 위경도를 알지 못한다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PathTrail : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Material material;
        [SerializeField, Min(0.1f)] float minPointDistanceM = 2f;
        [SerializeField, Min(2)] int maxPoints = 2000;
        [SerializeField, Min(0.01f)] float width = 2f;

        LineRenderer line;
        DroneView targetView;
        readonly List<Vector3> points = new List<Vector3>();
        Vector3[] buffer = new Vector3[0];

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            if (material != null)
                line.sharedMaterial = material;
            line.positionCount = 0;

            if (target != null)
                targetView = target.GetComponent<DroneView>();
        }

        void OnEnable()
        {
            if (targetView != null)
                targetView.PlacementReset += Clear;
        }

        void OnDisable()
        {
            if (targetView != null)
                targetView.PlacementReset -= Clear;
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            // 첫 목표가 배치되기 전의 초기 위치를 경로에 넣으면 원점에서 출발하는 선이 생긴다.
            if (targetView != null && !targetView.HasTarget)
                return;

            Vector3 position = target.position;
            if (points.Count > 0 &&
                (position - points[points.Count - 1]).sqrMagnitude < minPointDistanceM * minPointDistanceM)
                return;

            points.Add(position);
            if (points.Count > maxPoints)
                points.RemoveAt(0);

            Apply();
        }

        /// <summary>경로를 지운다. (유닛 재배치 시 사용)</summary>
        public void Clear()
        {
            points.Clear();
            if (line != null)
                line.positionCount = 0;
        }

        void Apply()
        {
            if (buffer.Length != points.Count)
                buffer = new Vector3[points.Count];
            points.CopyTo(buffer);
            line.positionCount = buffer.Length;
            line.SetPositions(buffer);
        }
    }
}
