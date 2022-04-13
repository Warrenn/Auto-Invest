using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public interface IBuySellLogic
    {
        void BuyComplete(ActionDetails details, Contract contract, IContractEditor editor);
        void SellComplete(ActionDetails details, Contract contract, IContractEditor editor);
    }
}
