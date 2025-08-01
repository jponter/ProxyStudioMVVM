

using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace ProxyStudio.Helpers
{
    public interface IConfigManager
    {
        AppConfig Config { get; }
        Task SaveConfigAsync();
        Task<AppConfig> LoadConfigAsync();
        void UpdateConfig(Action<AppConfig> updateAction);
        AppConfig LoadConfig();
        void SaveConfig();

    }
    
    
    
    public class ConfigManager : IConfigManager
    {
        private static readonly string ConfigFileName = "AppConfig.xml";
        private static readonly string appName = "ProxyStudio"; // Replace with your actual application name
        private static readonly string ConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName, ConfigFileName);
       



        private AppConfig? _config;
        public AppConfig Config 
        { 
            get 
            {
                //DebugHelper.WriteDebug($"Config getter called. _config is null: {_config == null}");
                if (_config == null)
                {
                    DebugHelper.WriteDebug("Loading config because _config is null");
                    _config = LoadConfig() ?? new AppConfig();
                }
                return _config;
            }
        }
        public ConfigManager()
        {
            var instanceId = Guid.NewGuid().ToString("N")[..8];
            DebugHelper.WriteDebug($"ConfigManager created: {instanceId}");
            
            
            // Ensure the config is loaded when the manager is created
            DebugHelper.WriteDebug($"_config is null: {_config == null}");
        }

        public async Task SaveConfigAsync()
        {
            DebugHelper.WriteDebug("Saving config to file.");
            await Task.Run(() =>
            {
                var directoryPath = Path.GetDirectoryName(ConfigFilePath);
                if (directoryPath != null)
                {
                    _ = Directory.CreateDirectory(directoryPath);
                    using (var writer = new StreamWriter(ConfigFilePath))
                    {
                        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(AppConfig));
                        serializer.Serialize(writer, _config);
                    }
                }
                else
                {
                    throw new InvalidOperationException("The configuration file path is invalid.");
                }
            });
        }


        public async Task<AppConfig> LoadConfigAsync()
        {
            _config = await Task.Run(() =>
            {
                if (File.Exists(ConfigFilePath))
                {
                    using (var reader = new StreamReader(ConfigFilePath))
                    {
                        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(AppConfig));
                        var deserializedConfig = serializer.Deserialize(reader) as AppConfig;

                        if (deserializedConfig == null)
                        {
                            throw new InvalidOperationException("Failed to deserialize the configuration file.");
                        }

                        return deserializedConfig;
                    }
                }
                else
                {
                    return new AppConfig();
                }
            });

            return _config;
        }
        
        public void UpdateConfig(Action<AppConfig> updateAction)
        {
            updateAction(_config);
        }
        
        // Keep static methods for backward compatibility if needed
        public static void SaveConfig(AppConfig config)
        {
            var directoryPath = Path.GetDirectoryName(ConfigFilePath);
            if (directoryPath != null)
            {
                _ = Directory.CreateDirectory(directoryPath);
                using (var writer = new StreamWriter(ConfigFilePath))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(AppConfig));
                    serializer.Serialize(writer, config);
                }
            }
            else
            {
                throw new InvalidOperationException("The configuration file path is invalid.");
            }
        }
        
        // Synchronous methods
        public void SaveConfig()
        {
            DebugHelper.WriteDebug("Saving config to file Sync.");
            var directoryPath = Path.GetDirectoryName(ConfigFilePath);
            if (directoryPath != null)
            {
                _ = Directory.CreateDirectory(directoryPath);
                using (var writer = new StreamWriter(ConfigFilePath))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(AppConfig));
                    serializer.Serialize(writer, _config);
                }
            }
            else
            {
                throw new InvalidOperationException("The configuration file path is invalid.");
            }
        }

        public AppConfig LoadConfig()
        {
            DebugHelper.WriteDebug("Loading config from file.");
    
            if (_config != null)
            {
                DebugHelper.WriteDebug("Config already loaded, returning cached instance.");
                return _config;
            }
    
            if (File.Exists(ConfigFilePath))
            {
                using (var reader = new StreamReader(ConfigFilePath))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(AppConfig));
                    _config = serializer.Deserialize(reader) as AppConfig;

                    if (_config == null)
                    {
                        throw new InvalidOperationException("Failed to deserialize the configuration file.");
                    }

                    return _config;
                }
            }
            else
            {
                DebugHelper.WriteDebug("Config file not found. Returning default config.");
                _config = new AppConfig();
                return _config;
            }
        }
        
    }
}
