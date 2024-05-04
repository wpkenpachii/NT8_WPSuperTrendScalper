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
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{	
	public class WPSuperTrendIndicator : Indicator
	{
		
		//Notes: Beta5, SMMA not available, HomodyneDiscriminator not available so those choices have been commented out for now
		
		public Series<bool> _trend;
		private Series<double> _avg;
		private int _length 	= 14;
        private int _smooth 	= 14;
        private int _thisbar 	= -1;
        private bool _showArrows;
        private bool _colorBars = true;
        private bool _playAlert;
		private bool firstArrow = true; // addded to eliminate first arrow draw which is usually on noise and distorts the autoscale
        private double _th;
        private double _tl = double.MaxValue;		
        private double _offset;
        private double _multiplier = 2.618;
        private Brush _barColorUp = Brushes.Blue;
        private Brush _barColorDown = Brushes.Red;
        private Brush _tempColor;
        private Brush _prevColor;
        private string _longAlert = @"C:\Program Files (x86)\NinjaTrader 8\sounds\Alert3.wav";
        private string _shortAlert = @"C:\Program Files (x86)\NinjaTrader 8\sounds\Alert4.wav";
        private WPMovingAverageType _maType = WPMovingAverageType.HMA;
        private WPSuperTrendMode _smode = WPSuperTrendMode.ATR;	

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description							= @"TSSuperTrend Indicator developed by TradingStudies.com (Version 2.3)";
				Name								= "WPSuperTrendIndicator";
				Calculate							= Calculate.OnBarClose;
				IsOverlay							= true;
				DisplayInDataBox					= true;
				DrawOnPricePanel					= true;
				DrawHorizontalGridLines				= true;
				DrawVerticalGridLines				= true;
				PaintPriceMarkers					= true;
				ScaleJustification					= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive			= true;
				AddPlot(new Stroke(Brushes.Green, 2), PlotStyle.Line, "UpTrend");
				AddPlot(new Stroke(Brushes.Red,2), PlotStyle.Line, "DownTrend");
				TimeFrameIndex						= 0;
			}
			else if (State == State.Configure)
			{
				_trend = new Series<bool>(this);
				_avg = new Series<double>(this);
				
                    switch (_maType)
                    {
                        case WPMovingAverageType.SMA:
                            _avg = SMA(Input, _smooth).Value;
                            break;
                        case WPMovingAverageType.TMA:
                            _avg = TMA(Input, _smooth).Value;
                            break;
                        case WPMovingAverageType.WMA:
                            _avg = WMA(Input, _smooth).Value;
                            break;
                        case WPMovingAverageType.VWMA:
                            _avg = VWMA(Input, _smooth).Value;
                            break;
                        case WPMovingAverageType.TEMA:
                            _avg = TEMA(Input, _smooth).Value;
                            break;
                        case WPMovingAverageType.HMA:
                            _avg = HMA(Input, _smooth).Value;
                            break;
                        case WPMovingAverageType.VMA:
                            _avg = VMA(Input, _smooth, _smooth).Value;
                            break;
                        default:
                            _avg = EMA(Input, _smooth).Value;
                            break;
                    }
			}
		}

		protected override void OnBarUpdate()
		{
           if (CurrentBar < 1)
		   {
               	_trend[0] 	= true;
                UpTrend[0]	= Input[0];
                DownTrend[0]= Input[0];
                return;
            }
		   
            switch (_smode)
            {
                case WPSuperTrendMode.ATR:
                    _offset = ATR(_length)[0] * Multiplier;
                    break;
                default:
                    _offset = Dtt(_length, Multiplier);
                    break;
            }
			
            if (IsFirstTickOfBar)
                _prevColor = _tempColor;
			
			if (UpTrend[1] > 0.0)    // Note in NT8 dataseries by default will hold 0, not the close[0] value.
			{
				_trend[0] = (Close[0] < UpTrend[1] ? false : _trend[1]);
			}
			else
				_trend[0] = (Close[0] > DownTrend[1] ? true : false);
			
			
            if (_trend[0] && !_trend[1])
            {
                _th = Highs[TimeFrameIndex][0];
				UpTrend[0] = Math.Max(_avg[0] - _offset, _tl);
                if (Plots[0].PlotStyle == PlotStyle.Line)  UpTrend[1] = DownTrend[1];
                _tempColor = _barColorUp;
                if (ShowArrows && !firstArrow)
                    Draw.ArrowUp(this, CurrentBar.ToString(), true, 0, UpTrend[0] - TickSize, _barColorUp);
				else firstArrow = false;  // Don't draw the very first arrow as it will be near the zero line.
				
                if(PlayAlert && _thisbar != CurrentBar)
                {
                    _thisbar = CurrentBar;
                    PlaySound(_longAlert);
                }
            }
            else
                if (!_trend[0] && _trend[1])
                {
                    _tl = Lows[TimeFrameIndex][0];
					DownTrend[0] = Math.Min(_avg[0] + _offset, _th);
                    if (Plots[1].PlotStyle == PlotStyle.Line) DownTrend[1] = UpTrend[1];
                    _tempColor = _barColorDown;
                    if (ShowArrows && !firstArrow)
                        Draw.ArrowDown(this, CurrentBar.ToString(), true, 0, DownTrend[0] + TickSize, _barColorDown);
					else firstArrow = false;  // Don't draw the very first arrow as it will be near the zero line.
					
                    if (PlayAlert && _thisbar != CurrentBar)
                    {
                        _thisbar = CurrentBar;
                        PlaySound(_shortAlert);
                    }
                }
                else
                {
                    if (_trend[0])
                    {
						UpTrend[0] = ((_avg[0] - _offset) > UpTrend[1] ? (_avg[0] - _offset) : UpTrend[1]);
                        _th = Math.Max(_th, Highs[TimeFrameIndex][0]);
                    }
                    else
                    {
                        DownTrend[0] = ((_avg[0] + _offset) < DownTrend[1] ? (_avg[0] + _offset) : DownTrend[1]);
                        _tl = Math.Min(_tl, Lows[TimeFrameIndex][0]);
                    }
                    RemoveDrawObject(CurrentBar.ToString());
                    _tempColor = _prevColor;
                }

            if (!_colorBars) 
                return;

            CandleOutlineBrush = _tempColor;

            BarBrush = Opens[TimeFrameIndex][0] < Closes[TimeFrameIndex][0]    ? Brushes.Transparent : _tempColor;
			
		}	// end protected override void OnBarUpdate()
		
        private double Dtt(int nDay, double mult)
        {
            double hh = MAX(Highs[TimeFrameIndex], nDay)[0];
            double hc = MAX(Closes[TimeFrameIndex], nDay)[0];
            double ll = MIN(Lows[TimeFrameIndex], nDay)[0];
            double lc = MIN(Closes[TimeFrameIndex], nDay)[0];
            return mult * Math.Max((hh - lc), (hc - ll));
        }		

		#region Properties
		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="TimeFrameIndex", Description="In What Period it gonna be calcualted", Order=1, GroupName="Parameters")]
		public int TimeFrameIndex
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="SuperTrend Mode", Description="", Order=1, GroupName="Parameters")]
		public WPSuperTrendMode Smode
		{ 
			get {return _smode;}
			set {_smode = value;}
		}		
		
		
		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="Period", Description="ATR/DT Period", Order=2, GroupName="Parameters")]
		public int Length
		{ 
			get {return _length;}
			set {_length = value;}
		}

		[Range(0.0001, double.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="Multiplier", Description="ATR Multiplier", Order=3, GroupName="Parameters")]
		public double Multiplier
		{ 
			get {return _multiplier;}
			set {_multiplier = value;}
		}
		[NinjaScriptProperty]
		[Display(Name="Moving Avg. Type", Description="", Order=4, GroupName="Parameters")]
		public WPMovingAverageType MaType
		{ 
			get {return _maType;}
			set {_maType = value;}
		}		
				

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="Smooth", Description="Smoothing Period", Order=5, GroupName="Parameters")]
		public int Smooth
		{ 
			get {return _smooth;}
			set {_smooth = value;}
		}

		[NinjaScriptProperty]
		[Display(Name="ShowArrows", Description="Show Arrows when Trendline is violated?", Order=6, GroupName="Visual")]
		public bool ShowArrows
		{ 
			get {return _showArrows ;} 
			set {_showArrows = value;}
		}

		[NinjaScriptProperty]
		[Display(Name="ColorBars", Description="Color the bars in the direction of the trend?", Order=7, GroupName="Visual")]
		public bool ColorBars
		{ 
			get {return _colorBars;} 
			set {_colorBars = value;}
		}

		[XmlIgnore]
		[Display(Name="BarColorUp", Description="Color of up bars", Order=8, GroupName="Visual")]
		public Brush BarColorUp
		{ 
			get {return _barColorUp;}
			set {_barColorUp = value;}
		}

		[Browsable(false)]
		public string BarColorUpSerializable
		{
			get { return Serialize.BrushToString(_barColorUp); }
			set {_barColorUp = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="BarColorDown", Description="Color of down bars", Order=9, GroupName="Visual")]
		public Brush BarColorDown
		{ 
			get {return _barColorDown;}
			set {_barColorDown = value;}
		}
		[Browsable(false)]
		public string BarColorDownSerializable
		{
			get { return Serialize.BrushToString(_barColorDown); }
			set { _barColorDown = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[Display(Name="PlayAlert", Description="Play alert sounds", Order=8, GroupName="Visual")]
		public bool PlayAlert
		{ 
			get {return _playAlert;}
			set {_playAlert = value;}
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> UpTrend
		{
			get 
			{return Values[0];}
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> DownTrend
		{
			get 
			{return Values[1];}
		}		
		#endregion

	}
}


    public enum WPSuperTrendMode
    {
        ATR,
        Adaptive
    }

    public enum WPMovingAverageType
    {
        SMA,
        TMA,
        WMA,
        VWMA,
        TEMA,
        HMA,
        EMA,
        VMA
    }

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private WPSuperTrendIndicator[] cacheWPSuperTrendIndicator;
		public WPSuperTrendIndicator WPSuperTrendIndicator(int timeFrameIndex, WPSuperTrendMode smode, int length, double multiplier, WPMovingAverageType maType, int smooth, bool showArrows, bool colorBars, bool playAlert)
		{
			return WPSuperTrendIndicator(Input, timeFrameIndex, smode, length, multiplier, maType, smooth, showArrows, colorBars, playAlert);
		}

		public WPSuperTrendIndicator WPSuperTrendIndicator(ISeries<double> input, int timeFrameIndex, WPSuperTrendMode smode, int length, double multiplier, WPMovingAverageType maType, int smooth, bool showArrows, bool colorBars, bool playAlert)
		{
			if (cacheWPSuperTrendIndicator != null)
				for (int idx = 0; idx < cacheWPSuperTrendIndicator.Length; idx++)
					if (cacheWPSuperTrendIndicator[idx] != null && cacheWPSuperTrendIndicator[idx].TimeFrameIndex == timeFrameIndex && cacheWPSuperTrendIndicator[idx].Smode == smode && cacheWPSuperTrendIndicator[idx].Length == length && cacheWPSuperTrendIndicator[idx].Multiplier == multiplier && cacheWPSuperTrendIndicator[idx].MaType == maType && cacheWPSuperTrendIndicator[idx].Smooth == smooth && cacheWPSuperTrendIndicator[idx].ShowArrows == showArrows && cacheWPSuperTrendIndicator[idx].ColorBars == colorBars && cacheWPSuperTrendIndicator[idx].PlayAlert == playAlert && cacheWPSuperTrendIndicator[idx].EqualsInput(input))
						return cacheWPSuperTrendIndicator[idx];
			return CacheIndicator<WPSuperTrendIndicator>(new WPSuperTrendIndicator(){ TimeFrameIndex = timeFrameIndex, Smode = smode, Length = length, Multiplier = multiplier, MaType = maType, Smooth = smooth, ShowArrows = showArrows, ColorBars = colorBars, PlayAlert = playAlert }, input, ref cacheWPSuperTrendIndicator);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.WPSuperTrendIndicator WPSuperTrendIndicator(int timeFrameIndex, WPSuperTrendMode smode, int length, double multiplier, WPMovingAverageType maType, int smooth, bool showArrows, bool colorBars, bool playAlert)
		{
			return indicator.WPSuperTrendIndicator(Input, timeFrameIndex, smode, length, multiplier, maType, smooth, showArrows, colorBars, playAlert);
		}

		public Indicators.WPSuperTrendIndicator WPSuperTrendIndicator(ISeries<double> input , int timeFrameIndex, WPSuperTrendMode smode, int length, double multiplier, WPMovingAverageType maType, int smooth, bool showArrows, bool colorBars, bool playAlert)
		{
			return indicator.WPSuperTrendIndicator(input, timeFrameIndex, smode, length, multiplier, maType, smooth, showArrows, colorBars, playAlert);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.WPSuperTrendIndicator WPSuperTrendIndicator(int timeFrameIndex, WPSuperTrendMode smode, int length, double multiplier, WPMovingAverageType maType, int smooth, bool showArrows, bool colorBars, bool playAlert)
		{
			return indicator.WPSuperTrendIndicator(Input, timeFrameIndex, smode, length, multiplier, maType, smooth, showArrows, colorBars, playAlert);
		}

		public Indicators.WPSuperTrendIndicator WPSuperTrendIndicator(ISeries<double> input , int timeFrameIndex, WPSuperTrendMode smode, int length, double multiplier, WPMovingAverageType maType, int smooth, bool showArrows, bool colorBars, bool playAlert)
		{
			return indicator.WPSuperTrendIndicator(input, timeFrameIndex, smode, length, multiplier, maType, smooth, showArrows, colorBars, playAlert);
		}
	}
}

#endregion
