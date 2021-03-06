﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Python.Runtime;
using QuantConnect.Algorithm;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Python;
using QuantConnect.Securities;
using QuantConnect.Securities.Cfd;
using QuantConnect.Securities.Equity;
using QuantConnect.Securities.Forex;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.Option;
using QuantConnect.Statistics;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QuantConnect.Jupyter
{
    /// <summary>
    /// Provides access to data for quantitative analysis
    /// </summary>
    public class QuantBook 
    {
        private dynamic _pandas;
        private QCAlgorithm _algorithm;
        private IDataCacheProvider _dataCacheProvider;
        private PandasConverter _converter;
        
        /// <summary>
        /// <see cref = "QuantBook" /> constructor.
        /// Provides access to data for quantitative analysis
        /// </summary>
        public QuantBook() : base()
        {
            try
            {
                using (Py.GIL())
                {
                    _pandas = Py.Import("pandas");
                }

                _converter = new PandasConverter(_pandas);

                // Create new instance of QCAlgorithm we are going to wrap
                _algorithm = new QCAlgorithm();
                
                // By default, set start date to end data which is yesterday
                SetStartDate(_algorithm.EndDate);
                
                // Initialize History Provider
                var composer = new Composer();
                var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(composer);
                _dataCacheProvider = new ZipDataCacheProvider(algorithmHandlers.DataProvider);

                var mapFileProvider = algorithmHandlers.MapFileProvider;
                _algorithm.HistoryProvider = composer.GetExportedValueByTypeName<IHistoryProvider>(Config.Get("history-provider", "SubscriptionDataReaderHistoryProvider"));
                _algorithm.HistoryProvider.Initialize(null, algorithmHandlers.DataProvider, _dataCacheProvider, mapFileProvider, algorithmHandlers.FactorFileProvider, null);
            }
            catch (Exception exception)
            {
                throw new Exception("QuantBook.Main(): " + exception);
            }
        }

        /// <summary>
        /// Set the start date for the backtest 
        /// </summary>
        /// <param name="start">Datetime Start date for backtest</param>
        /// <remarks>Must be less than end date and within data available</remarks>
        /// <seealso cref="SetStartDate(DateTime)"/>
        public void SetStartDate(DateTime start)
        {
            _algorithm.SetStartDate(start);
        }

        /// <summary>
        /// Set the start date for backtest.
        /// </summary>
        /// <param name="day">Int starting date 1-30</param>
        /// <param name="month">Int month starting date</param>
        /// <param name="year">Int year starting date</param>
        /// <remarks> 
        ///     Wrapper for SetStartDate(DateTime). 
        ///     Must be less than end date. 
        ///     Ignored in live trading mode.
        /// </remarks>
        public void SetStartDate(int year, int month, int day)
        {
            _algorithm.SetStartDate(year, month, day);
        }

        /// <summary>
        /// Creates and adds a new <see cref="Equity"/> security to the algorithm
        /// </summary>
        /// <param name="ticker">The equity ticker symbol</param>
        /// <param name="resolution">The <see cref="Resolution"/> of market data, Tick, Second, Minute, Hour, or Daily. Default is <see cref="Resolution.Minute"/></param>
        /// <param name="market">The equity's market, <seealso cref="Market"/>. Default value is null and looked up using BrokerageModel.DefaultMarkets in <see cref="AddSecurity{T}"/></param>
        /// <param name="fillDataForward">If true, returns the last available data even if none in that timeslice. Default is <value>true</value></param>
        /// <param name="leverage">The requested leverage for this equity. Default is set by <see cref="SecurityInitializer"/></param>
        /// <param name="extendedMarketHours">True to send data during pre and post market sessions. Default is <value>false</value></param>
        /// <returns>The new <see cref="Equity"/> security</returns>
        public Equity AddEquity(string ticker, Resolution resolution = Resolution.Minute, string market = null, bool fillDataForward = true, decimal leverage = 0m, bool extendedMarketHours = false)
        {
            return _algorithm.AddEquity(ticker, resolution, market, fillDataForward, leverage, extendedMarketHours);
        }

        /// <summary>
        /// Creates and adds a new <see cref="Forex"/> security to the algorithm
        /// </summary>
        /// <param name="ticker">The currency pair</param>
        /// <param name="resolution">The <see cref="Resolution"/> of market data, Tick, Second, Minute, Hour, or Daily. Default is <see cref="Resolution.Minute"/></param>
        /// <param name="market">The foreign exchange trading market, <seealso cref="Market"/>. Default value is null and looked up using BrokerageModel.DefaultMarkets in <see cref="AddSecurity{T}"/></param>
        /// <param name="fillDataForward">If true, returns the last available data even if none in that timeslice. Default is <value>true</value></param>
        /// <param name="leverage">The requested leverage for this equity. Default is set by <see cref="SecurityInitializer"/></param>
        /// <returns>The new <see cref="Forex"/> security</returns>
        public Forex AddForex(string ticker, Resolution resolution = Resolution.Minute, string market = null, bool fillDataForward = true, decimal leverage = 0m)
        {
            return _algorithm.AddForex(ticker, resolution, market, fillDataForward, leverage);
        }

        /// <summary>
        /// Creates and adds a new <see cref="Cfd"/> security to the algorithm
        /// </summary>
        /// <param name="ticker">The currency pair</param>
        /// <param name="resolution">The <see cref="Resolution"/> of market data, Tick, Second, Minute, Hour, or Daily. Default is <see cref="Resolution.Minute"/></param>
        /// <param name="market">The cfd trading market, <seealso cref="Market"/>. Default value is null and looked up using BrokerageModel.DefaultMarkets in <see cref="AddSecurity{T}"/></param>
        /// <param name="fillDataForward">If true, returns the last available data even if none in that timeslice. Default is <value>true</value></param>
        /// <param name="leverage">The requested leverage for this equity. Default is set by <see cref="SecurityInitializer"/></param>
        /// <returns>The new <see cref="Cfd"/> security</returns>
        public Cfd AddCfd(string ticker, Resolution resolution = Resolution.Minute, string market = null, bool fillDataForward = true, decimal leverage = 0m)
        {
            return _algorithm.AddCfd(ticker, resolution, market, fillDataForward, leverage);
        }

        /// <summary>
        /// Creates and adds a new <see cref="Future"/> security to the algorithm
        /// </summary>
        /// <param name="symbol">The futures contract symbol</param>
        /// <param name="resolution">The <see cref="Resolution"/> of market data, Tick, Second, Minute, Hour, or Daily. Default is <see cref="Resolution.Minute"/></param>
        /// <param name="market">The futures market, <seealso cref="Market"/>. Default is value null and looked up using BrokerageModel.DefaultMarkets in <see cref="AddSecurity{T}"/></param>
        /// <param name="fillDataForward">If true, returns the last available data even if none in that timeslice. Default is <value>true</value></param>
        /// <param name="leverage">The requested leverage for this equity. Default is set by <see cref="SecurityInitializer"/></param>
        /// <returns>The new <see cref="Future"/> security</returns>
        public Future AddFuture(string symbol, Resolution resolution = Resolution.Minute, string market = null, bool fillDataForward = true, decimal leverage = 0m)
        {
            return _algorithm.AddFuture(symbol, resolution, market, fillDataForward, leverage);
        }

        /// <summary>
            /// Creates and adds a new equity <see cref="Option"/> security to the algorithm
            /// </summary>
            /// <param name="underlying">The underlying equity symbol</param>
            /// <param name="resolution">The <see cref="Resolution"/> of market data, Tick, Second, Minute, Hour, or Daily. Default is <see cref="Resolution.Minute"/></param>
            /// <param name="market">The equity's market, <seealso cref="Market"/>. Default is value null and looked up using BrokerageModel.DefaultMarkets in <see cref="AddSecurity{T}"/></param>
            /// <param name="fillDataForward">If true, returns the last available data even if none in that timeslice. Default is <value>true</value></param>
            /// <param name="leverage">The requested leverage for this equity. Default is set by <see cref="SecurityInitializer"/></param>
            /// <returns>The new <see cref="Option"/> security</returns>
        public Option AddOption(string underlying, Resolution resolution = Resolution.Minute, string market = null, bool fillDataForward = true, decimal leverage = 0m)
        {
            return _algorithm.AddOption(underlying, resolution, market, fillDataForward, leverage);
        }

        /// <summary>
        /// Gets the historical data for the specified symbols between the specified dates. The symbols must exist in the Securities collection.
        /// </summary>
        /// <param name="span">The span over which to retrieve recent historical data</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>A pandas.DataFrame containing the requested historical data</returns>
        public PyObject History(TimeSpan span, Resolution? resolution = null)
        {
            return _converter.GetDataFrame(_algorithm.History(_algorithm.Securities.Keys, _algorithm.Time - span, _algorithm.Time, resolution).Memoize());
        }

        /// <summary>
        /// Get the history for all configured securities over the requested span.
        /// This will use the resolution and other subscription settings for each security.
        /// The symbols must exist in the Securities collection.
        /// </summary>
        /// <param name="periods">The number of bars to request</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>A pandas.DataFrame containing the requested historical data</returns>
        public PyObject History(int periods, Resolution? resolution = null)
        {
            return _converter.GetDataFrame(_algorithm.History(_algorithm.Securities.Keys, periods, resolution).Memoize());
        }

        /// <summary>
        /// Gets the historical data for the specified symbol over the request span. The symbol must exist in the Securities collection.
        /// </summary>
        /// <typeparam name="T">The data type of the symbol</typeparam>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="span">The span over which to retrieve recent historical data</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>An enumerable of slice containing the requested historical data</returns>
        public PyObject History<T>(Symbol symbol, TimeSpan span, Resolution? resolution = null)
            where T : IBaseDataBar
        {
            return _converter.GetDataFrame<T>(_algorithm.History<T>(symbol, _algorithm.Time - span, _algorithm.Time, resolution).Memoize());
        }

        /// <summary>
        /// Gets the historical data for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="periods">The number of bars to request</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>An enumerable of slice containing the requested historical data</returns>
        public PyObject History(Symbol symbol, int periods, Resolution? resolution = null)
        {
            return _converter.GetDataFrame(_algorithm.History(symbol, periods, resolution));
        }

        /// <summary>
        /// Gets the historical data for the specified symbol between the specified dates. The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="start">The start time in the algorithm's time zone</param>
        /// <param name="end">The end time in the algorithm's time zone</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>An enumerable of slice containing the requested historical data</returns>
        public PyObject History(Symbol symbol, DateTime start, DateTime end, Resolution? resolution = null)
        {
            if (symbol.SecurityType == SecurityType.Equity)
            {
                return _converter.GetDataFrame(_algorithm.History<TradeBar>(symbol, start, end, resolution));
            }
            else
            {
                return _converter.GetDataFrame(_algorithm.History<QuoteBar>(symbol, start, end, resolution));
            }
        }
        
        /// <summary>
        /// Get fundamental data from given symbols
        /// </summary>
        /// <param name="pyObject">The symbols to retrieve fundamental data for</param>
        /// <param name="selector">Selects a value from the Fundamental data to filter the request output</param>
        /// <param name="start">The start date of selected data</param>
        /// <param name="end">The end date of selected data</param>
        /// <returns></returns>
        public PyObject GetFundamental(PyObject tickers, string selector, DateTime? start = null, DateTime? end = null)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                return "Invalid selector. Cannot be None, empty or consist only of white-space characters".ToPython();
            }

            using (Py.GIL())
            {
                // If tickers are not a PyList, we create one
                if (!PyList.IsListType(tickers))
                {
                    var tmp = new PyList();
                    tmp.Append(tickers);
                    tickers = tmp;
                }

                var list = new List<Tuple<Symbol, DateTime, object>>();

                foreach (var ticker in tickers)
                {
                    var symbol = Symbol.Create(ticker.ToString(), SecurityType.Equity, Market.USA);
                    var dir = new DirectoryInfo(Path.Combine(Globals.DataFolder, "equity", symbol.ID.Market, "fundamental", "fine", symbol.Value.ToLower()));
                    if (!dir.Exists) continue;

                    var config = new SubscriptionDataConfig(typeof(FineFundamental), symbol, Resolution.Daily, TimeZones.NewYork, TimeZones.NewYork, false, false, false);

                    foreach (var fileName in dir.EnumerateFiles())
                    {
                        var date = DateTime.ParseExact(fileName.Name.Substring(0, 8), DateFormat.EightCharacter, CultureInfo.InvariantCulture);
                        if (date < start || date > end) continue;

                        var factory = new TextSubscriptionDataSourceReader(_dataCacheProvider, config, date, false);
                        var source = new SubscriptionDataSource(fileName.FullName, SubscriptionTransportMedium.LocalFile);
                        var value = factory.Read(source).Select(x => GetPropertyValue(x, selector)).First();

                        list.Add(Tuple.Create(symbol, date, value));
                    }
                }

                var data = new PyDict();
                foreach (var item in list.GroupBy(x => x.Item1))
                {
                    var index = item.Select(x => x.Item2);
                    data.SetItem(item.Key, _pandas.Series(item.Select(x => x.Item3).ToList(), index));
                }

                return _pandas.DataFrame(data);
            }
        }

        /// <summary>
        /// Gets <see cref="OptionHistory"/> object for a given symbol, date and resolution
        /// </summary>
        /// <param name="symbol">The symbol to retrieve historical option data for</param>
        /// <param name="date">Date of the data</param>
        /// <param name="resolution">The resolution to request</param>
        /// <returns>A <see cref="OptionHistory"/> object that contains historical option data.</returns>
        public OptionHistory GetOptionHistory(Symbol symbol, DateTime date, Resolution? resolution = null)
        {
            SetStartDate(date.AddDays(1));
            var option = _algorithm.Securities[symbol] as Option;
            var underlying = AddEquity(symbol.Underlying.Value, option.Resolution);

            var provider = new BacktestingOptionChainProvider();
            var allSymbols = provider.GetOptionContractList(symbol.Underlying, date);
            
            var requests = _algorithm.History(symbol.Underlying, TimeSpan.FromDays(1), resolution)
                .SelectMany(x => option.ContractFilter.Filter(new OptionFilterUniverse(allSymbols, x)))
                .Distinct()
                .Select(x =>
                     new HistoryRequest(date.AddDays(-1), 
                                        date, 
                                        typeof(QuoteBar), 
                                        x, 
                                        resolution ?? option.Resolution, 
                                        underlying.Exchange.Hours,
                                        MarketHoursDatabase.FromDataFolder().GetDataTimeZone(underlying.Symbol.ID.Market, underlying.Symbol, underlying.Type),
                                        Resolution.Minute, 
                                        underlying.IsExtendedMarketHours, 
                                        underlying.IsCustomData(), 
                                        DataNormalizationMode.Raw,
                                        LeanData.GetCommonTickTypeForCommonDataTypes(typeof(QuoteBar), underlying.Type))
                    );

            requests = requests.Union(new[] { new HistoryRequest(underlying.Subscriptions.FirstOrDefault(), underlying.Exchange.Hours, date.AddDays(-1), date) });

            return new OptionHistory(_algorithm.HistoryProvider.GetHistory(requests.OrderByDescending(x => x.Symbol.SecurityType), _algorithm.TimeZone).Memoize());
        }

        /// <summary>
        /// Gets the historical data of an indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="periods">The number of bars to request</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of an indicator</returns>
        public PyObject Indicator(IndicatorBase<IndicatorDataPoint> indicator, Symbol symbol, int period, Resolution? resolution = null, Func<IBaseData, decimal> selector = null)
        {
            var history = _algorithm.History<IBaseDataBar>(symbol, period, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets the historical data of a bar indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="periods">The number of bars to request</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of a bar indicator</returns>
        public PyObject Indicator(IndicatorBase<IBaseDataBar> indicator, Symbol symbol, int period, Resolution? resolution = null, Func<IBaseData, IBaseDataBar> selector = null)
        {
            var history = _algorithm.History<IBaseDataBar>(symbol, period, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets the historical data of a bar indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="periods">The number of bars to request</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of a bar indicator</returns>
        public PyObject Indicator(IndicatorBase<TradeBar> indicator, Symbol symbol, int period, Resolution? resolution = null, Func<IBaseData, TradeBar> selector = null)
        {
            var history = _algorithm.History<TradeBar>(symbol, period, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets the historical data of an indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="indicator">Indicator</param>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="span">The span over which to retrieve recent historical data</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of an indicator</returns>
        public PyObject Indicator(IndicatorBase<IndicatorDataPoint> indicator, Symbol symbol, TimeSpan span, Resolution? resolution = null, Func<IBaseData, decimal> selector = null)
        {
            var history = _algorithm.History<IBaseDataBar>(symbol, span, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets the historical data of a bar indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="indicator">Indicator</param>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="span">The span over which to retrieve recent historical data</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of a bar indicator</returns>
        public PyObject Indicator(IndicatorBase<IBaseDataBar> indicator, Symbol symbol, TimeSpan span, Resolution? resolution = null, Func<IBaseData, IBaseDataBar> selector = null)
        {
            var history = _algorithm.History<IBaseDataBar>(symbol, span, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets the historical data of a bar indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="indicator">Indicator</param>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="span">The span over which to retrieve recent historical data</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of a bar indicator</returns>
        public PyObject Indicator(IndicatorBase<TradeBar> indicator, Symbol symbol, TimeSpan span, Resolution? resolution = null, Func<IBaseData, TradeBar> selector = null)
        {
            var history = _algorithm.History<TradeBar>(symbol, span, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets the historical data of an indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="indicator">Indicator</param>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="start">The start time in the algorithm's time zone</param>
        /// <param name="end">The end time in the algorithm's time zone</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of an indicator</returns>
        public PyObject Indicator(IndicatorBase<IndicatorDataPoint> indicator, Symbol symbol, DateTime start, DateTime end, Resolution? resolution = null, Func<IBaseData, decimal> selector = null)
        {
            var history = _algorithm.History<IBaseDataBar>(symbol, start, end, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets the historical data of a bar indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="indicator">Indicator</param>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="start">The start time in the algorithm's time zone</param>
        /// <param name="end">The end time in the algorithm's time zone</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of a bar indicator</returns>
        public PyObject Indicator(IndicatorBase<IBaseDataBar> indicator, Symbol symbol, DateTime start, DateTime end, Resolution? resolution = null, Func<IBaseData, IBaseDataBar> selector = null)
        {
            var history = _algorithm.History<IBaseDataBar>(symbol, start, end, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets the historical data of a bar indicator for the specified symbol. The exact number of bars will be returned. 
        /// The symbol must exist in the Securities collection.
        /// </summary>
        /// <param name="indicator">Indicator</param>
        /// <param name="symbol">The symbol to retrieve historical data for</param>
        /// <param name="start">The start time in the algorithm's time zone</param>
        /// <param name="end">The end time in the algorithm's time zone</param>
        /// <param name="resolution">The resolution to request</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame of historical data of a bar indicator</returns>
        public PyObject Indicator(IndicatorBase<TradeBar> indicator, Symbol symbol, DateTime start, DateTime end, Resolution? resolution = null, Func<IBaseData, TradeBar> selector = null)
        {
            var history = _algorithm.History<TradeBar>(symbol, start, end, resolution);
            return Indicator(indicator, history, selector);
        }

        /// <summary>
        /// Gets Portfolio Statistics from a pandas.DataFrame with equity and benchmark values
        /// </summary>
        /// <param name="dataFrame">pandas.DataFrame with the information required to compute the Portfolio statistics</param>
        /// <returns><see cref="PortfolioStatistics"/> object wrapped in a <see cref="PyDict"/> with the portfolio statistics.</returns>
        public PyDict GetPortfolioStatistics(PyObject dataFrame)
        {
            var dictBenchmark = new SortedDictionary<DateTime, double>();
            var dictEquity = new SortedDictionary<DateTime, double>();
            var dictPL = new SortedDictionary<DateTime, double>();

            using (Py.GIL())
            {
                var result = new PyDict();

                try
                {
                    // Converts the data from pandas.DataFrame into dictionaries keyed by time
                    var df = ((dynamic)dataFrame).dropna();
                    dictBenchmark = GetDictionaryFromSeries((PyObject)df["benchmark"]);
                    dictEquity = GetDictionaryFromSeries((PyObject)df["equity"]);
                    dictPL = GetDictionaryFromSeries((PyObject)df["equity"].pct_change());
                }
                catch (PythonException e)
                {
                    result.SetItem("Runtime Error", e.Message.ToPython());
                    return result;
                }

                // Convert the double into decimal
                var equity = new SortedDictionary<DateTime, decimal>(dictEquity.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value));
                var profitLoss = new SortedDictionary<DateTime, decimal>(dictPL.ToDictionary(kvp => kvp.Key, kvp => double.IsNaN(kvp.Value) ? 0 : (decimal)kvp.Value));

                // Gets the last value of the day of the benchmark and equity
                var listBenchmark = CalculateDailyRateOfChange(dictBenchmark);
                var listPerformance = CalculateDailyRateOfChange(dictEquity);

                // Gets the startting capital
                var startingCapital = Convert.ToDecimal(dictEquity.FirstOrDefault().Value);

                // Compute portfolio statistics
                var stats = new PortfolioStatistics(profitLoss, equity, listPerformance, listBenchmark, startingCapital);

                result.SetItem("Average Win (%)", Convert.ToDouble(stats.AverageWinRate * 100).ToPython());
                result.SetItem("Average Loss (%)", Convert.ToDouble(stats.AverageLossRate * 100).ToPython());
                result.SetItem("Compounding Annual Return (%)", Convert.ToDouble(stats.CompoundingAnnualReturn * 100m).ToPython());
                result.SetItem("Drawdown (%)", Convert.ToDouble(stats.Drawdown * 100).ToPython());
                result.SetItem("Expectancy", Convert.ToDouble(stats.Expectancy).ToPython());
                result.SetItem("Net Profit (%)", Convert.ToDouble(stats.TotalNetProfit * 100).ToPython());
                result.SetItem("Sharpe Ratio", Convert.ToDouble(stats.SharpeRatio).ToPython());
                result.SetItem("Win Rate (%)", Convert.ToDouble(stats.WinRate * 100).ToPython());
                result.SetItem("Loss Rate (%)", Convert.ToDouble(stats.LossRate * 100).ToPython());
                result.SetItem("Profit-Loss Ratio", Convert.ToDouble(stats.ProfitLossRatio).ToPython());
                result.SetItem("Alpha", Convert.ToDouble(stats.Alpha).ToPython());
                result.SetItem("Beta", Convert.ToDouble(stats.Beta).ToPython());
                result.SetItem("Annual Standard Deviation", Convert.ToDouble(stats.AnnualStandardDeviation).ToPython());
                result.SetItem("Annual Variance", Convert.ToDouble(stats.AnnualVariance).ToPython());
                result.SetItem("Information Ratio", Convert.ToDouble(stats.InformationRatio).ToPython());
                result.SetItem("Tracking Error", Convert.ToDouble(stats.TrackingError).ToPython());
                result.SetItem("Treynor Ratio", Convert.ToDouble(stats.TreynorRatio).ToPython());

                return result;
            }
        }

        /// <summary>
        /// Converts a pandas.Series into a <see cref="SortedDictionary{DateTime, Double}"/>
        /// </summary>
        /// <param name="series">pandas.Series to be converted</param>
        /// <returns><see cref="SortedDictionary{DateTime, Double}"/> with pandas.Series information</returns>
        private SortedDictionary<DateTime, double> GetDictionaryFromSeries(PyObject series)
        {
            var dictionary = new SortedDictionary<DateTime, double>();

            var pyDict = new PyDict(((dynamic)series).to_dict());
            foreach (PyObject item in pyDict.Items())
            {
                var key = (DateTime)item[0].AsManagedObject(typeof(DateTime));
                var value = (double)item[1].AsManagedObject(typeof(double));
                dictionary.Add(key, value);
            }

            return dictionary;
        }

        /// <summary>
        /// Calculates the daily rate of change 
        /// </summary>
        /// <param name="dictionary"><see cref="IDictionary{DateTime, Double}"/> with prices keyed by time</param>
        /// <returns><see cref="List{Double}"/> with daily rate of change</returns>
        private List<double> CalculateDailyRateOfChange(IDictionary<DateTime, double> dictionary)
        {
            var daily = dictionary.GroupBy(kvp => kvp.Key.Date)
                .ToDictionary(x => x.Key, v => v.LastOrDefault().Value)
                .Values.ToArray();

            var rocp = new double[daily.Length];
            for (var i = 1; i < daily.Length; i++)
            {
                rocp[i] = (daily[i] - daily[i - 1]) / daily[i - 1];
            }
            rocp[0] = 0;

            return rocp.ToList();
        }

        /// <summary>
        /// Gets the historical data of an indicator and convert it into pandas.DataFrame
        /// </summary>
        /// <param name="indicator">Indicator</param>
        /// <param name="history">Historical data used to calculate the indicator</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame containing the historical data of <param name="indicator"></returns>
        private PyObject Indicator(IndicatorBase<IndicatorDataPoint> indicator, IEnumerable<IBaseDataBar> history, Func<IBaseData, decimal> selector = null)
        {
            // Reset the indicator
            indicator.Reset();
            
            // Create a dictionary of the properties
            var name = indicator.GetType().Name;

            var properties = indicator.GetType().GetProperties()
                .Where(x => x.PropertyType.IsGenericType)
                .ToDictionary(x => x.Name, y => new List<IndicatorDataPoint>());
            properties.Add(name, new List<IndicatorDataPoint>());

            indicator.Updated += (s, e) =>
            {
                if (!indicator.IsReady)
                {
                    return;
                }

                foreach (var kvp in properties)
                {
                    var dataPoint = kvp.Key == name ? e : GetPropertyValue(s, kvp.Key + ".Current");
                    kvp.Value.Add((IndicatorDataPoint)dataPoint);
                }
            };

            selector = selector ?? (x => x.Value);

            foreach (var bar in history)
            {
                var value = selector(bar);
                indicator.Update(bar.EndTime, value);
            }

            return _converter.GetIndicatorDataFrame(properties);
        }

        /// <summary>
        /// Gets the historical data of an bar indicator and convert it into pandas.DataFrame
        /// </summary>
        /// <param name="indicator">Bar indicator</param>
        /// <param name="history">Historical data used to calculate the indicator</param>
        /// <param name="selector">Selects a value from the BaseData to send into the indicator, if null defaults to the Value property of BaseData (x => x.Value)</param>
        /// <returns>pandas.DataFrame containing the historical data of <param name="indicator"></returns>
        private PyObject Indicator<T>(IndicatorBase<T> indicator, IEnumerable<T> history, Func<IBaseData, T> selector = null)
            where T : IBaseDataBar
        {
            // Reset the indicator
            indicator.Reset();

            // Create a dictionary of the properties
            var name = indicator.GetType().Name;

            var properties = indicator.GetType().GetProperties()
                .Where(x => x.PropertyType.IsGenericType)
                .ToDictionary(x => x.Name, y => new List<IndicatorDataPoint>());
            properties.Add(name, new List<IndicatorDataPoint>());

            indicator.Updated += (s, e) =>
            {
                if (!indicator.IsReady)
                {
                    return;
                }

                foreach (var kvp in properties)
                {
                    var dataPoint = kvp.Key == name ? e : GetPropertyValue(s, kvp.Key + ".Current");
                    kvp.Value.Add((IndicatorDataPoint)dataPoint);
                }
            };
            
            selector = selector ?? (x => (T)x);
            
            foreach (var bar in history)
            {
                indicator.Update(selector(bar));
            }

            return _converter.GetIndicatorDataFrame(properties);
        }
        
        /// <summary>
        /// Gets a value of a property
        /// </summary>
        /// <param name="baseData">Object with the desired property</param>
        /// <param name="fullName">Property name</param>
        /// <returns>Property value</returns>
        private object GetPropertyValue(object baseData, string fullName)
        {
            foreach (var name in fullName.Split('.'))
            {
                if (baseData == null) return null;

                var info = baseData.GetType().GetProperty(name);

                baseData = info == null ? null : info.GetValue(baseData, null);
            }

            return baseData;
        }
    }
}