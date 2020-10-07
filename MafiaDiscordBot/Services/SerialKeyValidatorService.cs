using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MafiaDiscordBot.Services
{
    public class SerialKeyValidatorService
    {
        [DllImport("KeyManager-64.dll", EntryPoint = "CheckKey")]
        private static extern byte CheckKey_64(string key);
        [DllImport("KeyManager.dll", EntryPoint = "CheckKey")]
        private static extern byte CheckKey_32(string key);
        
        [Flags]
        public enum KeyStatus
        {
            KeyVerified = 1 << 0,
            Original = 1 << 1,
            HostedByDevs = 1 << 2,
            Premium = 1 << 3,
            OwnedByDevs = 1 << 4
        }
        
        private readonly IConfigurationRoot _config;
        private KeyStatus? _botKeyStatus; 

        public SerialKeyValidatorService(IServiceProvider service)
        {
            _config = service.GetRequiredService<IConfigurationRoot>();
        }

        public KeyStatus GetKeyStatus() => _botKeyStatus ??= GetKeyStatus(_config["key"]);

        private static KeyStatus GetKeyStatus(string key)
        {
            Log.Verbose("Validating and getting status of key {key}", key);
            return (KeyStatus)(IntPtr.Size == 8 /* 64bit */ ? CheckKey_64(key) : CheckKey_32(key));            
        }
    }
}