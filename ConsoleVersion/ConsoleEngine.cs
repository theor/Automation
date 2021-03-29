using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Automation
{
    public abstract class GameConsole : IDisposable
    {
        #region Windows API
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            [MarshalAs(UnmanagedType.U4)] uint fileAccess,
            [MarshalAs(UnmanagedType.U4)] uint fileShare,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] int flags,
            IntPtr template
        );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, EntryPoint = "WriteConsoleOutputW")]
        private static extern bool WriteConsoleOutput(
            SafeFileHandle hConsoleOutput,
            CharInfo[] lpBuffer,
            Coord dwBufferSize,
            Coord dwBufferCoord,
            ref SmallRect lpWriteRegion
        );



        [StructLayout(LayoutKind.Sequential)]
        private struct Coord
        {
            public short X;
            public short Y;

            public Coord(short x, short y)
            {
                X = x;
                Y = y;
            }
        };

        [StructLayout(LayoutKind.Explicit)]
        private struct CharUnion
        {
            [FieldOffset(0)] public char UnicodeChar;
            [FieldOffset(0)] public byte AsciiChar;
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Auto)]
        private struct CharInfo
        {
            [FieldOffset(0)] public CharUnion Char;
            [FieldOffset(2)] public short Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SmallRect
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class CONSOLE_FONT_INFOEX
        {
            private int cbSize;
            public CONSOLE_FONT_INFOEX()
            {
                cbSize = Marshal.SizeOf(typeof(CONSOLE_FONT_INFOEX));
            }

            public int FontIndex;
            public short FontWidth;
            public short FontHeight;
            public int FontFamily;
            public int FontWeight;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FaceName;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCurrentConsoleFontEx
          (
          IntPtr ConsoleOutput,
          bool MaximumWindow,
          [In, Out] CONSOLE_FONT_INFOEX ConsoleCurrentFontEx
        );
        #endregion

        public struct KeyState
        {
            public bool Pressed;
            public bool Released;
            public bool Held;
        }

        private readonly SafeFileHandle _consolehandle;
        private readonly Plane<CharInfo> _screenbuf;
        private readonly Coord _screencoord;
        private readonly Coord _topleft = new Coord() { X = 0, Y = 0 };
        private readonly Thread _gamethread;
        private SmallRect _screenrect;

        private const int KEYSTATES = 0XFF;
        private readonly short[] _newkeystate = new short[KEYSTATES];
        private readonly short[] _oldkeystate = new short[KEYSTATES];

        public KeyState[] KeyStates { get; private set; }
        public short Width { get; private set; }
        public short Height { get; private set; }
        public string Title { get { return Console.Title; } set { Console.Title = value ?? "GameConsole by RobIII"; } }

        public enum PIXELS
        {
            PIXEL_NONE = '\0',
            PIXEL_SOLID = (char)0xDB,
            PIXEL_THREEQUARTERS = (char)0XB2,
            PIXEL_HALF = (char)0XB1,
            PIXEL_QUARTER = (char)0xB0
        }

        public enum COLOR
        {
            FG_BLACK = 0x0000,
            FG_DARK_BLUE = 0x0001,
            FG_DARK_GREEN = 0x0002,
            FG_DARK_CYAN = 0x0003,
            FG_DARK_RED = 0x0004,
            FG_DARK_MAGENTA = 0x0005,
            FG_DARK_YELLOW = 0x0006,
            FG_GREY = 0x0007,
            FG_DARK_GREY = 0x0008,
            FG_BLUE = 0x0009,
            FG_GREEN = 0x000A,
            FG_CYAN = 0x000B,
            FG_RED = 0x000C,
            FG_MAGENTA = 0x000D,
            FG_YELLOW = 0x000E,
            FG_WHITE = 0x000F,
            BG_BLACK = 0x0000,
            BG_DARK_BLUE = 0x0010,
            BG_DARK_GREEN = 0x0020,
            BG_DARK_CYAN = 0x0030,
            BG_DARK_RED = 0x0040,
            BG_DARK_MAGENTA = 0x0050,
            BG_DARK_YELLOW = 0x0060,
            BG_GREY = 0x0070,
            BG_DARK_GREY = 0x0080,
            BG_BLUE = 0x0090,
            BG_GREEN = 0x00A0,
            BG_CYAN = 0x00B0,
            BG_RED = 0x00C0,
            BG_MAGENTA = 0x00D0,
            BG_YELLOW = 0x00E0,
            BG_WHITE = 0x00F0,
        }

        public GameConsole(short width, short height, string title = null, string font = "Consolas", short fontwidth = 8, short fontheight = 8)
        {
            Console.Clear();
            Width = width;
            Height = height;
            Title = title;

            KeyStates = new KeyState[KEYSTATES];

            _consolehandle = CreateFile("CONOUT$", 0x40000000, 0x02, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

            if (!_consolehandle.IsInvalid)
            {
                _screenbuf = new Plane<CharInfo>(Width, Height);
                _screenrect = new SmallRect() { Left = 0, Top = 0, Right = Width, Bottom = Height };
                _screencoord = new Coord() { X = Width, Y = Height };

                var cfi = new CONSOLE_FONT_INFOEX()
                {
                    FaceName = font,
                    FontWidth = fontwidth,
                    FontHeight = fontheight,
                    FontFamily = 0,            //FF_DONTCARE
                    FontWeight = 0x0190,       //FW_NORMAL
                    FontIndex = 0
                };

                SetCurrentConsoleFontEx(_consolehandle.DangerousGetHandle(), false, cfi);
            }

            if (width > Console.LargestWindowWidth || height > Console.LargestWindowHeight)
                throw new InvalidOperationException($"Unable to create console; maximum width/height are {Console.LargestWindowWidth} x {Console.LargestWindowHeight}");

            Console.WindowWidth = width;
            Console.WindowHeight = height;
            Console.CursorVisible = false;
            //Console.OutputEncoding = Encoding.Unicode;

            _gamethread = new Thread(() =>
            {
                if (OnUserCreate())
                {
                    var sw = Stopwatch.StartNew();
                    var cont = true;

                    while (cont)
                    {
                        GetKeyStates();

                        cont = OnUserUpdate(sw.Elapsed);
                        Paint();
                    };

                }
            });
        }

        public void DrawSprite(int x, int y, Sprite sprite, char alphaChar = '\0')
        {
            for (int py = 0; py < sprite.Height; py++)
            {
                for (int px = 0; px < sprite.Width; px++)
                {
                    var c = sprite.GetChar(px, py);
                    if (c != alphaChar)
                        SetChar(x + px, y + py, sprite.GetChar(px, py), sprite.GetColor(px, py));
                }
            }
        }

        public void Clear()
        {
            Fill(0, 0, Width, Height, (char)PIXELS.PIXEL_NONE, (short)COLOR.BG_BLACK);
        }

        public void Fill(int x, int y, int width, int height, char c = (char)PIXELS.PIXEL_NONE, short attributes = (short)COLOR.BG_BLACK)
        {
            for (int xp = x; xp < width; xp++)
                for (int yp = y; yp < height; yp++)
                    SetChar(xp, yp, (char)PIXELS.PIXEL_NONE, 0);
        }

        public void Print(int x, int y, string text, short attributes = (int)COLOR.FG_WHITE)
        {
            for (int i = 0; i < text.Length; ++i)
            {
                SetChar(x + i, y, text[i], attributes);
            }
        }

        public void SetChar(int x, int y, char c, short attributes = (short)COLOR.FG_WHITE)
        {
            var offset = _screenbuf.GetOffset(x, y);
            _screenbuf.Data[offset].Attributes = attributes;
            _screenbuf.Data[offset].Char.UnicodeChar = c;
        }

        public char GetChar(int x, int y)
        {
            return _screenbuf.GetData(x, y).Char.UnicodeChar;
        }

        public void Start()
        {

            _gamethread.Start();
            _gamethread.Join();
        }

        public KeyState GetKeyState(ConsoleKey key)
        {
            return KeyStates[(int)key];
        }

        public static int Clamp(int v, int min, int max)
        {
            return Math.Min(Math.Max(v, min), max);
        }

        public static double ClampF(double v, double min, double max)
        {
            return Math.Min(Math.Max(v, min), max);
        }

        public abstract bool OnUserCreate();

        public abstract bool OnUserUpdate(TimeSpan elapsedTime);

        private void Paint()
        {
            WriteConsoleOutput(_consolehandle, _screenbuf.Data, _screencoord, _topleft, ref _screenrect);
        }

        private void GetKeyStates()
        {
            for (int i = 0; i < KEYSTATES; i++)
            {

                _newkeystate[i] = GetAsyncKeyState(i);

                KeyStates[i].Pressed = false;
                KeyStates[i].Released = false;

                if (_newkeystate[i] != _oldkeystate[i])
                {
                    if ((_newkeystate[i] & 0x8000) != 0)
                    {
                        KeyStates[i].Pressed = !KeyStates[i].Held;
                        KeyStates[i].Held = true;
                    }
                    else
                    {
                        KeyStates[i].Released = true;
                        KeyStates[i].Held = false;
                    }
                }
                _oldkeystate[i] = _newkeystate[i];
            }
        }

        #region IDisposable Support
        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects) here.
                }

                _consolehandle.Dispose();

                _disposed = true;
            }
        }

        ~GameConsole()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
    public class Plane<T>
    {
        private readonly T[] _data;

        public T[] Data { get { return _data; } }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public Plane(int width, int height)
        {
            Width = width;
            Height = height;
            _data = new T[width * height];
        }

        public int GetOffset(int x, int y)
        {
            return GameConsole.Clamp(y, 0, Height - 1) * Width + GameConsole.Clamp(x, 0, Width - 1);
        }

        private int EnsureValidOffset(int offset)
        {
            return GameConsole.Clamp(offset, 0, _data.Length);
        }

        public void SetData(int offset, T data)
        {
            _data[EnsureValidOffset(offset)] = data;
        }

        public void SetData(int x, int y, T data)
        {
            _data[GetOffset(x, y)] = data;
        }

        public T GetData(int offset)
        {
            return _data[EnsureValidOffset(offset)];
        }

        public T GetData(int x, int y)
        {
            return _data[GetOffset(x, y)];
        }
    }
    public class Sprite
    {
        private readonly Plane<char> _spritedata;
        private readonly Plane<short> _spritecolors;
        private readonly int _width;
        private readonly int _height;

        public int Width { get { return _width; } }
        public int Height { get { return _height; } }

        public Sprite(string[] spriteData, short[] spriteColors = null)
        {
            if (spriteData.Length == 0)
                throw new ArgumentException(nameof(spriteData));
            if (spriteData.Any(s => s.Length != spriteData[0].Length))
                throw new ArgumentException(nameof(spriteData));
            _width = spriteData[0].Length;
            _height = spriteData.Length;


            if (spriteColors != null && spriteColors.Length != _width * _height)
                throw new ArgumentException(nameof(spriteColors));

            _spritedata = new Plane<char>(_width, _height);
            _spritecolors = new Plane<short>(_width, _height);

            int i = 0;

            foreach (var s in spriteData)
                foreach (var c in s)
                    _spritedata.SetData(i++, c);

            for (i = 0; i < _spritecolors.Data.Length; i++)
                _spritecolors.SetData(i, spriteColors == null ? (short)GameConsole.COLOR.FG_GREY : spriteColors[i]);
        }

        public Sprite(string[] spriteData, GameConsole.COLOR[] spriteColors = null)
            : this(spriteData, spriteColors?.Select(c => (short)c).ToArray())
        { }

        public char GetChar(int x, int y)
        {
            return _spritedata.GetData(x, y);
        }

        public short GetColor(int x, int y)
        {
            return _spritecolors.GetData(x, y);
        }
    }
}