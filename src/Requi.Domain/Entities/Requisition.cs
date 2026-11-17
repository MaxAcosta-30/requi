using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class Requisition
{
    public Guid Id { get; set; }

    public long Number { get; set; }

    public Guid RequesterId { get; set; }

    public Guid SupplierId { get; set; }

    public string Status { get; set; } = null!;

    public decimal Total { get; set; }

    public string? Notes { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();

    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    public virtual ICollection<RequisitionLine> RequisitionLines { get; set; } = new List<RequisitionLine>();

    public virtual Supplier Supplier { get; set; } = null!;
}
