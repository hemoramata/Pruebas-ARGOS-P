using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;

namespace ARGOS.Server;

public partial class MainWindow : Window
{
    private record ServiceChoice(string Name, string Display);
    private bool busy;
    private readonly CancellationTokenSource lifetime = new();
    private static bool Elevated => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    public MainWindow()
    {
        InitializeComponent();
        Refresh();
        Loaded += OpenRequestedInstaller;
        Closed += (_, _) => lifetime.Cancel();
    }

    private void OpenRequestedInstaller(object sender, RoutedEventArgs e)
    {
        Loaded -= OpenRequestedInstaller;
        if (Environment.GetCommandLineArgs().Contains("--install") && Elevated)
            InstallClick(this, new RoutedEventArgs());
    }

    public static ProcessStartInfo InstallationElevation(string executable)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas" };
        info.ArgumentList.Add("--install");
        return info;
    }

    private void Refresh()
    {
        var selected = (Services.SelectedItem as ServiceChoice)?.Name;
        try
        {
            var all = ServiceController.GetServices();
            try
            {
                var choices = all.Where(s => s.ServiceName.StartsWith("ARGOS-", StringComparison.OrdinalIgnoreCase))
                    .Select(s => new ServiceChoice(s.ServiceName, s.DisplayName + " (" + s.ServiceName + ")")).ToArray();
                Services.ItemsSource = choices;
                Services.IsEnabled = choices.Length > 0;
                Services.SelectedItem = choices.FirstOrDefault(s => s.Name == selected) ?? choices.FirstOrDefault();
            }
            finally { foreach (var service in all) service.Dispose(); }
            ShowStatus();
        }
        catch { Message.Text = "No se pudo consultar los servicios de Windows. Revise los permisos del equipo."; }
    }

    private void ShowStatus()
    {
        ElevateButton.Visibility = Elevated ? Visibility.Collapsed : Visibility.Visible;
        StartButton.IsEnabled = StopButton.IsEnabled = false;
        if (Services.SelectedItem is not ServiceChoice choice)
        {
            Status.Text = "No hay un servicio ARGOS instalado.";
            Message.Text = "Puede seguir utilizando el asistente gráfico de pruebas. La instalación como servicio todavía requiere configurar el certificado HTTPS y el destino del servidor.";
            return;
        }
        try
        {
            using var service = new ServiceController(choice.Name);
            Status.Text = service.Status switch
            {
                ServiceControllerStatus.Running => "Servidor en ejecución",
                ServiceControllerStatus.Stopped => "Servidor detenido",
                _ => "Windows está cambiando el estado del servicio: " + service.Status
            };
            StartButton.IsEnabled = !busy && Elevated && service.Status == ServiceControllerStatus.Stopped;
            StopButton.IsEnabled = !busy && Elevated && service.CanStop && service.Status == ServiceControllerStatus.Running;
        }
        catch { Status.Text = "No se pudo consultar el estado del servicio seleccionado."; }
    }

    private async Task Change(bool start)
    {
        if (busy || !Elevated || Services.SelectedItem is not ServiceChoice choice) return;
        if (!start && MessageBox.Show(this, "Se interrumpirán las sesiones de " + choice.Name + ". ¿Desea detenerlo?",
                "Confirmar detención", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        busy = true;
        Services.IsEnabled = false;
        ShowStatus();
        Message.Text = start ? "Iniciando servidor…" : "Deteniendo servidor…";
        try
        {
            await Task.Run(() =>
            {
                using var service = new ServiceController(choice.Name);
                if (start) service.Start(); else service.Stop();
                service.WaitForStatus(start ? ServiceControllerStatus.Running : ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            });
            Message.Text = start ? "Windows confirmó el inicio. Esto no sustituye una prueba de conexión y funcionamiento." : "Servidor detenido.";
        }
        catch { Message.Text = "Windows no confirmó la operación. Actualice el estado y revise la configuración o los registros del servicio. No se modificó su base de datos."; }
        finally { busy = false; Services.IsEnabled = true; ShowStatus(); }
    }

    private async void StartClick(object sender, RoutedEventArgs e) => await Change(true);
    private void BackupClick(object sender, RoutedEventArgs e) => new BackupWindow { Owner = this }.ShowDialog();
    private void ScheduleBackupClick(object sender, RoutedEventArgs e)
    {
        if (!Elevated)
        {
            MessageBox.Show(this, "La programación se guarda protegida y se ejecuta como servicio de Windows. Autorice el panel y vuelva a pulsar esta opción.", "ARGOS · Autorización requerida", MessageBoxButton.OK, MessageBoxImage.Information);
            ElevateClick(sender, e);
            return;
        }
        new ScheduleBackupWindow { Owner = this }.ShowDialog();
    }
    private void RestoreClick(object sender, RoutedEventArgs e) => new RestoreWindow { Owner = this }.ShowDialog();
    private async void DiagnoseClick(object sender, RoutedEventArgs e)
    {
        DiagnosticButton.IsEnabled = false;
        DiagnosticAddress.IsEnabled = false;
        DiagnosticMessage.Text = "Comprobando conexión segura, sin modificar el servidor…";
        try
        {
            var result = await ServerDiagnostics.CheckAsync(DiagnosticAddress.Text.Trim(), lifetime.Token);
            DiagnosticMessage.Text = result.Message;
            DiagnosticMessage.BringIntoView();
        }
        finally { DiagnosticButton.IsEnabled = true; DiagnosticAddress.IsEnabled = true; }
    }
    private async void StopClick(object sender, RoutedEventArgs e) => await Change(false);
    private void RefreshClick(object sender, RoutedEventArgs e) => Refresh();
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsInitialized) ShowStatus(); }
    private void ElevateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            Close();
        }
        catch { Message.Text = "La autorización no se completó. Puede seguir consultando el estado sin privilegios."; }
    }
    private void OpenAssistant(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Seleccione ARGOS.Setup.exe del entorno de pruebas",
            Filter = "Asistente ARGOS|ARGOS.Setup.exe",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != true) return;
        if (!string.Equals(System.IO.Path.GetFileName(picker.FileName), "ARGOS.Setup.exe", StringComparison.OrdinalIgnoreCase)) return;
        // Do not launch the test assistant with inherited administrator privileges.
        if (Elevated) { Message.Text = "Abra el asistente desde su acceso directo normal, sin permisos de administrador."; return; }
        try { Process.Start(new ProcessStartInfo(picker.FileName) { UseShellExecute = true }); }
        catch { Message.Text = "No se pudo abrir el asistente seleccionado."; }
    }
    private void InstallClick(object sender, RoutedEventArgs e)
    {
        if (!Elevated)
        {
            try
            {
                using var elevated = Process.Start(InstallationElevation(Environment.ProcessPath!));
                if (elevated is null) throw new InvalidOperationException();
                Close();
            }
            catch
            {
                MessageBox.Show(this, "No se completó la autorización de Windows. No se instaló nada. Pulse Instalar servidor para intentarlo nuevamente.",
                    "ARGOS · Autorización requerida", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return;
        }
        try { new InstallWindow { Owner = this }.ShowDialog(); }
        catch
        {
            MessageBox.Show(this, "No se pudo abrir el formulario de instalación. No se inició una instalación.",
                "ARGOS · Instalación", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Refresh();
    }
}
