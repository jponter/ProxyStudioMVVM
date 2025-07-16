using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProxyStudio.Helpers;
using ProxyStudio.Models;

namespace ProxyStudio.ViewModels
{
    public class DesignTimeMainViewModel : MainViewModel
    {
        public DesignTimeMainViewModel() : base(new DesignTimeConfigManager())
        {
            // Add some design-time specific setup if needed
            // The base constructor will already call AddTestCards()
        }
    }
    
    /// <summary>
    /// Design-time implementation of IConfigManager that provides dummy data
    /// </summary>
    public class DesignTimeConfigManager : IConfigManager
    {
        // Implement all methods from IConfigManager interface with dummy/default values
        // Since I don't have access to your IConfigManager interface, I'll provide a template
        // You'll need to implement the actual methods based on your interface
        
        // Example implementations - replace with your actual interface methods:
        
        public string GetSetting(string key)
        {
            return "DesignTimeValue";
        }
        
        public void SetSetting(string key, string value)
        {
            // Do nothing in design time
        }
        
        public bool GetBoolSetting(string key)
        {
            return false;
        }
        
        public int GetIntSetting(string key)
        {
            return 0;
        }
        
        public void SaveSettings()
        {
            // Do nothing in design time
        }
        
        public void LoadSettings()
        {
            // Do nothing in design time
        }
        
        // Add any other methods that your IConfigManager interface requires
        // with appropriate default/dummy implementations
        public AppConfig Config { get; }
        public Task SaveConfigAsync()
        {
            throw new NotImplementedException();
        }

        public Task<AppConfig> LoadConfigAsync()
        {
            throw new NotImplementedException();
        }

        public void UpdateConfig(Action<AppConfig> updateAction)
        {
            throw new NotImplementedException();
        }

        public AppConfig LoadConfig()
        {
            throw new NotImplementedException();
        }

        public void SaveConfig()
        {
            throw new NotImplementedException();
        }
    }
}