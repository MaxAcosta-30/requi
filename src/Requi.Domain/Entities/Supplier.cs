using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class Supplier
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<Requisition> Requisitions { get; set; } = new List<Requisition>();

    public virtual ICollection<SupplierNotification> SupplierNotifications { get; set; } = new List<SupplierNotification>();
}
