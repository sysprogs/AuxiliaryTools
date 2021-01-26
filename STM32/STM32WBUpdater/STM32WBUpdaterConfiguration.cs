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

            public ulong GetParsedBaseAddress(int index)
            {
                var addr = BaseAddress.Split('/')[index];

                if (!addr.StartsWith("0x"))
                    throw new Exception("Invalid base address: " + BaseAddress);

                return ulong.Parse(addr.Substring(2), NumberStyles.AllowHexSpecifier);
            }

            internal bool IsCompatibleWithDevice(int index)
            {
                var addr = BaseAddress.Split('/')[index];
                if (addr == "-")
                    return false;
                return true;
            }

            public bool MatchesFilter(string filter)
            {
                if (string.IsNullOrEmpty(filter))
                    return true;

                foreach (var str in new[] { Line1, Line2, FileName, Version, BaseAddress })
                    if (str != null && str.IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return true;

                return false;
            }
        }

        public class ProgrammableBootloader : ProgrammableBinary
        {
            public string TriggerVersions;

            public bool ShouldProgram(ulong detectedVersion)
            {
                foreach(var ver in TriggerVersions.Split('/'))
                {
                    if (ver?.StartsWith("0x") != true)
                        throw new Exception("Invalid base address: " + ver);

                    var parsedVer = ulong.Parse(ver.Substring(2), NumberStyles.AllowHexSpecifier);
                    if (parsedVer == detectedVersion)
                        return true;
                }

                return false;
            }
        }

        public ProgrammableBootloader[] Bootloaders;
        public string Version;
        public string DeviceTypes;

        public ProgrammableBinary[] Stacks { get; set; }
    }
}
