namespace ARGOS.API;

public enum EnrollmentStage { Preparation, Data, SendingEmail, VerifyingEmail, Saving }

public static class EnrollmentDiagnostics
{
    // Fixed messages only: never print exception details, passwords or verification codes.
    public static string Describe(EnrollmentStage stage) => stage switch
    {
        EnrollmentStage.SendingEmail => "No se pudo enviar el codigo por Gmail. Revise la conexion, el correo destinatario y la contrasena de APLICACION del remitente. Para reemplazar la credencial guardada use -UpdateMailCredential. No se registro la cuenta.",
        EnrollmentStage.VerifyingEmail => "El codigo no pudo verificarse: pudo vencer o agotarse el numero de intentos. Repita el alta para recibir un codigo nuevo. No se registro la cuenta.",
        EnrollmentStage.Saving => "No se pudo confirmar el registro en la base de datos. Revise si ya existe el identificador o documento y si la estructura institucional sigue vigente. Compruebe las cuentas antes de repetir el alta.",
        EnrollmentStage.Data => "Los datos de la designacion no son validos. Revise identidad, identificador, referencias y vigencia. No se registro la cuenta.",
        _ => "No se pudo preparar el alta. Revise la conexion con PostgreSQL y la configuracion institucional publicada."
    };
}
