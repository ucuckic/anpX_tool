using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;

namespace d2_spritecomp
{
    class Program
    {
        //mom would not be proud of this one
        public static byte[] lazy_mspal_header = new byte[]{ 0x52, 0x49, 0x46, 0x46, 0x10, 0x04, 0x00, 0x00, 0x50, 0x41, 0x4C, 0x20, 0x64, 0x61, 0x74, 0x61, 0x04, 0x04, 0x00, 0x00, 0x00, 0x03, 0x00, 0x01 };

        //texture flags (position address)
        const int anp2_flag_enable_translation = 0x20;
        const int anp2_flag_enable_scale = 0x40;
        const int anp2_flag_unk = 0x80;

        //texture flags (scale address, second in order)
        const int anp2_flag_additive_blend = 0x10;
        const int anp2_flag_negative_blend = 0x20;
        const int anp2_flag_flip_x = 0x40;
        const int anp2_flag_flip_y = 0x80;

        //anp3

        const int anp3_flag_enable_rotate = 0x1;
        const int anp3_flag_enable_scale = 0x2;

        const int anp3_flag_flip_x = 0x4;
        const int anp3_flag_flip_y = 0x8;

        const int anp3_flag_enable_opacity = 0x10;

        //V103
        const int V103_flag_blendmode_add = 0x1;
        const int V103_flag_blendmode_sub = 0x2;

        const int V103_flag_double_y_offset = 0x4;
        const int V103_flag_double_x_offset = 0x8;
        const int V103_flag_flip_y = 0x10;
        const int V103_flag_quadruple_y_offset = 0x20;
        const int V103_flag_flip_x = 0x40;
        const int V103_flag_quadruple_x_offset = 0x80;

        //V154

        const int V154_flag_compress_chunk = 0x40;
        const int V154_flag_compress_image = 0x80;

        struct weac_dat
        {
            public uint paste_offset;
            public uint data_offset;
            public uint data_size;

            public byte[] data_array;

            public weac_dat(uint PasteOffset, uint DataOffset, uint DataSize, byte[] Data = null)
            {
                this.paste_offset = PasteOffset;
                this.data_offset = DataOffset;
                this.data_size = DataSize;
                this.data_array = Data;
            }
        }

        struct i_dat
        {
            //mostly universal
            public int x;
            public int y;
            public int len_x;
            public int len_y;
            public int paste_x;
            public int paste_y;
            public int flags;
            public int flags_2;
            public int rot_angle;
            public int use_palette;
            public int scale_x;
            public int scale_y;

            //anp2
            public int offset_x;
            public int offset_y;

            //anp3
            public int rot_axis_x;
            public int rot_axis_y;
            public byte[] pixel_data;

            //narikiri
            public int chunk_center_x;
            public int chunk_center_y;

            //hearts
            public uint image_data_offset;
        }

        struct Color
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;
        }

        public static bool exclude_char = false;
        public static bool exclude_wep = false;

        public static bool save_parts = false;
        public static bool gDoAttemptIndexed = false;

        public static int weapon_equip = 0;
        public static string weapon_equip_path = "";

        public static string in_path;
        public static string og_in_path;

        public static DirectoryInfo texture_sheet_path;
        public static DirectoryInfo in_dir;
        public static DirectoryInfo out_dir = new DirectoryInfo("out\\");
        public static DirectoryInfo part_out_dir = new DirectoryInfo(Path.Combine(out_dir.FullName, @"parts"));
        public static DirectoryInfo palettes_out_dir = new DirectoryInfo(Path.Combine(out_dir.FullName, @"palettes"));

        static void Main(string[] args)
        {

            if (args.Length == 0)
            {
                Console.WriteLine("Usage Syntax: filename -texture anp2_texture.png -outdir out_dir");
                return;
            }

            List<i_dat> idat_list = new List<i_dat>();

            byte[] in_dat = File.ReadAllBytes(args[0]);
            string anp_string = System.Text.Encoding.UTF8.GetString(in_dat,0,4);

            og_in_path = args[0];

            //Image in_sheet = Image.FromFile(args[1]);
            //Bitmap out_img = new Bitmap(256,256);

            //using var in_sheet = Image<Rgba32>.Load(args[1]);

            using ( MemoryStream chunk_dat_stream = new MemoryStream(in_dat) )
            {
                //chunk_dat_stream.Position = 0xA; //offset begin
                //int sprite_offset = chunk_dat.


                //chunk_dat.BaseStream.Position = (0x4);


                for(int i = 0; i < args.Length; i++)
                {
                    var c_arg = args[i];
                    switch(c_arg)
                    {
                        case "-nochar":
                            exclude_char = true;
                            break;
                        case "-nowep":
                            exclude_wep = true;
                            break;
                        case "-equip":
                            if (i + 1 < args.Length)
                            {
                                i++;
                                weapon_equip_path = args[i];
                                int.TryParse(args[i], out weapon_equip);
                            }
                            break;
                        case "-texture":
                            if (i + 1 < args.Length)
                            {
                                i++;
                                texture_sheet_path = new DirectoryInfo(args[i]);
                            }
                            break;
                        case "-tryindex":
                            gDoAttemptIndexed = true;
                            break;
                        case "-outdir":
                            if(i + 1 < args.Length)
                            {
                                i++;
                                out_dir = new DirectoryInfo(args[i]);
                            }
                            break;
                        case "-saveparts":
                            save_parts = true;
                            break;
                    }
                }


                in_dir = new DirectoryInfo(og_in_path);
                switch (anp_string)
                {
                    case "anp2": //destiny 2
                        if(texture_sheet_path == null)
                        {
                            Console.WriteLine("anp2 detected, but no texture sheet supplied. supply with: -texture");
                            break;
                        }
                        in_path = texture_sheet_path.FullName;
                        anp2_unpack(chunk_dat_stream,in_path);
                        break;
                    case "anp3": //destiny ps2/rebirth
                        anp3_unpack(chunk_dat_stream);
                        break;
                    case "V103": //narikiri dungeon x
                        V103_unpack(chunk_dat_stream);
                        break;
                    case "V154": //hearts
                        V154_unpack(chunk_dat_stream);
                        break;
                    default:
                        Console.WriteLine("anp header missing");
                        return;
                }
            }
            
        }

        public struct tm2_sheet
        {
            public UInt32 data_size;
            public byte pixel_format;

            //possibly unneeded
            public UInt16 buffer_paste_x;
            public UInt16 buffer_paste_y;


            public UInt16 width;
            public UInt16 height;

            public byte[] pixel_data;

            public int exist_y;
        }

        static void anp2_unpack(MemoryStream in_file, string in_path)
        {

            DirectoryInfo in_dir = new DirectoryInfo(og_in_path);
            string fname = in_dir.Name.Substring(0, in_dir.Name.Length - in_dir.Extension.Length);

            List<tm2_sheet> palette_sheets = new List<tm2_sheet>();
            List<tm2_sheet> texture_sheets = new List<tm2_sheet>();
            int composite_height = 0;

            byte[][] pals = new byte[0][];
            byte[] consolidated_pal = new byte[256 * 4];

            //fuck it read all bytes
            using (FileStream in_texture_stream = File.OpenRead(in_path))
            {
                using (BinaryReader tm2 = new BinaryReader(in_texture_stream))
                {
                    byte[] header_check = tm2.ReadBytes(3);
                    

                    //TM2
                    if (header_check.SequenceEqual(new byte[] { 0x54,0x4D,0x32 }))
                    {
                        byte tm2_type = tm2.ReadByte();

                        byte something = tm2.ReadByte();
                        byte palette_count = tm2.ReadByte();
                        byte chunk_count = tm2.ReadByte();
                        tm2.BaseStream.Position = 0x10;

                        pals = new byte[palette_count][];

                        //treat the palette sheets just as the game would, presumably, just in case
                        for (int i = 0; i < palette_count; i++)
                        {
                            tm2_sheet new_sheet = new tm2_sheet();


                            new_sheet.data_size = tm2.ReadUInt32() - 0x10;

                            new_sheet.pixel_format = tm2.ReadByte();
                            tm2.ReadBytes(3);
                            new_sheet.buffer_paste_x = tm2.ReadUInt16();
                            new_sheet.buffer_paste_y = tm2.ReadUInt16();
                            new_sheet.width = tm2.ReadUInt16();
                            new_sheet.height = tm2.ReadUInt16();

                            //

                            if(new_sheet.data_size == 0x40)
                            {
                                new_sheet.pixel_data = tm2.ReadBytes((int)new_sheet.data_size);
                            }
                            else
                            {
                                List<byte[]> pals_out = new List<byte[]>();
                                for (int col = 0; col < 0x8; col++)
                                {
                                    Console.WriteLine("off before: 0x{0:X}", tm2.BaseStream.Position);

                                    byte[] pal_1_half_1 = tm2.ReadBytes(0x20);
                                    byte[] pal_2_half_1 = tm2.ReadBytes(0x20);

                                    byte[] pal_1_half_2 = tm2.ReadBytes(0x20);
                                    byte[] pal_2_half_2 = tm2.ReadBytes(0x20);

                                    pals_out.Add(pal_1_half_1.Concat(pal_1_half_2).ToArray());
                                    pals_out.Add(pal_2_half_1.Concat(pal_2_half_2).ToArray());

                                    Console.WriteLine("off after: 0x{0:X}", tm2.BaseStream.Position);
                                }

                                new_sheet.pixel_data = pals_out.SelectMany(x => x).ToArray();
                            }




                            palette_sheets.Add(new_sheet);


                            //unfuck palettes while we are here and create a proper palette array
                            
                            byte[] add_pal = new byte[256 * 4];
                            for(int h = 0; h < add_pal.Length; h+=4)
                            {
                                add_pal[h] = (byte)(h / 4);
                                add_pal[h+1] = (byte)(h/4);
                                add_pal[h+2] = (byte)(h / 4);
                                add_pal[h + 3] = 0xff;
                            }
                            

                            new_sheet.pixel_data.CopyTo(add_pal,0);

                            if(i < 0x10)
                            {
                                new_sheet.pixel_data.CopyTo(consolidated_pal, i * 0x40);
                            }

                            pals[i] = add_pal;

                            palettes_out_dir.Create();
                            using(FileStream writecolor = File.OpenWrite(Path.Combine(palettes_out_dir.FullName, "t_pal" + i + ".pal")))
                            {
                                writecolor.Write(lazy_mspal_header);
                                writecolor.Write(add_pal);
                            }

                        }

                        /*
                        for (int g = 0; g < consolidated_pal.Length; g += 4)
                        {
                            //reverse the color palette for bmp export

                            byte[] buffer = new byte[3];
                            Array.ConstrainedCopy(consolidated_pal, g, buffer, 0, 3);
                            Array.Reverse(buffer);
                            Array.ConstrainedCopy(buffer, 0, consolidated_pal, g, 3);
                        }
                        */

                        for (int i = 0; i < chunk_count; i++)
                        {
                            tm2_sheet new_sheet = new tm2_sheet();

                            //where is this sheet on the total y axis
                            new_sheet.exist_y = composite_height;

                            new_sheet.data_size = tm2.ReadUInt32()-0x10;

                            new_sheet.pixel_format = tm2.ReadByte();
                            tm2.ReadBytes(3);
                            new_sheet.buffer_paste_x = tm2.ReadUInt16();
                            new_sheet.buffer_paste_y = tm2.ReadUInt16();
                            new_sheet.width = tm2.ReadUInt16();
                            new_sheet.height = tm2.ReadUInt16();

                            //keep increasing the y position by the culminated sheet heights
                            composite_height += new_sheet.height;

                            if (tm2_type == 0x23)
                            {
                                int inc_vl = (new_sheet.pixel_format == 20) ? 8 : 4;
                                int bpp = ( new_sheet.pixel_format == 20 ) ? 4 : 8;
                                new_sheet.pixel_data = new byte[(new_sheet.data_size * 8) + 1024];

                                long s_pos = tm2.BaseStream.Position;

                                for (int j = 1, k = 0; j < ( ( new_sheet.data_size ) / 128 ) + 1; j++)
                                {
                                    int div = j / 8;
                                    int cnk_div = (j % 8) * 16;

                                    for (int px_y = 0; px_y < (bpp*2); px_y++)
                                    {
                                        for (int px_x = 0; px_x < 16; px_x++, k += inc_vl)
                                        {
                                            byte raw_byte = tm2.ReadByte();

                                            if(new_sheet.pixel_format == 20)
                                            {
                                                byte pixel1 = (byte)(raw_byte & 0x0F);
                                                byte pixel2 = (byte)((raw_byte & 0xF0) >> 4);


                                                //alpha
                                                new_sheet.pixel_data[k + 7] = 0xff;

                                                new_sheet.pixel_data[k + 6] = 0;
                                                new_sheet.pixel_data[k + 5] = 0xff;
                                                new_sheet.pixel_data[k + 4] = 0;

                                                //alpha
                                                new_sheet.pixel_data[k + 3] = 0xff;

                                                new_sheet.pixel_data[k + 2] = 0;
                                                new_sheet.pixel_data[k + 1] = 0xff;
                                                new_sheet.pixel_data[k + 0] = 0;

                                                //alpha
                                                new_sheet.pixel_data[k + 7] = 0xff;

                                                new_sheet.pixel_data[k + 6] = pixel2;
                                                new_sheet.pixel_data[k + 5] = pixel2;
                                                new_sheet.pixel_data[k + 4] = pixel2;

                                                //alpha
                                                new_sheet.pixel_data[k + 3] = 0xff;

                                                new_sheet.pixel_data[k + 2] = pixel1;
                                                new_sheet.pixel_data[k + 1] = pixel1;
                                                new_sheet.pixel_data[k + 0] = pixel1;
                                            }
                                            else
                                            {
                                                new_sheet.pixel_data[k + 0] = raw_byte;
                                                new_sheet.pixel_data[k + 1] = raw_byte;
                                                new_sheet.pixel_data[k + 2] = raw_byte;
                                                new_sheet.pixel_data[k + 3] = 0xff;
                                            }



                                        }

                                        tm2.BaseStream.Position += (new_sheet.width / 2) - 16;
                                    }

                                    tm2.BaseStream.Position = s_pos + cnk_div + (div * (256*bpp) );
                                    //Console.WriteLine("seek "+tm2.BaseStream.Position+" div "+div);
                                }
                            }
                            else
                            {
                                switch (new_sheet.pixel_format)
                                {
                                    case 19:
                                        new_sheet.pixel_data = tm2.ReadBytes((int)new_sheet.data_size);
                                        break;
                                    case 20:
                                        new_sheet.pixel_data = new byte[(new_sheet.data_size * 8)];
                                        for (int j = 0, k = 0; j < new_sheet.data_size; j++, k += 8)
                                        {
                                            byte raw_byte = tm2.ReadByte();
                                            byte pixel1 = (byte)(raw_byte & 0x0F);
                                            byte pixel2 = (byte)((raw_byte & 0xF0) >> 4);


                                            //alpha
                                            new_sheet.pixel_data[k + 7] = 0xff;

                                            new_sheet.pixel_data[k + 6] = pixel2;
                                            new_sheet.pixel_data[k + 5] = pixel2;
                                            new_sheet.pixel_data[k + 4] = pixel2;

                                            //alpha
                                            new_sheet.pixel_data[k + 3] = 0xff;

                                            new_sheet.pixel_data[k + 2] = pixel1;
                                            new_sheet.pixel_data[k + 1] = pixel1;
                                            new_sheet.pixel_data[k + 0] = pixel1;
                                        }
                                        break;

                                }
                            }


                            texture_sheets.Add(new_sheet);

                            if (save_parts)
                            {
                                if (save_parts) part_out_dir.Create();
                                using (SKBitmap in_sheet = new SKBitmap())
                                {
                                    byte[] flip_data = new_sheet.pixel_data;
                                    //Array.Reverse(flip_data);

                                    var gcHandle = GCHandle.Alloc(flip_data, GCHandleType.Pinned);

                                    // install the pixels with the color type of the pixel data
                                    var img_info = new SKImageInfo(new_sheet.width, new_sheet.height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
                                    in_sheet.InstallPixels(img_info, gcHandle.AddrOfPinnedObject(), img_info.RowBytes, delegate { gcHandle.Free(); }, null);

                                    if(gDoAttemptIndexed)
                                    {
                                        //SKImage.FromBitmap(in_sheet).SaveBitmap(Path.Combine(part_out_dir.FullName, fname + "_sheet_" + i + ".bmp"),consolidated_pal,true);
                                        SKImage.FromBitmap(in_sheet).CursedPng(Path.Combine(part_out_dir.FullName, fname + "_sheet_" + i + ".png"), consolidated_pal);
                                    }
                                    else
                                    {
                                        //Console.WriteLine("svpart " + b);
                                        using (var data = in_sheet.Encode(SKEncodedImageFormat.Png, 100))
                                        using (var stream = File.OpenWrite(Path.Combine(part_out_dir.FullName, fname + "_sheet_" + i + ".png")))
                                        {
                                            // save the data to a stream
                                            data.SaveTo(stream);
                                        }
                                    }

                                }
                            }
                        }
                    }
                }
            }


            using (BinaryReader chunk_dat = new BinaryReader(in_file))
            {
                int chnk_sv = 0;

                chunk_dat.BaseStream.Position = 4;

                List<i_dat> idat_list = new List<i_dat>();
                uint num_sprites = chunk_dat.ReadByte();

                Console.WriteLine("num sprites: " + num_sprites);
                //System.Environment.Exit(0);

                for (int h = 0; h < num_sprites; h++)
                {
                    chunk_dat.BaseStream.Position = (0x10) + (h * 2);

                    Console.WriteLine("actual current before read offset: 0x{0:X}", chunk_dat.BaseStream.Position);

                    uint sprite_offset = chunk_dat.ReadUInt16();
                    Console.WriteLine("sprite offset: 0x{0:X}", sprite_offset);

                    chunk_dat.BaseStream.Position = sprite_offset;

                    int num_parts = chunk_dat.ReadByte();

                    int unk = chunk_dat.ReadByte();

                    int unk2 = chunk_dat.ReadInt16();

                    Console.WriteLine("pos before read: 0x{0:X}", chunk_dat.BaseStream.Position);

                    for (int i = 0; i < num_parts; i++)
                    {

                        uint sprite_part_data_offset = chunk_dat.ReadUInt16();

                        Console.WriteLine("chunk belongs to: " + h + " current chunk: " + i);
                        Console.WriteLine("goto: 0x{0:X}", sprite_part_data_offset);
                        Console.WriteLine("from: 0x{0:X}", chunk_dat.BaseStream.Position);

                        int x_offset = chunk_dat.ReadSByte();
                        int y_offset = chunk_dat.ReadSByte();

                        long current_offset = chunk_dat.BaseStream.Position;

                        chunk_dat.BaseStream.Position = sprite_part_data_offset;

                        Console.WriteLine("where am i: 0x{0:X}", chunk_dat.BaseStream.Position);

                        //cursed

                        //texture cut position
                        byte[] flag_buffer = new byte[2];
                        flag_buffer[0] = chunk_dat.ReadByte(); //first byte is actually fine

                        int tex_pos_2 = chunk_dat.ReadByte(); //second byte, 3? bits are flags (what the fuck)
                        int tex_flags = (tex_pos_2 & 0xE0); //mask out the lasts 5 bits

                        flag_buffer[1] = (byte)(tex_pos_2 & 0x1F); //only take the last five bits

                        int tex_coord_pos = BitConverter.ToInt16(flag_buffer);

                        //texture cut size
                        flag_buffer[0] = chunk_dat.ReadByte(); //first byte is actually fine

                        int tex_size_2 = chunk_dat.ReadByte(); //second byte, 4 bits are flags, 2 bits are the color palette, 2 bits are still size (what the fuck)
                        int tex_flags_2 = (tex_size_2 & 0xF0); //mask out the lasts 2 bits

                        flag_buffer[1] = (byte)(tex_size_2 & 3); //only take the last two bits

                        int tex_coord_len = BitConverter.ToInt16(flag_buffer);
                        int part_use_palette = (tex_size_2 & 0xC)>>2;

                        //tex_coord_len &= ~(1 << 2);
                        //tex_coord_len &= ~(1 << 3);

                        Console.WriteLine("sz " + tex_coord_len);


                        //chunk_dat.BaseStream.Position -=1;


                        int t_y = (tex_coord_pos / 32) * 8;
                        int t_x = (tex_coord_pos % 32) * 8;

                        int t_l_y = (tex_coord_len / 32) * 8;
                        int t_l_x = (tex_coord_len % 32) * 8;

                        Console.WriteLine("first offset vals x " + t_x + " y " + t_y + " lx " + t_l_x + " ly " + t_l_y);
                        Console.WriteLine("x off " + x_offset + " y off " + y_offset);

                        i_dat add_dat = new i_dat();
                        add_dat.x = t_x;
                        add_dat.y = t_y;
                        add_dat.len_x = t_l_x;
                        add_dat.len_y = t_l_y;
                        add_dat.paste_x = x_offset;
                        add_dat.paste_y = y_offset;
                        add_dat.flags = tex_flags;
                        add_dat.flags_2 = tex_flags_2;
                        add_dat.use_palette = part_use_palette;




                        //dynamic struct size for transforms
                        switch(tex_flags&(anp2_flag_enable_translation|anp2_flag_enable_scale))
                        {
                            case anp2_flag_enable_translation: //translate/rotate
                                Console.WriteLine("rot");

                                add_dat.offset_x = chunk_dat.ReadByte();
                                add_dat.offset_y = chunk_dat.ReadByte();
                                add_dat.rot_angle = chunk_dat.ReadInt16();
                                break;
                            case anp2_flag_enable_scale: //scale
                                Console.WriteLine("scl");

                                add_dat.scale_x = chunk_dat.ReadInt16();
                                add_dat.scale_y = chunk_dat.ReadInt16();
                                break;
                            case anp2_flag_enable_scale | anp2_flag_enable_translation: //both
                                Console.WriteLine("double trans");

                                add_dat.offset_x = chunk_dat.ReadByte(); //doing everything right in a row is surprisingly sane
                                add_dat.offset_y = chunk_dat.ReadByte();
                                add_dat.rot_angle = chunk_dat.ReadInt16();
                                add_dat.scale_x = chunk_dat.ReadInt16();
                                add_dat.scale_y = chunk_dat.ReadInt16();
                                break;
                        }


                        idat_list.Add(add_dat);

                        chunk_dat.BaseStream.Position = current_offset;
                    }

                    out_dir.Create();
                    //if (save_parts) part_out_dir.Create();

                    var info = new SKImageInfo(512, 512);
                    int off_x = info.Width / 2;
                    int off_y = (info.Height / 2);

                    List<SKBitmap> bitmp_list = new List<SKBitmap>();
                    using (var surface = SKSurface.Create(info))
                    {
                        SKCanvas canvas = surface.Canvas;
                        if (gDoAttemptIndexed) canvas.DrawColor(SKColors.Black); //color 0 in grayscale color palette

                        for (int g = 0; g < idat_list.Count(); g++, chnk_sv++)
                        {
                            i_dat chunk = idat_list[g];

                            SKBitmap in_sheet = new SKBitmap();
                            SKBitmap out_part = new SKBitmap();
                            if (texture_sheets.Count() == 0)
                            {
                                in_sheet = SKBitmap.Decode(in_path);
                                if (chunk.len_x > in_sheet.Width) continue;
                                if (chunk.len_y > in_sheet.Height) continue;
                                if (chunk.x > in_sheet.Width) continue;
                                if (chunk.y > in_sheet.Height) continue;
                                if (chunk.y + chunk.len_y > in_sheet.Height) continue;
                                if (chunk.x + chunk.len_x > in_sheet.Height) continue;
                            }
                            else
                            {
                                if (chunk.y > composite_height)
                                {
                                    continue;
                                }

                                int use_sheet = 0;
                                for(int i = 0; i < texture_sheets.Count(); i++)
                                {
                                    tm2_sheet c_sheet = texture_sheets[i];

                                    if( chunk.y >= c_sheet.exist_y )
                                    {
                                        Console.WriteLine("cy >= "+c_sheet.exist_y+" csheet height "+c_sheet.height+" sel "+use_sheet);
                                        Console.WriteLine("chunky "+chunk.y+" num sheets "+texture_sheets.Count());
                                        if (chunk.y < (c_sheet.exist_y+c_sheet.height))
                                        {
                                            use_sheet = i;
                                            chunk.y -= c_sheet.exist_y;

                                            Console.WriteLine("set sheet: exy "+ c_sheet.exist_y+" chky "+chunk.y);
                                        }
                                    }
                                }


                                var gcHandle = GCHandle.Alloc(texture_sheets[use_sheet].pixel_data, GCHandleType.Pinned);

                                // install the pixels with the color type of the pixel data
                                var img_info = new SKImageInfo(texture_sheets[use_sheet].width, texture_sheets[use_sheet].height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                                in_sheet.InstallPixels(img_info, gcHandle.AddrOfPinnedObject(), img_info.RowBytes, delegate { gcHandle.Free(); }, null);


                                if (!in_sheet.ExtractSubset(out_part, new SKRectI(chunk.x, chunk.y, chunk.x + chunk.len_x, chunk.y + chunk.len_y)))
                                {
                                    Console.WriteLine("failed to grab subset: x "+chunk.x+" y "+chunk.y);
                                    System.Environment.Exit(0);
                                }
                                
                                byte[] opb = out_part.Bytes;
                                for (int p = 0; p < out_part.ByteCount; p +=4)
                                {
                                    byte px_clr_r = opb[p];
                                    int use_al = (pals[chunk.use_palette][(px_clr_r * 4) + 3] * 2 > 255) ? 255 : pals[chunk.use_palette][(px_clr_r * 4) + 3] * 2;

                                    if (gDoAttemptIndexed)
                                    {
                                        px_clr_r += (byte)(0x10 * chunk.use_palette);
                                        opb[p + 0] = px_clr_r;
                                        opb[p + 1] = px_clr_r;
                                        opb[p + 2] = px_clr_r;
                                    }
                                    else
                                    {
                                        opb[p + 0] = pals[chunk.use_palette][(px_clr_r * 4) + 0];
                                        opb[p + 1] = pals[chunk.use_palette][(px_clr_r * 4) + 1];
                                        opb[p + 2] = pals[chunk.use_palette][(px_clr_r * 4) + 2];
                                    }


                                    opb[p + 3] = (byte)use_al;
                                }

                                var partgcHandle = GCHandle.Alloc(opb, GCHandleType.Pinned);
                                out_part.InstallPixels(out_part.Info, partgcHandle.AddrOfPinnedObject(), out_part.RowBytes, delegate { partgcHandle.Free(); }, null);

                                
                                /*
                                unsafe
                                {

                                    for (int p = 0; p < out_part.ByteCount; p += 4)
                                    {

                                        IntPtr pixel_data = out_part.GetPixels();
                                        byte* ptr = (byte*)pixel_data.ToPointer();

                                        //Console.Write(" rgb "+ px_clr_r+" "+ px_clr_g+" "+ px_clr_b+" "+ px_clr_a);

                                        *(ptr + 0+p) = pals[0][(*(ptr + 0+p) * 4) + 0];
                                        *(ptr + 1+p) = pals[0][(*(ptr + 0+p) * 4) + 1];
                                        *(ptr + 2+p) = pals[0][(*(ptr + 0+p) * 4) + 2];


                                        //*(ptr+3) = pals[0][(px_clr * 4) + 3];


                                    }

                                }
                                */

                                if (save_parts)
                                {
                                    //Console.WriteLine("svpart " + b);
                                    using (var data = out_part.Encode(SKEncodedImageFormat.Png, 100))
                                    using (var stream = File.OpenWrite(Path.Combine(part_out_dir.FullName, fname + "_spr_"+h+"_part_"+g+".png")))
                                    {
                                        // save the data to a stream
                                        data.SaveTo(stream);
                                    }
                                    
                                }
                            }


                            int cut_width = chunk.len_x;
                            int cut_height = chunk.len_y;

                            SKPaint part_paint = new SKPaint();
                            // Blend modes are destructive to grayscale palettization
                            if (!gDoAttemptIndexed)
                            {
                                if ((chunk.flags_2 & anp2_flag_additive_blend) != 0)
                                {
                                    part_paint.BlendMode = SKBlendMode.Plus;
                                }
                                else if ((chunk.flags_2 & anp2_flag_negative_blend) != 0)
                                {
                                    part_paint.BlendMode = SKBlendMode.DstATop;
                                }
                            }

                            canvas.Save();
                            canvas.Scale(-2.0f, 2.0f, info.Width / 2, info.Height / 2);
                            //if(gDoAttemptIndexed) canvas.Scale(1, -1, info.Width / 2, info.Height / 2);

                            float prt_scl_x = ((chunk.flags & anp2_flag_enable_scale) == 0) ? 1f : chunk.scale_x / 100f;
                            float prt_scl_y = ((chunk.flags & anp2_flag_enable_scale) == 0) ? 1f : chunk.scale_y / 100f;

                            //SKPoint canvas_trans = new SKPoint((chunk.paste_x - (cut_width / 2)) + off_x, (chunk.paste_y - (cut_height / 2)) + off_y);
                            //SKPoint canvas_trans = new SKPoint(off_x - ((cut_width / 2)), off_y - ((cut_height / 2)));
                            SKPoint canvas_trans = new SKPoint(off_x, off_y);
                            SKPoint chunk_center = new SKPoint(chunk.len_x / 2, chunk.len_y / 2);

                            canvas.Translate(canvas_trans.X, canvas_trans.Y);
                            canvas.Translate(chunk.paste_x - (chunk.len_x / 2), chunk.paste_y - (chunk.len_y / 2));



                            if ((chunk.flags & anp2_flag_enable_translation) != 0)
                            {
                                canvas.RotateDegrees(chunk.rot_angle / 11.33f, chunk_center.X + chunk.rot_axis_x, chunk_center.Y + chunk.rot_axis_y);
                                canvas.Translate(chunk.offset_x,chunk.offset_y);
                            }


                            switch (chunk.flags_2 & (anp2_flag_flip_x | anp2_flag_flip_y))
                            {
                                case anp2_flag_flip_x:
                                    canvas.Scale(-1, 1, chunk_center.X, chunk_center.Y);
                                    break;
                                case anp2_flag_flip_y:
                                    canvas.Scale(1, -1, chunk_center.X, chunk_center.Y);
                                    break;
                                case anp2_flag_flip_x | anp2_flag_flip_y:
                                    canvas.Scale(-1, -1, chunk_center.X, chunk_center.Y);
                                    break;
                            }


                            Console.WriteLine("cx " + chunk.x + " cy " + chunk.y);

                            canvas.DrawBitmap(out_part, new SKRect(0, 0, chunk.len_x, chunk.len_y), part_paint);
                            //canvas.DrawBitmap(in_sheet, new SKPoint(0, 0));
                            canvas.Restore();

                            in_sheet.Dispose();
                            out_part.Dispose();

                            //Console.WriteLine("part " + g + " sprite " + nm);
                        }

                        //Console.WriteLine("begin save " + b);

                        // save the data to a stream

                        if(gDoAttemptIndexed)
                        {
                            //consolidated_pal
                            //surface.Snapshot().SaveBitmap(Path.Combine(out_dir.FullName, fname + "_f_" + h + ".bmp"), consolidated_pal);

                            surface.Snapshot().CursedPng(Path.Combine(out_dir.FullName, fname + "_f_" + h + ".png"), consolidated_pal);

                        }
                        else
                        {
                            using (var image = surface.Snapshot())
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            using (var stream = File.OpenWrite(Path.Combine(out_dir.FullName, fname + "_f_" + h + ".png")))
                            {
                                data.SaveTo(stream);
                                //System.Environment.Exit(0);
                            }
                        }

                        /*
                        if (save_parts)
                        {
                            for (int l = 0; l < bitmp_list.Count; l++)
                            {
                                //Console.WriteLine("svpart " + b);
                                using (var data = bitmp_list[l].Encode(SKEncodedImageFormat.Png, 100))
                                using (var stream = File.OpenWrite(Path.Combine(part_out_dir.FullName, fname + "_a_" + a + "_f_" + b + "_p_" + l + "_indx_" + chnk_sv + ".png")))
                                {
                                    // save the data to a stream
                                    data.SaveTo(stream);
                                }
                            }
                        }
                        */
                    }


                    idat_list.Clear();
                }
            }
        }

        static void anp3_unpack(MemoryStream anp3_file_stream)
        {
            List<weac_dat> weac_tile_list = new List<weac_dat>();

            using (BinaryReader anp3_file = new BinaryReader(anp3_file_stream))
            {
                anp3_file_stream.Position = 4;
                List<i_dat> idat_list = new List<i_dat>();


                uint num_animations = anp3_file.ReadUInt32();


                uint imdat_offset = anp3_file.ReadUInt32();
                uint paldat_offset = anp3_file.ReadUInt32();
                uint palette_count = anp3_file.ReadUInt16();
                uint bpp = anp3_file.ReadUInt16();
                uint unk_hed = anp3_file.ReadUInt16();
                int col_1 = anp3_file.ReadInt16();
                int col_2 = anp3_file.ReadInt16();

                int col_3_sx = anp3_file.ReadInt16();
                int col_4_sy = anp3_file.ReadInt16();
                int col_5_ex = anp3_file.ReadInt16();
                int col_6_ey = anp3_file.ReadInt16();


                byte[][] pals = new byte[palette_count][];

                byte[] composite_palette = new byte[256 * 4];



                if (weapon_equip_path != "" && File.Exists(weapon_equip_path))
                {
                    using (FileStream weac_dat = File.OpenRead(weapon_equip_path))
                    {
                        using (BinaryReader read_weac = new BinaryReader(weac_dat))
                        {
                            int header_string = read_weac.ReadInt32(); //advance past header string
                            if (header_string != 0x43414557)
                            {
                                Console.WriteLine(" ! INVALID WEAC - NO HEADER STRING !");
                            }
                            else
                            {
                                uint entry_count = read_weac.ReadUInt32();
                                for (int i = 0; i < entry_count; i++)
                                {
                                    weac_dat weac_tile = new weac_dat(read_weac.ReadUInt32(), read_weac.ReadUInt32(), read_weac.ReadUInt32());
                                    long before_jump = read_weac.BaseStream.Position;

                                    read_weac.BaseStream.Position = weac_tile.data_offset;
                                    weac_tile.data_array = read_weac.ReadBytes((int)weac_tile.data_size);

                                    weac_tile_list.Add(weac_tile);

                                    read_weac.BaseStream.Position = before_jump;
                                }
                            }
                        }
                    }

                    using (BinaryWriter write_weac = new BinaryWriter(anp3_file_stream, Encoding.UTF8, true))
                    {
                        anp3_file_stream.Position = 0;
                        byte[] header_buffer = new byte[0x20];
                        anp3_file_stream.Read(header_buffer, 0, 0x20);

                        Console.WriteLine("prebpp " + bpp);

                        for (int i = 0; i < weac_tile_list.Count(); i++)
                        {

                            weac_dat paste_tile = weac_tile_list[i];
                            if (i == 0 && bpp == 4)
                            {
                                //really hope this is a max of 0x40
                                anp3_file_stream.Position = paste_tile.paste_offset;

                                write_weac.Write(paste_tile.data_array, 0, 0x20);
                                anp3_file_stream.Position += 0x20;
                                write_weac.Write(paste_tile.data_array, 0x20, 0x20);

                            }
                            else
                            {
                                anp3_file_stream.Position = paste_tile.paste_offset;
                                write_weac.Write(paste_tile.data_array);
                            }

                        }
                    }
                }


                anp3_file.BaseStream.Position = paldat_offset;
                for (int l = 0; l < palette_count; l++)
                {
                    if (bpp == 8)
                    {
                        //pals[l] = anp3_file.ReadBytes(0x400);
                        //break;

                        pals[l] = new byte[256 * 4];

                        List<byte[]> pals_out = new List<byte[]>();


                        for (int col = 0; col < 0x8; col++)
                        {
                            Console.WriteLine("off before: 0x{0:X}", anp3_file.BaseStream.Position);

                            byte[] pal_1_half_1 = anp3_file.ReadBytes(0x20);
                            byte[] pal_2_half_1 = anp3_file.ReadBytes(0x20);

                            byte[] pal_1_half_2 = anp3_file.ReadBytes(0x20);
                            byte[] pal_2_half_2 = anp3_file.ReadBytes(0x20);

                            pals_out.Add(pal_1_half_1.Concat(pal_1_half_2).ToArray());
                            pals_out.Add(pal_2_half_1.Concat(pal_2_half_2).ToArray());

                            Console.WriteLine("off after: 0x{0:X}", anp3_file.BaseStream.Position);
                        }

                        if (File.Exists("custom.pal"))
                        {
                            pals[l] = File.ReadAllBytes("custom.pal");
                        }
                        else
                        {
                            pals[l] = pals_out.SelectMany(x => x).ToArray();
                        }


                        Console.WriteLine("fpr " + pals[l].Length);

                        //File.WriteAllBytes("outtestpal.bin",pals[l]);

                        break;
                    }
                    else
                    {
                        byte[] pal_1_half_1 = anp3_file.ReadBytes(0x20);
                        byte[] pal_2_half_1 = anp3_file.ReadBytes(0x20);

                        byte[] pal_1_half_2 = anp3_file.ReadBytes(0x20);
                        byte[] pal_2_half_2 = anp3_file.ReadBytes(0x20);

                        pals[l] = pal_1_half_1.Concat(pal_1_half_2).ToArray();
                        pals[l + 1] = pal_2_half_1.Concat(pal_2_half_2).ToArray();

                        l++;

                        /*
                        byte[] pal_half_1 = anp3_file.ReadBytes(0x20);
                        anp3_file.BaseStream.Position += 0x20;
                        byte[] pal_half_2 = anp3_file.ReadBytes(0x20);

                        pals[l] = pal_half_1.Concat(pal_half_2).ToArray();

                        Console.WriteLine("size " + pals[l].Length);

                        anp3_file.BaseStream.Position -= 0x40;
                        */
                    }

                }

                if (gDoAttemptIndexed)
                {
                    palettes_out_dir.Create();
                    //File.WriteAllBytes(Path.Combine(palettes_out_dir.FullName, "combined_weapon_pal_" + weapon_equip + ".bin"), weapon_pals_list[weapon_equip].SelectMany(x => x).ToArray());
                    if (bpp == 4)
                    {
                        File.WriteAllBytes(Path.Combine(palettes_out_dir.FullName, "combined_chara_pal" + ".bin"), pals.SelectMany(x => x).ToArray());
                        pals.SelectMany(x => x).ToArray().CopyTo(composite_palette, 0);
                    }
                    else
                    {
                        File.WriteAllBytes(Path.Combine(palettes_out_dir.FullName, "combined_chara_pal" + ".bin"), pals[0]);
                        pals[0].CopyTo(composite_palette, 0);
                    }

                    /*
                    for(int g = 0; g < composite_palette.Length; g+=4)
                    {
                        //reverse the color palette for bmp export

                        byte[] buffer = new byte[3];
                        Array.ConstrainedCopy(composite_palette,g,buffer,0,3);
                        Array.Reverse(buffer);
                        Array.ConstrainedCopy(buffer, 0, composite_palette, g, 3);

                        
                    }
                    */
                }


                /*
                if (bpp == 8 && weac_tile_list.Count > 0)
                {
                    uint palarray_copy_index = weac_tile_list[0].paste_offset - paldat_offset;
                    Console.WriteLine("attempt copy at " + palarray_copy_index);
                    weac_tile_list[0].data_array.CopyTo(pals[0], palarray_copy_index);
                }
                */

                uint[] anim_offs = new uint[num_animations];

                for (int a = 0; a < num_animations; a++)
                {
                    anp3_file.BaseStream.Position = (0x20) + (a * 2);

                    Console.WriteLine("table off: 0x{0:X}", anp3_file.BaseStream.Position);

                    uint c_ani_offset = anp3_file.ReadUInt16();

                    Console.WriteLine("cani off: 0x{0:X}", c_ani_offset);

                    anp3_file.BaseStream.Position = c_ani_offset;

                    uint c_ani_frame_count = anp3_file.ReadByte();

                    Console.WriteLine("confirmed " + c_ani_frame_count + " frames");

                    uint c_ani_type = anp3_file.ReadByte();

                    //redundancy checking
                    bool skip = false;
                    for (int ani_c = 0; ani_c < anim_offs.Length; ani_c++)
                    {
                        if (anim_offs[ani_c] == c_ani_offset)
                        {
                            Console.WriteLine("prexist: 0x{0:X}", c_ani_offset);
                            skip = true;
                            break;
                        }
                    }

                    if (skip) continue;
                    anim_offs[a] = c_ani_offset;

                    //unk
                    anp3_file.BaseStream.Position += 2;



                    int chnk_sv = 0;

                    for (int b = 0; b < c_ani_frame_count; b++)
                    {
                        uint last_pos_anim = (uint)anp3_file.BaseStream.Position;

                        uint frame_offset = anp3_file.ReadUInt32();

                        Console.WriteLine("frameoff: 0x{0:X}", frame_offset);

                        uint frame_duration = anp3_file.ReadUInt32();

                        anp3_file.BaseStream.Position = frame_offset;
                        uint num_sprite_parts = anp3_file.ReadByte();
                        uint unk = anp3_file.ReadByte();

                        //unk
                        anp3_file.BaseStream.Position += 2;

                        Console.WriteLine("confirmed " + num_sprite_parts + " parts");

                        for (int c = 0; c < num_sprite_parts; c++)
                        {
                            Console.WriteLine("working part " + c);
                            i_dat sprite_part = new i_dat();

                            uint last_pos_part = (uint)anp3_file.BaseStream.Position;

                            uint image_data_offset = anp3_file.ReadUInt32();
                            Console.WriteLine("idatoff: 0x{0:X}", image_data_offset);

                            int part_xoff = anp3_file.ReadInt16();
                            int part_yoff = anp3_file.ReadInt16();
                            uint part_flags = anp3_file.ReadByte();

                            sprite_part.paste_x = part_xoff;
                            sprite_part.paste_y = part_yoff;
                            sprite_part.flags = (int)part_flags;

                            Console.WriteLine("part flgs value " + part_flags);

                            uint part_palette = anp3_file.ReadByte();

                            Console.WriteLine("load pal usevl: " + part_palette);

                            sprite_part.use_palette = (int)part_palette;

                            uint unk20 = anp3_file.ReadByte();
                            uint stretchyboy = anp3_file.ReadByte();


                            int come_out_offset = 0xC;

                            //both scale and rotate case
                            if ((part_flags & anp3_flag_enable_rotate) != 0 && (part_flags & anp3_flag_enable_scale) != 0)
                            {
                                Console.WriteLine("rotation and scale");

                                int x_rot_axis = anp3_file.ReadSByte();
                                int y_rot_axis = anp3_file.ReadSByte();
                                int rot_angle = anp3_file.ReadInt16();
                                int scale_x = anp3_file.ReadInt16();
                                int scale_y = anp3_file.ReadInt16();

                                come_out_offset += 8;

                                sprite_part.rot_axis_x = x_rot_axis;
                                sprite_part.rot_axis_y = y_rot_axis;
                                sprite_part.rot_angle = rot_angle;
                                sprite_part.scale_x = scale_x;
                                sprite_part.scale_y = scale_y;
                            }
                            else if ((part_flags & anp3_flag_enable_rotate) != 0) //rotate case
                            {
                                Console.WriteLine("rotation");

                                int x_rot_axis = anp3_file.ReadSByte();
                                int y_rot_axis = anp3_file.ReadSByte();
                                int rot_angle = anp3_file.ReadInt16();
                                //int scale_x = anp3_file.ReadInt16();
                                //int scale_y = anp3_file.ReadInt16();

                                come_out_offset += 4;

                                sprite_part.rot_axis_x = x_rot_axis;
                                sprite_part.rot_axis_y = y_rot_axis;
                                sprite_part.rot_angle = rot_angle;
                            }
                            else if ((part_flags & anp3_flag_enable_scale) != 0) //scale case
                            {
                                Console.WriteLine("scale");
                                int scale_x = anp3_file.ReadInt16();
                                int scale_y = anp3_file.ReadInt16();

                                come_out_offset += 4;

                                sprite_part.scale_x = scale_x;
                                sprite_part.scale_y = scale_y;
                            }
                            else
                            {
                                Console.WriteLine("normal");
                            }

                            anp3_file.BaseStream.Position = image_data_offset;

                            uint tile_width = anp3_file.ReadByte();
                            uint tile_height = anp3_file.ReadByte();
                            uint data_size = anp3_file.ReadUInt16();

                            sprite_part.len_x = (int)tile_width;
                            sprite_part.len_y = (int)tile_height;

                            //Console.WriteLine("t_w: "+tile_width+" t_h: "+tile_height);


                            //unk
                            anp3_file.BaseStream.Position += 0x0C;

                            //Console.WriteLine("load pixeldat pos "+ anp3_file.BaseStream.Position);

                            Console.WriteLine("bpp " + bpp);
                            if (bpp == 4)
                            {
                                byte[] pixel_data = new byte[(tile_width * tile_height)];

                                //Console.WriteLine("pxdl "+pixel_data.Length);

                                for (int d = 0, f = 0; d < (tile_width * tile_height) / 2; d++, f += 2)
                                {
                                    //Console.WriteLine("read 1 " + anp3_file.BaseStream.Position+" drd "+d);

                                    uint raw_byte = anp3_file.ReadByte();
                                    uint pixel_1 = ((raw_byte & 0xF0) >> 4);
                                    uint pixel_2 = (raw_byte & 0x0F);

                                    if (gDoAttemptIndexed)
                                    {
                                        pixel_1 += (uint)(sprite_part.use_palette) * 0x10;
                                        pixel_2 += (uint)(sprite_part.use_palette) * 0x10;
                                    }

                                    pixel_data[f + 1] = (byte)pixel_1;
                                    pixel_data[f] = (byte)pixel_2;
                                }

                                //File.WriteAllBytes("unswizzle.bin",pixel_data);


                                byte[] colored_pixel_data = new byte[pixel_data.Length * 4];
                                for (int f = 0; f < pixel_data.Length; f++)
                                {
                                    int pixel_value = pixel_data[f];
                                    byte[] use_palette = pals[part_palette];
                                    int pxval_offset = (sprite_part.use_palette) * 0x10;


                                    if (gDoAttemptIndexed)
                                    {
                                        int alpha = (use_palette[((pixel_value - pxval_offset) * 4) + 3] == 0) ? 0 : 0xff;

                                        colored_pixel_data[(f * 4) + 0] = (byte)pixel_value;
                                        colored_pixel_data[(f * 4) + 1] = (byte)pixel_value;
                                        colored_pixel_data[(f * 4) + 2] = (byte)pixel_value;
                                        colored_pixel_data[(f * 4) + 3] = (byte)alpha;
                                    }
                                    else
                                    {
                                        colored_pixel_data[(f * 4) + 0] = use_palette[(pixel_value * 4) + 0];
                                        colored_pixel_data[(f * 4) + 1] = use_palette[(pixel_value * 4) + 1];
                                        colored_pixel_data[(f * 4) + 2] = use_palette[(pixel_value * 4) + 2];
                                        colored_pixel_data[(f * 4) + 3] = use_palette[(pixel_value * 4) + 3];

                                        if (pixel_value == 0)
                                        {
                                            //hide
                                            colored_pixel_data[(f * 4) + 3] = 0;
                                        }
                                        else
                                        {
                                            //multiply alpha
                                            int use_al = (colored_pixel_data[(f * 4) + 3] * 2 > 255) ? 255 : colored_pixel_data[(f * 4) + 3];

                                            colored_pixel_data[(f * 4) + 3] = (byte)use_al;
                                        }
                                    }



                                }

                                //Console.WriteLine("pxdl_cl " + colored_pixel_data.Length);

                                //File.WriteAllBytes("unswizzle_clrd.bin", colored_pixel_data);
                                //System.Environment.Exit(0);

                                sprite_part.pixel_data = colored_pixel_data;
                                //sprite_part.pixel_data = pixel_data;
                            }
                            else
                            if (bpp == 8)
                            {

                                byte[] pixel_data = new byte[(tile_width * tile_height)];

                                //Console.WriteLine("pxdl "+pixel_data.Length);

                                for (int d = 0; d < pixel_data.Length; d++)
                                {
                                    //Console.WriteLine("read 1 " + anp3_file.BaseStream.Position+" drd "+d);

                                    //pixel_data[d] = anp3_file.ReadByte();
                                    pixel_data[d] = anp3_file.ReadByte();
                                    if (gDoAttemptIndexed)
                                    {
                                        //pixel_data[d] *= 0x10;
                                    }
                                }

                                byte[] colored_pixel_data = new byte[pixel_data.Length * 4];
                                for (int f = 0; f < pixel_data.Length; f++)
                                {

                                    byte[] use_palette = pals[part_palette];
                                    int pixel_value = pixel_data[f];
                                    int pxval_offset = (sprite_part.use_palette) * 0x10;


                                    if (gDoAttemptIndexed)
                                    {
                                        int alpha = (use_palette[((pixel_value - pxval_offset) * 4) + 3] == 0) ? 0 : 0xff;

                                        colored_pixel_data[(f * 4) + 0] = (byte)pixel_value;
                                        colored_pixel_data[(f * 4) + 1] = (byte)pixel_value;
                                        colored_pixel_data[(f * 4) + 2] = (byte)pixel_value;
                                        colored_pixel_data[(f * 4) + 3] = (byte)alpha;
                                    }
                                    else
                                    {
                                        colored_pixel_data[(f * 4) + 0] = use_palette[(pixel_value * 4) + 0];
                                        colored_pixel_data[(f * 4) + 1] = use_palette[(pixel_value * 4) + 1];
                                        colored_pixel_data[(f * 4) + 2] = use_palette[(pixel_value * 4) + 2];
                                        colored_pixel_data[(f * 4) + 3] = use_palette[(pixel_value * 4) + 3];

                                        if (pixel_value == 0)
                                        {
                                            //hide
                                            colored_pixel_data[(f * 4) + 3] = 0;
                                        }
                                        else
                                        {
                                            //multiply alpha
                                            int use_al = (colored_pixel_data[(f * 4) + 3] * 2 > 255) ? 255 : colored_pixel_data[(f * 4) + 3];

                                            colored_pixel_data[(f * 4) + 3] = (byte)use_al;
                                        }
                                    }

                                }

                                sprite_part.pixel_data = colored_pixel_data;
                            }

                            //Console.WriteLine("idatoff: 0x{0:X}", image_data_offset);

                            anp3_file.BaseStream.Position = last_pos_part + come_out_offset;
                            Console.WriteLine("return from part to: 0x{0:X}", anp3_file.BaseStream.Position);

                            idat_list.Add(sprite_part);
                        }


                        out_dir.Create();
                        if (save_parts) part_out_dir.Create();

                        var info = new SKImageInfo(1024, 768);
                        int off_x = info.Width / 2;
                        int off_y = (info.Height / 2 + 128);

                        idat_list.Reverse();

                        List<SKBitmap> bitmp_list = new List<SKBitmap>();
                        using (var surface = SKSurface.Create(info))
                        {
                            SKCanvas canvas = surface.Canvas;
                            if (gDoAttemptIndexed) canvas.DrawColor(SKColors.Black); //color 0 in grayscale color palette

                            for (int g = 0; g < idat_list.Count(); g++, chnk_sv++)
                            {
                                i_dat chunk = idat_list[g];

                                //skip parts with no pixel data
                                if (chunk.pixel_data.Length == 0) continue;

                                int cut_width = chunk.len_x;
                                int cut_height = chunk.len_y;


                                SKBitmap part_image = new SKBitmap();

                                //stackoverflow bros
                                // pin the managed array so that the GC doesn't move it
                                var gcHandle = GCHandle.Alloc(chunk.pixel_data, GCHandleType.Pinned);

                                // install the pixels with the color type of the pixel data
                                var infoa = new SKImageInfo(cut_width, cut_height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                                part_image.InstallPixels(infoa, gcHandle.AddrOfPinnedObject(), infoa.RowBytes, delegate { gcHandle.Free(); }, null);

                                if (save_parts)
                                {
                                    bitmp_list.Add(part_image);
                                }

                                SKPaint part_paint = new SKPaint();
                                // Blend modes are destructive to grayscale palettization
                                if (!gDoAttemptIndexed)
                                {
                                    if ((chunk.flags & anp3_flag_enable_opacity) != 0)
                                    {
                                        //part_paint.BlendMode = SKBlendMode.Plus;
                                    }
                                }

                                canvas.Save();
                                canvas.Scale(-2.0f, 2.0f, info.Width / 2, info.Height / 2);

                                //if (gDoAttemptIndexed) canvas.Scale(1, -1, info.Width / 2, info.Height / 2);



                                float prt_scl_x = ((chunk.flags & anp3_flag_enable_scale) == 0) ? 1f : chunk.scale_x / 100f;
                                float prt_scl_y = ((chunk.flags & anp3_flag_enable_scale) == 0) ? 1f : chunk.scale_y / 100f;

                                //SKPoint canvas_trans = new SKPoint((chunk.paste_x - (cut_width / 2)) + off_x, (chunk.paste_y - (cut_height / 2)) + off_y);
                                //SKPoint canvas_trans = new SKPoint(off_x - ((cut_width / 2)), off_y - ((cut_height / 2)));
                                SKPoint canvas_trans = new SKPoint(off_x, off_y);
                                SKPoint chunk_center = new SKPoint(chunk.len_x / 2, chunk.len_y / 2);

                                canvas.Translate(canvas_trans.X, canvas_trans.Y);
                                canvas.Translate(chunk.paste_x - (chunk.len_x / 2), chunk.paste_y - (chunk.len_y / 2));

                                if ((chunk.flags & anp3_flag_enable_rotate) != 0) canvas.RotateDegrees(chunk.rot_angle / 11.33f, chunk_center.X + chunk.rot_axis_x, chunk_center.Y + chunk.rot_axis_y);
                                if ((chunk.flags & anp3_flag_enable_scale) != 0) canvas.Scale(prt_scl_x, prt_scl_y, chunk_center.X, chunk_center.Y);



                                switch (chunk.flags & (anp3_flag_flip_x | anp3_flag_flip_y))
                                {
                                    case anp3_flag_flip_x:
                                        canvas.Scale(-1, 1, chunk_center.X, chunk_center.Y);
                                        break;
                                    case anp3_flag_flip_y:
                                        canvas.Scale(1, -1, chunk_center.X, chunk_center.Y);
                                        break;
                                    case anp3_flag_flip_x | anp3_flag_flip_y:
                                        canvas.Scale(-1, -1, chunk_center.X, chunk_center.Y);
                                        break;
                                }


                                canvas.DrawBitmap(part_image, new SKPoint(0, 0), part_paint);
                                canvas.Restore();

                                //Console.WriteLine("part " + g + " sprite " + nm);
                            }

                            //Console.WriteLine("begin save " + b);

                            DirectoryInfo in_dir = new DirectoryInfo(og_in_path);

                            string fname = in_dir.Name.Substring(0, in_dir.Name.Length - in_dir.Extension.Length);
                            string write_path = Path.Combine(out_dir.FullName, fname + "_a_" + a + "_f_" + b + "_d_" + frame_duration);

                            if (gDoAttemptIndexed)
                            {
                                //make a whole fucking bitmap
                                using (var image = surface.Snapshot() )
                                {
                                    //image.SaveBitmap(write_path + ".bmp", composite_palette);
                                    image.CursedPng(write_path + ".png", composite_palette);
                                }
                            }
                            else
                            {
                                // save the data to a stream
                                using (var image = surface.Snapshot())
                                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                                using (var stream = File.OpenWrite(write_path + ".png"))
                                {
                                    data.SaveTo(stream);
                                    //System.Environment.Exit(0);
                                }
                            }



                            if (save_parts)
                            {
                                for (int l = 0; l < bitmp_list.Count; l++)
                                {
                                    //Console.WriteLine("svpart " + b);
                                    using (var data = bitmp_list[l].Encode(SKEncodedImageFormat.Png, 100))
                                    using (var stream = File.OpenWrite(Path.Combine(part_out_dir.FullName, fname + "_a_" + a + "_f_" + b + "_p_" + l + "_indx_" + chnk_sv + ".png")))
                                    {
                                        // save the data to a stream
                                        data.SaveTo(stream);
                                    }
                                }
                            }

                        }


                        idat_list.Clear();

                        anp3_file.BaseStream.Position = last_pos_anim + 8;
                        //Console.WriteLine("return from anim to: 0x{0:X}", anp3_file.BaseStream.Position);



                    }
                }
            }
            //System.Environment.Exit(0);
        }


        static void V103_unpack(MemoryStream file_stream)
        {
            using (BinaryReader file = new BinaryReader(file_stream))
            {
                file_stream.Position +=4;
                List<i_dat> idat_list = new List<i_dat>();

                uint char_pal_size = file.ReadUInt32(); //pal related 0x4 (size of player palette block)
                uint pal_offset = file.ReadUInt32(); //0x8
                uint weapon_pal_size = file.ReadUInt32(); //0xC weapon pal size
                uint weapon_pal_offset = file.ReadUInt32(); //0x10
                //uint unk4 = file.ReadUInt32(); //0x14

                uint tex_length_1 = file.ReadUInt32(); //0x14
                uint tex_offset_1 = file.ReadUInt32(); //0x18

                //the offset to the equipped weapon from the texture sheet beginning
                uint equipped_weapon_size = file.ReadUInt32(); //0x1C

                uint tex_offset_2 = file.ReadUInt32(); //only if applicable? 0x20

                uint num_chunks = file.ReadUInt32(); //0x24

                uint chunk_tbl_offset = file.ReadUInt32(); //0x28

                uint frame_data_num = file.ReadUInt32(); //0x2C

                uint frame_data_offset = file.ReadUInt32(); //0x30

                uint num_sprites = file.ReadUInt32(); //0x34

                uint sprite_def_offset = file.ReadUInt32(); //0x38
                uint num_anim = file.ReadUInt32(); //0x3C
                uint animation_def_offset = file.ReadUInt32(); //0x40

                uint unk9 = file.ReadUInt32();

                uint num_pals_char = char_pal_size / 0x40;
                uint num_pals_weapon = weapon_pal_size / 0x40;

                byte[][] pals = new byte[num_pals_char][];

                List<byte[][]> weapon_pals_list = new List<byte[][]>();

                file.BaseStream.Position = pal_offset;
                for (int i = 0; i < num_pals_char; i++)
                {
                    pals[i] = file.ReadBytes(0x40);
                }

                file.BaseStream.Position = weapon_pal_offset;

                //keep slurping until you reach the end of the color data, accomodate all potential equipped weapon palette selections
                while(file.BaseStream.Position != chunk_tbl_offset)
                {
                    Console.WriteLine("slurping "+ file.BaseStream.Position+" npl "+num_pals_weapon);
                    if(file.BaseStream.Position > chunk_tbl_offset)
                    {
                        Console.WriteLine("the fuck " + file.BaseStream.Position);
                        System.Environment.Exit(0);

                    }

                    byte[][] weapon_pals = new byte[num_pals_weapon][];
                    for (int i = 0; i < num_pals_weapon; i++)
                    {
                        weapon_pals[i] = file.ReadBytes(0x40);
                    }

                    weapon_pals_list.Add(weapon_pals);
                }

                byte[] consolidiated_pal = new byte[256 * 4];

                if(gDoAttemptIndexed)
                {
                    palettes_out_dir.Create();

                    byte[] combined_chara_pal = pals.SelectMany(x => x).ToArray();
                    File.WriteAllBytes(Path.Combine(palettes_out_dir.FullName, "combined_weapon_pal_"+weapon_equip+".bin"), weapon_pals_list[weapon_equip].SelectMany(x => x).ToArray());
                    File.WriteAllBytes(Path.Combine(palettes_out_dir.FullName, "combined_chara_pal_" + weapon_equip + ".bin"), combined_chara_pal);

                    byte[] t = weapon_pals_list[weapon_equip].SelectMany(x => x).ToArray();

                    combined_chara_pal.CopyTo(consolidiated_pal, 0);
                    Array.ConstrainedCopy(t, 0, consolidiated_pal, combined_chara_pal.Length, t.Length);
                }
            


                List<uint> sprite_parts_list = new List<uint>();
                file.BaseStream.Position = sprite_def_offset;

                for (int a = 0; a < num_sprites; a++)
                {
                    uint sprite_parts = file.ReadUInt16();

                    sprite_parts_list.Add(sprite_parts);

                    file.BaseStream.Position += 18;
                }

                file.BaseStream.Position = chunk_tbl_offset;
                for (int c = 0; c < num_chunks; c++)
                {
                    Console.WriteLine("working part " + c);
                    i_dat sprite_part = new i_dat();

                    uint part_palette = file.ReadByte();
                    uint part_flags = file.ReadByte();

                    uint part_unk = file.ReadByte();

                    byte tile_height_read = file.ReadByte();

                    //the first 4 bits are used for something else, the y height is stored solely in the latter 4 bits
                    uint tile_height = (uint)(tile_height_read & 0x0F);

                    uint tile_width = file.ReadByte();

                    Console.WriteLine(" t scale y "+tile_height+" x "+tile_width);
                    Console.WriteLine("offset: 0x{0:X}", file.BaseStream.Position);

                    uint part_unk_2 = file.ReadByte();

                    int part_xoff = file.ReadSByte();
                    int part_yoff = file.ReadSByte();

                    int scale_x = file.ReadSByte();
                    int scale_y = file.ReadSByte();

                    int x_rot_axis = file.ReadSByte();
                    int y_rot_axis = file.ReadSByte();

                    int x_center = file.ReadInt16();
                    int y_center = file.ReadInt16();

                    int rot_angle = file.ReadInt16();
                    int tile_y = file.ReadInt16();

                    sprite_part.y = tile_y;

                    sprite_part.rot_axis_x = x_center+x_rot_axis;
                    sprite_part.rot_axis_y = y_center+y_rot_axis;

                    //sprite_part.rot_axis_y = y_rot_axis;
                    //sprite_part.rot_axis_y = y_rot_axis;

                    sprite_part.chunk_center_x = x_center;
                    sprite_part.chunk_center_y = y_center;


                    sprite_part.rot_angle = rot_angle;
                    sprite_part.scale_x = scale_x;
                    sprite_part.scale_y = scale_y;


                    sprite_part.paste_x = part_xoff;
                    sprite_part.paste_y = part_yoff;
                    sprite_part.flags = (int)part_flags;

                    Console.WriteLine("part flgs value " + part_flags);

                    Console.WriteLine("load pal usevl: " + part_palette);

                    sprite_part.use_palette = (int)part_palette;

                    sprite_part.len_x = (int)tile_width;
                    sprite_part.len_y = (int)tile_height;

                    //Console.WriteLine("read: 0x{0:X}", file.BaseStream.Position);

                    int cut_width = (32 * sprite_part.len_x);
                    int cut_height = (8 << sprite_part.len_y);

                    byte[] pixel_data = new byte[cut_width * cut_height];

                    Console.WriteLine(" lw "+ sprite_part.len_x+" ly "+ sprite_part.len_y);
                    Console.WriteLine("projected pxldt size " + pixel_data.Length);

                    uint last_pos_part = (uint)file.BaseStream.Position;


                    bool wp_eq = false;
                    if(sprite_part.use_palette < 0x20)
                    {
                        file.BaseStream.Position = tex_offset_1 + (sprite_part.y * 32);
                    }
                    else
                    {
                        file.BaseStream.Position = (tex_offset_2 + (equipped_weapon_size * weapon_equip)) + (sprite_part.y * 32);
                        wp_eq = true;
                    }
                

                    for (int d = 0, f = 0; d < ( (cut_width * cut_height) / 2); d++, f += 2)
                    {
                        if (sprite_part.len_y > 6) break;
                        uint raw_byte = file.ReadByte();
                        uint pixel_1 = ((raw_byte & 0xF0) >> 4);
                        uint pixel_2 = (raw_byte & 0x0F);

                        if(gDoAttemptIndexed)
                        {
                            pixel_1 += (uint)(sprite_part.use_palette & 0x0F) * 0x10;
                            pixel_2 += (uint)(sprite_part.use_palette & 0x0F) * 0x10;

                            if(wp_eq)
                            {
                                pixel_1 += (uint)pals.Length * 0x10;
                                pixel_2 += (uint)pals.Length * 0x10;
                            }
                        }


                        pixel_data[f + 1] = (byte)pixel_1;
                        pixel_data[f] = (byte)pixel_2;
                    }

                    byte[] colored_pixel_data = new byte[pixel_data.Length * 4];

                    Console.WriteLine("projected fullclr size " + colored_pixel_data.Length);

                    //System.Environment.Exit(0);

                    for (int f = 0; f < pixel_data.Length; f++)
                    {
                        if (sprite_part.len_y > 6) break;
                        int pixel_value = pixel_data[f];

                        byte[][] pal_array = ( sprite_part.use_palette >= 0x20 ) ? weapon_pals_list[weapon_equip] : pals;
                        int use_pal = (weapon_equip > 0 && sprite_part.use_palette >= 0x20) ? (sprite_part.use_palette & 0x0F) : (sprite_part.use_palette & 0x0F);

                        //if (use_pal == 1 && c_pal) pixel_value += 64;


                        int pxval_offset = (sprite_part.use_palette & 0x0F) * 0x10;

                        if (gDoAttemptIndexed)
                        {
                            int alpha = (consolidiated_pal[ ( (pixel_value - pxval_offset) * 4) + 3] == 0 )? 0 : 0xff;

                            colored_pixel_data[(f * 4) + 0] = (byte)pixel_value;
                            colored_pixel_data[(f * 4) + 1] = (byte)pixel_value;
                            colored_pixel_data[(f * 4) + 2] = (byte)pixel_value;
                            colored_pixel_data[(f * 4) + 3] = (byte)alpha;
                        }
                        else
                        {
                            colored_pixel_data[(f * 4) + 0] = pal_array[use_pal][(pixel_value * 4) + 0];
                            colored_pixel_data[(f * 4) + 1] = pal_array[use_pal][(pixel_value * 4) + 1];
                            colored_pixel_data[(f * 4) + 2] = pal_array[use_pal][(pixel_value * 4) + 2];
                            colored_pixel_data[(f * 4) + 3] = pal_array[use_pal][(pixel_value * 4) + 3];
                        }


                        if (pixel_value == 0)
                        {
                            //hide
                            colored_pixel_data[(f * 4) + 3] = 0;
                        }
                        else
                        {
                            //multiply alpha
                            int use_al = (colored_pixel_data[(f * 4) + 3] * 2 > 255) ? 255 : colored_pixel_data[(f * 4) + 3];

                            colored_pixel_data[(f * 4) + 3] = (byte)use_al;
                        
                        }
                    }

                    if(sprite_part.use_palette==0x20)
                    {
                        //File.WriteAllBytes("test.bin", pixel_data);
                        //System.Environment.Exit(0);
                    }

                    sprite_part.pixel_data = colored_pixel_data;

                    //if (chunk.len_y > 6) continue;

                    idat_list.Add(sprite_part);

                    file.BaseStream.Position = last_pos_part;
                }

                //file.BaseStream.Position = tex_offset_1;
                Console.WriteLine("start reading sprite data at: 0x{0:X}", file.BaseStream.Position);

                Console.WriteLine("confirmed "+sprite_parts_list.Count+" spr");

                out_dir.Create();
                if (save_parts) part_out_dir.Create();
                var info = new SKImageInfo(1024, 768);

                int off_x = info.Width / 2;
                int off_y = (info.Height / 2 + 128);

                    for (int nm = 0, f = 0; nm < sprite_parts_list.Count; nm++)
                    {
                        List<SKBitmap> bitmp_list = new List<SKBitmap>();
                        using (var surface = SKSurface.Create(info))
                        {
                            SKCanvas canvas = surface.Canvas;
                            if (gDoAttemptIndexed) canvas.DrawColor(SKColors.Black); //color 0 in grayscale color palette

                            for (int g = 0; g < sprite_parts_list[nm]; g++, f++)
                            {
                                uint num_to_process = sprite_parts_list[nm];
                                i_dat chunk = idat_list[f];

                                //oops?????
                                if (chunk.len_x == 0) continue;
                                if (chunk.use_palette < 0x20 && exclude_char) continue;
                                if (chunk.use_palette >= 0x20 && exclude_wep) continue;

                                Console.WriteLine("using palette: " + chunk.use_palette + " sz: x " + chunk.len_x + " y " + chunk.len_y + " file_len " + chunk.pixel_data.Length);
                                //File.WriteAllBytes("palette_data.bin", pals[0]);
                                //File.WriteAllBytes("pixel_data.bin", chunk.pixel_data);

                                int true_y = (chunk.pixel_data.Length / 4) / 32;

                                int cut_width = (32 * chunk.len_x);
                                int cut_height = (8 << chunk.len_y);

                                Console.WriteLine("len " + chunk.len_y + " ch " + cut_height);

                                byte[] test = new byte[] { 50, 50, 50, 255 };

                                Console.WriteLine("cut at: " + (chunk.y * 2) + " tru y: " + true_y);

                                Console.WriteLine("cutsz x: " + cut_width + " y: " + cut_height);

                                SKBitmap part_image = new SKBitmap();

                                //stackoverflow bros

                                // pin the managed array so that the GC doesn't move it
                                var gcHandle = GCHandle.Alloc(chunk.pixel_data, GCHandleType.Pinned);

                                // install the pixels with the color type of the pixel data
                                var infoa = new SKImageInfo(cut_width, cut_height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                                part_image.InstallPixels(infoa, gcHandle.AddrOfPinnedObject(), infoa.RowBytes, delegate { gcHandle.Free(); }, null);

                                //Console.WriteLine("check");

                                SKBitmap part_image_canvas = new SKBitmap(info.Width, info.Height);
                                SKCanvas part_canvas = new SKCanvas(part_image_canvas);

                                if (save_parts)
                                {
                                    bitmp_list.Add(part_image);
                                }

                                canvas.Save();

                                float prt_scl_x = (chunk.scale_x / 10f);
                                float prt_scl_y = (chunk.scale_y / 10f);

                                canvas.Scale(-2.0f, 2.0f, info.Width / 2, info.Height / 2);


                                SKPaint part_paint = new SKPaint();

                                //destructive to grayscale palettization
                                if (!gDoAttemptIndexed)
                                {
                                    if ((chunk.flags & V103_flag_blendmode_add) != 0)
                                    {
                                        part_paint.BlendMode = SKBlendMode.Plus;
                                    }
                                    else if ((chunk.flags & V103_flag_blendmode_sub) != 0)
                                    {
                                        part_paint.BlendMode = SKBlendMode.DstATop;
                                    }
                                }
                                else
                                {
                                    if ((chunk.flags & V103_flag_blendmode_add) != 0 || (chunk.flags & V103_flag_blendmode_sub) != 0) continue;
                                    //export these individually later

                                }


                                int t_x = off_x;
                                int t_y = off_y;

                                int paste_x_multi = 1;
                                int paste_y_multi = 1;

                                int x_multiplier_flag = V103_flag_quadruple_x_offset | V103_flag_double_x_offset;
                                int y_multiplier_flag = V103_flag_quadruple_y_offset | V103_flag_double_y_offset;

                                // Apply multiplier flags
                                if ((chunk.flags & x_multiplier_flag) != 0)
                                {
                                    chunk.paste_x *= ((chunk.flags & V103_flag_quadruple_x_offset) != 0) ? 4 : 2;
                                }
                                if ((chunk.flags & y_multiplier_flag) != 0)
                                {
                                    chunk.paste_y *= ((chunk.flags & V103_flag_quadruple_y_offset) != 0) ? 4 : 2;

                                }

                                int paste_x = chunk.paste_x * paste_x_multi;
                                int paste_y = chunk.paste_y * paste_y_multi;

                                //SKPoint canvas_trans = new SKPoint( (( (chunk.paste_x * prt_scl_x) - ( (cut_width * prt_scl_x) / 2) )) + off_x, (chunk.paste_y - (cut_height / 2)  ) + off_y);
                                SKPoint canvas_trans = new SKPoint((paste_x - (cut_width / 2)) + off_x, (paste_y - (cut_height / 2)) + off_y);

                                canvas.RotateDegrees(chunk.rot_angle, t_x + chunk.rot_axis_x, t_y - chunk.rot_axis_y);
                                canvas.Scale(prt_scl_x, prt_scl_y, t_x + chunk.chunk_center_x, t_y - chunk.chunk_center_y);

                                //if (prt_scl_x != 1) canvas.Scale(prt_scl_x, prt_scl_y);

                                //canvas.Translate(canvas_trans.X, canvas_trans.Y);

                                float use_scl_x = ((chunk.flags & V103_flag_flip_x) != 0) ? -1 : 1;
                                float use_scl_y = ((chunk.flags & V103_flag_flip_y) != 0) ? -1 : 1;

                                float use_flp_trans_x = ((chunk.flags & V103_flag_flip_x) != 0) ? canvas_trans.X = (paste_x + (cut_width / 2)) + off_x : canvas_trans.X;
                                float use_flp_trans_y = ((chunk.flags & V103_flag_flip_y) != 0) ? canvas_trans.Y = (paste_y + (cut_height / 2)) + off_y : canvas_trans.Y;


                                if ((chunk.flags & V103_flag_flip_x | V103_flag_flip_y) != 0)
                                {
                                    canvas.Scale(use_scl_x, use_scl_y, use_flp_trans_x, use_flp_trans_y);
                                    //canvas.Translate(0, 0);
                                    //canvas_trans.X *= -1;
                                }

                                canvas.DrawBitmap(part_image, new SKPoint(canvas_trans.X, canvas_trans.Y), part_paint);
                                //canvas.DrawBitmap(part_image, new SKPoint(0, 0));
                                canvas.Restore();

                                Console.WriteLine("part " + g + " sprite " + nm);
                            }

                            Console.WriteLine("begin save " + nm);

                            canvas.Save();

                            DirectoryInfo in_dir = new DirectoryInfo(og_in_path);

                            string fname = in_dir.Name.Substring(0, in_dir.Name.Length - in_dir.Extension.Length);


                            if(gDoAttemptIndexed)
                            {
                                //pals.SelectMany(x => x).ToArray()
                                surface.Snapshot().CursedPng(Path.Combine(out_dir.FullName, fname + "_" + nm + ".png"), consolidiated_pal);
                            }
                            else
                            {
                            using (var image = surface.Snapshot())
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            using (var stream = File.OpenWrite(Path.Combine(out_dir.FullName, fname + "_" + nm + ".png")))
                            {
                                // save the data to a stream
                                data.SaveTo(stream);
                            }
                            }


                            if (save_parts)
                            {
                                for (int l = 0; l < bitmp_list.Count; l++)
                                {
                                    Console.WriteLine("svpart " + f);
                                    using (var data = bitmp_list[l].Encode(SKEncodedImageFormat.Png, 100))
                                    using (var stream = File.OpenWrite(Path.Combine(part_out_dir.FullName, fname + "_a_" + nm + "_" + l + ".png")))
                                    {
                                        // save the data to a stream
                                        data.SaveTo(stream);
                                    }
                                }
                            }


                        }
                }
            }
        }


        static void V154_unpack(MemoryStream file_stream)
        {
            using (BinaryReader file = new BinaryReader(file_stream))
            {
                file_stream.Position += 4;

                uint palette_block_size = file.ReadUInt32(); //pal related 0x4
                uint main_pal_offset = file.ReadUInt32(); //0x8
                uint unk2 = file.ReadUInt32(); //0xC, probably flags
                uint weapon_pal_offset = file.ReadUInt32(); //0x10
                //uint unk4 = file.ReadUInt32(); //0x14

                uint tex_length_1 = file.ReadUInt32(); //0x14
                uint main_texture_offset = file.ReadUInt32(); //0x18

                uint unk4 = file.ReadUInt32(); //0x1C

                uint weapon_texture_offset = file.ReadUInt32(); //only if applicable? 0x20

                uint total_chunks = file.ReadUInt32(); //0x24

                uint chunk_table_offset = file.ReadUInt32(); //0x28

                uint total_frames = file.ReadUInt32(); //0x2C

                uint frame_data_offset = file.ReadUInt32(); //0x30

                uint total_sprites = file.ReadUInt32(); //0x34

                uint sprite_def_offset = file.ReadUInt32(); //0x38
                uint total_animations = file.ReadUInt32(); //0x3C
                uint animation_def_offset = file.ReadUInt32(); //0x40

                uint unk9 = file.ReadUInt32();
                uint compression_flags = file.ReadUInt32();

                bool hasCompressedImages = (compression_flags & V154_flag_compress_image) != 0;
                bool hasCompressedChunks = (compression_flags & V154_flag_compress_chunk) != 0;
                int x_multiplier_flag = V103_flag_quadruple_x_offset | V103_flag_double_x_offset;
                int y_multiplier_flag = V103_flag_quadruple_y_offset | V103_flag_double_y_offset;
                uint total_palettes = palette_block_size / 2;

                byte[] consolidated_palette = new byte[256 * 4];

                // Just 2 palettes per file
                Color[,] pals = new Color[2, total_palettes];

                file.BaseStream.Position = main_pal_offset;
                for (int i = 0; i < total_palettes; i++)
                {
                    uint col = file.ReadUInt16();
                    pals[0, i] = new Color()
                    {
                        r = (byte)((col & 0x1f) * 8),
                        g = (byte)(((col >> 5) & 0x1f) * 8),
                        b = (byte)(((col >> 10) & 0x1f) * 8),
                        a = (byte)((i % 0x10 == 0) ? 0x00 : 0xff)
                    };

                    consolidated_palette[0 + (i * 4)] = pals[0, i].r;
                    consolidated_palette[1 + (i * 4)] = pals[0, i].g;
                    consolidated_palette[2 + (i * 4)] = pals[0, i].b;
                    consolidated_palette[3 + (i * 4)] = pals[0, i].a;
                }

                // Apparently it can be 0
                if (weapon_pal_offset != 0)
                {
                    file.BaseStream.Position = weapon_pal_offset;
                    for (int i = 0; i < total_palettes; i++)
                    {
                        uint col = file.ReadUInt16();
                        pals[1, i] = new Color()
                        {
                            r = (byte)((col & 0x1f) * 8),
                            g = (byte)(((col >> 5) & 0x1f) * 8),
                            b = (byte)(((col >> 10) & 0x1f) * 8),
                            a = (byte)((i % 0x10 == 0) ? 0x00 : 0xff)
                        };
                    }
                }


                uint[] sprite_parts_list = new uint[total_sprites];
                uint[] chunk_offsets = new uint[total_sprites];
                file.BaseStream.Position = sprite_def_offset;

                for (int a = 0; a < total_sprites; a++)
                {
                    sprite_parts_list[a] = file.ReadUInt16();
                    file.BaseStream.Position += 0xA;

                    chunk_offsets[a] = file.ReadUInt32();
                }

                BinaryReader chunk_stream = file;
                if (hasCompressedChunks)
                {
                    Console.WriteLine("#### START COMPRESSED SPRITE INFO CHUNKS ####");
                    List<byte[]> uncompressed_blobs = new List<byte[]>();
                    foreach (int chunk_offset in chunk_offsets)
                    {
                        file.BaseStream.Position = chunk_table_offset + chunk_offset;
                        uncompressed_blobs.Add(decompress_LZ10(file));
    #if DEBUG
                        File.WriteAllBytes($"compress_debug/test_0x{chunk_table_offset + chunk_offset:X6}.bin", uncompressed_blobs.Last());
    #endif
                    }

                    byte[] decompressed_chunk_table = uncompressed_blobs.SelectMany(x => x).ToArray();
                    chunk_stream = new BinaryReader(new MemoryStream(decompressed_chunk_table));
    #if DEBUG
                    File.WriteAllBytes("compress_debug/chunks_blob.bin", decompressed_chunk_table);
    #endif
                    Console.WriteLine("#### END COMPRESSED SPRITE INFO CHUNKS ####");
                    Console.WriteLine("");
                }
                else
                {
                    chunk_stream.BaseStream.Position = chunk_table_offset;
                }

                i_dat[] idat_list = new i_dat[total_chunks];
                for (int c = 0; c < total_chunks; c++)
                {
                    i_dat spr = new i_dat();

                    spr.use_palette = chunk_stream.ReadByte();
                    spr.flags = chunk_stream.ReadByte();
                    byte tile_x_y = chunk_stream.ReadByte();

                    spr.len_x = (tile_x_y & 0xF0) >> 4;
                    spr.len_y = tile_x_y & 0x0F;

                    // skip unk
                    chunk_stream.ReadByte();

                    spr.paste_x = chunk_stream.ReadSByte();
                    spr.paste_y = chunk_stream.ReadSByte();

                    // Apply multiplier flags
                    if ((spr.flags & x_multiplier_flag) != 0)
                    {
                        spr.paste_x *= ((spr.flags & V103_flag_quadruple_x_offset) != 0) ? 4 : 2;
                    }
                    if ((spr.flags & y_multiplier_flag) != 0)
                    {
                        spr.paste_y *= ((spr.flags & V103_flag_quadruple_y_offset) != 0) ? 4 : 2;

                    }

                    spr.scale_x = chunk_stream.ReadSByte();
                    spr.scale_y = chunk_stream.ReadSByte();

                    spr.rot_axis_x = chunk_stream.ReadSByte();
                    spr.rot_axis_y = chunk_stream.ReadSByte();

                    spr.chunk_center_x = chunk_stream.ReadInt16();
                    spr.chunk_center_y = chunk_stream.ReadInt16();

                    spr.rot_angle = chunk_stream.ReadInt16();

                    spr.y = chunk_stream.ReadInt16();

                    spr.rot_axis_x += spr.chunk_center_x;
                    spr.rot_axis_y += spr.chunk_center_y;

                    Console.WriteLine($"PROCESED CHUNK {c:d} of {total_chunks:d}");
                    Console.WriteLine($"  Tile scale X: {spr.len_x:d}, Y: {spr.len_y:d}");
                    Console.WriteLine($"  offset: 0x{chunk_stream.BaseStream.Position:X}");
                    Console.WriteLine($"  Part flags value: 0x{spr.flags:X}");
                    Console.WriteLine($"  Palette value: {spr.use_palette:d}");
                    Console.WriteLine("");

                    // add to list
                    idat_list[c] = spr;
                }

                // Dispose the extra stream needed for compressed chunks
                if (hasCompressedChunks) chunk_stream.Dispose();

                // Gather image blobs
                for (int i = 0; i < idat_list.Length; i++)
                {
                    int use_weapon = (idat_list[i].use_palette & 0xF0) >> 4;
                    uint tex_offset = use_weapon == 1 ? weapon_texture_offset : main_texture_offset;

                    if (hasCompressedImages)
                    {
                        tex_offset += (uint)(idat_list[i].x * 16 + idat_list[i].y * 32);
                        file.BaseStream.Position = tex_offset;
                        idat_list[i].pixel_data = decompress_LZ10(file);
                    }
                    else
                    {
                        int cut_width = 8 << idat_list[i].len_x;
                        int cut_height = 8 << idat_list[i].len_y;

                        tex_offset += (uint)(idat_list[i].y * 32);
                        file.BaseStream.Position = tex_offset;
                        idat_list[i].pixel_data = file.ReadBytes((cut_width * cut_height) / 2);
                    }

                    Console.WriteLine($"PROCESSED IMAGE DATA {i:d} of {idat_list.Length:d}");
                    Console.WriteLine($"  isCompressed: {hasCompressedImages}");
                    Console.WriteLine($"  Img width: {idat_list[i].len_x:d}");
                    Console.WriteLine($"  Img Height: {idat_list[i].len_y:d}");
                    Console.WriteLine($"  Image data size: {idat_list[i].pixel_data.Length:d}");
                    Console.WriteLine("");
                }

                // Convert images to direct color
                // This can be done in the previous loop for less run-time
                for (int i = 0; i < idat_list.Length; i++)
                {
                    i_dat c_chunk = idat_list[i];
                    if (c_chunk.len_y > 6) continue;

                    // 4bpp indexed -> RGBA8888 conversion
                    byte[] direct_color_data = new byte[c_chunk.pixel_data.Length * 8];
                    Console.WriteLine($"CONVERTING IMAGE DATA {i:d} of {idat_list.Length:d}");
                    Console.WriteLine($"  RGBA8888 image size: {direct_color_data.Length:d}");

                    for (int j = 0; j < c_chunk.pixel_data.Length; j++)
                    {
                        int out_pixel_index = j * 8;
                        uint raw_byte = c_chunk.pixel_data[j];
                        byte pixel1 = (byte)(raw_byte & 0x0F);
                        byte pixel2 = (byte)((raw_byte & 0xF0) >> 4);

                        pixel1 += (byte)(c_chunk.use_palette * 0x10);
                        pixel2 += (byte)(c_chunk.use_palette * 0x10);

                        int pal = (c_chunk.use_palette >= 0x20) ? 1 : 0;
                        if (pal == 1 && gDoAttemptIndexed)
                        {
                            pixel1 += 64;
                            pixel2 += 64;
                        }


                        if (gDoAttemptIndexed)
                        {
                            //binary alpha
                            int alpha_1 = ( pals[pal, pixel1].a < 0xff ) ? 0 : 0xff;
                            int alpha_2 = ( pals[pal, pixel2].a < 0xff ) ? 0 : 0xff;

                            direct_color_data[out_pixel_index + 0] = pixel1;
                            direct_color_data[out_pixel_index + 1] = pixel1;
                            direct_color_data[out_pixel_index + 2] = pixel1;
                            direct_color_data[out_pixel_index + 3] = (byte)alpha_1;

                            direct_color_data[out_pixel_index + 4] = pixel2;
                            direct_color_data[out_pixel_index + 5] = pixel2;
                            direct_color_data[out_pixel_index + 6] = pixel2;
                            direct_color_data[out_pixel_index + 7] = (byte)alpha_2;
                        }
                        else
                        {
                            direct_color_data[out_pixel_index + 0] = pals[pal, pixel1].r;
                            direct_color_data[out_pixel_index + 1] = pals[pal, pixel1].g;
                            direct_color_data[out_pixel_index + 2] = pals[pal, pixel1].b;
                            direct_color_data[out_pixel_index + 3] = pals[pal, pixel1].a;

                            direct_color_data[out_pixel_index + 4] = pals[pal, pixel2].r;
                            direct_color_data[out_pixel_index + 5] = pals[pal, pixel2].g;
                            direct_color_data[out_pixel_index + 6] = pals[pal, pixel2].b;
                            direct_color_data[out_pixel_index + 7] = pals[pal, pixel2].a;
                        }
                    }

                    idat_list[i].pixel_data = direct_color_data;
                }


                // We saving bros, create output folders
                out_dir.Create();
                if (save_parts) part_out_dir.Create();

                var info = new SKImageInfo(1024, 768);
                int off_x = info.Width / 2;
                int off_y = (info.Height / 2 + 128);

                for (int nm = 0, f = 0; nm < sprite_parts_list.Length; nm++)
                {
                    List<SKBitmap> bitmp_list = new List<SKBitmap>();
                    using (var surface = SKSurface.Create(info))
                    {
                        SKCanvas canvas = surface.Canvas;
                        if (gDoAttemptIndexed) canvas.DrawColor(SKColors.Black); //color 0 in grayscale color palette

                        for (int g = 0; g < sprite_parts_list[nm]; g++, f++)
                        {
                            i_dat chunk = idat_list[f];
                            // Skip cursed chunks
                            if (((chunk.use_palette & 0xF0) >> 4) > 1) continue;

                            // Skip whatever this is
                            if (chunk.len_y > 6) continue;

                            // Power of 2-inator
                            int cut_width = 8 << chunk.len_x;
                            int cut_height = 8 << chunk.len_y;

                            //Console.WriteLine("using palette: " + chunk.use_palette + " sz: x " + chunk.len_x + " y " + chunk.len_y + " file_len " + chunk.pixel_data.Length);
                            //Console.WriteLine("chunk xl " + chunk.len_x + " chunk yl " + chunk.len_y);
                            //Console.WriteLine("len " + chunk.len_y + " ch " + cut_height);
                            //Console.WriteLine("cut at: " + (chunk.y * 2) + " tru y: " + chunk.pixel_data.Length / 128);
                            //Console.WriteLine("cutsz x: " + cut_width + " y: " + cut_height);


                            SKBitmap part_image = new SKBitmap();

                            //stackoverflow bros
                            // pin the managed array so that the GC doesn't move it
                            var gcHandle = GCHandle.Alloc(chunk.pixel_data, GCHandleType.Pinned);

                            // install the pixels with the color type of the pixel data
                            var infoa = new SKImageInfo(cut_width, cut_height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                            part_image.InstallPixels(infoa, gcHandle.AddrOfPinnedObject(), infoa.RowBytes, delegate { gcHandle.Free(); }, null);

                            //Console.WriteLine("check");

                            if (save_parts)
                            {
                                bitmp_list.Add(part_image);
                            }

                            SKPaint part_paint = new SKPaint();
                            // Blend modes are destructive to grayscale palettization
                            if (!gDoAttemptIndexed)
                            {
                                if ((chunk.flags & V103_flag_blendmode_add) != 0)
                                {
                                    part_paint.BlendMode = SKBlendMode.Plus;
                                }
                                else if ((chunk.flags & V103_flag_blendmode_sub) != 0)
                                {
                                    part_paint.BlendMode = SKBlendMode.DstATop;
                                }
                            }

                            canvas.Save();
                            canvas.Scale(2.0f, 2.0f, info.Width / 2, info.Height / 2);
                            float prt_scl_x = chunk.scale_x / 10f;
                            float prt_scl_y = chunk.scale_y / 10f;

                            canvas.RotateDegrees(chunk.rot_angle, off_x + chunk.rot_axis_x, off_y - chunk.rot_axis_y);
                            canvas.Scale(prt_scl_x, prt_scl_y, off_x + chunk.chunk_center_x, off_y - chunk.chunk_center_y);


                            SKPoint canvas_trans = new SKPoint((chunk.paste_x - (cut_width / 2)) + off_x, (chunk.paste_y - (cut_height / 2)) + off_y);

                            switch (chunk.flags & (V103_flag_flip_x | V103_flag_flip_y))
                            {
                                case V103_flag_flip_x:
                                    canvas_trans.X = (chunk.paste_x + (cut_width / 2)) + off_x;
                                    canvas.Scale(-1, 1, canvas_trans.X, canvas_trans.Y);
                                    break;
                                case V103_flag_flip_y:
                                    canvas_trans.Y = (chunk.paste_y + (cut_height / 2)) + off_y;
                                    canvas.Scale(1, -1, canvas_trans.X, canvas_trans.Y);
                                    break;
                                case V103_flag_flip_x | V103_flag_flip_y:
                                    canvas_trans.X = (chunk.paste_x + (cut_width / 2)) + off_x;
                                    canvas_trans.Y = (chunk.paste_y + (cut_height / 2)) + off_y;
                                    canvas.Scale(-1, -1, canvas_trans.X, canvas_trans.Y);
                                    break;
                            }

                            canvas.DrawBitmap(part_image, canvas_trans, part_paint);
                            canvas.Restore();

                            //Console.WriteLine("part " + g + " sprite " + nm);
                        }

                        Console.WriteLine("begin save " + nm);

                        DirectoryInfo in_dir = new DirectoryInfo(og_in_path);

                        string fname = in_dir.Name.Substring(0, in_dir.Name.Length - in_dir.Extension.Length);
                        if(gDoAttemptIndexed)
                        {
                            surface.Snapshot().CursedPng(Path.Combine(out_dir.FullName, fname + "_" + nm + ".png"), consolidated_palette);
                            //surface.Snapshot().SaveBitmap(Path.Combine(out_dir.FullName, fname + "_" + nm + ".bmp"), consolidated_palette);
                        }
                        else
                        {
                            // save the data to a stream
                            using (var image = surface.Snapshot())
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            using (var stream = File.OpenWrite(Path.Combine(out_dir.FullName, fname + "_" + nm + ".png")))
                            {
                                data.SaveTo(stream);
                            }
                        }

                        if (save_parts)
                        {
                            for (int l = 0; l < bitmp_list.Count; l++)
                            {
                                Console.WriteLine("svpart " + f);
                                using (var data = bitmp_list[l].Encode(SKEncodedImageFormat.Png, 100))
                                using (var stream = File.OpenWrite(Path.Combine(part_out_dir.FullName, fname + "_a_" + nm + "_" + l + "_indx_" + (f - bitmp_list.Count + l) + ".png")))
                                {
                                    // save the data to a stream
                                    data.SaveTo(stream);
                                }
                            }
                        }
                    }
                }
            }

        }

        // function is at 0x2071BAC in Hearts DS anime movie edition
        // this skips the lower nybble handling that function has
        // because apparently, those are reserved
        static byte[] decompress_LZ10(BinaryReader file)
        {
            Console.WriteLine("Decompressed from  -> 0x{0:X}", file.BaseStream.Position);
            uint header;
            byte magic;
            int size;
            byte[] data;

            header = file.ReadUInt32();
            size = (int)(header >> 8);
            magic = (byte)header;
            if (magic != 0x10 || size == 0)
            {
                throw new InvalidDataException("Ivalid LZ10 file, magic: " + magic + ", decompressed size: " + size);
            }

            data = new byte[size];
            uint flags;
            int cursor = 0;

            while (size > 0)
            {
                flags = file.ReadByte();
                for (int i = 0; i < 8; i++, flags <<= 1)
                {
                    if ((flags & 0x80) == 0)
                    {
                        data[cursor++] = file.ReadByte();
                        size--;
                    }
                    else
                    {
                        byte max_ref;
                        short backref;
                        max_ref = file.ReadByte();
                        backref = (short)(((max_ref & 0xf) << 8) | file.ReadByte() + 1);
                        max_ref >>= 4;

                        for (int j = -3; j < max_ref; j++)
                        {
                            data[cursor] = data[cursor++ - backref];
                            size--;
                        }
                    }

                    if (size <= 0) break;
                }
            }
            Console.WriteLine("Decompressed until -> 0x{0:X}", file.BaseStream.Position);
            return data;
        }
    }
}
