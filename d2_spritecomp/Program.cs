using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SkiaSharp;
using SkiaSharp.Internals;

namespace d2_spritecomp
{
    class Program
    {
        //texture flags (second part/ first flags)
        const int _flag_negative_blend = 0x20;
        const int _flag_flip_x = 0x40;
        const int _flag_flip_y = 0x80;

        //more texture flags
        const int _flag_enable_rotation = 0x20;

        //anp3

        const int anp3_flag_enable_rotate = 0x1;
        const int anp3_flag_enable_scale = 0x2;

        const int anp3_flag_flip_x = 0x4;
        const int anp3_flag_flip_y = 0x8;

        const int anp3_flag_enable_opacity = 0x10;

        //V103
        const int V103_flag_flip_y = 0x10;
        const int V103_flag_flip_x = 0x40;

        struct i_dat
        {
            public int x;
            public int y;
            public int len_x;
            public int len_y;
            public int paste_x;
            public int paste_y;
            public int flags;
            public int flags_2;
            public int rot_angle;

            //anp3
            public int use_palette;
            public int rot_axis_x;
            public int rot_axis_y;
            public int scale_x;
            public int scale_y;
            public byte[] pixel_data;

            //narikiri
            public int chunk_center_x;
            public int chunk_center_y;
        }

        static void Main(string[] args)
        {
            List<i_dat> idat_list = new List<i_dat>();

            byte[] in_dat = File.ReadAllBytes(args[0]);
            //Image in_sheet = Image.FromFile(args[1]);
            //Bitmap out_img = new Bitmap(256,256);

            //using var in_sheet = Image<Rgba32>.Load(args[1]);
            string in_path;

            using ( MemoryStream chunk_dat_stream = new MemoryStream(in_dat) )
            {
                //chunk_dat_stream.Position = 0xA; //offset begin
                //int sprite_offset = chunk_dat.

                using( BinaryReader chunk_dat = new BinaryReader(chunk_dat_stream) )
                {
                    //chunk_dat.BaseStream.Position = (0x4);
                    string anp_string = System.Text.Encoding.UTF8.GetString(chunk_dat.ReadBytes(4));


                    switch(anp_string)
                    {
                        case "anp2":
                            in_path = args[1];
                            anp2_unpack(chunk_dat,in_path);
                            break;
                        case "anp3":
                            anp3_unpack(chunk_dat);
                            break;
                        case "V103":
                            V103_unpack(chunk_dat);
                            break;
                        default:
                            Console.WriteLine("anp header missing");
                            System.Environment.Exit(0);
                            break;
                    }


                }
            }
        }

        static void anp2_unpack(BinaryReader chunk_dat, string in_path)
        {
            using var in_sheet = Image<Rgba32>.Load(in_path);
            List<i_dat> idat_list = new List<i_dat>();
            uint num_sprites = chunk_dat.ReadByte();

            Console.WriteLine("num sprites: " + num_sprites);
            //System.Environment.Exit(0);

            for (int h = 0; h < num_sprites; h++)
            {
                chunk_dat.BaseStream.Position = (0x10) + (h * 2);

                Console.WriteLine("actual current before read offset: 0x{0:X}", chunk_dat.BaseStream.Position + " hcnt " + h);

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

                    //rotate flag bit mask maybe
                    int bit_mask = 0x1F;

                    int bit_mask_y = 0xF8;
                    int bit_mask_z = 0x0F;

                    Console.WriteLine("where am i: 0x{0:X}", chunk_dat.BaseStream.Position);

                    //int tex_coord_pos = chunk_dat.ReadUInt16();

                    //surely the length would never surpass a 256x page
                    //int tex_coord_len = chunk_dat.ReadUInt16();

                    //cursed

                    byte[] why = new byte[2];
                    why[0] = chunk_dat.ReadByte();

                    int help = chunk_dat.ReadByte();
                    int tex_flags_2 = (help & bit_mask_y);

                    help = (help & bit_mask);

                    why[1] = (byte)help;

                    int d = why[0];
                    d = d >> 5;


                    //if ( d == 1 ) tex_flags_2 |= _flag_enable_rotation;

                    int tex_coord_pos = BitConverter.ToInt16(why);


                    why[0] = chunk_dat.ReadByte();

                    help = chunk_dat.ReadByte();

                    //sword palette flags unset
                    help &= ~(1 << 3);
                    help &= ~(1 << 2);

                    int tex_flags = (help & bit_mask_y);

                    help = (help & bit_mask_z);

                    why[1] = (byte)help;

                    int tex_coord_len = BitConverter.ToInt16(why);

                    //tex_coord_len &= ~(1 << 2);
                    //tex_coord_len &= ~(1 << 3);

                    Console.WriteLine("sz " + tex_coord_len);


                    //chunk_dat.BaseStream.Position -=1;


                    int t_y = (tex_coord_pos / 32) * 8;
                    int t_x = (tex_coord_pos % 32) * 8;


                    //int t_l_y = (tex_coord_len / 32) * 8;
                    //int t_l_x = (tex_coord_len % 32) * 8;

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

                    if ((tex_flags_2 & _flag_enable_rotation) != 0)
                    {
                        chunk_dat.BaseStream.Position += 2;

                        Console.WriteLine("read rot at " + chunk_dat.BaseStream.Position);

                        add_dat.rot_angle = chunk_dat.ReadInt16();

                        Console.WriteLine("rot val confirmed " + add_dat.rot_angle);
                    }


                    idat_list.Add(add_dat);

                    chunk_dat.BaseStream.Position = current_offset;
                }

                using (Image<Rgba32> image = new(512, 512))
                {
                    //image.Mutate(o => o.Opacity(0));
                    foreach (i_dat chunk in idat_list)
                    {
                        if (chunk.len_x > in_sheet.Width) continue;
                        if (chunk.len_y > in_sheet.Height) continue;
                        if (chunk.x > in_sheet.Width) continue;
                        if (chunk.y > in_sheet.Height) continue;
                        if (chunk.y + chunk.len_y > in_sheet.Height) continue;
                        if (chunk.x + chunk.len_x > in_sheet.Height) continue;

                        Console.WriteLine("check rect x" + chunk.x + " y " + chunk.y + " xlen " + chunk.len_x + " ylen " + chunk.len_y + " cflags " + chunk.flags + " check " + (chunk.flags & _flag_flip_x));

                        using (Image<Rgba32> copy_image = (Image<Rgba32>)in_sheet.Clone(c => c.Crop(new Rectangle(chunk.x, chunk.y, chunk.len_x, chunk.len_y))))
                        {
                            copy_image.Mutate(o => o.Resize(copy_image.Width * 2, copy_image.Height * 2, KnownResamplers.NearestNeighbor));

                            if ((chunk.flags & _flag_flip_x) != 0)
                            {
                                Console.WriteLine("flip x");
                                copy_image.Mutate(o => o.Flip(FlipMode.Horizontal));
                            }
                            if ((chunk.flags & _flag_flip_y) != 0)
                            {
                                copy_image.Mutate(o => o.Flip(FlipMode.Vertical));
                            }

                            if ((chunk.flags_2 & _flag_enable_rotation) != 0)
                            {
                                AffineTransformBuilder bld = new AffineTransformBuilder();
                                Vector2 origin = new Vector2(+(chunk.len_x / 2), (chunk.len_y / 2));

                                copy_image.Mutate(o => o.Rotate((float)chunk.rot_angle / 11.33f, KnownResamplers.NearestNeighbor));
                            }

                            image.Mutate(o => o.DrawImage(copy_image, new Point((-(copy_image.Width / 2)) + (chunk.paste_x * 2) + 256, (-(copy_image.Height / 2)) + (chunk.paste_y * 2) + 256), 1f));

                        }
                    }

                    //flippy endy
                    image.Mutate(o => o.Flip(FlipMode.Horizontal));
                    image.SaveAsPng("out/test_" + h + ".png");


                }
                idat_list.Clear();
            }
        }

        static void anp3_unpack(BinaryReader anp3_file)
        {
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

            anp3_file.BaseStream.Position = paldat_offset;
            for(int l = 0; l < palette_count; l++)
            {
                if(bpp == 8)
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

                        pals_out.Add( pal_1_half_1.Concat(pal_1_half_2).ToArray() );
                        pals_out.Add( pal_2_half_1.Concat(pal_2_half_2).ToArray() );

                        Console.WriteLine("off after: 0x{0:X}", anp3_file.BaseStream.Position);
                    }

                    if(File.Exists("custom.pal"))
                    {
                        pals[l] = File.ReadAllBytes("custom.pal");
                    }
                    else
                    {
                        pals[l] = pals_out.SelectMany(x => x).ToArray();
                    }
                    

                    Console.WriteLine("fpr " + pals[l].Length);

                    File.WriteAllBytes("outtestpal.bin",pals[l]);

                    break;
                }
                else
                {
                    byte[] pal_1_half_1 = anp3_file.ReadBytes(0x20);
                    byte[] pal_2_half_1 = anp3_file.ReadBytes(0x20);

                    byte[] pal_1_half_2 = anp3_file.ReadBytes(0x20);
                    byte[] pal_2_half_2 = anp3_file.ReadBytes(0x20);

                    pals[l] = pal_1_half_1.Concat(pal_1_half_2).ToArray();
                    pals[l+1] = pal_2_half_1.Concat(pal_2_half_2).ToArray();

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


            for (int a = 0; a < num_animations; a++)
            {
                anp3_file.BaseStream.Position = (0x20) + (a * 2);

                Console.WriteLine("table off: 0x{0:X}", anp3_file.BaseStream.Position);

                uint c_ani_offset = anp3_file.ReadUInt16();

                Console.WriteLine("cani off: 0x{0:X}", c_ani_offset);

                anp3_file.BaseStream.Position = c_ani_offset;

                uint c_ani_frame_count = anp3_file.ReadByte();

                Console.WriteLine("confirmed "+c_ani_frame_count+" frames");

                uint c_ani_type = anp3_file.ReadByte();

                //unk
                anp3_file.BaseStream.Position += 2;

                for(int b = 0; b < c_ani_frame_count; b++)
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

                    Console.WriteLine("confirmed "+num_sprite_parts+" parts");

                    for (int c = 0; c < num_sprite_parts; c++)
                    {
                        Console.WriteLine("working part "+c);
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

                        Console.WriteLine("part flgs value "+part_flags);

                        uint part_palette = anp3_file.ReadByte();

                        Console.WriteLine("load pal usevl: "+part_palette);

                        sprite_part.use_palette = (int)part_palette;

                        uint unk20 = anp3_file.ReadByte();
                        uint stretchyboy = anp3_file.ReadByte();


                        int come_out_offset = 0xC;

                        //both scale and rotate case
                        if ( (part_flags & anp3_flag_enable_rotate) != 0 && (part_flags & anp3_flag_enable_scale) != 0)
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
                        else if((part_flags & anp3_flag_enable_rotate) != 0) //rotate case
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


                        Console.WriteLine("bpp "+bpp);
                        if( bpp==4 )
                        {
                            byte[] pixel_data = new byte[(tile_width * tile_height)];

                            //Console.WriteLine("pxdl "+pixel_data.Length);

                            for (int d = 0, f = 0; d < (tile_width*tile_height)/2; d++, f+=2)
                            {
                                //Console.WriteLine("read 1 " + anp3_file.BaseStream.Position+" drd "+d);

                                uint raw_byte = anp3_file.ReadByte();
                                uint pixel_1 = ( (raw_byte&0xF0) >> 4 );
                                uint pixel_2 = (raw_byte & 0x0F);

                                pixel_data[f+1] = (byte)pixel_1;
                                pixel_data[f] = (byte)pixel_2;
                            }

                            //File.WriteAllBytes("unswizzle.bin",pixel_data);


                            byte[] colored_pixel_data = new byte[pixel_data.Length * 4];
                            for (int f = 0; f < pixel_data.Length; f++)
                            {
                                int pixel_value = pixel_data[f];

                                //Console.WriteLine("pixel value "+pixel_value+" chunk pal "+sprite_part.use_palette);
                                //Console.WriteLine("target cpal addr " + ((pixel_value * 4) + 0));

                                colored_pixel_data[(f * 4)+ 0] = pals[part_palette][(pixel_value * 4) + 0];
                                colored_pixel_data[(f * 4)+ 1] = pals[part_palette][(pixel_value * 4) + 1];
                                colored_pixel_data[(f * 4)+ 2] = pals[part_palette][(pixel_value * 4) + 2];
                                colored_pixel_data[(f * 4)+ 3] = pals[part_palette][(pixel_value * 4) + 3];

                                if( pixel_value == 0 )
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

                            //Console.WriteLine("pxdl_cl " + colored_pixel_data.Length);

                            //File.WriteAllBytes("unswizzle_clrd.bin", colored_pixel_data);
                            //System.Environment.Exit(0);

                            sprite_part.pixel_data = colored_pixel_data;
                            //sprite_part.pixel_data = pixel_data;
                        }
                        else
                        if(bpp==8)
                        {
                            byte[] pixel_data = new byte[(tile_width * tile_height)];

                            //Console.WriteLine("pxdl "+pixel_data.Length);

                            for (int d = 0; d < pixel_data.Length; d++)
                            {
                                //Console.WriteLine("read 1 " + anp3_file.BaseStream.Position+" drd "+d);

                                pixel_data[d] = anp3_file.ReadByte();
                            }

                            byte[] colored_pixel_data = new byte[pixel_data.Length * 4];
                            for (int f = 0; f < pixel_data.Length; f++)
                            {
                                int pixel_value = pixel_data[f];

                                //Console.WriteLine("pixel value "+pixel_value+" chunk pal "+sprite_part.use_palette);
                                //Console.WriteLine("target cpal addr " + ((pixel_value * 4) + 0));

                                colored_pixel_data[(f * 4) + 0] = pals[part_palette][(pixel_value * 4) + 0];
                                colored_pixel_data[(f * 4) + 1] = pals[part_palette][(pixel_value * 4) + 1];
                                colored_pixel_data[(f * 4) + 2] = pals[part_palette][(pixel_value * 4) + 2];
                                colored_pixel_data[(f * 4) + 3] = pals[part_palette][(pixel_value * 4) + 3];

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

                            sprite_part.pixel_data = colored_pixel_data;
                        }

                        //Console.WriteLine("idatoff: 0x{0:X}", image_data_offset);

                        anp3_file.BaseStream.Position = last_pos_part + come_out_offset;
                        Console.WriteLine("return from part to: 0x{0:X}", anp3_file.BaseStream.Position);

                        idat_list.Add(sprite_part);
                    }


                    using (Image<Rgba32> image = new(1024, 768))
                    {
                        idat_list.Reverse();
                        //image.Mutate(o => o.Opacity(0));
                        foreach (i_dat chunk in idat_list)
                        {
                            //oops?????
                            if (chunk.len_x == 0 || chunk.len_y == 0) continue;

                            //if (chunk.len_x > in_sheet.Width) continue;
                            //if (chunk.len_y > in_sheet.Height) continue;
                            //if (chunk.x > in_sheet.Width) continue;
                            //if (chunk.y > in_sheet.Height) continue;
                            //if (chunk.y + chunk.len_y > in_sheet.Height) continue;
                            //if (chunk.x + chunk.len_x > in_sheet.Height) continue;

                            Console.WriteLine("using palette: "+chunk.use_palette+" sz: x "+chunk.len_x+" y "+chunk.len_y+" file_len "+chunk.pixel_data.Length);
                            File.WriteAllBytes("palette_data.bin", pals[0]);
                            File.WriteAllBytes("pixel_data.bin",chunk.pixel_data);

                            byte[] test = new byte[] { 50, 50, 50, 255 };
                            
                            using (Image<Rgba32> part_image = Image.LoadPixelData<Rgba32>(chunk.pixel_data, chunk.len_x, chunk.len_y))
                            {
                                //part_image.SaveAsPng("debug/testimg_"+a+"_"+b+".png");

                                using (Image<Rgba32> copy_image = (Image<Rgba32>)part_image.Clone(c => c.Crop(new Rectangle(chunk.x, chunk.y, chunk.len_x, chunk.len_y))))
                                {
                                    if( (chunk.flags&anp3_flag_enable_scale)!=0 )
                                    {
                                        float mul_x = (float)chunk.scale_x / 100;
                                        float mul_y = (float)chunk.scale_y / 100;

                                        copy_image.Mutate(o => o.Resize((int)(copy_image.Width * mul_x), (int)(copy_image.Height * mul_y), KnownResamplers.NearestNeighbor));
                                    }


                                    copy_image.Mutate(o => o.Resize(copy_image.Width * 2, copy_image.Height * 2, KnownResamplers.NearestNeighbor));

                                    if ((chunk.flags & anp3_flag_flip_x) != 0)
                                    {
                                        Console.WriteLine("flip x");
                                        copy_image.Mutate(o => o.Flip(FlipMode.Horizontal));
                                    }
                                    if ((chunk.flags & anp3_flag_flip_y) != 0)
                                    {
                                        copy_image.Mutate(o => o.Flip(FlipMode.Vertical));
                                    }

                                    if ((chunk.flags & anp3_flag_enable_rotate) != 0)
                                    {
                                        //degrees
                                        float rot_angle = chunk.rot_angle / 11.33f;
                                        //rot_angle = rot_angle * (3.14159265f / 180f);

                                        
                                        Vector2 origin = new Vector2( (chunk.len_x / 2), (chunk.len_y / 2) );

                                        //Console.WriteLine("pre w " + copy_image.Width + " h " + copy_image.Height);
                                        copy_image.Mutate(o =>
                                        {
                                            if (!(chunk.rot_axis_x == 0 && chunk.rot_axis_y == 0))
                                            {
                                                //there is a non-0 axis adjustment value
                                                Console.WriteLine("!axis adjusted rotate!");

                                                int o_width = copy_image.Width;
                                                int o_height = copy_image.Height;

                                                //give 256px offset lenience, signed byte, so +-128 potential
                                                o.Pad(copy_image.Width + 512, copy_image.Height + 512);

                                                int[] rect_param = new int[4];

                                                int rot_axis_x = chunk.rot_axis_x * 4;
                                                int rot_axis_y = chunk.rot_axis_y * 4;

                                                if (rot_axis_x >= 0)
                                                {
                                                    rect_param[0] = 0; //x position offset
                                                    rect_param[2] = rot_axis_x; //width offset
                                                }
                                                else if (rot_axis_x < 0)
                                                {
                                                    rect_param[0] = rot_axis_x; //x position offset
                                                    rect_param[2] = Math.Abs(rot_axis_x); //width offset
                                                }

                                                if (rot_axis_y >= 0)
                                                {
                                                    rect_param[1] = 0; //y position offset
                                                    rect_param[3] = rot_axis_y; //height offset
                                                }
                                                else if (rot_axis_y < 0)
                                                {
                                                    rect_param[1] = rot_axis_y; //x position offset
                                                    rect_param[3] = Math.Abs(rot_axis_y); //height offset
                                                }

                                                Rectangle use_rect = new Rectangle(256 + rect_param[0], 256 + rect_param[1], o_width + rect_param[2], o_height + rect_param[3]);

                                                Console.WriteLine(" padded size: " + copy_image.Width + " h " + copy_image.Height);
                                                Console.WriteLine("offset value: x " + chunk.rot_axis_x + " y " + chunk.rot_axis_y);
                                                Console.WriteLine(" crop rect dimensions: x " + use_rect.X + " y " + use_rect.Y + " uw " + use_rect.Width + " uh " + use_rect.Height);

                                                o.Crop(use_rect);

                                                //copy_image.SaveAsPng("debug/testimg_crop_" + a + "_" + b + ".png");


                                                AffineTransformBuilder bld = new AffineTransformBuilder();
                                                bld.AppendRotationDegrees(rot_angle);
                                                bld.AppendTranslation(new PointF(rot_axis_x, rot_axis_y));

                                                o.Pad(image.Width, image.Height);

                                                o.Transform(bld, KnownResamplers.NearestNeighbor);
                                            }
                                            else
                                            {
                                                //no rotational axis adjustment
                                                Console.WriteLine("normal rotate");
                                                o.Rotate((float)chunk.rot_angle / 11.33f, KnownResamplers.NearestNeighbor);
                                            }
                                        });
                                    }

                                    int x_offset = image.Width / 2;
                                    int y_offset = image.Height - 128;

                                    image.Mutate(o => o.DrawImage(copy_image, new Point((-(copy_image.Width / 2)) + (chunk.paste_x * 2) + x_offset, (-(copy_image.Height / 2)) + (chunk.paste_y * 2) + y_offset), 1f));

                                }
                            }

                            Console.WriteLine("check rect x" + chunk.x + " y " + chunk.y + " xlen " + chunk.len_x + " ylen " + chunk.len_y + " cflags " + chunk.flags + " check " + (chunk.flags & _flag_flip_x));


                        }

                        //flippy endy
                        image.Mutate(o => o.Flip(FlipMode.Horizontal));
                        image.SaveAsPng("out/fr_" + a + "_prt_"+b+".png");


                    }
                    idat_list.Clear();

                    anp3_file.BaseStream.Position = last_pos_anim+8;
                    Console.WriteLine("return from anim to: 0x{0:X}", anp3_file.BaseStream.Position);



                }
            }
            //System.Environment.Exit(0);
        }


        static void V103_unpack(BinaryReader file)
        {
            List<i_dat> idat_list = new List<i_dat>();

            uint unk1 = file.ReadUInt32(); //pal related 0x4
            uint pal_offset = file.ReadUInt32(); //0x8
            uint unk2 = file.ReadUInt32(); //0xC
            uint weapon_pal_offset = file.ReadUInt32(); //0x10
            //uint unk4 = file.ReadUInt32(); //0x14

            uint tex_length_1 = file.ReadUInt32(); //0x14
            uint tex_offset_1 = file.ReadUInt32(); //0x18

            uint unk4 = file.ReadUInt32(); //0x1C

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


            byte[][] pals = new byte[2][];
            file.BaseStream.Position = pal_offset;
            pals[0] = file.ReadBytes(0x400);

            file.BaseStream.Position = weapon_pal_offset;
            pals[1] = file.ReadBytes(0x400);


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
 
                int cut_width = 32*sprite_part.len_x;
                int cut_height = 8;

                for (int j = 0; j < sprite_part.len_y; j++) cut_height *= 2;
                //for (int j = 0; j < sprite_part.len_x; j++) cut_width +=32;

                byte[] pixel_data = new byte[cut_width * cut_height];

                Console.WriteLine(" lw "+ sprite_part.len_x+" ly "+ sprite_part.len_y);
                Console.WriteLine("projected pxldt size " + pixel_data.Length);

                uint last_pos_part = (uint)file.BaseStream.Position;

                if(sprite_part.use_palette < 0x20)
                {
                    file.BaseStream.Position = tex_offset_1 + (sprite_part.y * 32);
                }
                else
                {
                    file.BaseStream.Position = tex_offset_2 + (sprite_part.y * 32);
                }
                

                for (int d = 0, f = 0; d < ( (cut_width * cut_height) / 2); d++, f += 2)
                {
                    if (sprite_part.len_y > 6) break;
                    uint raw_byte = file.ReadByte();
                    uint pixel_1 = ((raw_byte & 0xF0) >> 4);
                    uint pixel_2 = (raw_byte & 0x0F);

                    pixel_1 += (uint)sprite_part.use_palette * 0x10;
                    pixel_2 += (uint)sprite_part.use_palette * 0x10;

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

                    //Console.WriteLine("pixel value "+pixel_value+" chunk pal "+sprite_part.use_palette);
                    //Console.WriteLine("target cpal addr " + ((pixel_value * 4) + 0));

                    //olored_pixel_data[(f * 4) + 0] = (byte)pixel_value;
                    //colored_pixel_data[(f * 4) + 1] = (byte)pixel_value;
                    //colored_pixel_data[(f * 4) + 2] = (byte)pixel_value;
                    //colored_pixel_data[(f * 4) + 3] = 0xff;

                    int use_pal = (sprite_part.use_palette == 0x20) ? 1 : 0;

                    colored_pixel_data[(f * 4) + 0] = pals[use_pal][(pixel_value * 4) + 0];
                    colored_pixel_data[(f * 4) + 1] = pals[use_pal][(pixel_value * 4) + 1];
                    colored_pixel_data[(f * 4) + 2] = pals[use_pal][(pixel_value * 4) + 2];
                    colored_pixel_data[(f * 4) + 3] = pals[use_pal][(pixel_value * 4) + 3];

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


            

            

            for(int i = 0; i < idat_list.Count; i++)
            {

            }

            /*
            for (int d = 0, f = 0, ck = 0, ck_len = 0; d < tex_length_1; d++, f += 2, ck_len++)
            {
                uint raw_byte = file.ReadByte();
                uint pixel_1 = ((raw_byte & 0xF0) >> 4);
                uint pixel_2 = (raw_byte & 0x0F);

                pixel_data[f + 1] = (byte)pixel_1;
                pixel_data[f] = (byte)pixel_2;
            }
            */




            //idat_list.Reverse();
            //image.Mutate(o => o.Opacity(0));

            for (int nm = 0, f = 0; nm < sprite_parts_list.Count; nm++)
            {
                using (Image<Rgba32> image = new(512, 512))
                {
                    for (int g = 0; g < sprite_parts_list[nm]; g++, f++)
                    {
                        uint num_to_process = sprite_parts_list[nm];
                        i_dat chunk = idat_list[f];

                        //oops?????
                        if (chunk.len_x == 0 ) continue;

                        //Console.WriteLine("using palette: " + chunk.use_palette + " sz: x " + chunk.len_x + " y " + chunk.len_y + " file_len " + chunk.pixel_data.Length);
                        //File.WriteAllBytes("palette_data.bin", pals[0]);
                        //File.WriteAllBytes("pixel_data.bin", chunk.pixel_data);

                        int true_y = (chunk.pixel_data.Length / 4) / 32;

                        int cut_width = 32*chunk.len_x;
                        int cut_height = 8;

                        for (int c = 0; c < chunk.len_y; c++) cut_height *= 2;
                        //for (int j = 0; j < chunk.len_x; j++) cut_width += 32;

                        //if (chunk.len_y > 6) continue;

                        Console.WriteLine("len " + chunk.len_y + " ch " + cut_height);

                        byte[] test = new byte[] { 50, 50, 50, 255 };

                        Console.WriteLine("cut at: "+(chunk.y * 2)+" tru y: "+ true_y);

                        using (Image<Rgba32> part_image = Image.LoadPixelData<Rgba32>(chunk.pixel_data, cut_width, cut_height))
                        {
                        }

                        Console.WriteLine("check rect x" + chunk.x + " y " + chunk.y + " xlen " + chunk.len_x + " ylen " + chunk.len_y + " cflags " + chunk.flags + " check " + (chunk.flags & _flag_flip_x));
                    }

                    image.SaveAsPng("out/fr_" + nm+".png");
                }

                
            }

            //flippy endy
            //image.Mutate(o => o.Flip(FlipMode.Horizontal));
            //image.SaveAsPng("out/ani_" + a + "_fr_" + b + ".png");

            //System.Environment.Exit(0);
        }
    }
}
