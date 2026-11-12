using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ARGOS.Desktop;

internal sealed record CapturedDocument(string Name, string MimeType, byte[] Content, string Hash, string SuggestedClass, string RecognizedText, BitmapSource? Preview);

internal static class DocumentCapture
{
    private const int MaxBytes = 25 * 1024 * 1024;

    public static async Task<CapturedDocument?> ImportAsync()
    {
        var picker = new OpenFileDialog { Filter = "Documentos compatibles|*.pdf;*.png;*.jpg;*.jpeg|PDF|*.pdf|Imágenes|*.png;*.jpg;*.jpeg", CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog() != true) return null;
        await using var stream = new FileStream(picker.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length == 0 || stream.Length > MaxBytes) throw new InvalidOperationException("El archivo debe pesar entre 1 byte y 25 MB.");
        var content = new byte[stream.Length];
        await stream.ReadExactlyAsync(content);
        return await AnalyzeAsync(Path.GetFileName(picker.FileName), content);
    }

    public static async Task<CapturedDocument> ScanAsync()
    {
        var dialogType = Type.GetTypeFromProgID("WIA.CommonDialog") ?? throw new InvalidOperationException("Windows no tiene disponible el componente de escaneo WIA.");
        dynamic dialog = Activator.CreateInstance(dialogType) ?? throw new InvalidOperationException("No se pudo iniciar el selector de escáner.");
        dynamic image = dialog.ShowAcquireImage();
        if (image is null) throw new OperationCanceledException();
        dynamic file = image.FileData;
        byte[] content = (byte[])file.BinaryData;
        if (content.Length == 0 || content.Length > MaxBytes) throw new InvalidOperationException("La imagen escaneada debe pesar entre 1 byte y 25 MB.");
        return await AnalyzeAsync("escaneo.jpg", content);
    }

    private static async Task<CapturedDocument> AnalyzeAsync(string name, byte[] content)
    {
        var mime = DetectMime(content) ?? throw new InvalidOperationException("Solo se admiten PDF, PNG y JPEG válidos.");
        var hash = Convert.ToHexString(SHA256.HashData(content));
        var preview = mime == "application/pdf" ? null : CreatePreview(content);
        var text = mime == "application/pdf" ? "OCR pendiente para PDF. El archivo se clasificó por sus metadatos; revíselo antes de continuar." : await RecognizeImageAsync(content);
        return new CapturedDocument(name, mime, content, hash, SuggestClass(name, text), text, preview);
    }

    private static string? DetectMime(byte[] bytes) => bytes.Length switch
    {
        >= 5 when bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D => "application/pdf",
        >= 8 when bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A => "image/png",
        >= 3 when bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF => "image/jpeg",
        _ => null
    };

    private static BitmapSource CreatePreview(byte[] content)
    {
        using var memory = new MemoryStream(content, writable: false);
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = memory; image.DecodePixelWidth = 900; image.EndInit(); image.Freeze();
        return image;
    }

    private static async Task<string> RecognizeImageAsync(byte[] content) => await Task.Run(async () =>
    {
        try
        {
            var script = "Add-Type -AssemblyName System.Runtime.WindowsRuntime;" +
                "$bytes=[Convert]::FromBase64String([Console]::In.ReadToEnd());" +
                "$stream=New-Object Windows.Storage.Streams.InMemoryRandomAccessStream;" +
                "$writer=New-Object Windows.Storage.Streams.DataWriter($stream);$writer.WriteBytes($bytes);$writer.StoreAsync().AsTask().Wait();$stream.Seek(0);" +
                "$decoder=[Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream).AsTask().Result;$bitmap=$decoder.GetSoftwareBitmapAsync().AsTask().Result;" +
                "$lang=New-Object Windows.Globalization.Language('es-CR');$engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($lang);if($null -eq $engine){$engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()};" +
                "if($null -eq $engine){exit 3};$result=$engine.RecognizeAsync($bitmap).AsTask().Result;[Console]::Out.Write($result.Text)";
            var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            using var process = new Process { StartInfo = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -EncodedCommand " + encodedScript)
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            process.Start();
            await process.StandardInput.WriteAsync(Convert.ToBase64String(content)); process.StandardInput.Close();
            var output = await process.StandardOutput.ReadToEndAsync(); var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) return "OCR no disponible en esta instalación de Windows.";
            return string.IsNullOrWhiteSpace(output) ? "No se pudo reconocer texto. Revise la calidad del escaneo." : output.Trim();
        }
        catch (Exception) { return "No se pudo ejecutar OCR. Revise la calidad del escaneo y la instalación de idiomas de Windows."; }
    });

    private static string SuggestClass(string name, string text)
    {
        var value = (name + " " + text).ToUpperInvariant();
        if (value.Contains("CÉDULA") || value.Contains("CEDULA") || value.Contains("DIMEX") || value.Contains("PASAPORTE")) return "Documento de identidad";
        if (value.Contains("FACTURA") || value.Contains("RECIBO") || value.Contains("COMPROBANTE")) return "Comprobante / recibo";
        if (value.Contains("OFICIO") || value.Contains("RESOLUCIÓN") || value.Contains("RESOLUCION")) return "Resolución / oficio";
        if (value.Contains("FORMULARIO") || value.Contains("SOLICITUD")) return "Formulario institucional";
        return "Documento general";
    }
}
