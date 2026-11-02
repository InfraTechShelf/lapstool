using System;

namespace LapsTool.Core.Laps
{
    /// <summary>利用者にそのまま見せてよいメッセージを持つ例外。</summary>
    public class LapsException : Exception
    {
        public LapsException(string message) : base(message)
        {
        }

        public LapsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
