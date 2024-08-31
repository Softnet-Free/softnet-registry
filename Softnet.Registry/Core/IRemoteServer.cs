using System.Net;

namespace Softnet.Registry.Core
{
    public interface IRemoteServer
    {
        string ServiceName { get; }
        int Id { get; }
        IPAddress IP { get; }
        void OnTargetServerRegistered(IRemoteServer remoteServer);
        void OnTargetServerRemoved(IRemoteServer remoteServer);
    }
}
