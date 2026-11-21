using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Requi.IntegrationTests.Identidad;

/// <summary>Sink de prueba: guarda cada evento ya serializado (mensaje, propiedades y excepción).</summary>
public sealed class LogsEnMemoria : ILogEventSink
{
    private readonly List<string> _eventos = [];

    public void Emit(LogEvent evento)
    {
        using var texto = new StringWriter();
        new CompactJsonFormatter().Format(evento, texto);
        lock (_eventos) _eventos.Add(texto.ToString());
    }

    public string[] Todos()
    {
        lock (_eventos) return [.. _eventos];
    }
}
