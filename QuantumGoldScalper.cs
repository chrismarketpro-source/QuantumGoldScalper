using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class QuantumGoldScalper : Robot
    {
        [Parameter("Volume (Units)", DefaultValue = 1000)]
        public double VolumeUnits { get; set; }

        [Parameter("Stop Loss (Pips)", DefaultValue = 100)]
        public double StopLossPips { get; set; }

        [Parameter("Take Profit (Pips)", DefaultValue = 150)]
        public double TakeProfitPips { get; set; }

        [Parameter("Maximum Spread (Pips)", DefaultValue = 5)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Daily Loss Limit (%)", DefaultValue = 3)]
        public double DailyLossLimitPercent { get; set; }

        [Parameter("RSI Period", DefaultValue = 14)]
        public int RsiPeriod { get; set; }

        [Parameter("Label", DefaultValue = "QuantumGoldScalper")]
        public string BotLabel { get; set; }

        private Bars trendBars;
        private ExponentialMovingAverage ema50;
        private ExponentialMovingAverage ema200;
        private RelativeStrengthIndex rsi;

        private double dayStartEquity;
        private DateTime currentDay;

        protected override void OnStart()
        {
            trendBars = MarketData.GetBars(TimeFrame.Minute5);

            ema50 = Indicators.ExponentialMovingAverage(
                trendBars.ClosePrices, 50);

            ema200 = Indicators.ExponentialMovingAverage(
                trendBars.ClosePrices, 200);

            rsi = Indicators.RelativeStrengthIndex(
                Bars.ClosePrices, RsiPeriod);

            currentDay = Server.Time.Date;
            dayStartEquity = Account.Equity;

            Print("QuantumGoldScalper started on {0}", SymbolName);
        }

        protected override void OnBar()
        {
            if (Bars.Count < 50 || trendBars.Count < 205)
                return;

            if (Server.Time.Date != currentDay)
            {
                currentDay = Server.Time.Date;
                dayStartEquity = Account.Equity;
            }

            if (DailyLossReached())
                return;

            if (Positions.Find(BotLabel, SymbolName) != null)
                return;

            if (Symbol.Spread > MaxSpreadPips)
                return;

            int m1 = Bars.Count - 2;
            int m5 = trendBars.Count - 2;

            if (m1 < 2 || m5 < 200)
                return;

            double close = Bars.ClosePrices[m1];
            double open = Bars.OpenPrices[m1];

            double trendFast = ema50.Result[m5];
            double trendSlow = ema200.Result[m5];

            double rsiValue = rsi.Result[m1];

            bool bullishCandle = close > open;
            bool bearishCandle = close < open;

            bool bullishTrend = trendFast > trendSlow;
            bool bearishTrend = trendFast < trendSlow;

            if (bullishTrend && bullishCandle && rsiValue > 50)
            {
                ExecuteMarketOrder(
                    TradeType.Buy,
                    SymbolName,
                    Symbol.NormalizeVolumeInUnits(
                        VolumeUnits, RoundingMode.Down),
                    BotLabel,
                    StopLossPips,
                    TakeProfitPips);
            }

            if (bearishTrend && bearishCandle && rsiValue < 50)
            {
                ExecuteMarketOrder(
                    TradeType.Sell,
                    SymbolName,
                    Symbol.NormalizeVolumeInUnits(
                        VolumeUnits, RoundingMode.Down),
                    BotLabel,
                    StopLossPips,
                    TakeProfitPips);
            }
        }

        private bool DailyLossReached()
        {
            double loss = dayStartEquity - Account.Equity;

            double limit = dayStartEquity *
                DailyLossLimitPercent / 100.0;

            if (loss >= limit)
            {
                Print("Daily loss limit reached. Trading stopped.");

                foreach (var position in Positions)
                {
                    if (position.SymbolName == SymbolName &&
                        position.Label == BotLabel)
                    {
                        ClosePosition(position);
                    }
                }

                return true;
            }

            return false;
        }
    }
}
