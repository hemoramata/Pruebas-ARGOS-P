using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ARGOS.Desktop;

// Isolated visual fixture. No server, account, database or production login bypass.
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new Application();
        var warning = Create("ARGOS · PRUEBA · Aviso de inactividad", 820, 620, 115_000);
        var blocked = Create("ARGOS · PRUEBA · Bloqueo sin conexión", 1080, 720, -1);
        warning.Show();
        blocked.Show();
        var check = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        check.Tick += (_, _) =>
        {
            check.Stop();
            var failures = new List<string>();
            if (!Control<TextBlock>(warning, "LoginStatus").Text.Contains("Interactúe con ARGOS")) failures.Add("Missing warning");
            if (Control<TextBlock>(blocked, "SessionName").Text != "Sesión bloqueada") failures.Add("Missing lock");
            if (Control<PasswordBox>(blocked, "Password").Password.Length != 0
                || Control<PasswordBox>(blocked, "VerificationCode").Password.Length != 0
                || Control<TextBox>(blocked, "Identification").Text.Length != 0) failures.Add("Credentials retained");
            if (Control<StackPanel>(blocked, "CredentialsPanel").Visibility != Visibility.Visible
                || Control<StackPanel>(blocked, "CodePanel").Visibility != Visibility.Collapsed) failures.Add("Wrong lock panels");
            var handler = (HttpClientHandler)Field("sessionHandler").GetValue(blocked)!;
            if (handler.CookieContainer.GetCookies(new Uri("https://argos-visual.invalid/")).Count != 0) failures.Add("Cookie retained");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "visual-check-results.txt"),
                failures.Count == 0 ? "PASS: warning, offline lock, identity/password/code clearing, login panels and cookie removal." : string.Join(Environment.NewLine, failures));
        };
        check.Start();
        app.Run();
    }

    private static MainWindow Create(string title, double width, double height, long idleMilliseconds)
    {
        var window = new MainWindow { Title = title, Width = width, Height = height };
        Control<StackPanel>(window, "CredentialsPanel").Visibility = Visibility.Collapsed;
        Control<StackPanel>(window, "CodePanel").Visibility = Visibility.Collapsed;
        Control<TextBlock>(window, "SessionName").Text = "Persona de prueba";
        Control<TextBox>(window, "Identification").Text = "TEST-ONLY";
        Control<PasswordBox>(window, "Password").Password = "TEST-ONLY";
        Control<PasswordBox>(window, "VerificationCode").Password = "123456";
        Field("sessionActive").SetValue(window, true);
        Field("sessionRequest").SetValue(window, true); // Never perform network requests from this fixture.
        Field("loginServer").SetValue(window, new Uri("https://argos-visual.invalid/"));
        Field("idleDeadline").SetValue(window, Environment.TickCount64 + idleMilliseconds);
        Field("absoluteDeadline").SetValue(window, Environment.TickCount64 + 28_800_000);
        var handler = (HttpClientHandler)Field("sessionHandler").GetValue(window)!;
        handler.CookieContainer.Add(new Uri("https://argos-visual.invalid/"), new Cookie("ARGOS.Session", "TEST-ONLY", "/"));
        ((DispatcherTimer)Field("sessionTimer").GetValue(window)!).Start();
        return window;
    }

    private static FieldInfo Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static T Control<T>(MainWindow window, string name) where T : FrameworkElement => (T)window.FindName(name);
}
