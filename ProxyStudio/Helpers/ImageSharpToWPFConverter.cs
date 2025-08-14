/*
ProxyStudio - A cross-platform proxy management application.
Copyright (C) 2025 James Ponter

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
*/

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