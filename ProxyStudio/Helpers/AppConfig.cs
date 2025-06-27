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


        public AppConfig()
        {
            // Default constructor

         
        }

}