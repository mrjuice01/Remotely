﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Remotely.Desktop.XPlat.Native.Linux
{
    public class Libc
    {
        [DllImport("libc", SetLastError = true)]
        public static extern uint geteuid();
    }
}
