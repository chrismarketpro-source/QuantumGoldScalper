// Quantum Gold Scalper V1 - conservative demo-first cTrader cBot
// Attach to XAUUSD M1. Uses M5 trend filter, M1 EMA/RSI confirmation,
// fixed volume, ATR-based SL/TP, spread and daily-loss/trade-count guards.
// This source is NOT compiled into an .algo file yet.

using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class QuantumGoldScalperV1 : Robot
    {
        [Parameter("Volume (units)", DefaultValue = 1000, MinValue = 1)]
        public int VolumeUnits { get; set; }

        [Parameter("Max Spread (pips)", DefaultValue = 3.0, MinValue = 0.1)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Daily Loss Limit (%)", DefaultValue = 2.0, MinValue = 0.1)]
        public double DailyLossPercent { get; set; }

        [Parameter("Max Trades Per Day", DefaultValue = 3, MinValue = 1)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("ATR Period", DefaultValue = 14, MinValue = 2)]
        public int AtrPeriod { get; set; }

        [Parameter("SL ATR Multiplier", DefaultValue = 1.5, MinValue = 0.1)]
        public double SlAtrMultiplier { get; set; }

        [Parameter("TP ATR Multiplier", DefaultValue = 2.0, MinValue = 0.1)]
        public double TpAtrMultiplier { get; set; }

        private const string BotLabel = "QuantumGoldScalperV1";
        private Bars _m5Bars;
        private ExponentialMovingAverage _m1Fast, _m1Slow, _m5Fast, _m5Slow;
        private RelativeStrengthIndex _rsi;
        private AverageTrueRange _atr;
        private DateTime _day;
        private double _dayStartEquity;
        private int _tradesToday;

        protected override void OnStart()
        {
            if (SymbolName.ToUpper().Contains("XAU") == false)
            {
                Print("This cBot is designed for Gold symbols (XAU). Stopping.");
                Stop();
                return;
            }

            _m5Bars = MarketData.GetBars(TimeFrame.Minute5);
            _m1Fast = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 9);
            _m1Slow = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 21);
            _m5Fast = Indicators.ExponentialMovingAverage(_m5Bars.ClosePrices, 50);
            _m5Slow = Indicators.ExponentialMovingAverage(_m5Bars.ClosePrices, 200);
            _rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, 14);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);

            ResetDailyCounters();
            Print("Quantum Gold Scalper V1 started on {0}. Demo testing only.", SymbolName);
        }

        protected override void OnBar()
        {
            if (Bars.Count < 220 || _m5Bars.Count < 205)
                return;

            if (Server.Time.Date != _day)
                ResetDailyCounters();

            if (DailyLossHit() || _tradesToday >= MaxTradesPerDay)
                return;

            if (Symbol.Spread / Symbol.PipSize > MaxSpreadPips)
                return;

            if (Positions.Find(BotLabel, SymbolName) != null)
                return;

            double atrPips = _atr.Result.Last(1) / Symbol.PipSize;
            if (atrPips <= 0)
                return;

            double slPips = atrPips * SlAtrMultiplier;
            double tpPips = atrPips * TpAtrMultiplier;
            long volume = Symbol.NormalizeVolumeInUnits(VolumeUnits, RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin || volume > Symbol.VolumeInUnitsMax)
                return;

            bool bullishTrend = _m5Fast.Result.Last(1) > _m5Slow.Result.Last(1);
            bool bearishTrend = _m5Fast.Result.Last(1) < _m5Slow.Result.Last(1);
            bool bullishEntry = _m1Fast.Result.Last(1) > _m1Slow.Result.Last(1) && _rsi.Result.Last(1) > 50 && Bars.ClosePrices.Last(1) > Bars.OpenPrices.Last(1);
            bool bearishEntry = _m1Fast.Result.Last(1) < _m1Slow.Result.Last(1) && _rsi.Result.Last(1) < 50 && Bars.ClosePrices.Last(1) < Bars.OpenPrices.Last(1);

            if (bullishTrend && bullishEntry)
                PlaceTrade(TradeType.Buy, volume, slPips, tpPips);
            else if (bearishTrend && bearishEntry)
                PlaceTrade(TradeType.Sell, volume, slPips, tpPips);
        }

        private void PlaceTrade(TradeType type, long volume, double slPips, double tpPips)
        {
            var result = ExecuteMarketOrder(type, SymbolName, volume, BotLabel, slPips, tpPips);
            if (result != null && result.IsSuccessful)
            {
                _tradesToday++;
                Print("Trade opened: {0}; trades today: {1}", type, _tradesToday);
            }
            else
            {
                Print("Order failed or rejected. No retry.");
            }
        }

        private bool DailyLossHit()
        {
            if (_dayStartEquity <= 0)
                return true;
            double lossPercent = (_dayStartEquity - Account.Equity) / _dayStartEquity * 100.0;
            return lossPercent >= DailyLossPercent;
        }

        private void ResetDailyCounters()
        {
            _day = Server.Time.Date;
            _dayStartEquity = Account.Equity;
            _tradesToday = 0;
        }
    }
}
