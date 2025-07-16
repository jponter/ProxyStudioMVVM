

using System;
using System.IO;
using System.Threading.Tasks;

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

        
        private AppConfig _config;

        public AppConfig Config => _config;

        public ConfigManager()
        {
            _config = new AppConfig();
        }

        public async Task SaveConfigAsync()
        {
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

        public  AppConfig LoadConfig()
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
        }
        
    }
}
