using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class Approval
{
    public Guid Id { get; set; }

    public Guid RequisitionId { get; set; }

    public Guid ApproverId { get; set; }

    public string Decision { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime DecidedAt { get; set; }

    public virtual Requisition Requisition { get; set; } = null!;
}
