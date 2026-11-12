using System.Net.Http;
using System.Net.Security;
using System.Text.Json;

namespace ARGOS.Server;
public sealed record ServerDiagnosticResult(bool Healthy, string Message);
public static class ServerDiagnostics
{
    public static async Task<ServerDiagnosticResult> CheckAsync(string address, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            return new(false, "Indique la dirección HTTPS del servidor, sin rutas ni credenciales.");
        var certificateError = false;
        DateTime? expires = null;
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
        {
            certificateError = errors != SslPolicyErrors.None;
            expires = certificate?.NotAfter.ToUniversalTime();
            // Observe expiry but preserve the full platform validation decision.
            return errors == SslPolicyErrors.None;
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 16384 };
        try
        {
            using var response = await client.GetAsync(new Uri(uri, "health"), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(false, $"El servidor respondió HTTP {(int)response.StatusCode}. Revise el servicio; no se modificó nada.");
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!IsArgosHealth(body)) return new(false, "La respuesta no corresponde al diagnóstico saludable de ARGOS.API.");
            return new(true, "ARGOS responde por HTTPS con certificado válido. " + ExpiryMessage(expires, DateTime.UtcNow)
                + " Esta prueba no verifica correo, permisos ni trámites.");
        }
        catch
        {
            return new(false, certificateError
                ? "El certificado no supera la validación de Windows. Revise confianza, nombre y vigencia; no desactive la seguridad."
                : "No se pudo comprobar el servidor en el tiempo disponible. Revise dirección, puerto y estado del servicio.");
        }
    }
    public static bool IsArgosHealth(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("service", out var service) && service.ValueKind == JsonValueKind.String && service.GetString() == "ARGOS.API"
                && root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String && status.GetString() == "Healthy";
        }
        catch (JsonException) { return false; }
    }
    public static string ExpiryMessage(DateTime? expiry, DateTime now)
    {
        if (expiry is null) return "No se pudo obtener la fecha del certificado.";
        var days = (expiry.Value - now).TotalDays;
        return days <= 0 ? "El certificado está vencido." :
            $"Certificado: vence el {expiry.Value.ToLocalTime():dd/MM/yyyy}. " +
            (days <= 30 ? $"Atención: restan aproximadamente {Math.Ceiling(days)} días; programe la renovación." : "");
    }
}
