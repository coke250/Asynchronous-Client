using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

namespace Asynchronous_Client
{
    public class StateObject
    {
        public Socket workSocket = null;
        public const int BufferSize = 256;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();
    }

    public class AsynchronousClient
    {
        private const int port = 10000;

        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);

        private static string response = string.Empty;

        public static void StartClient()
        {
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry("127.0.0.1");
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                // TCP/IP 소켓 생성
                Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                // Remote Endpoint에 연결
                client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();

                // Remote device에 데이터 전송
                Send(client, "This is a test<EOF>");
                sendDone.WaitOne();

                // Remote Device로부터 데이터 수신
                Receive(client);
                receiveDone.WaitOne();

                // 수신받은 데이터 콘솔에 출력
                Console.WriteLine("Response received : " + response);

                // 소켓 해제
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // state object로부터 socket 추출
                Socket client = (Socket)ar.AsyncState;

                // 연결 완료
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());

                connectDone.Set();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Receive(Socket client)
        {
            try
            {
                // StateObject 생성
                StateObject state = new StateObject();
                state.workSocket = client;

                // Remote Device로부터 데이터 수신
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // StateObject로부터 client Socket 추출
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Remote Device로부터 데이터 읽음
                int bytesRead = client.EndReceive(ar);

                if(bytesRead > 0)
                {
                    // 수신받은 데이터 저장
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // 남은 데이터 수신
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // 모든 데이터가 도착한 경우 response에 대입
                    if(state.sb.Length > 1)
                    {
                        response = state.sb.ToString();
                    }

                    receiveDone.Set();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Send(Socket client, string data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // StateObject로부터 Socket 추출
                Socket client = (Socket)ar.AsyncState;

                // Remote Device로 송신 완료
                int bytesSent = client.EndSendTo(ar);
                Console.WriteLine("Send{0} bytes to server.", bytesSent);

                // 송신 완료 신호
                sendDone.Set();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            AsynchronousClient.StartClient();

        }
    }
}
