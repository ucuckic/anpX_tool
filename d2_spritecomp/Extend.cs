using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace d2_spritecomp
{
    public static class Extend
    {

        public static void CursedPng(this SKImage in_mage, string out_path, byte[] color_palette)
        {
            unsafe
            {
                var image = SKBitmap.FromImage(in_mage);
                SKPixmap img_map = image.PeekPixels();

                byte[] px_grid = new byte[img_map.BytesSize / 4];
                var pxgrd_handle = GCHandle.Alloc(px_grid, GCHandleType.Pinned);

                byte* img_ptr = (byte*)img_map.GetPixels().ToPointer();
                byte* out_ptr = (byte*)pxgrd_handle.AddrOfPinnedObject();

                for (int i = 0; i < px_grid.Length; i++)
                {
                    *(out_ptr + i) = *(img_ptr + (i * 4));
                }

                var px_info = new SKImageInfo(image.Width, image.Height, SKColorType.Gray8, SKAlphaType.Unpremul);
                image.InstallPixels(px_info, pxgrd_handle.AddrOfPinnedObject(), px_info.RowBytes, delegate { pxgrd_handle.Free(); }, null);

                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var stream = File.Open(out_path, FileMode.Create))
                {
                    //data.SaveTo(stream);
                    using (BinaryReader strread = new BinaryReader(data.AsStream()))
                    {
                        using (BinaryWriter strwrite = new BinaryWriter(stream))
                        {
                            strwrite.Write(strread.ReadBytes(0x10));

                            byte[] be_width = BitConverter.GetBytes(image.Width).Reverse().ToArray();
                            byte[] be_height = BitConverter.GetBytes(image.Height).Reverse().ToArray();

                            byte[] stuff = new byte[] { 0x08, 0x03, 0x00, 0x00, 0x00 }; //ihdr param
                            byte[] ihdr_data = (be_width.Concat(be_height).Concat(stuff)).ToArray();
                            strwrite.Write(ihdr_data);

                            //ihdr crc
                            //stream.Position = 0x10;
                            byte[] ihdr_crc = BitConverter.GetBytes(Crc32(ihdr_data, 0, ihdr_data.Length, ihdrCrc));
                            Array.Reverse(ihdr_crc);
                            strwrite.Write(ihdr_crc);

                            long pos = stream.Position;
                            strread.BaseStream.Position = pos;

                            byte[] img_buffer = strread.ReadBytes((int)strread.BaseStream.Length);

                            //plte
                            byte[] plte_chunk = new byte[] { 0, 0, 3, 0, 0x50, 0x4C, 0x54, 0x45, };
                            byte[] plte_pal = new byte[0x300];

                            pos = stream.Position;
                            for (int clr = 0, sclr = 0; clr < color_palette.Length; clr += 4, sclr += 3)
                            {
                                plte_pal[sclr] = color_palette[clr + 0];
                                plte_pal[sclr + 1] = color_palette[clr + 1];
                                plte_pal[sclr + 2] = color_palette[clr + 2];
                            }

                            strwrite.Write(plte_chunk.Concat(plte_pal).ToArray());
                            byte[] plte_crc = BitConverter.GetBytes(Crc32(plte_pal, 0, 0x300, plteCrc));
                            Array.Reverse(plte_crc);
                            strwrite.Write(plte_crc);

                            //trns
                            strwrite.Write(new byte[] { 0, 0, 0, 1, 0x74, 0x52, 0x4E, 0x53 });
                            strwrite.Write((byte)0);
                            byte[] trns_crc = new byte[] { 0x40, 0xE6, 0xD8, 0x66 };
                            strwrite.Write(trns_crc);

                            strwrite.Write(img_buffer);
                        }
                    }
                }
            }

        }
        public static void SaveBitmap(this SKImage image, string out_path, byte[] color_palette, bool flip_write = false)
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

                        if(flip_write)
                        {
                            for (int p = image_data.BytesSize; p > 0; p -= 4)
                            {

                                IntPtr pixel_data = image_data.GetPixels() + p-4;
                                byte* ptr = (byte*)pixel_data.ToPointer();

                                bitmap.Write(*ptr);
                            }
                        }
                        else
                        {
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


        static uint[] crcTable;

        // Stores a running CRC (initialized with the CRC of "IDAT" string). When
        // you write this to the PNG, write as a big-endian value
        static uint ihdrCrc = Crc32(new byte[] { (byte)'I', (byte)'H', (byte)'D', (byte)'R' }, 0, 4, 0);
        static uint plteCrc = Crc32(new byte[] { (byte)'P', (byte)'L', (byte)'T', (byte)'E' }, 0, 4, 0);
        static uint trnsCrc = Crc32(new byte[] { (byte)'t', (byte)'R', (byte)'N', (byte)'S' }, 0, 4, 0);

        // Call this function with the compressed image bytes, 
        // passing in idatCrc as the last parameter
        private static uint Crc32(byte[] stream, int offset, int length, uint crc)
        {
            uint c;
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n <= 255; n++)
                {
                    c = n;
                    for (var k = 0; k <= 7; k++)
                    {
                        if ((c & 1) == 1)
                            c = 0xEDB88320 ^ ((c >> 1) & 0x7FFFFFFF);
                        else
                            c = ((c >> 1) & 0x7FFFFFFF);
                    }
                    crcTable[n] = c;
                }
            }
            c = crc ^ 0xffffffff;
            var endOffset = offset + length;
            for (var i = offset; i < endOffset; i++)
            {
                c = crcTable[(c ^ stream[i]) & 255] ^ ((c >> 8) & 0xFFFFFF);
            }
            return c ^ 0xffffffff;
        }
    }
}
