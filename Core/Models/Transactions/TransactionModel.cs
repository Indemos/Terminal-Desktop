using Core.EnumSpace;
using FluentValidation;
using System;

namespace Core.ModelSpace
{
  /// <summary>
  /// Generic order model
  /// </summary>
  public interface ITransactionModel : IBaseModel
  {
    /// <summary>
    /// Contract size
    /// </summary>
    double? Size { get; set; }

    /// <summary>
    /// Open price for the order
    /// </summary>
    double? Price { get; set; }

    /// <summary>
    /// Price the makes order active, e.g. stop price for stop limit orders
    /// </summary>
    double? ActivationPrice { get; set; }

    /// <summary>
    /// Parameter that can be used to group a set of transactions
    /// </summary>
    string Group { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    DateTime? Time { get; set; }

    /// <summary>
    /// Transaction type, e.g. withdrawal or order placement
    /// </summary>
    OperationEnum? Operation { get; set; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    OrderStatusEnum? Status { get; set; }

    /// <summary>
    /// Instrument to buy or sell
    /// </summary>
    IInstrumentModel Instrument { get; set; }
  }

  /// <summary>
  /// Generic order model
  /// </summary>
  public class TransactionModel : BaseModel, ITransactionModel
  {
    /// <summary>
    /// Contract size
    /// </summary>
    public virtual double? Size { get; set; }

    /// <summary>
    /// Open price for the order
    /// </summary>
    public virtual double? Price { get; set; }

    /// <summary>
    /// Price the makes order active, e.g. stop price for stop limit orders
    /// </summary>
    public virtual double? ActivationPrice { get; set; }

    /// <summary>
    /// Parameter that can be used to group a set of orders
    /// </summary>
    public virtual string Group { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public virtual DateTime? Time { get; set; }

    /// <summary>
    /// Transaction type, e.g. withdrawal or order placement
    /// </summary>
    public virtual OperationEnum? Operation { get; set; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    public virtual OrderStatusEnum? Status { get; set; }

    /// <summary>
    /// Instrument to buy or sell
    /// </summary>
    public virtual IInstrumentModel Instrument { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public TransactionModel()
    {
      Id = Guid.NewGuid().ToString("N");
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionValidation : AbstractValidator<ITransactionModel>
  {
    public TransactionValidation()
    {
      RuleFor(o => o.Instrument).NotNull().NotEmpty().WithMessage("No instrument");
      RuleFor(o => o.Size).NotNull().NotEqual(0).WithMessage("No size");
    }
  }
}
