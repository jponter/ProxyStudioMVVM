using System.IO;
using Metsys.Bson;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace ProxyStudio.Helpers;

public class ImageSharpToWPFConverter
{
    public static byte[] ImageToByteArray(Image<Rgba32> image)
    {
        using (var memoryStream = new MemoryStream())
        {
            image.Save(memoryStream, new JpegEncoder()); // Use JpegEncoder() or another if needed
            return memoryStream.ToArray();
        }
    }

    public static Image<Rgba32> ByteArrayToImage(byte[] byteArray)
    {
        using var memoryStream = new MemoryStream(byteArray);
        if ( byteArray.Length == 0)
        {

            //Helper.WriteDebug("ByteArrayToImage: byteArray is null or empty.");
            return null!; // Return null if the byte array is empty or null
        }
        else
        {
            return Image.Load<Rgba32>(memoryStream);
        }
    }
}