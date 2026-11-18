using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class AuditEvent
{
    public Guid Id { get; set; }

    public DateTime OccurredAt { get; set; }

    public Guid ActorId { get; set; }

    public string Action { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    public string? Data { get; set; }
}
