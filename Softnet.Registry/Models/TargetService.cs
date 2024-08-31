using Softnet.Registry.Core;

namespace Softnet.Registry.Models
{
    public class TargetService
    {
        public readonly string Name;
        public readonly IRemoteServer[] Instances;

        public TargetService(string name, IRemoteServer[] instances)
        {
            Name = name;
            Instances = instances;
        }
    }
}
