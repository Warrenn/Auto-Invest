namespace Auto_Invest
{
    public class WebSocketResponse
    {
        public string ev { get; set; }
        public string sym { get; set; }
        public int v { get; set; }
        public int av { get; set; }
        public decimal op { get; set; }
        public decimal vw { get; set; }
        public decimal o { get; set; }
        public decimal c { get; set; }
        public decimal h { get; set; }
        public decimal l { get; set; }
        public decimal a { get; set; }
        public int z { get; set; }
        public long s { get; set; }
        public long e { get; set; }
    }
}
