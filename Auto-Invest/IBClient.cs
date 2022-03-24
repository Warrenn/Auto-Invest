using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft;
using IBApi;
using Newtonsoft.Json;

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
            var messageString = JsonConvert.SerializeObject(message);
            Console.WriteLine($"callerName: {callerName}");
            Console.WriteLine($"message: {messageString}");
            Console.WriteLine();
            Console.WriteLine();
            if (action == null) return;
            if (_sc != null) _sc.Post((t) => action(message), null);
            else action(message);
        }

        private void Post<T1, T2>(Action<T1, T2> action, T1 id, T2 message, [CallerMemberName] string callerName = "")
        {
            var messageString = JsonConvert.SerializeObject(message);
            var idString = JsonConvert.SerializeObject(id);
            Console.WriteLine($"callerName: {callerName}");
            Console.WriteLine($"message: {messageString}");
            Console.WriteLine($"id: {idString}");
            Console.WriteLine();
            Console.WriteLine();
            if (action == null) return;
            if (_sc != null) _sc.Post((t) => action(id, message), null);
            else action(id, message);
        }

        private void Post(Action action, [CallerMemberName] string callerName = "")
        {
            Console.WriteLine(callerName);
            if (action == null) return;
            if (_sc != null) _sc.Post((t) => action(), null);
            else action();
        }

        private void PostError(int id, int errorCode, string errorMsg, Exception ex, [CallerMemberName] string callerName = "")
        {
            Console.WriteLine($"{id} Exception {ex}; error {errorMsg}");
            if (Error == null) return;
            if (_sc != null) _sc.Post((t) => Error(id, errorCode, errorMsg, ex), null);
            else Error(id, errorCode, errorMsg, ex);
        }

        public event Action<int, int, string, Exception> Error;

        public void error(Exception e) => PostError(0, 0, null, e);
        public void error(string str) => PostError(0, 0, str, null);
        public void error(int id, int errorCode, string errorMsg) => PostError(id, errorCode, errorMsg, null);

        public event Action<long> CurrentTime;
        public void currentTime(long time) => Post(CurrentTime, time);

        public event Action<(int tickerId, int field, double price, TickAttrib attribs)> TickPrice;
        public void tickPrice(int tickerId, int field, double price, TickAttrib attribs) => Post(TickPrice, (tickerId, field, price, attribs));

        public event Action<(int tickerId, int field, int size)> TickSize;
        public void tickSize(int tickerId, int field, int size) => Post(TickSize, (tickerId, field, size));

        public event Action<(int tickerId, int field, string value)> TickString;
        public void tickString(int tickerId, int field, string value) => Post(TickString, (tickerId, field, value));

        public event Action<(int tickerId, int field, double value)> TickGeneric;
        public void tickGeneric(int tickerId, int field, double value) => Post(TickGeneric, (tickerId, field, value));

        public event Action<(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate)> TickEFP;
        public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) => Post(TickEFP, (tickerId, tickType, basisPoints, formattedBasisPoints, impliedFuture, holdDays, futureLastTradeDate, dividendImpact, dividendsToLastTradeDate));

        public event Action<int, DeltaNeutralContract> DeltaNeutralValidation;
        public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) => Post(DeltaNeutralValidation, reqId, deltaNeutralContract);

        public event Action<(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)> TickOptionComputation;
        public void tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) => Post(TickOptionComputation, (tickerId, field, impliedVolatility, delta, optPrice, pvDividend, gamma, vega, theta, undPrice));

        public event Action<int> TickSnapshotEnd;
        public void tickSnapshotEnd(int tickerId) => Post(TickSnapshotEnd, tickerId);

        public event Action<int> NextValidId;

        public void nextValidId(int orderId)
        {
            Post(NextValidId, orderId);
            _currentOrderId = orderId;
        }

        public event Action<string[]> ManagedAccounts;
        public void managedAccounts(string accountsList) => Post(ManagedAccounts, accountsList?.Split(','));

        public event Action ConnectionClosed;
        public void connectionClosed() => Post(ConnectionClosed);

        public event Action<(int reqId, string account, string tag, string value, string currency)> AccountSummary;
        public void accountSummary(int reqId, string account, string tag, string value, string currency) => Post(AccountSummary, (reqId, account, tag, value, currency));

        public event Action<int> AccountSummaryEnd;
        public void accountSummaryEnd(int reqId) => Post(AccountSummaryEnd, reqId);

        public event Action<int, ContractDetails> BondContractDetails;
        public void bondContractDetails(int reqId, ContractDetails contract) => Post(BondContractDetails, reqId, contract);

        public event Action<(string key, string value, string currency, string accountName)> UpdateAccountValue;
        public void updateAccountValue(string key, string value, string currency, string accountName) => Post(UpdateAccountValue, (key, value, currency, accountName));

        public event Action<(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName)> UpdatePortfolio;
        public void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName) => Post(UpdatePortfolio, (contract, position, marketPrice, marketValue, averageCost, unrealizedPNL, realizedPNL, accountName));

        public event Action<string> UpdateAccountTime;
        public void updateAccountTime(string timestamp) => Post(UpdateAccountTime, timestamp);

        public event Action<string> AccountDownloadEnd;
        public void accountDownloadEnd(string account) => Post(AccountDownloadEnd, account);

        public event Action<(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)> OrderStatus;
        public void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice) => Post(OrderStatus, (orderId, status, filled, remaining, avgFillPrice, permId, parentId, lastFillPrice, clientId, whyHeld, mktCapPrice));

        public event Action<(int orderId, Contract contract, Order order, OrderState orderState)> OpenOrder;
        public void openOrder(int orderId, Contract contract, Order order, OrderState orderState) => Post(OpenOrder, (orderId, contract, order, orderState));

        public event Action OpenOrderEnd;
        public void openOrderEnd() => Post(OpenOrderEnd);

        public event Action<int, ContractDetails> ContractDetails;
        public void contractDetails(int reqId, ContractDetails contractDetails) => Post(ContractDetails, reqId, contractDetails);

        public event Action<int> ContractDetailsEnd;
        public void contractDetailsEnd(int reqId) => Post(ContractDetailsEnd, reqId);

        public event Action<(int reqId, Contract contract, Execution execution)> ExecDetails;
        public void execDetails(int reqId, Contract contract, Execution execution) => Post(ExecDetails, (reqId, contract, execution));

        public event Action<int> ExecDetailsEnd;
        public void execDetailsEnd(int reqId) => Post(ExecDetailsEnd, reqId);

        public event Action<CommissionReport> CommissionReport;
        public void commissionReport(CommissionReport commissionReport) => Post(CommissionReport, commissionReport);

        public event Action<int, string> FundamentalData;
        public void fundamentalData(int reqId, string data) => Post(FundamentalData, reqId, data);

        public event Action<int, Bar> HistoricalData;
        public void historicalData(int reqId, Bar bar) => Post(HistoricalData, reqId, bar);

        public event Action<int, Bar> HistoricalDataUpdate;
        public void historicalDataUpdate(int reqId, Bar bar) => Post(HistoricalDataUpdate, reqId, bar);

        public event Action<(int reqId, string start, string end)> HistoricalDataEnd;
        public void historicalDataEnd(int reqId, string start, string end) => Post(HistoricalDataEnd, (reqId, start, end));

        public event Action<int, int> MarketDataType;
        public void marketDataType(int reqId, int marketDataType) => Post(MarketDataType, reqId, marketDataType);

        public event Action<(int tickerId, int position, int operation, int side, double price, int size)> UpdateMktDepth;
        public void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size) => Post(UpdateMktDepth, (tickerId, position, operation, side, price, size));

        public event Action<(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth)> UpdateMktDepthL2;
        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth) => Post(UpdateMktDepthL2, (tickerId, position, marketMaker, operation, side, price, size, isSmartDepth));

        public event Action<(int msgId, int msgType, string message, string origExchange)> UpdateNewsBulletin;
        public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) => Post(UpdateNewsBulletin, (msgId, msgType, message, origExchange));

        public event Action<(string account, Contract contract, double pos, double avgCost)> Position;
        public void position(string account, Contract contract, double pos, double avgCost) => Post(Position, (account, contract, pos, avgCost));

        public event Action PositionEnd;
        public void positionEnd() => Post(PositionEnd);

        public event Action<(int reqId, long date, double open, double high, double low, double close, long volume, double WAP, int count)> RealtimeBar;
        public void realtimeBar(int reqId, long date, double open, double high, double low, double close, long volume, double WAP, int count) => Post(RealtimeBar, (reqId, date, open, high, low, close, volume, WAP, count));

        public event Action<string> ScannerParameters;
        public void scannerParameters(string xml) => Post(ScannerParameters, xml);

        public event Action<(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)> ScannerData;
        public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) => Post(ScannerData, (reqId, rank, contractDetails, distance, benchmark, projection, legsStr));

        public event Action<int> ScannerDataEnd;
        public void scannerDataEnd(int reqId) => Post(ScannerDataEnd, reqId);

        public event Action<int, string> ReceiveFA;
        public void receiveFA(int faDataType, string faXmlData) => Post(ReceiveFA, faDataType, faXmlData);

        public event Action<string> VerifyMessageAPI;
        public void verifyMessageAPI(string apiData) => Post(VerifyMessageAPI, apiData);

        public event Action<bool, string> VerifyCompleted;
        public void verifyCompleted(bool isSuccessful, string errorText) => Post(VerifyCompleted, isSuccessful, errorText);

        public event Action<string, string> VerifyAndAuthMessageAPI;
        public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) => Post(VerifyAndAuthMessageAPI, apiData, xyzChallenge);

        public event Action<bool, string> VerifyAndAuthCompleted;
        public void verifyAndAuthCompleted(bool isSuccessful, string errorText) => Post(VerifyAndAuthCompleted, isSuccessful, errorText);

        public event Action<int, string> DisplayGroupList;
        public void displayGroupList(int reqId, string groups) => Post(DisplayGroupList, reqId, groups);

        public event Action<int, string> DisplayGroupUpdated;
        public void displayGroupUpdated(int reqId, string contractInfo) => Post(DisplayGroupUpdated, reqId, contractInfo);

        public event Action ConnectAck;
        public void connectAck() => Post(ConnectAck);

        public event Action<(int requestId, string account, string modelCode, Contract contract, double pos, double avgCost)> PositionMulti;
        public void positionMulti(int requestId, string account, string modelCode, Contract contract, double pos, double avgCost) => Post(PositionMulti, (requestId, account, modelCode, contract, pos, avgCost));

        public event Action<int> PositionMultiEnd;
        public void positionMultiEnd(int requestId) => Post(PositionMultiEnd, requestId);

        public event Action<(int requestId, string account, string modelCode, string key, string value, string currency)> AccountUpdateMulti;
        public void accountUpdateMulti(int requestId, string account, string modelCode, string key, string value, string currency) => Post(AccountUpdateMulti, (requestId, account, modelCode, key, value, currency));

        public event Action<int> AccountUpdateMultiEnd;
        public void accountUpdateMultiEnd(int requestId) => Post(AccountUpdateMultiEnd, requestId);

        public event Action<int> SecurityDefinitionOptionParameterEnd;
        public void securityDefinitionOptionParameterEnd(int reqId) => Post(SecurityDefinitionOptionParameterEnd, reqId);

        public event Action<int, SoftDollarTier[]> SoftDollarTiers;
        public void softDollarTiers(int reqId, SoftDollarTier[] tiers) => Post(SoftDollarTiers, reqId, tiers);

        public event Action<FamilyCode[]> FamilyCodes;
        public void familyCodes(FamilyCode[] familyCodes) => Post(FamilyCodes, familyCodes);

        public event Action<int, ContractDescription[]> SymbolSamples;
        public void symbolSamples(int reqId, ContractDescription[] contractDescriptions) => Post(SymbolSamples, reqId, contractDescriptions);

        public event Action<DepthMktDataDescription[]> MktDepthExchanges;
        public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) => Post(MktDepthExchanges, depthMktDataDescriptions);

        public event Action<(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData)> TickNews;
        public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) => Post(TickNews, (tickerId, timeStamp, providerCode, articleId, headline, extraData));

        public event Action<(int tickerId, double minTick, string bboExchange, int snapshotPermissions)> TickReqParams;
        public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) => Post(TickReqParams, (tickerId, minTick, bboExchange, snapshotPermissions));

        public event Action<NewsProvider[]> NewsProviders;
        public void newsProviders(NewsProvider[] newsProviders) => Post(NewsProviders, newsProviders);

        public event Action<(int requestId, int articleType, string articleText)> NewsArticle;
        public void newsArticle(int requestId, int articleType, string articleText) => Post(NewsArticle, (requestId, articleType, articleText));

        public event Action<(int requestId, string time, string providerCode, string articleId, string headline)> HistoricalNews;
        public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) => Post(HistoricalNews, (requestId, time, providerCode, articleId, headline));

        public event Action<int, bool> HistoricalNewsEnd;
        public void historicalNewsEnd(int requestId, bool hasMore) => Post(HistoricalNewsEnd, requestId, hasMore);

        public event Action<int, string> HeadTimestamp;
        public void headTimestamp(int reqId, string headTimestamp) => Post(HeadTimestamp, reqId, headTimestamp);

        public event Action<int, HistogramEntry[]> HistogramData;
        public void histogramData(int reqId, HistogramEntry[] data) => Post(HistogramData, reqId, data);

        public event Action<(int reqId, int conId, string exchange)> RerouteMktDataReq;
        public void rerouteMktDataReq(int reqId, int conId, string exchange) => Post(RerouteMktDataReq, (reqId, conId, exchange));

        public event Action<(int reqId, int conId, string exchange)> RerouteMktDepthReq;
        public void rerouteMktDepthReq(int reqId, int conId, string exchange) => Post(RerouteMktDepthReq, (reqId, conId, exchange));

        public event Action<int, PriceIncrement[]> MarketRule;
        public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) => Post(MarketRule, marketRuleId, priceIncrements);

        public event Action<(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL)> Pnl;
        public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) => Post(Pnl, (reqId, dailyPnL, unrealizedPnL, realizedPnL));

        public event Action<(int reqId, int pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value)> PnlSingle;
        public void pnlSingle(int reqId, int pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) => Post(PnlSingle, (reqId, pos, dailyPnL, unrealizedPnL, realizedPnL, value));

        public event Action<(int reqId, HistoricalTick[] ticks, bool done)> HistoricalTicks;
        public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) => Post(HistoricalTicks, (reqId, ticks, done));

        public event Action<(int reqId, HistoricalTickBidAsk[] ticks, bool done)> HistoricalTicksBidAsk;
        public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) => Post(HistoricalTicksBidAsk, (reqId, ticks, done));

        public event Action<(int reqId, HistoricalTickLast[] ticks, bool done)> HistoricalTicksLast;
        public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) => Post(HistoricalTicksLast, (reqId, ticks, done));

        public event Action<(int reqId, int tickType, long time, double price, int size, TickAttribLast tickAttriblast, string exchange, string specialConditions)> TickByTickAllLast;
        public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttribLast tickAttriblast, string exchange, string specialConditions) => Post(TickByTickAllLast, (reqId, tickType, time, price, size, tickAttriblast, exchange, specialConditions));

        public event Action<(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk)> TickByTickBidAsk;
        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk) => Post(TickByTickBidAsk, (reqId, time, bidPrice, askPrice, bidSize, askSize, tickAttribBidAsk));

        public event Action<(int reqId, long time, double midPoint)> TickByTickMidPoint;
        public void tickByTickMidPoint(int reqId, long time, double midPoint) => Post(TickByTickMidPoint, (reqId, time, midPoint));

        public event Action<(long orderId, int apiClientId, int apiOrderId)> OrderBound;
        public void orderBound(long orderId, int apiClientId, int apiOrderId) => Post(OrderBound, (orderId, apiClientId, apiOrderId));

        public event Action<(Contract contract, Order order, OrderState orderState)> CompletedOrder;
        public void completedOrder(Contract contract, Order order, OrderState orderState) => Post(CompletedOrder, (contract, order, orderState));

        public event Action CompletedOrdersEnd;
        public void completedOrdersEnd() => Post(CompletedOrdersEnd);

        public event Action<(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)> SecurityDefinitionOptionParameter;

        public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId,
            string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)
            => Post(SecurityDefinitionOptionParameter,
                (reqId, exchange, underlyingConId, tradingClass, multiplier, expirations, strikes));

        public event Action<(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap)> SmartComponents;
        public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) => Post(SmartComponents, (reqId, theMap));
    }
}
