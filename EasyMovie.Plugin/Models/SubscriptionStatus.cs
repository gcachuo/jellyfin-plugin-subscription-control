using System;

namespace EasyMovie.Plugin.Models;

public sealed class SubscriptionStatus
{
    public string Status { get; set; } = "active";
    public string? ExpirationDate { get; set; }
    public int? DaysUntilExpiration { get; set; }
    public string? Email { get; set; }
    public bool IsTrial { get; set; }
    public int? SubscriptionDuration { get; set; }
    public bool FailSafe { get; set; }
    public bool Cached { get; set; }
    public string? Error { get; set; }

    public bool IsExpired => Status.Equals("expired", StringComparison.OrdinalIgnoreCase);
    public bool IsExpiring => Status.Equals("expiring", StringComparison.OrdinalIgnoreCase);
    public bool IsCourtesy => Status.Equals("courtesy", StringComparison.OrdinalIgnoreCase);
}
