using System.Windows;
using Microsoft.Win32;

namespace ARGOS.Server;
public partial class RestoreWindow : Window
{
    private bool busy;
    public RestoreWindow() { InitializeComponent(); Closing += (_, e) => { if (busy) e.Cancel = true; }; }
    private void ChooseBackup(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "Respaldo ARGOS|*.argosbackup", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) BackupFile.Text = picker.FileName;
    }
    private void ChooseBin(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog(); if (picker.ShowDialog(this) == true) Bin.Text = picker.FolderName;
    }
    private async void Restore(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (Consent.IsChecked != true) { MessageBox.Show(this, "Confirme que la base destino es exclusiva de recuperación."); return; }
        string connection;
        try { connection = DatabaseConfiguration.Build(Host.Text, Port.Text, Database.Text, User.Text, Password.Password); }
        catch (Exception error) { MessageBox.Show(this, error.Message); return; }
        if (RecoveryPassphrase.Password.Length < 12) { MessageBox.Show(this, "Indique la clave de recuperación."); return; }
        if (MessageBox.Show(this, "ARGOS escribirá datos en la base destino indicada. La base original no será contactada. ¿Continuar?", "Confirmar restauración", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        busy = true; Fields.IsEnabled = RestoreButton.IsEnabled = false; Result.Text = "Descifrando y restaurando en la base destino…";
        try { await DatabaseBackup.RestoreAsync(BackupFile.Text.Trim(), RecoveryPassphrase.Password, Bin.Text.Trim(), connection); Result.Text = "Restauración completada. Verifique la aplicación en este entorno aislado."; }
        catch { Result.Text = "No se completó la restauración. No use la base como recuperada hasta revisar el respaldo, clave y destino."; }
        finally { Password.Clear(); RecoveryPassphrase.Clear(); busy = false; Fields.IsEnabled = RestoreButton.IsEnabled = true; }
    }
}
