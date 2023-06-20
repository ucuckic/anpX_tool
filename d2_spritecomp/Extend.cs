using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace d2_spritecomp
{
    public static class Extend
    {
        public static void SaveBitmap(this SKImage image, string out_path, byte[] color_palette)
        {
            using (var stream_bitmap = File.Create(out_path))
            {
                using (BinaryWriter bitmap = new BinaryWriter(stream_bitmap))
                {
                    unsafe
                    {
                        UInt16 bitmap_header = 0x4D42;
                        uint bitmap_size = 0;

                        //should just be this every time, fixed header after all with fixed palette size
                        uint data_offset = 0x436;

                        bitmap.Write(bitmap_header);
                        bitmap.Write(bitmap_size);
                        bitmap.Write(new byte[4]);
                        bitmap.Write(data_offset);

                        uint core_header_size = 0x28;

                        int bitmap_width = image.Width;
                        int bitmap_height = image.Height;
                        UInt16 cplanes = 1;
                        UInt16 bitmap_bpp = 8;
                        uint compression = 0;
                        uint data_size = 0;
                        uint ppm_x = 0;
                        uint ppm_y = 0;

                        uint color_count = 0;
                        uint important_color = 0;

                        bitmap.Write(core_header_size);
                        bitmap.Write(bitmap_width);
                        bitmap.Write(bitmap_height);
                        bitmap.Write(cplanes);
                        bitmap.Write(bitmap_bpp);
                        bitmap.Write(compression);
                        bitmap.Write(data_size);
                        bitmap.Write(ppm_x);
                        bitmap.Write(ppm_y);

                        bitmap.Write(color_count);
                        bitmap.Write(important_color);

                        //palette data here

                        bitmap.Write(color_palette);

                        //bitmap pixel data

                        SKPixmap image_data = image.PeekPixels();


                        for (int p = 0; p < image_data.BytesSize; p += 4)
                        {

                            IntPtr pixel_data = image_data.GetPixels() + p;
                            byte* ptr = (byte*)pixel_data.ToPointer();

                            bitmap.Write(*ptr);
                        }
                        
                    }
                }
            }
        }
    }
}
