using System;
using System.Collections.Generic;

namespace Requi.Domain.Entities;

public partial class OutboxMessage
{
    public Guid Id { get; set; }

    public string Type { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public DateTime OccurredAt { get; set; }

    public string Status { get; set; } = null!;

    public int Attempts { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
