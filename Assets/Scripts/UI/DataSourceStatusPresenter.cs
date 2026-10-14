using DroneC2.Core;
using DroneC2.DataSource.Mavlink;
using UnityEngine;
using UnityEngine.UIElements;

namespace DroneC2.UI
{
    /// <summary>
    /// 상단 표시줄에 현재 데이터 소스와 수신 상태(기존 파서·라우터 카운터)를 표시한다. 표시 전용이다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DataSourceStatusPresenter : MonoBehaviour
    {
        const string SimulatedClass = "source-simulated";
        const string MavlinkClass = "source-mavlink";
        const string ErrorClass = "source-error";

        [SerializeField] TelemetrySourceSelector selector;
        [SerializeField, Min(0.05f)] float refreshIntervalSec = 0.25f;

        Label label;
        float sinceRefresh;
        bool started;

        void Start()
        {
            started = true;
            Bind();
        }

        void OnEnable()
        {
            if (started)
                Bind();
        }

        void Bind()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            label = root?.Q<Label>("source-status");
            if (label == null)
                Debug.LogError("DataSourceStatusPresenter: source-status 라벨을 찾지 못했습니다.", this);
            Refresh();
        }

        void Update()
        {
            sinceRefresh += Time.unscaledDeltaTime;
            if (sinceRefresh < refreshIntervalSec)
                return;
            sinceRefresh = 0f;
            Refresh();
        }

        void Refresh()
        {
            if (label == null)
                return;

            DataSourceStatus status;
            if (selector == null)
            {
                // 선택기가 없으면 소스를 알 수 없다. 가상으로 단정하지 않는다.
                status = new DataSourceStatus("데이터 소스: 알 수 없음", DataSourceTone.Error);
            }
            else
            {
                MavlinkReceiver receiver = selector.Receiver;
                MavlinkParser parser = receiver != null ? receiver.Parser : null;
                long received = parser != null ? parser.V1Frames + parser.V2Frames : 0;
                long rejected = parser != null ? parser.Rejected : 0;
                status = DataSourceStatusFormat.Describe(selector.Mode, receiver != null,
                    receiver != null && receiver.IsListening, receiver != null ? receiver.Port : 0,
                    received, rejected, receiver != null ? receiver.UnknownSysid : 0);
            }

            label.text = status.Text;
            label.EnableInClassList(SimulatedClass, status.Tone == DataSourceTone.Simulated);
            label.EnableInClassList(MavlinkClass, status.Tone == DataSourceTone.Mavlink);
            label.EnableInClassList(ErrorClass, status.Tone == DataSourceTone.Error);
        }
    }
}
