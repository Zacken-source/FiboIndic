# FiboIndic

cTrader indicator that draws a Fibonacci retracement on the last impulse of a higher timeframe (H4 by default), so it can be used on M15 / M5 for entries.

- impulse = last swing that broke the previous swing of the same type
- only closed candles of the higher timeframe are used
- retracements 0.382 / 0.5 / 0.618 / 0.705 / 0.786, entry zone 0.618 - 0.786
- extensions 1.272 / 1.618 / 2.618 to check take profits

## Install

In cTrader: Algo > Indicators > New, name it `FiboIndic`, paste `FiboIndic/FiboIndic.cs` and build.
