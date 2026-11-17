using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class Product
{
    public Guid Id { get; set; }

    public Guid SupplierId { get; set; }

    public string Sku { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();

    public virtual ICollection<RequisitionLine> RequisitionLines { get; set; } = new List<RequisitionLine>();

    public virtual Supplier Supplier { get; set; } = null!;
}
