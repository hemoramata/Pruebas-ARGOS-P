using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;

namespace ARGOS.Desktop;

public partial class MainWindow : Window
{
    private Uri? loginServer;
    private Guid challengeId;
    private Guid challengeBinding;

    private async void BeginLogin(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(ServerAddress.Text.Trim(), UriKind.Absolute, out var address)
            || address.Scheme != Uri.UriSchemeHttps || address.UserInfo.Length != 0
            || address.Query.Length != 0 || address.Fragment.Length != 0)
        {
            Password.Clear();
            LoginStatus.Text = "El inicio de sesión requiere una dirección HTTPS institucional válida.";
            return;
        }
        loginServer = new Uri(address.AbsoluteUri.TrimEnd('/') + "/");
        LoginButton.IsEnabled = false;
        ServerAddress.IsEnabled = false;
        CheckButton.IsEnabled = false;
        try
        {
            var input = new { Identification = Identification.Text.Trim(), Password = Password.Password };
            Password.Clear();
            using var response = await client.PostAsJsonAsync(new Uri(loginServer, "api/session/begin"), input, lifetime.Token);
            if (!response.IsSuccessStatusCode)
            {
                LoginStatus.Text = "No se pudo iniciar el acceso. Verifique sus datos o consulte con TI si su cuenta y el servicio están habilitados.";
                return;
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(lifetime.Token));
            challengeId = json.RootElement.GetProperty("id").GetGuid();
            challengeBinding = json.RootElement.GetProperty("binding").GetGuid();
            DeliveryDestination.Text = "Código enviado a " + json.RootElement.GetProperty("maskedDestination").GetString();
            CredentialsPanel.Visibility = Visibility.Collapsed;
            CodePanel.Visibility = Visibility.Visible;
            LogoutButton.Visibility = Visibility.Visible;
            LoginStatus.Text = "Introduzca el código recibido. Tiene vigencia limitada.";
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        { LoginStatus.Text = "No se pudo completar el acceso. Revise la conexión con TI."; }
        finally
        {
            LoginButton.IsEnabled = true;
            if (challengeId == Guid.Empty) { ServerAddress.IsEnabled = true; CheckButton.IsEnabled = true; }
        }
    }

    private async void VerifyLogin(object sender, RoutedEventArgs e)
    {
        if (loginServer is null || challengeId == Guid.Empty) return;
        VerifyButton.IsEnabled = false;
        LogoutButton.IsEnabled = false;
        try
        {
            var input = new { Id = challengeId, Binding = challengeBinding, Code = VerificationCode.Password };
            VerificationCode.Clear();
            using var response = await client.PostAsJsonAsync(new Uri(loginServer, "api/session/verify"), input, lifetime.Token);
            if (!response.IsSuccessStatusCode)
            { LoginStatus.Text = "Código no aceptado, vencido o límite alcanzado. Puede cancelar y volver a iniciar."; return; }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(lifetime.Token));
            SessionName.Text = json.RootElement.GetProperty("fullName").GetString();
            var mustChange = json.RootElement.TryGetProperty("mustChangePassword", out var change)
                && change.GetBoolean();
            CodePanel.Visibility = Visibility.Collapsed;
            challengeId = Guid.Empty;
            challengeBinding = Guid.Empty;
            Identification.Clear();
            PasswordChangePanel.Visibility = mustChange ? Visibility.Visible : Visibility.Collapsed;
            DocumentPanel.Visibility = mustChange ? Visibility.Collapsed : Visibility.Visible;
            LoginStatus.Text = mustChange
                ? "Por seguridad debe establecer una nueva clave antes de continuar."
                : "Sesión iniciada. Los módulos de trabajo todavía están en construcción.";
            await StartSessionWatchAsync();
            if (!mustChange) await LoadDocumentCategoriesAsync();
            if (!mustChange) await LoadCaseFilesAsync();
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        { LoginStatus.Text = "No se pudo confirmar la sesión. Cancele y vuelva a iniciar."; }
        finally { VerifyButton.IsEnabled = true; LogoutButton.IsEnabled = true; }
    }

    private async void Logout(object sender, RoutedEventArgs e)
    {
        StopSessionWatch();
        LogoutButton.IsEnabled = false;
        try
        {
            if (loginServer is not null)
            {
                if (challengeId != Guid.Empty)
                {
                    using var cancelled = await client.PostAsJsonAsync(new Uri(loginServer, "api/session/cancel"),
                        new { Id = challengeId, Binding = challengeBinding }, lifetime.Token);
                    cancelled.EnsureSuccessStatusCode();
                }
                using var response = await client.PostAsync(new Uri(loginServer, "api/session/logout"), null, lifetime.Token);
                response.EnsureSuccessStatusCode();
            }
            challengeId = Guid.Empty;
            challengeBinding = Guid.Empty;
            Password.Clear();
            CurrentPassword.Clear();
            NewPassword.Clear();
            VerificationCode.Clear();
            Identification.Clear();
            SessionName.Text = "Sin sesión iniciada";
            CredentialsPanel.Visibility = Visibility.Visible;
            CodePanel.Visibility = Visibility.Collapsed;
            PasswordChangePanel.Visibility = Visibility.Collapsed;
            DiscardCapturedDocument();
            DocumentPanel.Visibility = Visibility.Collapsed;
            CaseFilePanel.Visibility = Visibility.Collapsed;
            LogoutButton.Visibility = Visibility.Collapsed;
            ServerAddress.IsEnabled = true;
            CheckButton.IsEnabled = true;
            LoginStatus.Text = "Acceso cerrado.";
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            LockSession();
            LoginStatus.Text = "Acceso local cerrado. No se pudo confirmar el cierre en el servidor.";
        }
        finally { LogoutButton.IsEnabled = true; }
    }

    private async void ChangePassword(object sender, RoutedEventArgs e)
    {
        if (loginServer is null) return;
        ChangePasswordButton.IsEnabled = false;
        try
        {
            var input = new { CurrentPassword = CurrentPassword.Password, NewPassword = NewPassword.Password };
            CurrentPassword.Clear(); NewPassword.Clear();
            using var response = await client.PostAsJsonAsync(new Uri(loginServer, "api/session/password/change"),
                input, lifetime.Token);
            if (!response.IsSuccessStatusCode)
            { LoginStatus.Text = "La clave no cumple la política o la clave actual no es válida."; return; }
            LockSession();
            LoginStatus.Text = "Clave actualizada. Inicie sesión con su nueva clave y el doble factor.";
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        { LoginStatus.Text = "No se pudo actualizar la clave. Revise la conexión con TI."; }
        finally { ChangePasswordButton.IsEnabled = true; }
    }
    private readonly CancellationTokenSource lifetime = new();
    private readonly HttpClientHandler sessionHandler = new() { AllowAutoRedirect = false };
    private readonly HttpClient client;
    private static HttpClient CreateClient(HttpClientHandler handler) => new(handler)
    {
        Timeout = TimeSpan.FromSeconds(25),
        MaxResponseContentBufferSize = 16384
    };

    public MainWindow()
    {
        InitializeComponent();
        client = CreateClient(sessionHandler);
        InitializeSessionWatch();
        Closed += (_, _) =>
        {
            lifetime.Cancel();
            StopSessionWatch();
            DiscardCapturedDocument();
            client.Dispose();
        };
    }

    private async void CheckConnection(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(ServerAddress.Text.Trim(), UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttps
                && !(address.Scheme == Uri.UriSchemeHttp && address.IsLoopback))
            || !string.IsNullOrEmpty(address.UserInfo)
            || !string.IsNullOrEmpty(address.Query)
            || !string.IsNullOrEmpty(address.Fragment))
        {
            ConnectionStatus.Text = "Use HTTPS sin credenciales ni parámetros. HTTP solo se permite en este equipo para desarrollo.";
            return;
        }

        CheckButton.IsEnabled = false;
        ServerAddress.IsEnabled = false;
        ConnectionStatus.Text = "Comprobando disponibilidad…";
        try
        {
            var healthAddress = new Uri(new Uri(address.AbsoluteUri.TrimEnd('/') + "/"), "health");
            using var response = await client.GetAsync(healthAddress, lifetime.Token);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(lifetime.Token);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var valid = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
                && status.GetString() == "Healthy"
                && root.TryGetProperty("service", out var service) && service.ValueKind == JsonValueKind.String
                && service.GetString() == "ARGOS.API";
            ConnectionStatus.Text = valid
                ? "ARGOS Server está disponible. Esta comprobación no inicia sesión ni concede permisos."
                : "La respuesta no corresponde al servicio de salud esperado de ARGOS.";
        }
        catch (OperationCanceledException)
        {
            ConnectionStatus.Text = "La comprobación se canceló o el servidor tardó demasiado en responder.";
        }
        catch (HttpRequestException)
        {
            ConnectionStatus.Text = "No se pudo confirmar la conexión. Revise la dirección, la red y el certificado con TI.";
        }
        catch (JsonException)
        {
            ConnectionStatus.Text = "El servidor devolvió una respuesta no reconocida.";
        }
        finally
        {
            CheckButton.IsEnabled = true;
            ServerAddress.IsEnabled = true;
        }
    }
}
