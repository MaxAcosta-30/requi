using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class SupplierNotification
{
    public Guid Id { get; set; }

    public Guid SupplierId { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;
}
