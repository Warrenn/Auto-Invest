using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class OrderProgress
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public double AvgPrice { get; set; }
        public double Commission { get; set; }
        public string ExecId { get; set; }
        public double CumQty { get; set; }
        public ActionSide Side { get; set; }
        public ProgressStatus Progress { get; set; }
    }
}
