using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using talos.domain;
using talos.infrastructure;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==================================================");
Console.WriteLine("  INICIANDO CAPTURA DE DADOS DA ESTAÇÃO (.NET 10) ");
Console.WriteLine("==================================================");
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.Write("Sistema Operacional: ");
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine(RuntimeInformation.OSDescription);

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.Write("Arquitetura: ");
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine($"{RuntimeInformation.OSArchitecture}\n");
Console.ResetColor();

IHardwareCollector collector = new HardwareCollector();
MonitorInfo info = collector.CaptureData();

Console.ForegroundColor = ConsoleColor.Yellow;
Console.Write("[Nome da Máquina]: ");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine(info.NameHost);

Console.ForegroundColor = ConsoleColor.Yellow;
Console.Write("[Sistema Operacional]: ");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine(info.OSDescription);

Console.ForegroundColor = ConsoleColor.Yellow;
Console.Write("[Categoria do S.O.]: ");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine(info.Category);
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n[Endereços IP Detectados (IPv4)]:");
Console.ForegroundColor = ConsoleColor.Green;
foreach (var ip in info.IPAddresses)
{
    Console.WriteLine($"  - {ip}");
}
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n[Endereços MAC Detectados (Únicos)]:");
Console.ForegroundColor = ConsoleColor.Green;
foreach (var mac in info.MacAddresses)
{
    Console.WriteLine($"  - {mac}");
}
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"\n[Programas Instalados] (Total: {info.Programs.Count}):");
Console.ForegroundColor = ConsoleColor.Green;
foreach (var programa in info.Programs)
{
    Console.WriteLine($"  * {programa}");
}
Console.ResetColor();

// Salvar em um arquivo JSON com o nome da Estação de Trabalho
string nomeArquivo = $"{info.NameHost}.json";
string caminhoJson = Path.Combine(AppContext.BaseDirectory, nomeArquivo);
try
{
    var opcoesSerializer = new JsonSerializerOptions { WriteIndented = true };
    string jsonString = JsonSerializer.Serialize(info, opcoesSerializer);
    File.WriteAllText(caminhoJson, jsonString);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("\n==================================================");
    Console.Write("Dados exportados com sucesso para: ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine(caminhoJson);
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("==================================================");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nErro ao salvar o arquivo JSON: {ex.Message}");
}

Console.ResetColor();
Console.WriteLine("\nCaptura finalizada com sucesso. Pressione qualquer tecla para sair.");
Console.ReadKey();