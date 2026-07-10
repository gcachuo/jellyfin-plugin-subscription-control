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
    public PlanInfo? Plan { get; set; }
    public bool TestMode { get; set; }
    public string[] TestUsers { get; set; } = Array.Empty<string>();

    public bool IsExpired => Status.Equals("expired", StringComparison.OrdinalIgnoreCase);
    public bool IsExpiring => Status.Equals("expiring", StringComparison.OrdinalIgnoreCase);
    public bool IsCourtesy => Status.Equals("courtesy", StringComparison.OrdinalIgnoreCase);
}

public sealed class PlanInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool EnableAllFolders { get; set; }
    public string[]? EnabledFolderIds { get; set; }
    public bool AllowLiveTv { get; set; }
}
