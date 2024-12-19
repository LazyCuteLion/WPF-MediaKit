using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFMediaKit.DirectShow
{
    public class CameraParmeter
    {
        public Guid SubType { get; set; }
        public string SubTypeName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int FPS { get; set; }
        public long Size => Width * Height;

        public override string ToString()
        {
            return $"{Width}x{Height}@{FPS}->{SubTypeName}";
        }
    }
}
