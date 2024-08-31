using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Softnet.Registry;
using System.Collections;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Softnet.Registry.Core;
using System.Net;
using Softnet.Asn;
using Softnet.Registry.Models;

namespace Softnet.Registry.Services
{
    public class RegistryAPI : SoftnetRegistry.SoftnetRegistryBase, IRemoteServer
    {
        readonly ILogger<RegistryAPI> m_logger;
        readonly IHttpContextAccessor m_httpContextAccessor;
        readonly Controller m_controller;

        int m_serverId;
        public int Id
        {
            get { return m_serverId; }
        }
        private void SetServerId(int Id)
        {
            m_serverId = Id;
        }

        string m_serviceName = null!;
        public string ServiceName
        {
            get { return m_serviceName; }
        }

        IPAddress m_serverIP = null!;
        public IPAddress IP
        {
            get { return m_serverIP; }
        }

        public RegistryAPI(Controller controller, IHttpContextAccessor httpContextAccessor, ILogger<RegistryAPI> logger)
        {
            m_controller = controller;
            m_httpContextAccessor = httpContextAccessor;
            m_logger = logger;
        }

        object mutex = new object();
        IServerStreamWriter<SyncMessage> m_syncStream = null!;
        Queue<SyncMessage> m_syncMessages = new Queue<SyncMessage>();
        bool m_sending_messages = false;

        public async Task GetSiteLocation()
        { 
            
        }

        public override async Task RegisterServer(ServerData serverData, IServerStreamWriter<SyncMessage> syncStream, ServerCallContext context)
        {
            try
            {
                var serverIP = m_httpContextAccessor.HttpContext?.Connection.RemoteIpAddress!;
                if (serverIP is null)
                    throw new InvalidDataException($"A server '{serverData.ServiceName}' has failed to provide the IP address");

                m_syncStream = syncStream;
                m_serviceName = serverData.ServiceName;
                m_serverIP = serverIP;

                m_logger.LogInformation($"A server '{serverData.ServiceName}' has connected");

                lock (mutex)
                {
                    InitialState initialState = m_controller.Register(this, SetServerId, serverData.TargetServices.ToArray<string>());

                    ASNEncoder asnEncoder = new ASNEncoder();
                    SequenceEncoder sequenceEncoder = asnEncoder.Sequence;
                    sequenceEncoder.Int32(m_serverId);
                    if (initialState.targetServices != null)
                    {
                        var asnTargetServices = sequenceEncoder.Sequence();
                        foreach (TargetService targetService in initialState.targetServices)
                        {
                            var asnTargetService = asnTargetServices.Sequence();
                            asnTargetService.PrintableString(targetService.Name);
                            foreach (IRemoteServer remoteServer in targetService.Instances)
                            {
                                var asnInstance = asnTargetService.Sequence();
                                asnInstance.Int32(remoteServer.Id);
                                asnInstance.OctetString(remoteServer.IP.GetAddressBytes());
                            }
                        }
                    }

                    SyncMessage message = new SyncMessage
                    {
                        MsgType = MessageTypes.InitialState,
                        Data = ByteString.CopyFrom(asnEncoder.GetEncoding())
                    };

                    m_sending_messages = true;
                    Task.Run(async () => await SendMessages(message));
                }

                await Task.Delay(Timeout.Infinite, context.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                m_logger.LogInformation($"A server '{serverData.ServiceName}' has disconnected");
            }
            catch (Exception e)
            {
                m_logger.LogError($"A server '{serverData.ServiceName}' has lost the connection. Error: {e.Message}");
            }
            finally
            {
                m_controller.Remove(this);
            }
        }

        private async Task SendMessages(SyncMessage message)
        {
            while (true)
            {
                await m_syncStream.WriteAsync(message);
                lock (mutex)
                {
                    if (m_syncMessages.Count == 0)
                    {
                        m_sending_messages = false;
                        return;
                    }
                    message = m_syncMessages.Dequeue();
                }
            }
        }

        void IRemoteServer.OnTargetServerRegistered(IRemoteServer remoteServer)
        {
            ASNEncoder asnEncoder = new ASNEncoder();
            SequenceEncoder asnTargetServer = asnEncoder.Sequence;
            asnTargetServer.PrintableString(remoteServer.ServiceName);
            asnTargetServer.Int32(remoteServer.Id);
            asnTargetServer.OctetString(remoteServer.IP.GetAddressBytes());

            SyncMessage message = new SyncMessage
            {
                MsgType = MessageTypes.ServerRegistered,
                Data = ByteString.CopyFrom(asnEncoder.GetEncoding())
            };

            lock (mutex)
            {
                if (m_sending_messages)
                {
                    m_syncMessages.Enqueue(message);
                }
                else
                {
                    m_sending_messages = true;
                    Task.Run(async () => await SendMessages(message));
                }
            }
        }

        void IRemoteServer.OnTargetServerRemoved(IRemoteServer remoteServer)
        {
            ASNEncoder asnEncoder = new ASNEncoder();
            SequenceEncoder asnTargetServer = asnEncoder.Sequence;
            asnTargetServer.PrintableString(remoteServer.ServiceName);
            asnTargetServer.Int32(remoteServer.Id);

            SyncMessage message = new SyncMessage
            {
                MsgType = MessageTypes.ServerRemoved,
                Data = ByteString.CopyFrom(asnEncoder.GetEncoding())
            };

            lock (mutex)
            {
                if (m_sending_messages)
                {
                    m_syncMessages.Enqueue(message);
                }
                else
                {
                    m_sending_messages = true;
                    Task.Run(async () => await SendMessages(message));
                }
            }
        }
    }
}
