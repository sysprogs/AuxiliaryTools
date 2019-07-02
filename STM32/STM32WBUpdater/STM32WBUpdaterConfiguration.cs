using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STM32WBUpdater
{
    public class STM32WBUpdaterConfiguration
    {
        public string SupportedDeviceIDRegex;

        public class ProgrammableBinary
        {
            public string Line1 { get; set; }
            public string Line2 { get; set; }

            public string FileName { get; set; }
            public string Version { get; set; }
            public string BaseAddress { get; set; }

            public ulong ParsedBaseAddress
            {
                get
                {
                    if (BaseAddress?.StartsWith("0x") != true)
                        throw new Exception("Invalid base address: " + BaseAddress);

                    return ulong.Parse(BaseAddress.Substring(2), NumberStyles.AllowHexSpecifier);
                }
            }
        }

        public ProgrammableBinary Bootloader;
        public string ExpectedBootloaderVersion;

        public ProgrammableBinary[] Stacks { get; set; }
    }
}
