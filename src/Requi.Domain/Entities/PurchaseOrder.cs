using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class PurchaseOrder
{
    public Guid Id { get; set; }

    public long Number { get; set; }

    public Guid RequisitionId { get; set; }

    public Guid SupplierId { get; set; }

    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();

    public virtual Requisition Requisition { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual SupplierNotification? SupplierNotification { get; set; }
}
