using System;
using System.Collections.Generic;
using System.Text;

namespace SharedCode
{
    static class MessageConstants
    {
        public static readonly int NextPreviousMessage = 0x1;
        public static readonly int FrameMessage = 0x2;
        public static readonly int SourceListMessage = 0x3;

        public static readonly char SourceListSeparator = '|';
    }
}
