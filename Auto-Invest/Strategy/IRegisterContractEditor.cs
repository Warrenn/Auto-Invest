namespace Auto_Invest.Strategy
{
    public interface IRegisterContractEditor
    {
        void RegisterEditor(Contract state, IContractEditor contractEditor);
    }
}