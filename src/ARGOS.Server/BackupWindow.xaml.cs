using System.Windows;
using Microsoft.Win32;

namespace ARGOS.Server;
public partial class BackupWindow : Window
{
    private bool busy;
    public BackupWindow()
    {
        InitializeComponent();
        Closing += (_, e) => { if (busy) e.Cancel = true; };
    }
    private void ChooseDestination(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog();
        if (picker.ShowDialog(this) == true) Destination.Text = picker.FolderName;
    }
    private void ChooseBin(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog();
        if (picker.ShowDialog(this) == true) Bin.Text = picker.FolderName;
    }
    private async void CreateBackup(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (Consent.IsChecked != true) { MessageBox.Show(this, "Confirme la base y el destino protegido."); return; }
        string connection;
        try { connection = DatabaseConfiguration.Build(Host.Text, Port.Text, Database.Text, User.Text, Password.Password); }
        catch (Exception error) { MessageBox.Show(this, error.Message); return; }
        if (RecoveryPassphrase.Password.Length < 12 || RecoveryPassphrase.Password != RecoveryPassphraseConfirmation.Password)
        {
            MessageBox.Show(this, "Indique y confirme una clave de recuperación de al menos 12 caracteres. ARGOS no la guardará.");
            return;
        }
        if (MessageBox.Show(this, $"Crear copia de {Database.Text.Trim()} en {Destination.Text}? No se restaurará ni modificará la base.", "Confirmar respaldo", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        busy = true; Fields.IsEnabled = CreateButton.IsEnabled = false;
        Result.Text = "Creando y comprobando el archivo. No cierre el programa…";
        try
        {
            var folder = await DatabaseBackup.CreateAsync(connection, Bin.Text.Trim(), Destination.Text.Trim(), RecoveryPassphrase.Password);
            Result.Text = "Respaldo cifrado y listado correctamente en: " + folder + "\nConserve la clave de recuperación: no se puede restaurar sin ella.";
            MessageBox.Show(this, Result.Text, "ARGOS · Respaldo creado");
        }
        catch { Result.Text = "No se completó el respaldo. Revise conexión, permisos, herramientas y espacio. Los archivos parciales no son respaldos válidos."; MessageBox.Show(this, Result.Text); }
        finally { Password.Clear(); RecoveryPassphrase.Clear(); RecoveryPassphraseConfirmation.Clear(); busy = false; Fields.IsEnabled = CreateButton.IsEnabled = true; Result.BringIntoView(); }
    }
}
