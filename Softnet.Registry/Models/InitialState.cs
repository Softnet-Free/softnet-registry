namespace Softnet.Registry.Models
{
    public class InitialState
    {
        public List<TargetService> targetServices;
        public InitialState()
        {
            targetServices = new List<TargetService>();
        }
    }
}
