﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Common.Log;
using Core.Candles;
using Core.Enumerators;
using Lykke.MarketProfileService.Client;
using Lykke.MarketProfileService.Client.Models;
using Lykke.Service.CandlesHistory.Client;
using Lykke.Service.CandlesHistory.Client.Models;
using LykkeApi2.Models.Markets;
using Microsoft.AspNetCore.Mvc;

namespace LykkeApi2.Controllers
{
    [Route("api/markets")]
    public class MarketsController : Controller
    {
        private readonly ILykkeMarketProfileServiceAPI _marketProfileService;
        private readonly ICandlesHistoryServiceProvider _candlesHistoryProvider;
        private readonly ILog _log;

        #region Initialization

        public MarketsController(ILykkeMarketProfileServiceAPI marketProfileService,
            ICandlesHistoryServiceProvider candlesHistoryProvider,
            ILog log)
        {
            _marketProfileService = marketProfileService ?? throw new ArgumentNullException(nameof(marketProfileService));
            _candlesHistoryProvider = candlesHistoryProvider ?? throw new ArgumentNullException(nameof(candlesHistoryProvider));
            _log = log?.CreateComponentScope(nameof(MarketsController)) ?? throw new ArgumentNullException(nameof(log));
        }

        #endregion

        #region PublicApi

        /// <summary>
        /// Get actual market state for all registered asset pairs.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(MarketSlice[]), (int) HttpStatusCode.OK)]
        [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Get()
        {
            try
            {
                var result = await GetSpotMarketSnapshotAsync();

                return Ok(result);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(Get), "AllPairs", e);
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Get actual market state for the given asset pair.
        /// </summary>
        /// <param name="assetPairId">The target asset pair ID.</param>
        [HttpGet("{assetPairId}")]
        [ProducesResponseType(typeof(MarketSlice), (int) HttpStatusCode.OK)]
        [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Get(string assetPairId)
        {
            if (string.IsNullOrWhiteSpace(assetPairId))
                return BadRequest("Please, specify the target asset pair id.");

            try
            {
                var result = await GetSpotMarketSnapshotAsync(assetPairId);

                return Ok(result.Single());
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(Get), assetPairId, e);
                return BadRequest(e.Message);
            }
        }

        #endregion

        #region Private

        /// <summary>
        /// Gathers generalized information about the current state of the Spot market.
        /// </summary>
        /// <param name="assetPairId">The target asset pair ID. If not specified (is null or empty string), there will be gathered the info about all the registered asset pairs.</param>
        private async Task<List<MarketSlice>> GetSpotMarketSnapshotAsync(string assetPairId = null)
        {
            var marketProfiles = await GetMarketProfilesAsync(assetPairId);
            var todayCandles = await GetTodaySpotCandlesAsync(assetPairId); // May have (and usually does) a different count of returned records than from market profile query.

            var result = new Dictionary<string, MarketSlice>();

            foreach (var marketProfile in marketProfiles)
            {
                result[marketProfile.AssetPair] = new MarketSlice
                {
                    AssetPair = marketProfile.AssetPair,
                    Bid = (decimal)marketProfile.BidPrice,
                    Ask = (decimal)marketProfile.AskPrice
                };
            }

            foreach (var todayCandle in todayCandles)
            {
                var candleValue = todayCandle.Value;
                var priceChange24 = 
                    candleValue.Close > 0
                    ? (decimal) ((candleValue.Close - candleValue.Open) / candleValue.Open)
                    : 0;

                if (result.TryGetValue(todayCandle.Key, out var existingAssetRecord))
                {
                    existingAssetRecord.Volume24H = (decimal)candleValue.TradingVolume;
                    existingAssetRecord.PriceChange24H = priceChange24;
                    existingAssetRecord.LastPrice = (decimal)candleValue.Close;
                }
                else
                {
                    result[todayCandle.Key] = new MarketSlice
                    {
                        AssetPair = todayCandle.Key,
                        Volume24H = (decimal) candleValue.TradingVolume,
                        PriceChange24H = priceChange24,
                        LastPrice = (decimal) candleValue.Close
                    };
                }
            }
            
            return result.Values.ToList();
        }

        /// <summary>
        /// Gets (a set of) market profile(s).
        /// </summary>
        /// <param name="assetPairId">The target asset pair ID. If not specified (is null or empty string), there will be gathered the info about all the registered asset pairs.</param>
        private async Task<List<AssetPairModel>> GetMarketProfilesAsync(string assetPairId = null)
        {
            var marketProfiles = new List<AssetPairModel>();

            if (!string.IsNullOrWhiteSpace(assetPairId))
            {
                var marketProfile = await _marketProfileService.TryGetAssetPairAsync(assetPairId);
                if (marketProfile == null)
                    throw new InvalidOperationException($"Asset pair {assetPairId} is is not registered.");

                marketProfiles.Add(await _marketProfileService.GetAssetPairAsync(assetPairId));
            }
            else
                marketProfiles.AddRange(await _marketProfileService.ApiMarketProfileGetAsync());

            return marketProfiles;
        }

        /// <summary>
        /// Gets (a set of) today's Day candle(s) of type Trades and Spot market.
        /// </summary>
        /// <param name="assetPairId">The target asset pair ID. If not specified (is null or empty string), there will be gathered the info about all the registered asset pairs.</param>
        /// <returns>A dictionary where the Key is the asset pair ID and the Value contains the today's Day Spot candle for the asset pair.</returns>
        /// <remarks>When there is no Day Spot Trade candle for some asset pair, it will not be presented in the resulting dictionary. Thus, if assetPairId parameter is specified
        /// but there is no a suitable candle for it, the method will return an empty dictionary.</remarks>
        private async Task<Dictionary<string, Candle>> GetTodaySpotCandlesAsync(string assetPairId = null)
        {
            var historyService = _candlesHistoryProvider.Get(MarketType.Spot);

            var todayCandles = new Dictionary<string, Candle>();

            var dateFromInclusive = DateTime.UtcNow.Date;
            var dateToExclusive = dateFromInclusive.AddDays(1);

            if (!string.IsNullOrWhiteSpace(assetPairId))
            {
                var todayCandleHistory = await historyService.TryGetCandlesHistoryAsync(assetPairId,
                    CandlePriceType.Trades, CandleTimeInterval.Day, dateFromInclusive, dateToExclusive);

                if (todayCandleHistory?.History == null ||
                    !todayCandleHistory.History.Any())
                    return todayCandles;

                if (todayCandleHistory.History.Count > 1) // The unbelievable case.
                    throw new AmbiguousMatchException($"It seems like we have more than one today's Day Spot trade candle for asset pair {assetPairId}.");

                todayCandles.Add(
                    assetPairId,
                    todayCandleHistory
                        .History
                        .Single()
                    );
            }
            else
            {
                var assetPairs = await historyService.GetAvailableAssetPairsAsync();
                var todayCandleHistoryForPairs = await historyService.GetCandlesHistoryBatchAsync(assetPairs,
                    CandlePriceType.Trades, CandleTimeInterval.Day, dateFromInclusive, dateToExclusive);

                if (todayCandleHistoryForPairs == null) // Some technical issue has happened without an exception.
                    throw new InvalidOperationException("Could not obtain today's Day Spot trade candles at all.");

                if (!todayCandleHistoryForPairs.Any())
                    return todayCandles;

                foreach (var historyForPair in todayCandleHistoryForPairs)
                {
                    if (historyForPair.Value?.History == null ||
                        !historyForPair.Value.History.Any())
                        continue;

                    if (historyForPair.Value.History.Count > 1) // The unbelievable case.
                        throw new AmbiguousMatchException($"It seems like we have more than one today's Day Spot trade candle for asset pair {assetPairId}.");

                    todayCandles.Add(
                        historyForPair.Key,
                        historyForPair.Value
                            .History
                            .Single()
                        );
                }
            }

            return todayCandles;
        }

        #endregion
    }
}
