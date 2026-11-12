using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ARGOS.Server;
public partial class InstallWindow : Window
{
    private bool installing;
    private LocalTestCertificate? localCertificate;
    public InstallWindow()
    {
        InitializeComponent();
        var testRoot = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
        if (testRoot != null && Path.GetFileName(testRoot) == "ARGOS_PRUEBAS_20260917")
        {
            var package = Path.Combine(testRoot, "server-package-20260922");
            if (File.Exists(Path.Combine(package, "ARGOS.API.exe"))) Package.Text = package;
            Destination.Text = Path.Combine(testRoot, "Servidor-Pruebas");
        }
        Closing += (_, e) => { if (installing) e.Cancel = true; };
        Closed += (_, _) => localCertificate?.Dispose();
    }
    private void PrepareLocalCertificate(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(Address.Text.Trim(), UriKind.Absolute, out var address)
            || address.Scheme != "https" || address.Host != "localhost"
            || DatabaseName.Text.Trim() != "ARGOS_PRUEBAS_20260917")
        {
            ShowError("Esta opción solo corresponde a https://localhost y la base ARGOS_PRUEBAS_20260917. Para otros entornos seleccione el certificado institucional.");
            return;
        }
        if (MessageBox.Show(this, "Se creará un certificado temporal de 30 días para localhost. No es para producción y no se agregará a los certificados confiables de Windows. ¿Continuar?",
            "Certificado exclusivo de pruebas", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        try
        {
            var next = LocalTestCertificate.Create();
            localCertificate?.Dispose();
            localCertificate = next;
            Certificate.Text = next.Path;
            CertificatePassword.Password = next.Password;
            MessageBox.Show(this, "Certificado y contraseña preparados. No necesita copiar ni recordar esa contraseña. Se guardarán protegidos al instalar. La confianza del cliente todavía debe configurarse antes de probar conexiones HTTPS.", "ARGOS · Certificado preparado");
        }
        catch { ShowError("No se pudo preparar el certificado local. No se cambió la confianza de Windows."); }
    }
    private void SelectPackage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Paquete publicado de ARGOS.API" };
        if (dialog.ShowDialog(this) == true) Package.Text = dialog.FolderName;
    }
    private void SelectDestination(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Carpeta contenedora; se creará una subcarpeta nueva" };
        if (dialog.ShowDialog(this) == true) Destination.Text = Path.Combine(dialog.FolderName, ServiceName.Text.Trim());
    }
    private void SelectCertificate(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Certificado PFX|*.pfx", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) Certificate.Text = dialog.FileName;
    }
    private async void Install(object sender, RoutedEventArgs e)
    {
        if (installing) return;
        if (Confirmed.IsChecked != true) { ShowError("Confirme primero la instalación y la base seleccionada."); return; }
        if (localCertificate != null && Certificate.Text == localCertificate.Path &&
            (DatabaseName.Text.Trim() != "ARGOS_PRUEBAS_20260917" || !Uri.TryCreate(Address.Text.Trim(), UriKind.Absolute, out var localAddress) || localAddress.Host != "localhost"))
        { ShowError("El certificado generado es exclusivo de localhost y la base de pruebas. Revise los datos modificados."); return; }
        if (string.IsNullOrWhiteSpace(Certificate.Text))
        { ShowError("Falta el certificado HTTPS del servidor. Seleccione un archivo PFX válido antes de instalar."); return; }
        string connection;
        try { connection = DatabaseConfiguration.Build(DatabaseHost.Text, DatabasePort.Text, DatabaseName.Text, DatabaseUser.Text, Database.Password); }
        catch (InvalidOperationException error) { ShowError(error.Message); return; }
        var request = new ServerInstallation(ServiceName.Text.Trim(), Package.Text.Trim(), Destination.Text.Trim(),
            Address.Text.Trim(), Certificate.Text.Trim(), CertificatePassword.Password, connection,
            Sender.Text.Trim(), MailPassword.Password);
        try { NativeServerInstaller.Validate(request); }
        catch (Exception error) { ShowError(error.Message); return; }
        if (MessageBox.Show(this, $"Se instalará {request.Name} en {request.Destination}.\nBase: {DatabaseName.Text.Trim()}\nEquipo: {DatabaseHost.Text.Trim()}:{DatabasePort.Text.Trim()}\nNo se iniciará ni modificará la base. ¿Continuar?",
                "Confirmar instalación", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        installing = true; Fields.IsEnabled = InstallButton.IsEnabled = false;
        Result.Text = "Comprobando puerto y conexión PostgreSQL antes de instalar…";
        try
        {
            await Task.Run(() => NativeServerInstaller.InstallAsync(request));
            Result.Text = "Instalado y detenido. Cierre esta ventana para seleccionarlo en el panel e iniciarlo cuando haya revisado certificado, base y puertos.";
        }
        catch (InstallationPreflightException error)
        {
            Fields.IsEnabled = InstallButton.IsEnabled = true;
            ShowError(error.Message);
        }
        catch
        {
            Result.Text = "La instalación no se completó. No reintente sobre la misma carpeta: conserve los archivos para revisión. El servicio no se inició. No se muestran claves ni detalles sensibles.";
            Fields.IsEnabled = InstallButton.IsEnabled = true;
        }
        finally { installing = false; CertificatePassword.Clear(); Database.Clear(); MailPassword.Clear(); }
    }
    private void ShowError(string text)
    {
        Result.Text = text;
        Result.BringIntoView();
        MessageBox.Show(this, text, "ARGOS · Revise la configuración", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
