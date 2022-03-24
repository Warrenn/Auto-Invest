using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public interface IBuySellLogic
    {
        Task TrailingBuyComplete(ActionDetails details);
        Task TrailingSellComplete(ActionDetails details);
        Task EmergencyActionComplete(EmergencyActionDetails details);
    }
}
