using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class RequisitionLine
{
    public Guid Id { get; set; }

    public Guid RequisitionId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Requisition Requisition { get; set; } = null!;
}
