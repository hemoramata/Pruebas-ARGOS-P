using System.Windows;
using Microsoft.Win32;

namespace ARGOS.Server;

public partial class ScheduleBackupWindow : Window
{
    public ScheduleBackupWindow() => InitializeComponent();

    private void ChooseBin(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog();
        if (picker.ShowDialog(this) == true) Bin.Text = picker.FolderName;
    }
    private void ChooseDestination(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog();
        if (picker.ShowDialog(this) == true) Destination.Text = picker.FolderName;
    }
    private void CreateSchedule(object sender, RoutedEventArgs e)
    {
        if (Consent.IsChecked != true) { MessageBox.Show(this, "Confirme que conservará la clave de recuperación."); return; }
        if (!TimeOnly.TryParse(Time.Text.Trim(), out var time)) { MessageBox.Show(this, "Indique una hora válida, por ejemplo 21:30."); return; }
        if (RecoveryPassphrase.Password != RecoveryConfirmation.Password) { MessageBox.Show(this, "La confirmación de la clave de recuperación no coincide."); return; }
        try
        {
            var connection = DatabaseConfiguration.Build(Host.Text, Port.Text, Database.Text, User.Text, Password.Password);
            var request = new ScheduledBackupRequest(ScheduleName.Text, connection, Bin.Text.Trim(), Destination.Text.Trim(), RecoveryPassphrase.Password, time);
            var config = ScheduledBackup.Configure(request, Environment.ProcessPath!);
            Password.Clear(); RecoveryPassphrase.Clear(); RecoveryConfirmation.Clear();
            Result.Text = "Respaldo diario programado. Windows conservará la configuración cifrada en este equipo. Archivo protegido: " + config;
            MessageBox.Show(this, "El respaldo diario quedó programado. Puede cerrar esta ventana.", "ARGOS · Respaldo diario");
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, "ARGOS · Respaldo diario", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
