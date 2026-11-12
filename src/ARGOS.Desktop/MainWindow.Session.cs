using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Threading;

namespace ARGOS.Desktop;

public partial class MainWindow
{
    private readonly DispatcherTimer sessionTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool sessionActive;
    private bool sessionRequest;
    private int sessionGeneration;
    private long idleDeadline;
    private long absoluteDeadline;
    private long lastSessionCheck;
    private long lastActivityReport;

    private void InitializeSessionWatch()
    {
        sessionTimer.Tick += async (_, _) =>
        {
            if (!sessionActive) return;
            var remaining = Math.Min(idleDeadline, absoluteDeadline) - Environment.TickCount64;
            if (remaining <= 0) { LockSession(); return; }
            if (remaining <= 120_000)
                LoginStatus.Text = $"La sesión se bloqueará en {Math.Ceiling(remaining / 1000d)} segundos. "
                    + (absoluteDeadline <= idleDeadline ? "Debe renovar el acceso." : "Interactúe con ARGOS para continuar.");
            if (Environment.TickCount64 - lastSessionCheck >= 15_000) await CheckSessionAsync(false);
        };
        PreviewKeyDown += async (_, _) => await ReportInteractionAsync();
        PreviewMouseDown += async (_, _) => await ReportInteractionAsync();
        PreviewMouseWheel += async (_, _) => await ReportInteractionAsync();
        PreviewTouchDown += async (_, _) => await ReportInteractionAsync();
    }

    private async Task StartSessionWatchAsync()
    {
        sessionGeneration++;
        sessionActive = true;
        var now = Environment.TickCount64;
        idleDeadline = now + 30 * 60_000;
        absoluteDeadline = now + 8 * 60 * 60_000;
        lastActivityReport = now;
        sessionTimer.Start();
        await CheckSessionAsync(false);
    }

    private void StopSessionWatch()
    {
        sessionActive = false;
        sessionGeneration++;
        sessionTimer.Stop();
    }

    private async Task ReportInteractionAsync()
    {
        if (!sessionActive || !IsActive) return;
        var now = Environment.TickCount64;
        if (now >= Math.Min(idleDeadline, absoluteDeadline)) { LockSession(); return; }
        // Only actual input sends activity. Status polling never extends the server deadline.
        if (now - lastActivityReport < 10_000) return;
        await CheckSessionAsync(true);
    }

    private async Task CheckSessionAsync(bool activity)
    {
        if (!sessionActive || sessionRequest || loginServer is null) return;
        sessionRequest = true;
        var generation = sessionGeneration;
        var started = Environment.TickCount64;
        lastSessionCheck = started;
        if (activity) lastActivityReport = started;
        try
        {
            using var response = activity
                ? await client.PostAsync(new Uri(loginServer, "api/session/activity"), null, lifetime.Token)
                : await client.GetAsync(new Uri(loginServer, "api/session/status"), lifetime.Token);
            if (generation != sessionGeneration || !sessionActive) return;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            { LockSession(); return; }
            response.EnsureSuccessStatusCode();
            var remaining = await response.Content.ReadFromJsonAsync<RemainingSession>(lifetime.Token);
            if (generation != sessionGeneration || !sessionActive) return;
            if (remaining is null || !double.IsFinite(remaining.IdleSeconds) || !double.IsFinite(remaining.AbsoluteSeconds)
                || remaining.IdleSeconds <= 0 || remaining.AbsoluteSeconds <= 0)
            { LockSession(); return; }
            idleDeadline = started + (long)(Math.Min(1800, remaining.IdleSeconds) * 1000);
            absoluteDeadline = Math.Min(absoluteDeadline, started + (long)(Math.Min(28800, remaining.AbsoluteSeconds) * 1000));
            if (activity) LoginStatus.Text = "Sesión activa.";
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            if (generation == sessionGeneration && sessionActive)
                LoginStatus.Text = "No se pudo comprobar la sesión. Se mantienen los límites de seguridad.";
        }
        finally { sessionRequest = false; }
    }

    private void LockSession()
    {
        StopSessionWatch();
        // Discard local credentials even when the server is unreachable.
        if (loginServer is not null)
            foreach (Cookie cookie in sessionHandler.CookieContainer.GetCookies(loginServer)) cookie.Expired = true;
        Password.Clear();
        CurrentPassword.Clear();
        NewPassword.Clear();
        VerificationCode.Clear();
        Identification.Clear();
        challengeId = challengeBinding = Guid.Empty;
        SessionName.Text = "Sesión bloqueada";
        CredentialsPanel.Visibility = Visibility.Visible;
        CodePanel.Visibility = Visibility.Collapsed;
        PasswordChangePanel.Visibility = Visibility.Collapsed;
        DiscardCapturedDocument();
        DocumentPanel.Visibility = Visibility.Collapsed;
        CaseFilePanel.Visibility = Visibility.Collapsed;
        LogoutButton.Visibility = Visibility.Collapsed;
        ServerAddress.IsEnabled = CheckButton.IsEnabled = true;
        LoginStatus.Text = "Sesión bloqueada por inactividad, vencimiento o revocación. Inicie sesión nuevamente.";
    }

    private sealed record RemainingSession(double IdleSeconds, double AbsoluteSeconds);
}
