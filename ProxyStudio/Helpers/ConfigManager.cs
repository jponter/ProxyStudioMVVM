

using System;
using System.IO;

namespace ProxyStudio.Helpers
{
    public static class ConfigManager
    {
        private static readonly string ConfigFileName = "AppConfig.xml";
        private static readonly string appName = "ProxyStudio"; // Replace with your actual application name
        private static readonly string ConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName, ConfigFileName);

        public static void SaveConfig(AppConfig config)
        {
            var directoryPath = Path.GetDirectoryName(ConfigFilePath);
            if (directoryPath != null) // Ensure directoryPath is not null
            {
                _ = Directory.CreateDirectory(directoryPath); // Ensure the directory exists
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

        public static AppConfig LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                using (var reader = new StreamReader(ConfigFilePath))
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(AppConfig));
                    var deserializedConfig = serializer.Deserialize(reader) as AppConfig;

                    // Fix for CS8600 and CS8603: Ensure non-null return
                    if (deserializedConfig == null)
                    {
                        throw new InvalidOperationException("Failed to deserialize the configuration file.");
                    }

                    return deserializedConfig;
                }
            }
            else
            {
                // Return a new instance with default values if the config file does not exist
                return new AppConfig();
            }
        }
    }
}
