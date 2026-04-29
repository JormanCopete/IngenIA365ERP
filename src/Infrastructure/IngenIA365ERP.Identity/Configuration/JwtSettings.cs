namespace IngenIA365ERP.Identity.Configuration;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string PrivateKeyPath { get; set; } = "Keys/dev_private.pem";
    public string PublicKeyPath { get; set; } = "Keys/dev_public.pem";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
    public string Issuer { get; set; } = "IngenIA365ERP";
    public string Audience { get; set; } = "IngenIA365ERP.Clients";
}
