using Core.CollectionSpace;
using Core.EnumSpace;
using Core.IndicatorSpace;
using Core.MessageSpace;
using Core.ModelSpace;
using Gateway.Simulation;
using System;
using System.Configuration;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Client.StrategySpace
{
  /// <summary>
  /// Gateway with aggregation by time
  /// </summary>
  public class TimeGatewayClient : GatewayClient
  {
    protected ITimeSpanCollection<IPointModel> _collection = new TimeSpanCollection<IPointModel>();

    public TimeGatewayClient(IInstrumentModel instrument)
    {
      instrument.PointGroups = _collection;
    }

    protected override IPointModel UpdatePoints(IPointModel point)
    {
      point.Instrument.Points.Add(point);
      _collection.Add(point, point.Instrument.TimeFrame);

      return point;
    }
  }

  /// <summary>
  /// Strategy
  /// </summary>
  public class ImbalanceStrategy : BaseStrategy
  {
    const string _asset = "GOOG";
    const string _account = "Simulation";

    protected DateTime? _date = null;
    protected TimeSpan? _span = null;
    protected IInstrumentModel _instrument = null;
    protected ImbalanceIndicator _imbalanceIndicator = null;
    protected PerformanceIndicator _performanceIndicator = null;

    public override Task OnLoad()
    {
      _date = DateTime.MinValue;
      _span = TimeSpan.FromMinutes(1);

      _instrument = new InstrumentModel
      {
        Name = _asset,
        TimeFrame = _span
      };

      var account = new AccountModel
      {
        Name = _account,
        Balance = 50000,
        InitialBalance = 50000,
        Instruments = new NameCollection<string, IInstrumentModel> { [_asset] = _instrument }
      };

      var gateway = new TimeGatewayClient(_instrument)
      {
        Name = _account,
        Account = account,
        Evaluate = Parse,
        Source = ConfigurationManager.AppSettings["DataLocation"].ToString()
      };

      _imbalanceIndicator = new ImbalanceIndicator { Name = "Imbalance" };
      _performanceIndicator = new PerformanceIndicator { Name = "Balance" };

      _disposables.Add(gateway
        .Account
        .Instruments
        .Values
        .Select(o => o.PointGroups.ItemStream)
        .Merge()
        .Subscribe(OnData));

      CreateCharts();
      CreateGateways(gateway);

      return Task.FromResult(0);
    }

    protected void OnData(ITransactionMessage<IPointModel> message)
    {
      var point = message.Next;
      var account = point.Account;
      var instrument = point.Account.Instruments[_asset];
      var points = instrument.PointGroups;
      var volumes = _imbalanceIndicator.Calculate(points).Values;
      var performanceIndicator = _performanceIndicator.Calculate(points, Gateways.Select(o => o.Account));

      if (points.Count > 1 && IsNextPoint(point))
      {
        var currentPoint = points.ElementAt(points.Count - 1);
        var previousPoint = points.ElementAt(points.Count - 2);
        var currentVolume = volumes.ElementAt(volumes.Count - 1).Last;
        var previousVolume = volumes.ElementAt(volumes.Count - 2).Last;
        var isLong = currentVolume > 0 && previousVolume > 0 && previousPoint.Bar.Close > previousPoint.Bar.Open;
        var isShort = currentVolume < 0 && previousVolume < 0 && previousPoint.Bar.Close < previousPoint.Bar.Open;

        if (account.ActiveOrders.Count == 0 && account.ActivePositions.Count == 0)
        {
          if (isLong) CreateOrder(point, TransactionTypeEnum.Buy, 1);
          if (isShort) CreateOrder(point, TransactionTypeEnum.Sell, 1);
        }

        if (account.ActivePositions.Count > 0)
        {
          var activePosition = account.ActivePositions.Last();

          switch (activePosition.Type)
          {
            case TransactionTypeEnum.Buy: if (isShort) CreateOrder(point, TransactionTypeEnum.Sell, activePosition.Size.Value + 1); break;
            case TransactionTypeEnum.Sell: if (isLong) CreateOrder(point, TransactionTypeEnum.Buy, activePosition.Size.Value + 1); break;
          }
        }
      }
    }

    /// <summary>
    /// Next bar event
    /// </summary>
    /// <param name="pointModel"></param>
    /// <returns></returns>
    protected bool IsNextPoint(IPointModel pointModel)
    {
      if (Equals(pointModel.Time, _date) == false)
      {
        _date = pointModel.Time;

        return true;
      }

      return false;
    }

    /// <summary>
    /// Helper method to send orders
    /// </summary>
    /// <param name="point"></param>
    /// <param name="side"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    protected ITransactionOrderModel CreateOrder(IPointModel point, TransactionTypeEnum side, double size)
    {
      var gateway = point.Account.Gateway;
      var instrument = point.Account.Instruments[_asset];
      var order = new TransactionOrderModel
      {
        Size = size,
        Type = side,
        Instrument = instrument
      };

      //switch (side)
      //{
      //  case TransactionTypeEnum.Buy:

      //    order.Orders.Add(new TransactionOrderModel
      //    {
      //      Size = size,
      //      Type = TransactionTypeEnum.SellLimit,
      //      Price = point.Ask + 2,
      //      Instrument = instrument,
      //      Container = order
      //    });

      //    order.Orders.Add(new TransactionOrderModel
      //    {
      //      Size = size,
      //      Type = TransactionTypeEnum.SellStop,
      //      Price = point.Bid - 2,
      //      Instrument = instrument,
      //      Container = order
      //    });

      //    break;

      //  case TransactionTypeEnum.Sell:

      //    order.Orders.Add(new TransactionOrderModel
      //    {
      //      Size = size,
      //      Type = TransactionTypeEnum.SellStop,
      //      Price = point.Bid + 2,
      //      Instrument = instrument,
      //      Container = order
      //    });

      //    order.Orders.Add(new TransactionOrderModel
      //    {
      //      Size = size,
      //      Type = TransactionTypeEnum.BuyLimit,
      //      Price = point.Ask - 2,
      //      Instrument = instrument,
      //      Container = order
      //    });

      //    break;
      //}

      gateway.OrderSenderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
      {
        Action = ActionEnum.Create,
        Next = order
      });

      _date = point.Time;

      return order;
    }

    /// <summary>
    /// Define what gateways will be used
    /// </summary>
    protected void CreateGateways(IGatewayModel gateway)
    {
      Gateways.Add(gateway);
    }

    /// <summary>
    /// Define what entites will be displayed on the chart
    /// </summary>
    protected void CreateCharts()
    {
      _instrument.ChartData.Area = "Chart";
      _instrument.ChartData.Name = _instrument.Name;
      _instrument.ChartData.Shape = nameof(ShapeEnum.Candle);

      _imbalanceIndicator.ChartData.Area = "Volume";
      _imbalanceIndicator.ChartData.Name = _imbalanceIndicator.Name;
      _imbalanceIndicator.ChartData.Shape = nameof(ShapeEnum.Bar);

      _performanceIndicator.ChartData.Area = "Performance";
      _performanceIndicator.ChartData.Name = _performanceIndicator.Name;
      _performanceIndicator.ChartData.Shape = nameof(ShapeEnum.Area);

      var deals = new ChartDataModel
      {
        Name = "Transactions",
        Area = _instrument.ChartData.Area,
        Shape = nameof(ShapeEnum.Arrow)
      };

      Charts = new IndexCollection<IChartModel>
      {
        new ChartModel
        {
          Name = _instrument.ChartData.Area,
          ChartData = new NameCollection<string, IChartDataModel> { _instrument.ChartData, deals }
        },
        new ChartModel
        {
          Center = 0,
          Name = _imbalanceIndicator.ChartData.Area,
          ChartData = new NameCollection<string, IChartDataModel> { _imbalanceIndicator.ChartData }
        },
        new ChartModel
        {
          Name = _performanceIndicator.ChartData.Area,
          ChartData = new NameCollection<string, IChartDataModel> { _performanceIndicator.ChartData }
        }
      };
    }
  }
}
