using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MafiaDiscordBot.Services
{
    public class SystemMetricsService
    {
        struct MemoryMetrics
        {
            public long Total;
            public long Used;
            public long Free;
        }

        public SystemMetricsService(IServiceProvider service)
        {
            UpdateOSInfo();
        }

        public bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public bool IsOSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public bool IsUnix => IsLinux || IsOSX;

        private MemoryMetrics _memory;
        private DateTime _lastMemoryMetricsUpdate = DateTime.MinValue;
        private object _memoryMetricsLocker = new object();
        private TimeSpan _memoryUpdateInterval = TimeSpan.FromSeconds(5);

        public long RamUsed
        {
            get
            {
                if (DateTime.Now - _lastMemoryMetricsUpdate > _memoryUpdateInterval) UpdateMemoryMetrics();
                return _memory.Used;
            }
        }

        public long RamTotal
        {
            get
            {
                if (DateTime.Now - _lastMemoryMetricsUpdate > _memoryUpdateInterval) UpdateMemoryMetrics();
                return _memory.Total;
            }
        }

        public long RamFree
        {
            get
            {
                if (DateTime.Now - _lastMemoryMetricsUpdate > _memoryUpdateInterval) UpdateMemoryMetrics();
                return _memory.Free;
            }
        }

        public void UpdateMemoryMetrics()
        {
            if (IsUnix) UpdateUnixMemoryMetrics();
            else UpdateWindowsMemoryMetrics();
        }

        private void UpdateWindowsMemoryMetrics()
        {
            lock (_memoryMetricsLocker)
            {
                var output = "";

                var info = new ProcessStartInfo();
                info.FileName = "wmic";
                //info.Arguments = "OS get FreePhysicalMemory,TotalVisibleMemorySize /Value";
                info.Arguments = "os get freephysicalmemory /Value";
                info.RedirectStandardOutput = true;

                using (var process = Process.Start(info))
                    output = process.StandardOutput.ReadToEnd();

                var lines = output.Trim().Split("\n");
                var freeMemoryParts = lines[0].Split("=", StringSplitOptions.RemoveEmptyEntries);
                _memory.Free = long.Parse(freeMemoryParts[1]) * 1024;

                info.Arguments = "MEMORYCHIP get Capacity /Value";
                using (var process = Process.Start(info))
                    output = process.StandardOutput.ReadToEnd();
                lines = output.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                _memory.Total = 0;
                foreach (var line in lines)
                {
                    int ind;
                    if ((ind = line.IndexOf('=')) == -1) continue;
                    _memory.Total += long.Parse(line.Substring(ind + 1));
                }

                _memory.Used = _memory.Total - _memory.Free;

                _lastMemoryMetricsUpdate = DateTime.Now;
            }
        }

        private void UpdateUnixMemoryMetrics()
        {
            lock (_memoryMetricsLocker)
            {
                var output = "";

                var info = new ProcessStartInfo("free -m");
                info.FileName = "/bin/bash";
                info.Arguments = "-c \"free -m\"";
                info.RedirectStandardOutput = true;

                using (var process = Process.Start(info))
                    output = process.StandardOutput.ReadToEnd();

                var lines = output.Split("\n");
                var memory = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);

                _memory.Total = long.Parse(memory[1]);
                _memory.Used = long.Parse(memory[2]);
                _memory.Free = long.Parse(memory[3]);
            }
        }

        public string OSName { get; private set; }
        public string OSVersion { get; private set; }

        public void UpdateOSInfo()
        {
            OSName = OSVersion = "Unknown";

            if (IsLinux) UpdateLinuxOSInfo();
            else if (IsWindows) UpdateWindowsOSInfo();
        }
        private void UpdateLinuxOSInfo()
        {
            OSName = OSVersion = "Unknown";

            string filename;

            if (File.Exists("/etc/os-release")) filename = "/etc/os-release";
            else if (File.Exists("/usr/lib/os-release")) filename = "/usr/lib/os-release";
            else return;

            using (var file = File.OpenRead(filename))
            using (var reader = new StreamReader(file))
            {
                // bit field property
                //    - bit 1: os name fetched
                //    - bit 2: os version fetched
                byte readProps = 0;
                while ((readProps & 0b11) != 0b11 && !reader.EndOfStream)
                {
                    // Read the next line
                    string line = reader.ReadLine();
                    if (line == null) break;

                    // If we do not have the os name but the line have the name
                    const string OSNameKey = "NAME";
                    if ((readProps & 0b1) == 0 && line.StartsWith($"{OSNameKey}="))
                    {
                        OSName = line.Substring(OSNameKey.Length + 1, line.Length - 1 - OSNameKey.Length - 1);
                        readProps |= 0b1;
                        continue;
                    }

                    // If we do not have the os version but the line have the version
                    const string OSVersionKey = "VERSION_ID";
                    if ((readProps & 0b10) == 0 && line.StartsWith($"{OSVersionKey}="))
                    {
                        OSVersion = line.Substring(OSVersionKey.Length + 1, line.Length - 1 - OSVersionKey.Length - 1);
                        readProps |= 0b10;
                        continue;
                    }
                }
            }
        }
        private void UpdateWindowsOSInfo()
        {
            OSName = OSVersion = "Unknown";

            OSName = (string) Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "ProductName", OSName);
            OSVersion = (string) Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "ReleaseId", OSVersion);
        }
    }
}