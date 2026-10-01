using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

internal static class DiagnosticCertificate
{
    internal static X509Certificate2 Create()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        names.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var pfx = generated.Export(X509ContentType.Pkcs12, password);
        try
        {
            // Windows Schannel cannot reliably use the CreateSelfSigned ephemeral key.
            // UserKeySet gives it an OS key container; omitting PersistKeySet makes Dispose delete it.
            // The encrypted PFX never leaves memory; no machine store or trust store is modified here.
            return X509CertificateLoader.LoadPkcs12(pfx, password,
                OperatingSystem.IsWindows() ? X509KeyStorageFlags.UserKeySet : X509KeyStorageFlags.EphemeralKeySet);
        }
        finally { CryptographicOperations.ZeroMemory(pfx); }
    }
}
