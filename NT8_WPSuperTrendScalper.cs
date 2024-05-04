#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class WPSuperTrendScalper : Strategy
	{
		private int barCount = 0;
		private int barNumber = 0;
		private WPSuperTrendIndicator tstrend;
		private Order order = null;
		private double TicksToConsiderAligned;
		
		#region FastTimeFrame
		private Stack<int> LastBullishTrendZoneIndexes = new Stack<int>();
		private Stack<int> LastBearishTrendZoneIndexes = new Stack<int>();
		private bool IsBullishZone = false;
		private bool IsBearishZone = false;
		#endregion
		
		#region MediumTimeFrame
		private int MTF_barNumber = 0;
//		private BackBrushes myBackBrushes;
		private Stack<int> MTF_LastBullishTrendZoneIndexes = new Stack<int>();
		private Stack<int> MTF_LastBearishTrendZoneIndexes = new Stack<int>();
		private WPSuperTrendIndicator tstrendMediumTF;
		private bool MTF_IsBullishZone = false;
		private bool MTF_IsBearishZone = false;
		private EMA MTF_FastMA;
		private SMA MTF_MediumMA;
		private SMA MTF_LargeMA;
		#endregion
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "WPSuperTrendScalper";
				Calculate									= Calculate.OnBarClose;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
				
				AddPlot(Brushes.White, "FastMA");
				AddPlot(Brushes.Gold, "MediumMA");
				AddPlot(Brushes.DeepSkyBlue, "LargeMA");
				
				MediumTimeFrameRange						= 30;
			}
			else if (State == State.Configure)
			{
				AddDataSeries(BarsPeriodType.Range, MediumTimeFrameRange);
			}
			else if (State == State.DataLoaded) {
				TicksToConsiderAligned = TickSize * 2;
				tstrend = WPSuperTrendIndicator(0, WPSuperTrendMode.ATR, 14, 2.618, WPMovingAverageType.HMA, 14, true, true, false);
			}
		}
		
		private void TrailingStop(double newStopPrice) {
			if (Position.MarketPosition != MarketPosition.Flat && order != null) {
				SetStopLoss(CalculationMode.Price, newStopPrice);
				ChangeOrder(order, order.Quantity, order.LimitPrice, newStopPrice);
			}
		}
		
		private bool CheckBearishTrend() {
			// Check Moving Average Alignment
			bool fastBelowMedium 	= FastMA[0] < MediumMA[0] && Math.Abs(FastMA[0] - MediumMA[0]) >= TicksToConsiderAligned;
			bool largeAboveBoth		= LargeMA[0] > MediumMA[0] && Math.Abs(LargeMA[0] - MediumMA[0]) >= TicksToConsiderAligned;
			
			// Check Moving Average Pointing Direction
			bool isFastPoitingToDown = FastMA[0] < FastMA[1] && FastMA[1] < FastMA[2];
			bool isMediumPoitingToDown = MediumMA[0] < MediumMA[1] && MediumMA[1] < MediumMA[2];
			
			bool allCondSatisfied = (fastBelowMedium && largeAboveBoth && isFastPoitingToDown && isMediumPoitingToDown);
			
//			if (allCondSatisfied)
//				BackBrushes[0] = Brushes.DarkRed;
			
			return allCondSatisfied;
		}
		
		private bool CheckBullishTrend() {
			// Check Moving Average Alignment
			bool fastAboveMedium 	= FastMA[0] > MediumMA[0] && Math.Abs(FastMA[0] - MediumMA[0]) >= TicksToConsiderAligned;
			bool largeBelowBoth		= LargeMA[0] < MediumMA[0] && Math.Abs(LargeMA[0] - MediumMA[0]) >= TicksToConsiderAligned;
			
			// Check Moving Average Pointing Direction
			bool isFastPoitingToUp = FastMA[0] > FastMA[1] && FastMA[1] > FastMA[2];
			bool isMediumPoitingToUp = MediumMA[0] > MediumMA[1] && MediumMA[1] > MediumMA[2];
			
			bool allCondSatisfied = (fastAboveMedium && largeBelowBoth && isFastPoitingToUp && isMediumPoitingToUp);
			
//			if (allCondSatisfied)
//				BackBrushes[0] = Brushes.DarkGreen;
			
			return allCondSatisfied;
		}
		
		private void GetFlat() {
			if (Position.MarketPosition == MarketPosition.Long) { ExitLong("Long"); }
			if (Position.MarketPosition == MarketPosition.Short) { ExitShort("Short"); }
		}
		
		private void CheckEntry() {
			if ((MTF_IsBullishZone && IsBullishZone) && LastBearishTrendZoneIndexes.Count > 1 && IsFirstTickOfBar) {
				Draw.ArrowUp(this, CurrentBar.ToString(), true, 0, tstrend.UpTrend[0] - TickSize, Brushes.LimeGreen);
				int barsAgo = LowestBar(Low, Math.Abs(barNumber - LastBearishTrendZoneIndexes.ToArray()[0]));
				Draw.ArrowUp(this, $"Position_{CurrentBar.ToString()}", true, 0, tstrend.UpTrend[0] - TickSize, Brushes.LimeGreen);
				Draw.ArrowUp(this, $"Stop_{CurrentBar.ToString()}", true, barsAgo, Low[barsAgo] - TickSize, Brushes.White);
				// Draw.Line(this, $"Stop_Position_{CurrentBar.ToString()}", barsAgo, Low[barsAgo], 0, Low[0] - TickSize, Brushes.White);
				SetStopLoss(CalculationMode.Price, Low[barsAgo]);
				if (Position.MarketPosition == MarketPosition.Flat) {
					EnterLong(DefaultQuantity, "Long");
				}
//				else if (Position.MarketPosition == MarketPosition.Short) {
//					ExitShort("Short");
//					EnterLong(DefaultQuantity, "Long");
//				}
			}
			
			if ((MTF_IsBearishZone && IsBearishZone) && LastBearishTrendZoneIndexes.Count > 1 && IsFirstTickOfBar) {
				Draw.ArrowDown(this, CurrentBar.ToString(), true, 0, tstrend.DownTrend[0] + TickSize, Brushes.DarkRed);
				int barsAgo = HighestBar(High, Math.Abs(barNumber - LastBullishTrendZoneIndexes.ToArray()[0]));
				Draw.ArrowDown(this, $"Position_{CurrentBar.ToString()}", true, 0, tstrend.DownTrend[0] + TickSize, Brushes.DarkRed);
				Draw.ArrowDown(this, $"Stop_{CurrentBar.ToString()}", true, barsAgo, High[barsAgo] + TickSize, Brushes.White);
				// Draw.Line(this, $"Stop_Position_{CurrentBar.ToString()}", barsAgo, High[barsAgo] + TickSize, 0, High[0], Brushes.White);
				SetStopLoss(CalculationMode.Price, High[barsAgo]);
				if (Position.MarketPosition == MarketPosition.Flat && State != State.Historical) {
					EnterShort(DefaultQuantity, "Short");
				}
//				else if (Position.MarketPosition == MarketPosition.Long && State != State.Historical) {
//					ExitLong("Long");
//					EnterShort(DefaultQuantity, "Short");
//				}
			}
		}

		protected override void OnBarUpdate()
		{
			// Medium Timeframe
			if (BarsInProgress == 1 && CurrentBars[1] < 1) {
				tstrendMediumTF = WPSuperTrendIndicator(1, WPSuperTrendMode.ATR, 14, 2.618, WPMovingAverageType.HMA, 14, false, false, false);
			}
			if (BarsInProgress == 1 && IsFirstTickOfBar) {
				if (CurrentBars[1] < 2)
					return;
				MTF_barNumber = barNumber;
				MTF_FastMA = EMA(Closes[1], 9);
				MTF_MediumMA = SMA(Closes[1], 20);
				MTF_LargeMA = SMA(Closes[1], 50);
				
				RunOnMediumTimeFrame();
			}
			//Add your custom strategy logic here.
			if (BarsInProgress != 0)
				return;
			
			Values[0][0] = EMA(Closes[0], 9)[0];
			Values[1][0] = SMA(Closes[0], 20)[0];
			Values[2][0] = SMA(Closes[0], 50)[0];
			
			barNumber = CurrentBar;
			
			if (MTF_IsBearishZone)
				BackBrushes[0] = new SolidColorBrush(Colors.DarkRed) {Opacity = 0.15};
			if (MTF_IsBullishZone)
				BackBrushes[0] = new SolidColorBrush(Colors.DarkGreen) {Opacity = 0.15};
			
			 if (order != null && order.IsBacktestOrder && State == State.Realtime)
      			order = GetRealtimeOrder(order);
			
			if (CurrentBars[0] < 5)
				return;
			
			if (Bars.IsFirstBarOfSession) {
				barCount = 0;
				Draw.Diamond(this, "FirstBar" + barNumber, true, 0, High[0] + (TickSize * 50), Brushes.AliceBlue);
			}
			
//			if (Position.MarketPosition == MarketPosition.Long && !IsBullishZone) {
//				ExitLong("Long");
//			}
//			else if (Position.MarketPosition == MarketPosition.Short && !IsBearishZone) {
//				ExitShort("Short");
//			}
		
			
			if (tstrend._trend[0] && !tstrend._trend[1]) { // Bullish
				LastBullishTrendZoneIndexes.Push(barNumber);
				IsBullishZone = true;
				IsBearishZone = false;
				
				CheckEntry();
				
			} else if (!tstrend._trend[0] && tstrend._trend[1]) { // Bearish
				LastBearishTrendZoneIndexes.Push(barNumber);
				IsBullishZone = false;
				IsBearishZone = true;
				
				CheckEntry();
			}
		}
		
		private void RunOnMediumTimeFrame() {
			if (tstrendMediumTF._trend[0] && !tstrendMediumTF._trend[1]) { // Bullish
				Draw.ArrowUp(this, $"MTF_UP_{CurrentBar}", true, 0, Low[0] - TickSize, Brushes.Gold);
				MTF_LastBullishTrendZoneIndexes.Push(MTF_barNumber);
				MTF_IsBearishZone = false;
				MTF_IsBullishZone = true;
				if (MTF_CheckBullishTrend() && MTF_LastBullishTrendZoneIndexes.Count > 1) {
					CheckEntry();
				}
				
			}
			else if (!tstrendMediumTF._trend[0] && tstrendMediumTF._trend[1]) { // Bearish
				Draw.ArrowDown(this, $"MTF_DOWN_{CurrentBar}", true, 0, High[0] + TickSize, Brushes.Gold);
				MTF_LastBearishTrendZoneIndexes.Push(MTF_barNumber);
				MTF_IsBearishZone = true;
				MTF_IsBullishZone = false;
				if (MTF_CheckBearishTrend() && MTF_LastBearishTrendZoneIndexes.Count > 0) {
					CheckEntry();
				}
			}
		}

		private bool MTF_CheckBearishTrend() {
			// Check Moving Average Alignment
			bool fastBelowMedium 	= MTF_FastMA[0] < MTF_MediumMA[0] && Math.Abs(MTF_FastMA[0] - MTF_MediumMA[0]) >= TicksToConsiderAligned;
			bool largeAboveBoth		= MTF_LargeMA[0] > MTF_MediumMA[0] && Math.Abs(MTF_LargeMA[0] - MTF_MediumMA[0]) >= TicksToConsiderAligned;
			
			// Check Moving Average Pointing Direction
			bool isFastPoitingToDown = MTF_FastMA[0] < MTF_FastMA[1] && MTF_FastMA[1] < MTF_FastMA[2];
			bool isMediumPoitingToDown = MTF_MediumMA[0] < MTF_MediumMA[1] && MTF_MediumMA[1] < MTF_MediumMA[2];
			
			bool allCondSatisfied = (fastBelowMedium && largeAboveBoth && isFastPoitingToDown && isMediumPoitingToDown);
			
//			if (allCondSatisfied)
//				Draw.Region(this, $"BearishZone_{CurrentBars[1]}", MTF_LastBearishTrendZoneIndexes.ToArray()[0], MTF_LastBearishTrendZoneIndexes.ToArray()[1], High, Low, Brushes.Transparent, Brushes.Pink, 30);
			
			return allCondSatisfied;
		}
		
		private bool MTF_CheckBullishTrend() {
			// Check Moving Average Alignment
			bool fastAboveMedium 	= MTF_FastMA[0] > MTF_MediumMA[0] && Math.Abs(MTF_FastMA[0] - MTF_MediumMA[0]) >= TicksToConsiderAligned;
			bool largeBelowBoth		= MTF_LargeMA[0] < MTF_MediumMA[0] && Math.Abs(MTF_LargeMA[0] - MTF_MediumMA[0]) >= TicksToConsiderAligned;
			
			// Check Moving Average Pointing Direction
			bool isFastPoitingToUp = MTF_FastMA[0] > MTF_FastMA[1] && MTF_FastMA[1] > MTF_FastMA[2];
			bool isMediumPoitingToUp = MTF_MediumMA[0] > MTF_MediumMA[1] && MTF_MediumMA[1] > MTF_MediumMA[2];
			
			bool allCondSatisfied = (fastAboveMedium && largeBelowBoth && isFastPoitingToUp && isMediumPoitingToUp);
			
//			if (allCondSatisfied)
//				Draw.Region(this, $"BullishZone_{CurrentBars[1]}", MTF_LastBullishTrendZoneIndexes.ToArray()[0], MTF_LastBullishTrendZoneIndexes.ToArray()[1], High, Low, Brushes.Transparent, Brushes.DeepSkyBlue, 30);
			
			return allCondSatisfied;
		}
		
		#region Properties
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="MediumTimeFrameRange", Description="The Medium Timeframe in Range", Order=1, GroupName="Parameters")]
		public int MediumTimeFrameRange
		{ get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> FastMA
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> MediumMA
		{
			get { return Values[1]; }
		}
		
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> LargeMA
		{
			get { return Values[2]; }
		}
		#endregion
	}
}
