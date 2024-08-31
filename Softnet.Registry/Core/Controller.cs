using Softnet.Registry.Core.Models;
using Softnet.Registry.Models;
using Softnet.Registry.Services;
using System.Net;

namespace Softnet.Registry.Core
{
    public class Controller
    {
        ILogger<Controller> m_logger;
        public Controller(ILogger<Controller> logger)
        {
            m_logger = logger;
            m_onlineServices = new List<OnlineService>();
            m_random = new Random();
        }

        object mutex = new object();
        List<OnlineService> m_onlineServices;

        public InitialState Register(IRemoteServer remoteServer, SetServerIdCallback setServerIdCallback, string[] targetServiceNames)
        {
            m_logger.LogInformation($"Service Name: '{remoteServer.ServiceName}', Server IP: {remoteServer.IP.ToString()}, Target Services: {string.Join(" ", targetServiceNames.ToArray<string>())}");

            var initialState = new InitialState();

            lock (mutex)
            {
                if (targetServiceNames.Length > 0)
                {
                    foreach (string targetServiceName in targetServiceNames)
                    {
                        OnlineService? onlineService = m_onlineServices.FirstOrDefault(service => service.Name.Equals(targetServiceName));
                        if (onlineService != null)
                        {
                            if (onlineService.Instances.Count > 0)
                            {
                                var targetService = new TargetService(onlineService.Name, onlineService.Instances.ToArray());
                                initialState.targetServices.Add(targetService);
                            }
                        }
                        else
                        {
                            onlineService = new OnlineService(targetServiceName);
                            m_onlineServices.Add(onlineService);
                        }
                        onlineService.Listeners.Add(remoteServer);
                    }
                }

                OnlineService? service = m_onlineServices.FirstOrDefault(service => service.Name.Equals(remoteServer.ServiceName));
                if (service != null)
                {
                    setServerIdCallback(GenerateServerId(service));
                    service.Instances.Add(remoteServer);
                    foreach (IRemoteServer listener in service.Listeners)
                    {
                        if (listener.Id != remoteServer.Id)
                            listener.OnTargetServerRegistered(remoteServer);
                    }
                }
                else
                {
                    setServerIdCallback(m_random.Next());
                    service = new OnlineService(remoteServer.ServiceName);
                    service.Instances.Add(remoteServer);
                    m_onlineServices.Add(service);
                }                
            }

            return initialState;
        }

        public void Remove(IRemoteServer remoteServer)
        {
            lock (mutex)
            {
                OnlineService? onlineService = m_onlineServices.FirstOrDefault(service => service.Name.Equals(remoteServer.ServiceName));
                if (onlineService != null)
                {
                    onlineService.Instances.Remove(remoteServer);
                    foreach (IRemoteServer listener in onlineService.Listeners)
                    {
                        if (listener.Id != remoteServer.Id)
                            listener.OnTargetServerRemoved(remoteServer);
                    }
                }

                foreach (OnlineService service in m_onlineServices)
                {
                    service.Listeners.Remove(remoteServer);
                }
            }
        }

        Random m_random;
        int GenerateServerId(OnlineService onlineService)
        {
            for (int i = 0; i < 100; i++)
            {
                int serverId = m_random.Next();
                if (onlineService.Instances.Any(remoteServer => remoteServer.Id == serverId) == false)
                    return serverId;
            }

            int lowestId = onlineService.Instances.Min(remoteServer => remoteServer.Id);
            lowestId--;
            return lowestId;
        }
    }
}
