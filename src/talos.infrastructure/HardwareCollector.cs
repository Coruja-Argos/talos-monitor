using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using talos.domain;

namespace talos.infrastructure;

public class HardwareCollector : IHardwareCollector
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullLengthExtended;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public MonitorInfo CaptureData()
    {
        var nomeMaquina = Environment.MachineName;
        var ips = new List<string>();
        var macs = new List<string>();

        // Captura de Rede Multiplataforma (.NET Nativo)
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            // MAC Address
            var mac = nic.GetPhysicalAddress().ToString();
            if (!string.IsNullOrEmpty(mac))
            {
                // Formata o MAC para o padrão XX:XX:XX:XX:XX:XX
                var formattedMac = string.Join(":", Enumerable.Range(0, mac.Length / 2).Select(i => mac.Substring(i * 2, 2)));
                if (!macs.Contains(formattedMac))
                {
                    macs.Add(formattedMac);
                }
            }

            // IPs da Interface (Apenas IPv4)
            var ipProps = nic.GetIPProperties();
            foreach (var ip in ipProps.UnicastAddresses)
            {
                if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    ips.Add(ip.Address.ToString());
                }
            }
        }

        // Captura de Programas de acordo com o SO
        List<string> programas;
        string categoria;
        string osDesc = RuntimeInformation.OSDescription;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            programas = ObterProgramasWindows();
            categoria = "Windows";
        }
        else
        {
            programas = ObterProgramasLinux();
            categoria = "Linux";
        }

        var cpuName = ObterNomeCpu();
        var totalRamGb = ObterTotalRamGb();
        var totalStorageGb = ObterTotalStorageGb();

        return new MonitorInfo(nomeMaquina, osDesc, categoria, ips, macs, programas, cpuName, totalRamGb, totalStorageGb);
    }

    private string ObterNomeCpu()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Processador Desconhecido (Windows)";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (File.Exists("/proc/cpuinfo"))
                {
                    var linhas = File.ReadAllLines("/proc/cpuinfo");
                    var linhaModel = linhas.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                    if (linhaModel != null)
                    {
                        var partes = linhaModel.Split(':', 2);
                        if (partes.Length > 1)
                        {
                            return partes[1].Trim();
                        }
                    }
                }
                return "Processador Desconhecido (Linux)";
            }
        }
        catch (Exception ex)
        {
            return $"Erro ao obter CPU: {ex.Message}";
        }

        return "SO Não Suportado";
    }

    private double ObterTotalRamGb()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    return Math.Round((double)memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 2);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (File.Exists("/proc/meminfo"))
                {
                    var linhas = File.ReadAllLines("/proc/meminfo");
                    var linhaMem = linhas.FirstOrDefault(l => l.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase));
                    if (linhaMem != null)
                    {
                        var partes = linhaMem.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (partes.Length >= 2 && double.TryParse(partes[1], out double kb))
                        {
                            return Math.Round(kb / (1024.0 * 1024.0), 2);
                        }
                    }
                }
            }
        }
        catch
        {
            // Retorna 0 em caso de erro
        }

        return 0;
    }

    private double ObterTotalStorageGb()
    {
        try
        {
            double totalBytes = 0;
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        totalBytes += drive.TotalSize;
                    }
                }
                catch
                {
                    // Ignora unidades sem acesso ou não prontas
                }
            }
            return Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 2);
        }
        catch
        {
            return 0;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private List<string> ObterProgramasWindows()
    {
        var lista = new List<string>();
        
        // Caminhos sob HKEY_LOCAL_MACHINE (para toda a máquina)
        string[] chavesRegistroHlm =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        ];

        foreach (var chave in chavesRegistroHlm)
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(chave);
            if (baseKey == null) continue;

            foreach (var subKeyName in baseKey.GetSubKeyNames())
            {
                using var subKey = baseKey.OpenSubKey(subKeyName);
                var nome = subKey?.GetValue("DisplayName")?.ToString();
                if (!string.IsNullOrEmpty(nome) && !lista.Contains(nome))
                {
                    lista.Add(nome);
                }
            }
        }

        // Caminho sob HKEY_CURRENT_USER (instalado para o usuário atual)
        string chaveRegistroHcu = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        using (var baseKeyHcu = Registry.CurrentUser.OpenSubKey(chaveRegistroHcu))
        {
            if (baseKeyHcu != null)
            {
                foreach (var subKeyName in baseKeyHcu.GetSubKeyNames())
                {
                    using var subKey = baseKeyHcu.OpenSubKey(subKeyName);
                    var nome = subKey?.GetValue("DisplayName")?.ToString();
                    if (!string.IsNullOrEmpty(nome) && !lista.Contains(nome))
                    {
                        lista.Add(nome);
                    }
                }
            }
        }

        return [.. lista.Order()];
    }

    private List<string> ObterProgramasLinux()
    {
        var lista = new List<string>();
        try
        {
            // Abordagem para distribuições baseadas em Debian/Ubuntu (dpkg)
            if (File.Exists("/usr/bin/dpkg-query"))
            {
                lista.AddRange(ExecutarComandoLinux("dpkg-query", "-W -f='${Package} (${Version})\n'"));
            }
            // Abordagem para distribuições baseadas em RedHat/Fedora/CentOS (rpm)
            else if (File.Exists("/usr/bin/rpm"))
            {
                lista.AddRange(ExecutarComandoLinux("rpm", "-qa"));
            }
            else
            {
                lista.Add("Gerenciador de pacotes Linux não suportado nativamente.");
            }
        }
        catch (Exception ex)
        {
            lista.Add($"Erro ao listar pacotes Linux: {ex.Message}");
        }

        return lista;
    }

    private static List<string> ExecutarComandoLinux(string comando, string argumentos)
    {
        var resultado = new List<string>();
        var startInfo = new ProcessStartInfo
        {
            FileName = comando,
            Arguments = argumentos,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var processo = Process.Start(startInfo);
        if (processo == null) return resultado;

        while (processo.StandardOutput.ReadLine() is { } linha)
        {
            if (!string.IsNullOrWhiteSpace(linha))
                resultado.Add(linha.Trim('\''));
        }

        return resultado;
    }
}