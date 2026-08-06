namespace InventorySystem.Services;

public static class BarcodeScannerMessages
{
    public static string ToUserMessage(Exception error) =>
        error switch
        {
            UnauthorizedAccessException =>
                "Windows no tiene permitido el acceso a la cámara. Habilita el acceso en Configuración > Privacidad y seguridad > Cámara.",
            InvalidOperationException invalid when invalid.Message.Contains("ocupada", StringComparison.OrdinalIgnoreCase) =>
                "La cámara está siendo utilizada por otra aplicación.",
            InvalidOperationException invalid when invalid.Message.Contains("desconect", StringComparison.OrdinalIgnoreCase) =>
                "La cámara seleccionada se desconectó. Selecciona otra cámara.",
            InvalidOperationException invalid when invalid.Message.Contains("compatible", StringComparison.OrdinalIgnoreCase) =>
                "La cámara no tiene un formato compatible para lectura.",
            System.Runtime.InteropServices.COMException com when com.HResult == unchecked((int)0x80070020) =>
                "La cámara está siendo utilizada por otra aplicación.",
            _ => "No pudimos iniciar el escáner de cámara. Puedes escribir el código manualmente."
        };
}
