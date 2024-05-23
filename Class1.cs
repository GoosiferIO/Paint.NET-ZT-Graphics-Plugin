using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Drawing.Text;
using System.IO.Compression;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;
using PaintDotNet.Rendering;
using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using TextboxControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;
using LabelComment = System.String;
using LayerControl = System.Int32;

namespace PDNzT
{
    public sealed class ZtGfxPluginPDNFactory : IFileTypeFactory
    {
        public FileType[] GetFileTypeInstances()
        {
            return new[] { new ZtGfxPluginPDN() };
        }
    }
    public enum PropertyNames
    {
        Amount1
    }

    public class ZtGfxPluginPDNSupportInfo : IPluginSupportInfo
    {
        public string Author => base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
        public string Copyright => base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
        public string DisplayName => base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
        public Version Version => base.GetType().Assembly.GetName().Version;
        public Uri WebsiteUri => new Uri("https://www.getpaint.net/redirect/plugins.html");
    }

    public class ZtGfx
    {
        public string Magic { get; set; }
        public int AnimationSpeed { get; set; }
        public string PalFileName { get; set; }
        public int FrameCount { get; set; }
        public List<ZtGfxFrame> Frames { get; set; }

        public ZtGfx()
        {
            Frames = new List<ZtGfxFrame>();
        }
    }

    public class ZtGfxFrame
    {
        public int FrameSize { get; set; }
        public int InitialOffset { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public int VerticalOffset { get; set; }
        public int HorizontalOffset { get; set; }
        public List<ZtGfxPixelSet> PixelSets { get; set; }

        public ZtGfxFrame()
        {
            PixelSets = new List<ZtGfxPixelSet>();
        }
    }

    public class ZtGfxPixelSet
    {
        public int PixelSetCount { get; set; }
        public int TransparentPixelCount { get; set; }
        public int ColorPixelCount { get; set; }
        public List<int> PixelIndexes { get; set; }
        public ZtGfxPixelSet()
        {
            PixelIndexes = new List<int>();
        }
    }

    public class PAL
    {
        public int ColorCount { get; set; }
        public List<ColorBgra> Colors { get; set; }

        public PAL()
        {
            Colors = new List<ColorBgra>();
        }
    }

    class SimpleLogger
    {
        private static readonly string logFilePath = "logs/logfile.txt";

        public static void Log(string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
            using (StreamWriter writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
            {
                writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
            }
        }
    }

    [PluginSupportInfo(DisplayName = "pdn-ztgfx")]
    public class ZtGfxPluginPDN : PropertyBasedFileType
    {
        public ZtGfx zt { get; set; }
        public PAL pal { get; set; }

        public ZtGfxPluginPDN()
            : base(
                "ZT Graphics Format",
                new FileTypeOptions
                {
                    LoadExtensions = new string[] { ".ztgfx" },
                    SaveExtensions = new string[] { ".*", ".pal" },
                    SupportsCancellation = true,
                    SupportsLayers = false
                })
        {
        }

        protected ColorBgra GetColorFromPal(int index)
        {
            return pal.Colors[index];
        }

        protected sealed override Document OnLoad(Stream input)
        {
            return LoadImage(input);
        }

        Document LoadImage(Stream input)
        {
            System.Diagnostics.Debug.WriteLine("OnLoad function called");
            SimpleLogger.Log("OnLoad function called");
            try
            {
                using (BinaryReader reader = new BinaryReader(input))
                {
                    // Check for the magic number "FATZ"
                    string magic = new string(reader.ReadChars(4));
                    SimpleLogger.Log($"Assumed magic number: {magic}");
                    if (magic == "FATZ")
                    {
                        // Log the format detection
                        System.Diagnostics.Debug.WriteLine("Magic number 'FATZ' detected. Format 1.");
                        SimpleLogger.Log("Magic number 'FATZ' detected. Format 1.");

                        // Skip the next 5 bytes
                        reader.ReadBytes(5);
                    }
                    else
                    {
                        // Log the lack of magic number detection
                        System.Diagnostics.Debug.WriteLine("No magic number detected. Assuming Format 2.");
                        SimpleLogger.Log("No magic number detected. Assuming Format 2.");

                        // Reset the stream position
                        input.Position = 0;
                    }

                    // Read the remaining header information
                    zt = new ZtGfx
                    {
                        Magic = magic,
                        AnimationSpeed = reader.ReadInt32()
                    };

                    int palFileNameLength = reader.ReadInt32();
                    zt.PalFileName = new string(reader.ReadChars(palFileNameLength)).TrimEnd('\0');
                    zt.FrameCount = reader.ReadInt32();

                    // Log the header information
                    System.Diagnostics.Debug.WriteLine($"AnimationSpeed: {zt.AnimationSpeed}");
                    System.Diagnostics.Debug.WriteLine($"PalFileName: {zt.PalFileName}");
                    System.Diagnostics.Debug.WriteLine($"FrameCount: {zt.FrameCount}");
                    SimpleLogger.Log($"AnimationSpeed: {zt.AnimationSpeed}");
                    SimpleLogger.Log($"PalFileName: {zt.PalFileName}");
                    SimpleLogger.Log($"FrameCount: {zt.FrameCount}");

                    // Load the frame data
                    for (int i = 0; i < zt.FrameCount; i++)
                    {
                        ZtGfxFrame frame = new ZtGfxFrame
                        {
                            // overall size of this frame in number of bytes EXCLUDING the current 4 bytes
                            FrameSize = reader.ReadInt32(),
                            // curr pos
                            InitialOffset = (int)reader.BaseStream.Position,
                            Height = reader.ReadInt16(),
                            Width = reader.ReadInt16(),
                            VerticalOffset = reader.ReadInt16(),
                            HorizontalOffset = reader.ReadInt16(),
                        };
                        reader.ReadInt16(); // Skip unknown value
                        // read pixel count (1 byte)
                        
                        // pixel sets
                        for (int j = 0; j < frame.Height; j++)
                        {
                            // number of pixel sets in this line
                            ZtGfxPixelSet pixelSet = new ZtGfxPixelSet();
                            pixelSet.PixelSetCount = reader.ReadByte();
                            
                            for (int k = 0; k < pixelSet.PixelSetCount; k++)
                            {
                                SimpleLogger.Log($"Frame {i} PixelSet {k} Stats: PixelSetCount={pixelSet.PixelSetCount}");
                                // get transparent pixel count
                                pixelSet.TransparentPixelCount = reader.ReadByte();
                                // get color pixel count
                                pixelSet.ColorPixelCount = reader.ReadByte();
                                SimpleLogger.Log($"TransparentPixelCount: {pixelSet.TransparentPixelCount}, ColorPixelCount: {pixelSet.ColorPixelCount}");
                                // get pixel indexes
                                for (int l = 0; l < pixelSet.ColorPixelCount; l++)
                                {
                                    pixelSet.PixelIndexes.Add(reader.ReadByte());
                                }
                            }
                            frame.PixelSets.Add(pixelSet);
                        }

                        // skip full size of from from InitialOffset
                        int currentPos = (int)reader.BaseStream.Position;
                        int skipSize = frame.InitialOffset + frame.FrameSize;
                        // print position information
                        SimpleLogger.Log($"CurrentPos: {currentPos}, SkipSize: {skipSize}");
                        SimpleLogger.Log($"FrameSize: {frame.FrameSize}, InitialOffset: {frame.InitialOffset}");
                        if (currentPos < skipSize)
                        {
                            reader.BaseStream.Position = skipSize;
                        }
                        SimpleLogger.Log($"CurrentPos: {reader.BaseStream.Position}, SkipSize: {skipSize}");

                        zt.Frames.Add(frame);

                        // Log frame information
                        System.Diagnostics.Debug.WriteLine($"Frame {i}: Size={frame.FrameSize}, Height={frame.Height}, Width={frame.Width}");
                        SimpleLogger.Log($"Frame {i} Stats: Size={frame.FrameSize}, Height={frame.Height}, Width={frame.Width}, VerticalOffset={frame.VerticalOffset}, HorizontalOffset={frame.HorizontalOffset}");
                    }

                    // Load the palette file
                    string palFileName = zt.PalFileName;
                    using (FileStream palFile = new FileStream(palFileName, FileMode.Open))
                    using (BinaryReader palReader = new BinaryReader(palFile))
                    {
                        pal = new PAL
                        {
                            ColorCount = palReader.ReadInt32()
                        };

                        // Log palette color count
                        System.Diagnostics.Debug.WriteLine($"Palette ColorCount: {pal.ColorCount}");
                        SimpleLogger.Log($"Palette ColorCount: {pal.ColorCount}");

                        for (int i = 0; i < pal.ColorCount; i++)
                        {
                            byte r = palReader.ReadByte();
                            byte g = palReader.ReadByte();
                            byte b = palReader.ReadByte();
                            byte a = palReader.ReadByte();
                            pal.Colors.Add(ColorBgra.FromBgra(b, g, r, a));
                        }
                    }

                    SimpleLogger.Log($"Loaded {zt.FrameCount} frames with {pal.ColorCount} colors");

                    Document document = new Document(zt.Frames[0].Width, zt.Frames[0].Height);
                    // Draw the frames in Paint.NET
                    for (int i = 0; i < zt.FrameCount; i++)
                    {
                        ZtGfxFrame frame = zt.Frames[i];
                        Surface surface = new Surface(frame.Width, frame.Height);
                        for (int j = 0; j < frame.Height; j++)
                        {
                            ZtGfxPixelSet pixelSet = frame.PixelSets[j];
                            int x = 0;
                            // draw invisible pixels
                            for (int k = 0; k < pixelSet.TransparentPixelCount; k++)
                            {
                                ColorBgra color = ColorBgra.Transparent;
                                surface[x, j] = color;
                                x++;
                            }

                            // if transparent pixel count is size of the line, skip drawing the rest
                            if (pixelSet.TransparentPixelCount == frame.Width)
                            {
                                continue;
                            }

                            for (int k = 0; k < pixelSet.PixelIndexes.Count; k++)
                            {
                                ColorBgra color = GetColorFromPal(pixelSet.PixelIndexes[k]);
                                for (int l = 0; l < 1; l++)
                                {
                                    surface[x, j] = color;
                                    x++;
                                }
                            }
                        }
                        document.Layers.Add(new BitmapLayer(surface));
                    }
                    return document;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading file: {ex.Message}");
                SimpleLogger.Log($"Error loading file: {ex.Message}");
                throw new FormatException("The file format is not recognized or is corrupt.", ex);
            }
        }

        void SaveImage(Document input, Stream output, SaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            using (BinaryWriter writer = new BinaryWriter(output))
            {
                writer.Write(input.Width);
                writer.Write(input.Height);

                BitmapLayer layer = (BitmapLayer)input.Layers[0];
                Surface surface = layer.Surface;

                for (int y = 0; y < input.Height; y++)
                {
                    for (int x = 0; x < input.Width; x++)
                    {
                        ColorBgra color = surface[x, y];
                        writer.Write(color.R);
                        writer.Write(color.G);
                        writer.Write(color.B);
                    }
                }
            }
        }

        public override PaintDotNet.PropertySystem.PropertyCollection OnCreateSavePropertyCollection()
        {
            throw new NotImplementedException();
        }

        private const string HeaderSignature = ".*";

        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            throw new NotImplementedException();
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultSaveConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.DisplayName, string.Empty);
            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.Description, "Checkbox Description");
            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.ShowHeaderLine, false);

            return configUI;
        }
    }
}