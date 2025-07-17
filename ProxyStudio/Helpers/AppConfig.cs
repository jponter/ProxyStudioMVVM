using System;
using Avalonia.Controls;

namespace ProxyStudio.Helpers;


    /// <summary>
    ///     This class represents the application configuration settings.
    /// </summary>
    /// 

    [Serializable]
    public class AppConfig
    {





        public WindowState WindowState; 

        public int WindowWidth { get; set; } = 800;
        public int WindowHeight { get; set; } = 600;
        public int WindowLeft { get; set; } = 100;
        public int WindowTop { get; set; } = 100;
        
        
        private bool _globalBleedEnabled;
        public bool GlobalBleedEnabled 
        { 
            get => _globalBleedEnabled;
            set 
            {
                DebugHelper.WriteDebug($"GlobalBleedEnabled being set to: {value}");
                DebugHelper.WriteDebug($"Stack trace: {Environment.StackTrace}");
                _globalBleedEnabled = value;
            }
        }
        
        
        public AppConfig()
        {
            // Default constructor
            
            DebugHelper.WriteDebug("Creating new config. AppConfigConstructor");
            DebugHelper.WriteDebug($"Stack trace: {Environment.StackTrace}");


        }

}