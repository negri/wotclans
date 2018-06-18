using System;

namespace Negri.Wot
{
    /// <summary>
    /// Atributo de cor
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class RgbColorAttribute : Attribute
    {
        private readonly byte _red;
        private readonly byte _green;
        private readonly byte _blue;

        public RgbColorAttribute(byte red, byte green, byte blue)
        {
            _red = red;
            _green = green;
            _blue = blue;
        }


        public byte Red
        {
            get { return _red; }
        }

        public byte Green
        {
            get { return _green; }
        }

        public byte Blue
        {
            get { return _blue; }
        }
    }
}