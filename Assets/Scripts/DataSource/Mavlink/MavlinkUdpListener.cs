using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>
    /// 루프백(127.0.0.1) UDP 수신 전용. 백그라운드 스레드가 데이터그램을 읽어 프레임으로 파싱하고
    /// 스레드 안전 큐에 넣는다. 메인 스레드는 <see cref="TryDequeue"/>로 꺼낸다.
    /// 송신 코드는 없다(수신·표시 전용 원칙). 큐가 가득 차면 가장 오래된 프레임을 버린다.
    /// </summary>
    public sealed class MavlinkUdpListener : IDisposable
    {
        readonly int requestedPort;
        readonly int maxQueued;
        readonly ConcurrentQueue<MavlinkFrame> queue = new ConcurrentQueue<MavlinkFrame>();
        readonly MavlinkParser parser = new MavlinkParser();

        Socket socket;
        Thread thread;
        volatile bool stopping;
        int queued;
        long datagrams;
        long dropped;

        public MavlinkUdpListener(int port, int maxQueued = 2048)
        {
            requestedPort = port;
            this.maxQueued = Math.Max(1, maxQueued);
        }

        /// <summary>바인드된 포트. 0을 요청하면 OS가 정한 값.</summary>
        public int LocalPort { get; private set; }

        public MavlinkParser Parser => parser;
        public long Datagrams => Interlocked.Read(ref datagrams);

        /// <summary>큐가 가득 차 버린 프레임 수.</summary>
        public long Dropped => Interlocked.Read(ref dropped);

        /// <summary>수신 스레드가 오류로 멈췄다면 그 이유.</summary>
        public string FaultMessage { get; private set; }

        /// <summary>바인드에 실패하면 예외를 던진다(포트 사용 중 등).</summary>
        public void Start()
        {
            if (socket != null)
                return;

            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                s.Bind(new IPEndPoint(IPAddress.Loopback, requestedPort));
            }
            catch
            {
                s.Dispose();
                throw;
            }

            socket = s;
            LocalPort = ((IPEndPoint)s.LocalEndPoint).Port;
            stopping = false;
            thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "MavlinkUdpListener" };
            thread.Start();
        }

        public bool TryDequeue(out MavlinkFrame frame)
        {
            if (queue.TryDequeue(out frame))
            {
                Interlocked.Decrement(ref queued);
                return true;
            }
            return false;
        }

        void ReceiveLoop()
        {
            Socket s = socket; // Dispose가 필드를 비워도 이 스레드는 자기 소켓을 쥐고 있는다.
            var buffer = new byte[65536];
            var frames = new List<MavlinkFrame>();
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (!stopping)
            {
                int n;
                try
                {
                    n = s.ReceiveFrom(buffer, ref remote);
                }
                catch (SocketException e)
                {
                    if (stopping)
                        break;
                    // 수신만 하므로 ICMP로 인한 ConnectionReset은 발생하지 않지만 방어적으로 계속한다.
                    if (e.SocketErrorCode == SocketError.ConnectionReset || e.SocketErrorCode == SocketError.MessageSize)
                        continue;
                    FaultMessage = e.Message;
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                Interlocked.Increment(ref datagrams);
                frames.Clear();
                try
                {
                    parser.Parse(buffer, 0, n, frames);
                }
                catch (Exception e)
                {
                    // 외부 입력 때문에 수신이 멈추면 안 된다.
                    FaultMessage = e.Message;
                    continue;
                }

                for (int i = 0; i < frames.Count; i++)
                    Enqueue(frames[i]);
            }
        }

        void Enqueue(MavlinkFrame frame)
        {
            if (Interlocked.Increment(ref queued) > maxQueued)
            {
                if (queue.TryDequeue(out _))
                    Interlocked.Decrement(ref queued);
                Interlocked.Increment(ref dropped);
            }
            queue.Enqueue(frame);
        }

        public void Dispose()
        {
            stopping = true;
            Socket s = socket;
            socket = null;
            if (s != null)
            {
                try { s.Close(); } catch (SocketException) { }
            }
            thread?.Join(500);
            thread = null;
        }
    }
}
