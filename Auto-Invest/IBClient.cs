using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using IBApi;

namespace CoreBot
{
    public class IBClient : EWrapper
    {
        public int ClientId { get; set; }
        public string Host { get; }
        public int Port { get; }
        public EClientSocket ClientSocket { get; }
        public int CurrentOrderId => _currentOrderId;

        private CancellationTokenSource _source;
        private EReader _reader;
        private int _currentOrderId;
        private readonly SynchronizationContext _sc;
        private readonly EReaderSignal _signal;

        public IBClient(int clientId, string host, int port)
        {
            ClientId = clientId;
            Host = host;
            Port = port;

            _signal = new EReaderMonitorSignal();
            ClientSocket = new EClientSocket(this, _signal);
            _sc = SynchronizationContext.Current;
        }

        public int GetNextOrderId()
        {
            _currentOrderId++;
            return _currentOrderId;
        }

        public void Connect()
        {
            ClientSocket.AllowRedirect = true;
            ClientSocket.eConnect(Host, Port, ClientId);
            if (!ClientSocket.IsConnected())
                throw new Exception("Client failed connection");
        }

        public void Disconnect()
        {
            _source.Cancel();
            ClientSocket.eDisconnect();
        }

        public void Run()
        {
            _source = new CancellationTokenSource();
            var token = _source.Token;

            _reader = new EReader(ClientSocket, _signal);
            _reader.Start();

            Task.Run(() =>
            {
                while (ClientSocket.IsConnected() && !token.IsCancellationRequested)
                {
                    _signal.waitForSignal();
                    _reader.processMsgs();
                }
            }, token);
        }

        private void Post<T>(Action<T> action, T message, [CallerMemberName] string callerName = "")
        {
            PostCallBackEvent?.Invoke(message, callerName);
            if (action == null) return;
            if (_sc != null) _sc.Post((t) => action(message), null);
            else action(message);
        }
        private void Post(Action action, [CallerMemberName] string callerName = "")
        {
            PostCallBackEvent?.Invoke(null, callerName);
            if (action == null) return;
            if (_sc != null) _sc.Post((t) => action(), null);
            else action();
        }

        private void PostError(int id, int errorCode, string errorMsg, Exception ex, [CallerMemberName] string callerName = "")
        {
            PostCallBackEvent?.Invoke(new { id, errorCode, errorMsg, ex }, callerName);
            if (Error == null) return;
            if (_sc != null) _sc.Post((t) => Error(id, errorCode, errorMsg, ex), null);
            else Error(id, errorCode, errorMsg, ex);
        }

        public event Action<object, string> PostCallBackEvent;

        public event Action<int, int, string, Exception> Error;
        public void error(Exception e) => PostError(0, 0, null, e);
        public void error(string str) => PostError(0, 0, str, null);
        public void error(int id, int errorCode, string errorMsg) => PostError(id, errorCode, errorMsg, null);

        public event Action<int> NextValidId;
        public void nextValidId(int orderId)
        {
            Post(NextValidId, orderId);
            _currentOrderId = orderId;
        }

        public event Action<string[]> ManagedAccounts;
        public void managedAccounts(string accountsList) => Post(ManagedAccounts, accountsList?.Split(','));


        public class TickPriceClass
        {
            public int TickerId { get; set; }
            public int Field { get; set; }
            public double Price { get; set; }
            public TickAttrib Attribs { get; set; }
        }

        public class TickSizeClass
        {
            public int TickerId { get; set; }
            public int Field { get; set; }
            public int Size { get; set; }
        }

        public class TickStringClass
        {
            public int TickerId { get; set; }
            public int Field { get; set; }
            public string Value { get; set; }
        }

        public class TickGenericClass
        {
            public int TickerId { get; set; }
            public int Field { get; set; }
            public double Value { get; set; }
        }

        public class TickEFPClass
        {
            public int TickerId { get; set; }
            public int TickType { get; set; }
            public double BasisPoints { get; set; }
            public string FormattedBasisPoints { get; set; }
            public double ImpliedFuture { get; set; }
            public int HoldDays { get; set; }
            public string FutureLastTradeDate { get; set; }
            public double DividendImpact { get; set; }
            public double DividendsToLastTradeDate { get; set; }
        }

        public class DeltaNeutralValidationClass
        {
            public int ReqId { get; set; }
            public DeltaNeutralContract DeltaNeutralContract { get; set; }
        }

        public class TickOptionComputationClass
        {
            public int TickerId { get; set; }
            public int Field { get; set; }
            public double ImpliedVolatility { get; set; }
            public double Delta { get; set; }
            public double OptPrice { get; set; }
            public double PvDividend { get; set; }
            public double Gamma { get; set; }
            public double Vega { get; set; }
            public double Theta { get; set; }
            public double UndPrice { get; set; }
        }

        public class AccountSummaryClass
        {
            public int ReqId { get; set; }
            public string Account { get; set; }
            public string Tag { get; set; }
            public string Value { get; set; }
            public string Currency { get; set; }
        }

        public class BondContractDetailsClass
        {
            public int ReqId { get; set; }
            public ContractDetails Contract { get; set; }
        }

        public class UpdateAccountValueClass
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public string Currency { get; set; }
            public string AccountName { get; set; }
        }

        public class UpdatePortfolioClass
        {
            public Contract Contract { get; set; }
            public double Position { get; set; }
            public double MarketPrice { get; set; }
            public double MarketValue { get; set; }
            public double AverageCost { get; set; }
            public double UnrealizedPNL { get; set; }
            public double RealizedPNL { get; set; }
            public string AccountName { get; set; }
        }

        public class OrderStatusClass
        {
            public int OrderId { get; set; }
            public string Status { get; set; }
            public double Filled { get; set; }
            public double Remaining { get; set; }
            public double AvgFillPrice { get; set; }
            public int PermId { get; set; }
            public int ParentId { get; set; }
            public double LastFillPrice { get; set; }
            public int ClientId { get; set; }
            public string WhyHeld { get; set; }
            public double MktCapPrice { get; set; }
        }

        public class OpenOrderClass
        {
            public int OrderId { get; set; }
            public Contract Contract { get; set; }
            public Order Order { get; set; }
            public OrderState OrderState { get; set; }
        }

        public class ContractDetailsClass
        {
            public int ReqId { get; set; }
            public ContractDetails ContractDetails { get; set; }
        }

        public class ExecDetailsClass
        {
            public int ReqId { get; set; }
            public Contract Contract { get; set; }
            public Execution Execution { get; set; }
        }

        public class FundamentalDataClass
        {
            public int ReqId { get; set; }
            public string Data { get; set; }
        }

        public class HistoricalDataClass
        {
            public int ReqId { get; set; }
            public Bar Bar { get; set; }
        }

        public class HistoricalDataUpdateClass
        {
            public int ReqId { get; set; }
            public Bar Bar { get; set; }
        }

        public class HistoricalDataEndClass
        {
            public int ReqId { get; set; }
            public string Start { get; set; }
            public string End { get; set; }
        }

        public class MarketDataTypeClass
        {
            public int ReqId { get; set; }
            public int MarketDataType { get; set; }
        }

        public class UpdateMktDepthClass
        {
            public int TickerId { get; set; }
            public int Position { get; set; }
            public int Operation { get; set; }
            public int Side { get; set; }
            public double Price { get; set; }
            public int Size { get; set; }
        }

        public class UpdateMktDepthL2Class
        {
            public int TickerId { get; set; }
            public int Position { get; set; }
            public string MarketMaker { get; set; }
            public int Operation { get; set; }
            public int Side { get; set; }
            public double Price { get; set; }
            public int Size { get; set; }
            public bool IsSmartDepth { get; set; }
        }

        public class UpdateNewsBulletinClass
        {
            public int MsgId { get; set; }
            public int MsgType { get; set; }
            public string Message { get; set; }
            public string OrigExchange { get; set; }
        }

        public class PositionClass
        {
            public string Account { get; set; }
            public Contract Contract { get; set; }
            public double Pos { get; set; }
            public double AvgCost { get; set; }
        }

        public class RealtimeBarClass
        {
            public int ReqId { get; set; }
            public long Date { get; set; }
            public double Open { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Close { get; set; }
            public long Volume { get; set; }
            public double WAP { get; set; }
            public int Count { get; set; }
        }

        public class ScannerDataClass
        {
            public int ReqId { get; set; }
            public int Rank { get; set; }
            public ContractDetails ContractDetails { get; set; }
            public string Distance { get; set; }
            public string Benchmark { get; set; }
            public string Projection { get; set; }
            public string LegsStr { get; set; }
        }

        public class ReceiveFAClass
        {
            public int FaDataType { get; set; }
            public string FaXmlData { get; set; }
        }

        public class VerifyCompletedClass
        {
            public bool IsSuccessful { get; set; }
            public string ErrorText { get; set; }
        }

        public class VerifyAndAuthMessageAPIClass
        {
            public string ApiData { get; set; }
            public string XyzChallenge { get; set; }
        }

        public class VerifyAndAuthCompletedClass
        {
            public bool IsSuccessful { get; set; }
            public string ErrorText { get; set; }
        }

        public class DisplayGroupListClass
        {
            public int ReqId { get; set; }
            public string Groups { get; set; }
        }

        public class DisplayGroupUpdatedClass
        {
            public int ReqId { get; set; }
            public string ContractInfo { get; set; }
        }

        public class PositionMultiClass
        {
            public int RequestId { get; set; }
            public string Account { get; set; }
            public string ModelCode { get; set; }
            public Contract Contract { get; set; }
            public double Pos { get; set; }
            public double AvgCost { get; set; }
        }

        public class AccountUpdateMultiClass
        {
            public int RequestId { get; set; }
            public string Account { get; set; }
            public string ModelCode { get; set; }
            public string Key { get; set; }
            public string Value { get; set; }
            public string Currency { get; set; }
        }

        public class SecurityDefinitionOptionParameterClass
        {
            public int ReqId { get; set; }
            public string Exchange { get; set; }
            public int UnderlyingConId { get; set; }
            public string TradingClass { get; set; }
            public string Multiplier { get; set; }
            public HashSet<string> Expirations { get; set; }
            public HashSet<double> Strikes { get; set; }
        }

        public class SoftDollarTiersClass
        {
            public int ReqId { get; set; }
            public SoftDollarTier[] Tiers { get; set; }
        }

        public class SymbolSamplesClass
        {
            public int ReqId { get; set; }
            public ContractDescription[] ContractDescriptions { get; set; }
        }

        public class TickNewsClass
        {
            public int TickerId { get; set; }
            public long TimeStamp { get; set; }
            public string ProviderCode { get; set; }
            public string ArticleId { get; set; }
            public string Headline { get; set; }
            public string ExtraData { get; set; }
        }

        public class SmartComponentsClass
        {
            public int ReqId { get; set; }
            public Dictionary<int, KeyValuePair<string, char>> TheMap { get; set; }
        }

        public class TickReqParamsClass
        {
            public int TickerId { get; set; }
            public double MinTick { get; set; }
            public string BboExchange { get; set; }
            public int SnapshotPermissions { get; set; }
        }

        public class NewsArticleClass
        {
            public int RequestId { get; set; }
            public int ArticleType { get; set; }
            public string ArticleText { get; set; }
        }

        public class HistoricalNewsClass
        {
            public int RequestId { get; set; }
            public string Time { get; set; }
            public string ProviderCode { get; set; }
            public string ArticleId { get; set; }
            public string Headline { get; set; }
        }

        public class HistoricalNewsEndClass
        {
            public int RequestId { get; set; }
            public bool HasMore { get; set; }
        }

        public class HeadTimestampClass
        {
            public int ReqId { get; set; }
            public string HeadTimestamp { get; set; }
        }

        public class HistogramDataClass
        {
            public int ReqId { get; set; }
            public HistogramEntry[] Data { get; set; }
        }

        public class RerouteMktDataReqClass
        {
            public int ReqId { get; set; }
            public int ConId { get; set; }
            public string Exchange { get; set; }
        }

        public class RerouteMktDepthReqClass
        {
            public int ReqId { get; set; }
            public int ConId { get; set; }
            public string Exchange { get; set; }
        }

        public class MarketRuleClass
        {
            public int MarketRuleId { get; set; }
            public PriceIncrement[] PriceIncrements { get; set; }
        }

        public class PnlClass
        {
            public int ReqId { get; set; }
            public double DailyPnL { get; set; }
            public double UnrealizedPnL { get; set; }
            public double RealizedPnL { get; set; }
        }

        public class PnlSingleClass
        {
            public int ReqId { get; set; }
            public int Pos { get; set; }
            public double DailyPnL { get; set; }
            public double UnrealizedPnL { get; set; }
            public double RealizedPnL { get; set; }
            public double Value { get; set; }
        }

        public class HistoricalTicksClass
        {
            public int ReqId { get; set; }
            public HistoricalTick[] Ticks { get; set; }
            public bool Done { get; set; }
        }

        public class HistoricalTicksBidAskClass
        {
            public int ReqId { get; set; }
            public HistoricalTickBidAsk[] Ticks { get; set; }
            public bool Done { get; set; }
        }

        public class HistoricalTicksLastClass
        {
            public int ReqId { get; set; }
            public HistoricalTickLast[] Ticks { get; set; }
            public bool Done { get; set; }
        }

        public class TickByTickAllLastClass
        {
            public int ReqId { get; set; }
            public int TickType { get; set; }
            public long Time { get; set; }
            public double Price { get; set; }
            public int Size { get; set; }
            public TickAttribLast TickAttriblast { get; set; }
            public string Exchange { get; set; }
            public string SpecialConditions { get; set; }
        }

        public class TickByTickBidAskClass
        {
            public int ReqId { get; set; }
            public long Time { get; set; }
            public double BidPrice { get; set; }
            public double AskPrice { get; set; }
            public int BidSize { get; set; }
            public int AskSize { get; set; }
            public TickAttribBidAsk TickAttribBidAsk { get; set; }
        }

        public class TickByTickMidPointClass
        {
            public int ReqId { get; set; }
            public long Time { get; set; }
            public double MidPoint { get; set; }
        }

        public class OrderBoundClass
        {
            public long OrderId { get; set; }
            public int ApiClientId { get; set; }
            public int ApiOrderId { get; set; }
        }

        public class CompletedOrderClass
        {
            public Contract Contract { get; set; }
            public Order Order { get; set; }
            public OrderState OrderState { get; set; }
        }


        public event Action<long> CurrentTimeEvent;
        public void currentTime(long time) => Post(CurrentTimeEvent, time);
        public event Action<TickPriceClass> TickPriceEvent;
        public void tickPrice(int tickerId, int field, double price, TickAttrib attribs) => Post(TickPriceEvent, new TickPriceClass
        {
            TickerId = tickerId,
            Field = field,
            Price = price,
            Attribs = attribs
        });

        public event Action<TickSizeClass> TickSizeEvent;
        public void tickSize(int tickerId, int field, int size) => Post(TickSizeEvent, new TickSizeClass
        {
            TickerId = tickerId,
            Field = field,
            Size = size
        });

        public event Action<TickStringClass> TickStringEvent;
        public void tickString(int tickerId, int field, string value) => Post(TickStringEvent, new TickStringClass
        {
            TickerId = tickerId,
            Field = field,
            Value = value
        });

        public event Action<TickGenericClass> TickGenericEvent;
        public void tickGeneric(int tickerId, int field, double value) => Post(TickGenericEvent, new TickGenericClass
        {
            TickerId = tickerId,
            Field = field,
            Value = value
        });

        public event Action<TickEFPClass> TickEFPEvent;
        public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) => Post(TickEFPEvent, new TickEFPClass
        {
            TickerId = tickerId,
            TickType = tickType,
            BasisPoints = basisPoints,
            FormattedBasisPoints = formattedBasisPoints,
            ImpliedFuture = impliedFuture,
            HoldDays = holdDays,
            FutureLastTradeDate = futureLastTradeDate,
            DividendImpact = dividendImpact,
            DividendsToLastTradeDate = dividendsToLastTradeDate
        });

        public event Action<DeltaNeutralValidationClass> DeltaNeutralValidationEvent;
        public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) => Post(DeltaNeutralValidationEvent, new DeltaNeutralValidationClass
        {
            ReqId = reqId,
            DeltaNeutralContract = deltaNeutralContract
        });

        public event Action<TickOptionComputationClass> TickOptionComputationEvent;
        public void tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) => Post(TickOptionComputationEvent, new TickOptionComputationClass
        {
            TickerId = tickerId,
            Field = field,
            ImpliedVolatility = impliedVolatility,
            Delta = delta,
            OptPrice = optPrice,
            PvDividend = pvDividend,
            Gamma = gamma,
            Vega = vega,
            Theta = theta,
            UndPrice = undPrice
        });

        public event Action<int> TickSnapshotEndEvent;
        public void tickSnapshotEnd(int tickerId) => Post(TickSnapshotEndEvent, tickerId);
        public event Action ConnectionClosedEvent;
        public void connectionClosed() => Post(ConnectionClosedEvent);
        public event Action<AccountSummaryClass> AccountSummaryEvent;
        public void accountSummary(int reqId, string account, string tag, string value, string currency) => Post(AccountSummaryEvent, new AccountSummaryClass
        {
            ReqId = reqId,
            Account = account,
            Tag = tag,
            Value = value,
            Currency = currency
        });

        public event Action<int> AccountSummaryEndEvent;
        public void accountSummaryEnd(int reqId) => Post(AccountSummaryEndEvent, reqId);
        public event Action<BondContractDetailsClass> BondContractDetailsEvent;
        public void bondContractDetails(int reqId, ContractDetails contract) => Post(BondContractDetailsEvent, new BondContractDetailsClass
        {
            ReqId = reqId,
            Contract = contract
        });

        public event Action<UpdateAccountValueClass> UpdateAccountValueEvent;
        public void updateAccountValue(string key, string value, string currency, string accountName) => Post(UpdateAccountValueEvent, new UpdateAccountValueClass
        {
            Key = key,
            Value = value,
            Currency = currency,
            AccountName = accountName
        });

        public event Action<UpdatePortfolioClass> UpdatePortfolioEvent;
        public void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName) => Post(UpdatePortfolioEvent, new UpdatePortfolioClass
        {
            Contract = contract,
            Position = position,
            MarketPrice = marketPrice,
            MarketValue = marketValue,
            AverageCost = averageCost,
            UnrealizedPNL = unrealizedPNL,
            RealizedPNL = realizedPNL,
            AccountName = accountName
        });

        public event Action<string> UpdateAccountTimeEvent;
        public void updateAccountTime(string timestamp) => Post(UpdateAccountTimeEvent, timestamp);
        public event Action<string> AccountDownloadEndEvent;
        public void accountDownloadEnd(string account) => Post(AccountDownloadEndEvent, account);
        public event Action<OrderStatusClass> OrderStatusEvent;
        public void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice) => Post(OrderStatusEvent, new OrderStatusClass
        {
            OrderId = orderId,
            Status = status,
            Filled = filled,
            Remaining = remaining,
            AvgFillPrice = avgFillPrice,
            PermId = permId,
            ParentId = parentId,
            LastFillPrice = lastFillPrice,
            ClientId = clientId,
            WhyHeld = whyHeld,
            MktCapPrice = mktCapPrice
        });

        public event Action<OpenOrderClass> OpenOrderEvent;
        public void openOrder(int orderId, Contract contract, Order order, OrderState orderState) => Post(OpenOrderEvent, new OpenOrderClass
        {
            OrderId = orderId,
            Contract = contract,
            Order = order,
            OrderState = orderState
        });

        public event Action OpenOrderEndEvent;
        public void openOrderEnd() => Post(OpenOrderEndEvent);
        public event Action<ContractDetailsClass> ContractDetailsEvent;
        public void contractDetails(int reqId, ContractDetails contractDetails) => Post(ContractDetailsEvent, new ContractDetailsClass
        {
            ReqId = reqId,
            ContractDetails = contractDetails
        });

        public event Action<int> ContractDetailsEndEvent;
        public void contractDetailsEnd(int reqId) => Post(ContractDetailsEndEvent, reqId);
        public event Action<ExecDetailsClass> ExecDetailsEvent;
        public void execDetails(int reqId, Contract contract, Execution execution) => Post(ExecDetailsEvent, new ExecDetailsClass
        {
            ReqId = reqId,
            Contract = contract,
            Execution = execution
        });

        public event Action<int> ExecDetailsEndEvent;
        public void execDetailsEnd(int reqId) => Post(ExecDetailsEndEvent, reqId);
        public event Action<CommissionReport> CommissionReportEvent;
        public void commissionReport(CommissionReport commissionReport) => Post(CommissionReportEvent, commissionReport);
        public event Action<FundamentalDataClass> FundamentalDataEvent;
        public void fundamentalData(int reqId, string data) => Post(FundamentalDataEvent, new FundamentalDataClass
        {
            ReqId = reqId,
            Data = data
        });

        public event Action<HistoricalDataClass> HistoricalDataEvent;
        public void historicalData(int reqId, Bar bar) => Post(HistoricalDataEvent, new HistoricalDataClass
        {
            ReqId = reqId,
            Bar = bar
        });

        public event Action<HistoricalDataUpdateClass> HistoricalDataUpdateEvent;
        public void historicalDataUpdate(int reqId, Bar bar) => Post(HistoricalDataUpdateEvent, new HistoricalDataUpdateClass
        {
            ReqId = reqId,
            Bar = bar
        });

        public event Action<HistoricalDataEndClass> HistoricalDataEndEvent;
        public void historicalDataEnd(int reqId, string start, string end) => Post(HistoricalDataEndEvent, new HistoricalDataEndClass
        {
            ReqId = reqId,
            Start = start,
            End = end
        });

        public event Action<MarketDataTypeClass> MarketDataTypeEvent;
        public void marketDataType(int reqId, int marketDataType) => Post(MarketDataTypeEvent, new MarketDataTypeClass
        {
            ReqId = reqId,
            MarketDataType = marketDataType
        });

        public event Action<UpdateMktDepthClass> UpdateMktDepthEvent;
        public void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size) => Post(UpdateMktDepthEvent, new UpdateMktDepthClass
        {
            TickerId = tickerId,
            Position = position,
            Operation = operation,
            Side = side,
            Price = price,
            Size = size
        });

        public event Action<UpdateMktDepthL2Class> UpdateMktDepthL2Event;
        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth) => Post(UpdateMktDepthL2Event, new UpdateMktDepthL2Class
        {
            TickerId = tickerId,
            Position = position,
            MarketMaker = marketMaker,
            Operation = operation,
            Side = side,
            Price = price,
            Size = size,
            IsSmartDepth = isSmartDepth
        });

        public event Action<UpdateNewsBulletinClass> UpdateNewsBulletinEvent;
        public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) => Post(UpdateNewsBulletinEvent, new UpdateNewsBulletinClass
        {
            MsgId = msgId,
            MsgType = msgType,
            Message = message,
            OrigExchange = origExchange
        });

        public event Action<PositionClass> PositionEvent;
        public void position(string account, Contract contract, double pos, double avgCost) => Post(PositionEvent, new PositionClass
        {
            Account = account,
            Contract = contract,
            Pos = pos,
            AvgCost = avgCost
        });

        public event Action PositionEndEvent;
        public void positionEnd() => Post(PositionEndEvent);
        public event Action<RealtimeBarClass> RealtimeBarEvent;
        public void realtimeBar(int reqId, long date, double open, double high, double low, double close, long volume, double WAP, int count) => Post(RealtimeBarEvent, new RealtimeBarClass
        {
            ReqId = reqId,
            Date = date,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            WAP = WAP,
            Count = count
        });

        public event Action<string> ScannerParametersEvent;
        public void scannerParameters(string xml) => Post(ScannerParametersEvent, xml);
        public event Action<ScannerDataClass> ScannerDataEvent;
        public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) => Post(ScannerDataEvent, new ScannerDataClass
        {
            ReqId = reqId,
            Rank = rank,
            ContractDetails = contractDetails,
            Distance = distance,
            Benchmark = benchmark,
            Projection = projection,
            LegsStr = legsStr
        });

        public event Action<int> ScannerDataEndEvent;
        public void scannerDataEnd(int reqId) => Post(ScannerDataEndEvent, reqId);
        public event Action<ReceiveFAClass> ReceiveFAEvent;
        public void receiveFA(int faDataType, string faXmlData) => Post(ReceiveFAEvent, new ReceiveFAClass
        {
            FaDataType = faDataType,
            FaXmlData = faXmlData
        });

        public event Action<string> VerifyMessageAPIEvent;
        public void verifyMessageAPI(string apiData) => Post(VerifyMessageAPIEvent, apiData);
        public event Action<VerifyCompletedClass> VerifyCompletedEvent;
        public void verifyCompleted(bool isSuccessful, string errorText) => Post(VerifyCompletedEvent, new VerifyCompletedClass
        {
            IsSuccessful = isSuccessful,
            ErrorText = errorText
        });

        public event Action<VerifyAndAuthMessageAPIClass> VerifyAndAuthMessageAPIEvent;
        public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) => Post(VerifyAndAuthMessageAPIEvent, new VerifyAndAuthMessageAPIClass
        {
            ApiData = apiData,
            XyzChallenge = xyzChallenge
        });

        public event Action<VerifyAndAuthCompletedClass> VerifyAndAuthCompletedEvent;
        public void verifyAndAuthCompleted(bool isSuccessful, string errorText) => Post(VerifyAndAuthCompletedEvent, new VerifyAndAuthCompletedClass
        {
            IsSuccessful = isSuccessful,
            ErrorText = errorText
        });

        public event Action<DisplayGroupListClass> DisplayGroupListEvent;
        public void displayGroupList(int reqId, string groups) => Post(DisplayGroupListEvent, new DisplayGroupListClass
        {
            ReqId = reqId,
            Groups = groups
        });

        public event Action<DisplayGroupUpdatedClass> DisplayGroupUpdatedEvent;
        public void displayGroupUpdated(int reqId, string contractInfo) => Post(DisplayGroupUpdatedEvent, new DisplayGroupUpdatedClass
        {
            ReqId = reqId,
            ContractInfo = contractInfo
        });

        public event Action ConnectAckEvent;
        public void connectAck() => Post(ConnectAckEvent);
        public event Action<PositionMultiClass> PositionMultiEvent;
        public void positionMulti(int requestId, string account, string modelCode, Contract contract, double pos, double avgCost) => Post(PositionMultiEvent, new PositionMultiClass
        {
            RequestId = requestId,
            Account = account,
            ModelCode = modelCode,
            Contract = contract,
            Pos = pos,
            AvgCost = avgCost
        });

        public event Action<int> PositionMultiEndEvent;
        public void positionMultiEnd(int requestId) => Post(PositionMultiEndEvent, requestId);
        public event Action<AccountUpdateMultiClass> AccountUpdateMultiEvent;
        public void accountUpdateMulti(int requestId, string account, string modelCode, string key, string value, string currency) => Post(AccountUpdateMultiEvent, new AccountUpdateMultiClass
        {
            RequestId = requestId,
            Account = account,
            ModelCode = modelCode,
            Key = key,
            Value = value,
            Currency = currency
        });

        public event Action<int> AccountUpdateMultiEndEvent;
        public void accountUpdateMultiEnd(int requestId) => Post(AccountUpdateMultiEndEvent, requestId);
        public event Action<SecurityDefinitionOptionParameterClass> SecurityDefinitionOptionParameterEvent;
        public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) => Post(SecurityDefinitionOptionParameterEvent, new SecurityDefinitionOptionParameterClass
        {
            ReqId = reqId,
            Exchange = exchange,
            UnderlyingConId = underlyingConId,
            TradingClass = tradingClass,
            Multiplier = multiplier,
            Expirations = expirations,
            Strikes = strikes
        });

        public event Action<int> SecurityDefinitionOptionParameterEndEvent;
        public void securityDefinitionOptionParameterEnd(int reqId) => Post(SecurityDefinitionOptionParameterEndEvent, reqId);
        public event Action<SoftDollarTiersClass> SoftDollarTiersEvent;
        public void softDollarTiers(int reqId, SoftDollarTier[] tiers) => Post(SoftDollarTiersEvent, new SoftDollarTiersClass
        {
            ReqId = reqId,
            Tiers = tiers
        });

        public event Action<FamilyCode[]> FamilyCodesEvent;
        public void familyCodes(FamilyCode[] familyCodes) => Post(FamilyCodesEvent, familyCodes);
        public event Action<SymbolSamplesClass> SymbolSamplesEvent;
        public void symbolSamples(int reqId, ContractDescription[] contractDescriptions) => Post(SymbolSamplesEvent, new SymbolSamplesClass
        {
            ReqId = reqId,
            ContractDescriptions = contractDescriptions
        });

        public event Action<DepthMktDataDescription[]> MktDepthExchangesEvent;
        public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) => Post(MktDepthExchangesEvent, depthMktDataDescriptions);
        public event Action<TickNewsClass> TickNewsEvent;
        public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) => Post(TickNewsEvent, new TickNewsClass
        {
            TickerId = tickerId,
            TimeStamp = timeStamp,
            ProviderCode = providerCode,
            ArticleId = articleId,
            Headline = headline,
            ExtraData = extraData
        });

        public event Action<SmartComponentsClass> SmartComponentsEvent;
        public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) => Post(SmartComponentsEvent, new SmartComponentsClass
        {
            ReqId = reqId,
            TheMap = theMap
        });

        public event Action<TickReqParamsClass> TickReqParamsEvent;
        public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) => Post(TickReqParamsEvent, new TickReqParamsClass
        {
            TickerId = tickerId,
            MinTick = minTick,
            BboExchange = bboExchange,
            SnapshotPermissions = snapshotPermissions
        });

        public event Action<NewsProvider[]> NewsProvidersEvent;
        public void newsProviders(NewsProvider[] newsProviders) => Post(NewsProvidersEvent, newsProviders);
        public event Action<NewsArticleClass> NewsArticleEvent;
        public void newsArticle(int requestId, int articleType, string articleText) => Post(NewsArticleEvent, new NewsArticleClass
        {
            RequestId = requestId,
            ArticleType = articleType,
            ArticleText = articleText
        });

        public event Action<HistoricalNewsClass> HistoricalNewsEvent;
        public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) => Post(HistoricalNewsEvent, new HistoricalNewsClass
        {
            RequestId = requestId,
            Time = time,
            ProviderCode = providerCode,
            ArticleId = articleId,
            Headline = headline
        });

        public event Action<HistoricalNewsEndClass> HistoricalNewsEndEvent;
        public void historicalNewsEnd(int requestId, bool hasMore) => Post(HistoricalNewsEndEvent, new HistoricalNewsEndClass
        {
            RequestId = requestId,
            HasMore = hasMore
        });

        public event Action<HeadTimestampClass> HeadTimestampEvent;
        public void headTimestamp(int reqId, string headTimestamp) => Post(HeadTimestampEvent, new HeadTimestampClass
        {
            ReqId = reqId,
            HeadTimestamp = headTimestamp
        });

        public event Action<HistogramDataClass> HistogramDataEvent;
        public void histogramData(int reqId, HistogramEntry[] data) => Post(HistogramDataEvent, new HistogramDataClass
        {
            ReqId = reqId,
            Data = data
        });

        public event Action<RerouteMktDataReqClass> RerouteMktDataReqEvent;
        public void rerouteMktDataReq(int reqId, int conId, string exchange) => Post(RerouteMktDataReqEvent, new RerouteMktDataReqClass
        {
            ReqId = reqId,
            ConId = conId,
            Exchange = exchange
        });

        public event Action<RerouteMktDepthReqClass> RerouteMktDepthReqEvent;
        public void rerouteMktDepthReq(int reqId, int conId, string exchange) => Post(RerouteMktDepthReqEvent, new RerouteMktDepthReqClass
        {
            ReqId = reqId,
            ConId = conId,
            Exchange = exchange
        });

        public event Action<MarketRuleClass> MarketRuleEvent;
        public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) => Post(MarketRuleEvent, new MarketRuleClass
        {
            MarketRuleId = marketRuleId,
            PriceIncrements = priceIncrements
        });

        public event Action<PnlClass> PnlEvent;
        public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) => Post(PnlEvent, new PnlClass
        {
            ReqId = reqId,
            DailyPnL = dailyPnL,
            UnrealizedPnL = unrealizedPnL,
            RealizedPnL = realizedPnL
        });

        public event Action<PnlSingleClass> PnlSingleEvent;
        public void pnlSingle(int reqId, int pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) => Post(PnlSingleEvent, new PnlSingleClass
        {
            ReqId = reqId,
            Pos = pos,
            DailyPnL = dailyPnL,
            UnrealizedPnL = unrealizedPnL,
            RealizedPnL = realizedPnL,
            Value = value
        });

        public event Action<HistoricalTicksClass> HistoricalTicksEvent;
        public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) => Post(HistoricalTicksEvent, new HistoricalTicksClass
        {
            ReqId = reqId,
            Ticks = ticks,
            Done = done
        });

        public event Action<HistoricalTicksBidAskClass> HistoricalTicksBidAskEvent;
        public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) => Post(HistoricalTicksBidAskEvent, new HistoricalTicksBidAskClass
        {
            ReqId = reqId,
            Ticks = ticks,
            Done = done
        });

        public event Action<HistoricalTicksLastClass> HistoricalTicksLastEvent;
        public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) => Post(HistoricalTicksLastEvent, new HistoricalTicksLastClass
        {
            ReqId = reqId,
            Ticks = ticks,
            Done = done
        });

        public event Action<TickByTickAllLastClass> TickByTickAllLastEvent;
        public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttribLast tickAttriblast, string exchange, string specialConditions) => Post(TickByTickAllLastEvent, new TickByTickAllLastClass
        {
            ReqId = reqId,
            TickType = tickType,
            Time = time,
            Price = price,
            Size = size,
            TickAttriblast = tickAttriblast,
            Exchange = exchange,
            SpecialConditions = specialConditions
        });

        public event Action<TickByTickBidAskClass> TickByTickBidAskEvent;
        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk) => Post(TickByTickBidAskEvent, new TickByTickBidAskClass
        {
            ReqId = reqId,
            Time = time,
            BidPrice = bidPrice,
            AskPrice = askPrice,
            BidSize = bidSize,
            AskSize = askSize,
            TickAttribBidAsk = tickAttribBidAsk
        });

        public event Action<TickByTickMidPointClass> TickByTickMidPointEvent;
        public void tickByTickMidPoint(int reqId, long time, double midPoint) => Post(TickByTickMidPointEvent, new TickByTickMidPointClass
        {
            ReqId = reqId,
            Time = time,
            MidPoint = midPoint
        });

        public event Action<OrderBoundClass> OrderBoundEvent;
        public void orderBound(long orderId, int apiClientId, int apiOrderId) => Post(OrderBoundEvent, new OrderBoundClass
        {
            OrderId = orderId,
            ApiClientId = apiClientId,
            ApiOrderId = apiOrderId
        });

        public event Action<CompletedOrderClass> CompletedOrderEvent;
        public void completedOrder(Contract contract, Order order, OrderState orderState) => Post(CompletedOrderEvent, new CompletedOrderClass
        {
            Contract = contract,
            Order = order,
            OrderState = orderState
        });

        public event Action CompletedOrdersEndEvent;
        public void completedOrdersEnd() => Post(CompletedOrdersEndEvent);
    }
}
