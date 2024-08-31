using Softnet.Registry.Models;

namespace Softnet.Registry.Core.Models
{
    public class OnlineService
    {
        public readonly string Name;
        public readonly List<IRemoteServer> Instances;
        public readonly List<IRemoteServer> Listeners;

        public OnlineService(string name)
        {
            Name = name;
            Instances = new List<IRemoteServer>();
            Listeners = new List<IRemoteServer>();
        }
    }
}
