namespace Auto_Invest
{
    public static class SecTypes
    {
        public const string STOCK = "STK";
    }

    public static class Currency
    {
        public const string USD = "USD";
    }
    public static class Exchanges
    {
        public const string SMART = "SMART";
        public const string ISLAND = "ISLAND";
    }
    public static class ActionTypes
    {
        public const string BUY = "BUY";
        public const string SELL = "SELL";
    }
    public static class OrderTypes
    {
        public const string STOP_LIMIT = "STP LMT";
        public const string STOP = "STP";
    }

    public static class OrderStatus
    {
        public const string ApiPending = "ApiPending";
        public const string PendingSubmit = "PendingSubmit";
        public const string PendingCancel = "PendingCancel";
        public const string PreSubmitted = "PreSubmitted ";
        public const string Submitted = "Submitted";
        public const string ApiCancelled = "ApiCancelled";
        public const string Cancelled = "Cancelled";
        public const string Filled = "Filled";
        public const string Inactive = "Inactive";
    }
}
