namespace MiniHR.Data;
public interface ITenantContext { Guid OrgId { get; set; } }
public sealed class TenantContext : ITenantContext
{
    public static readonly Guid DefaultOrgId = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    public const string DefaultApiKey = "demo-hr";
    public const string CookieName = "org_key";
    public Guid OrgId { get; set; } = DefaultOrgId;
}
