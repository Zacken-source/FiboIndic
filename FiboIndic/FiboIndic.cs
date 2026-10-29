using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    // Fibonacci retracement on the last impulse of a higher timeframe (H4 by default).
    // Impulse = last swing that broke the previous swing of the same type.
    // Only closed bars of the higher timeframe are used.
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class FiboIndic : Indicator
    {
        [Parameter("Timeframe", DefaultValue = "Hour4")]
        public TimeFrame FiboTimeFrame { get; set; }

        [Parameter("Swing strength (bars)", DefaultValue = 3, MinValue = 1)]
        public int SwingStrength { get; set; }

        [Parameter("Bars to scan", DefaultValue = 300, MinValue = 50)]
        public int BarsToScan { get; set; }

        [Parameter("Retracements", DefaultValue = "0.382,0.5,0.618,0.705,0.786", Group = "Levels")]
        public string RetracementLevels { get; set; }

        [Parameter("Extensions", DefaultValue = "1.272,1.618,2.618", Group = "Levels")]
        public string ExtensionLevels { get; set; }

        [Parameter("Entry zone from", DefaultValue = 0.618, Group = "Levels")]
        public double ZoneFrom { get; set; }

        [Parameter("Entry zone to", DefaultValue = 0.786, Group = "Levels")]
        public double ZoneTo { get; set; }

        [Parameter("Line color", DefaultValue = "White", Group = "Colors")]
        public Color LineColor { get; set; }

        [Parameter("Extension color", DefaultValue = "SkyBlue", Group = "Colors")]
        public Color ExtensionColor { get; set; }

        [Parameter("Zone color", DefaultValue = "Gold", Group = "Colors")]
        public Color ZoneColor { get; set; }

        private class Swing
        {
            public int Index;
            public double Price;
            public bool IsHigh;
        }

        private Bars htf;
        private List<string> objects = new List<string>();
        private int lastIndex = -1;
        private int counter = 0;

        protected override void Initialize()
        {
            htf = MarketData.GetBars(FiboTimeFrame);
        }

        public override void Calculate(int index)
        {
            if (!IsLastBar || index == lastIndex)
                return;
            lastIndex = index;

            foreach (var name in objects)
                Chart.RemoveObject(name);
            objects.Clear();

            var swings = FindSwings();
            Swing a, b;
            if (!FindImpulse(swings, out a, out b))
            {
                Chart.DrawStaticText(NewName(), "Fibo: no impulse found", VerticalAlignment.Top, HorizontalAlignment.Right, Color.Gray);
                return;
            }

            DrawFibo(a, b);
        }

        // swing = high/low that is the extreme of SwingStrength bars on each side
        private List<Swing> FindSwings()
        {
            var swings = new List<Swing>();
            int last = htf.Count - 2;  // htf.Count - 1 is still forming
            int first = Math.Max(SwingStrength, last - BarsToScan);

            for (int i = first; i <= last - SwingStrength; i++)
            {
                bool isHigh = true;
                bool isLow = true;
                for (int k = 1; k <= SwingStrength; k++)
                {
                    if (htf.HighPrices[i - k] >= htf.HighPrices[i] || htf.HighPrices[i + k] > htf.HighPrices[i])
                        isHigh = false;
                    if (htf.LowPrices[i - k] <= htf.LowPrices[i] || htf.LowPrices[i + k] < htf.LowPrices[i])
                        isLow = false;
                }
                if (isHigh)
                    AddSwing(swings, new Swing { Index = i, Price = htf.HighPrices[i], IsHigh = true });
                if (isLow)
                    AddSwing(swings, new Swing { Index = i, Price = htf.LowPrices[i], IsHigh = false });
            }
            return swings;
        }

        // keep highs and lows alternating: two highs in a row -> keep the higher one
        private void AddSwing(List<Swing> swings, Swing s)
        {
            if (swings.Count > 0)
            {
                var prev = swings[swings.Count - 1];
                if (prev.IsHigh == s.IsHigh)
                {
                    bool better = s.IsHigh ? s.Price > prev.Price : s.Price < prev.Price;
                    if (better)
                        swings[swings.Count - 1] = s;
                    return;
                }
            }
            swings.Add(s);
        }

        private bool FindImpulse(List<Swing> swings, out Swing a, out Swing b)
        {
            a = null;
            b = null;
            for (int i = swings.Count - 1; i >= 2; i--)
            {
                var end = swings[i];
                var previousSame = swings[i - 2];
                bool broke = end.IsHigh ? end.Price > previousSame.Price : end.Price < previousSame.Price;
                if (broke)
                {
                    a = swings[i - 1];
                    b = end;
                    return true;
                }
            }
            return false;
        }

        private void DrawFibo(Swing a, Swing b)
        {
            DateTime timeA = htf.OpenTimes[a.Index];
            DateTime timeB = htf.OpenTimes[b.Index];
            DateTime right = Bars.OpenTimes.LastValue.AddTicks((Bars.OpenTimes.LastValue - Bars.OpenTimes[Bars.Count - 2]).Ticks * 15);
            double move = b.Price - a.Price;
            string format = "F" + Symbol.Digits;

            // impulse line
            Chart.DrawTrendLine(NewName(), timeA, a.Price, timeB, b.Price, Color.Orange, 2, LineStyle.Solid);
            Chart.DrawText(NewName(), "A", timeA, a.Price, Color.Orange);
            Chart.DrawText(NewName(), "B", timeB, b.Price, Color.Orange);

            // 0% = B, 100% = A
            DrawLevel(0, b.Price, timeA, right, LineColor, LineStyle.Solid, format);
            DrawLevel(1, a.Price, timeA, right, LineColor, LineStyle.Solid, format);
            foreach (double r in ParseLevels(RetracementLevels))
                DrawLevel(r, b.Price - move * r, timeA, right, LineColor, LineStyle.Dots, format);

            foreach (double r in ParseLevels(ExtensionLevels))
                DrawLevel(r, a.Price + move * r, timeB, right, ExtensionColor, LineStyle.DotsRare, format);

            double zoneTop = b.Price - move * ZoneFrom;
            double zoneBottom = b.Price - move * ZoneTo;
            var zone = Chart.DrawRectangle(NewName(), timeB, zoneTop, right, zoneBottom, Color.FromArgb(50, ZoneColor));
            zone.IsFilled = true;

            // where is price compared to the zone
            double price = Bars.ClosePrices[Bars.Count - 2];
            bool bullish = move > 0;
            string info;
            if (bullish ? price < a.Price : price > a.Price)
                info = "impulse broken (price beyond 100%)";
            else if (price <= Math.Max(zoneTop, zoneBottom) && price >= Math.Min(zoneTop, zoneBottom))
                info = "price in entry zone";
            else
                info = "price outside entry zone";

            string dir = bullish ? "bullish" : "bearish";
            Chart.DrawStaticText(NewName(), "Fibo " + FiboTimeFrame + " " + dir + " impulse - " + info,
                VerticalAlignment.Top, HorizontalAlignment.Right, Color.Orange);
        }

        private void DrawLevel(double ratio, double price, DateTime from, DateTime to, Color color, LineStyle style, string format)
        {
            Chart.DrawTrendLine(NewName(), from, price, to, price, color, 1, style);
            string label = (ratio * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%  " + price.ToString(format);
            var text = Chart.DrawText(NewName(), label, to, price, color);
            text.FontSize = 8;
            text.VerticalAlignment = VerticalAlignment.Center;
        }

        private List<double> ParseLevels(string input)
        {
            var result = new List<double>();
            foreach (string part in input.Split(','))
            {
                double value;
                if (double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    result.Add(value);
            }
            return result;
        }

        private string NewName()
        {
            string name = "fibo_" + counter++;
            objects.Add(name);
            return name;
        }
    }
}
