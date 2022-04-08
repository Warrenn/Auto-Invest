using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public interface IRecordTick
    {
        Task Tick(TickPosition tick);
    }
}
