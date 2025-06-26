using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;


using System.IO;

namespace ProxyStudio.Models
{
    public class Card : ObservableObject
    {
        public string Name { get; set; } = "Default Name";
        public string Id { get; set; } = "Default ID";
        public string Description { get; set; } = "Default Description";
        public string Query { get; set; } = "Default Query"; // Default query string
        public byte[] ImageData { get; set; } = new byte[0]; // Default to an empty byte array
        
        public int Width { get; set; } = 83; // Default width in mm
        public int Height { get; set; } = 118; // Default height in mm
        public bool BleedChecked { get; set; } = false;
        public bool ImageDownloaded { get; set; } = true; // Indicates if the image has been downloaded

        private IImage _cached;

        public Card(string name, string id, byte[] image)
        {
            Name = name;
            Id = id;
            ImageData = image ?? new byte[0]; // Ensure _Image is never null
        }

        public Card(string name, string id, string query)
        {
            Name = name;
            Id = id;
            Query = query ?? "Default Query"; // Ensure _Query is never null
            ImageData = new byte[0]; // Default to an empty byte array
        }


        public IImage ImageSource
        {
            get
            {
                if (_cached is null && ImageData is not null)
                {
                    using var stream = new MemoryStream(ImageData);
                    _cached = new Bitmap(stream);
                }
                return _cached;
            }
        }

        
    }
}