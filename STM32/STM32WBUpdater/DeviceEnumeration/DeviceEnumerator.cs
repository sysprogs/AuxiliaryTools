using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace STM32WBUpdater.DeviceEnumeration
{
    public class DeviceInformationSet : IDisposable
    {
        public class DeviceInfo
        {
            public string HardwareID;
            public string DeviceID;
            public string UserFriendlyName;
            private DeviceInformationSet deviceEnumerator;
            public SP_DEVINFO_DATA DevinfoData;
            public string Driver;

            public DeviceInfo(DeviceInformationSet deviceEnumerator, SP_DEVINFO_DATA devinfoData, string hardwareID, string deviceID, string userFriendlyName)
            {
                this.deviceEnumerator = deviceEnumerator;
                this.DevinfoData = devinfoData;
                HardwareID = hardwareID;
                DeviceID = deviceID;
                UserFriendlyName = userFriendlyName;
            }

            public bool ChangeDeviceState(DICS newState)
            {
                SP_PROPCHANGE_PARAMS pcp = new SP_PROPCHANGE_PARAMS();
                pcp.ClassInstallHeader.cbSize = Marshal.SizeOf(typeof(SP_CLASSINSTALL_HEADER));
                pcp.ClassInstallHeader.InstallFunction = DI_FUNCTION.DIF_PROPERTYCHANGE;
                pcp.StateChange = (int)newState;
                pcp.Scope = (int)DICS_FLAG.DICS_FLAG_GLOBAL;
                pcp.HwProfile = 0;
                if (!SetupDiSetClassInstallParams(deviceEnumerator._HardwareDeviceInfo, ref DevinfoData, ref pcp, Marshal.SizeOf(pcp)))
                    return false;
                if (!SetupDiCallClassInstaller((uint)DI_FUNCTION.DIF_PROPERTYCHANGE, deviceEnumerator._HardwareDeviceInfo, ref DevinfoData))
                    return false;
                return true;
            }

        }

        #region SETUPAPI function prototypes
        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public Int32 cbSize;
            public Guid ClassGuid;
            public UInt32 DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_CLASSINSTALL_HEADER
        {
            public int cbSize;
            public DI_FUNCTION InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_PROPCHANGE_PARAMS
        {
            public SP_CLASSINSTALL_HEADER ClassInstallHeader;
            public Int32 StateChange;
            public Int32 Scope;
            public Int32 HwProfile;
        }

        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr RESERVED;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SP_DEVICE_INTERFACE_DETAIL_DATA_W
        {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            public string DevicePath;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern int CM_Locate_DevNode(out int pdnDevInst, string pDeviceID, int ulFlags);

        [DllImport("setupapi.dll")]
        static extern int CM_Get_Parent(out int pdnDevInst, int dnDevInst, int ulFlags);

        public static string LocateParentDevice(string deviceID)
        {
            int devInst;
            if (CM_Locate_DevNode(out devInst, deviceID, 0) != 0)
                return null;

            int parentInst;
            if (CM_Get_Parent(out parentInst, devInst, 0) != 0)
                return null;

            StringBuilder sb = new StringBuilder { Capacity = 1024 };
            int r = CM_Get_Device_ID(parentInst, sb, sb.Capacity - 1, 0);
            return sb.ToString().TrimEnd('\0');
        }


        [Flags]
        enum DIGCF : uint
        {
            DIGCF_DEFAULT = 0x00000001,    // only valid with DIGCF_DEVICEINTERFACE
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }

        enum DI_FUNCTION : int
        {
            DIF_SELECTDEVICE = 0x00000001,
            DIF_INSTALLDEVICE = 0x00000002,
            DIF_ASSIGNRESOURCES = 0x00000003,
            DIF_PROPERTIES = 0x00000004,
            DIF_REMOVE = 0x00000005,
            DIF_FIRSTTIMESETUP = 0x00000006,
            DIF_FOUNDDEVICE = 0x00000007,
            DIF_SELECTCLASSDRIVERS = 0x00000008,
            DIF_VALIDATECLASSDRIVERS = 0x00000009,
            DIF_INSTALLCLASSDRIVERS = 0x0000000A,
            DIF_CALCDISKSPACE = 0x0000000B,
            DIF_DESTROYPRIVATEDATA = 0x0000000C,
            DIF_VALIDATEDRIVER = 0x0000000D,
            DIF_MOVEDEVICE = 0x0000000E,
            DIF_DETECT = 0x0000000F,
            DIF_INSTALLWIZARD = 0x00000010,
            DIF_DESTROYWIZARDDATA = 0x00000011,
            DIF_PROPERTYCHANGE = 0x00000012,
            DIF_ENABLECLASS = 0x00000013,
            DIF_DETECTVERIFY = 0x00000014,
            DIF_INSTALLDEVICEFILES = 0x00000015,
            DIF_UNREMOVE = 0x00000016,
            DIF_SELECTBESTCOMPATDRV = 0x00000017,
            DIF_ALLOW_INSTALL = 0x00000018,
            DIF_REGISTERDEVICE = 0x00000019,
            DIF_NEWDEVICEWIZARD_PRESELECT = 0x0000001A,
            DIF_NEWDEVICEWIZARD_SELECT = 0x0000001B,
            DIF_NEWDEVICEWIZARD_PREANALYZE = 0x0000001C,
            DIF_NEWDEVICEWIZARD_POSTANALYZE = 0x0000001D,
            DIF_NEWDEVICEWIZARD_FINISHINSTALL = 0x0000001E,
            DIF_UNUSED1 = 0x0000001F,
            DIF_INSTALLINTERFACES = 0x00000020,
            DIF_DETECTCANCEL = 0x00000021,
            DIF_REGISTER_COINSTALLERS = 0x00000022,
            DIF_ADDPROPERTYPAGE_ADVANCED = 0x00000023,
            DIF_ADDPROPERTYPAGE_BASIC = 0x00000024,
            DIF_RESERVED1 = 0x00000025,
            DIF_TROUBLESHOOTER = 0x00000026,
            DIF_POWERMESSAGEWAKE = 0x00000027,
            DIF_ADDREMOTEPROPERTYPAGE_ADVANCED = 0x00000028,
            DIF_UPDATEDRIVER_UI = 0x00000029,
            DIF_RESERVED2 = 0x00000030,
        };

        public enum DICS
        {
             DICS_ENABLE      = 0x00000001,
             DICS_DISABLE     = 0x00000002,
             DICS_PROPCHANGE  = 0x00000003,
             DICS_START       = 0x00000004,
             DICS_STOP        = 0x00000005,
        }

        [Flags]
        enum DICS_FLAG
        {
            DICS_FLAG_GLOBAL         = 0x00000001,  // make change in all hardware profiles
            DICS_FLAG_CONFIGSPECIFIC = 0x00000002,  // make change in specified profile only
            DICS_FLAG_CONFIGGENERAL  = 0x00000004,  // 1 or more hardware profile-specific
                                                         // changes to follow.
        }

        enum SPDRP
        {
            SPDRP_DEVICEDESC = 0x00000000,
            SPDRP_HARDWAREID = 1,
            SPDRP_DRIVER = 9,
            SPDRP_FRIENDLYNAME = (0x0000000C),  // FriendlyName (R/W)
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            SPDRP Property,
            out UInt32 PropertyRegDataType,
            StringBuilder PropertyBuffer,
            uint PropertyBufferSize,
            out UInt32 RequiredSize
            );
        
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(
                                                  ref Guid ClassGuid,
                                                  [MarshalAs(UnmanagedType.LPTStr)] string Enumerator,
                                                  IntPtr hwndParent,
                                                  UInt32 Flags
                                                 );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(
                                                  IntPtr ClassGuid,
                                                  [MarshalAs(UnmanagedType.LPTStr)] string Enumerator,
                                                  IntPtr hwndParent,
                                                  UInt32 Flags
                                                 );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

        [DllImport("setupapi.dll", CharSet=CharSet.Auto, SetLastError = true)]
        static extern Boolean SetupDiEnumDeviceInterfaces(
           IntPtr hDevInfo,
           IntPtr devInfo,
           IntPtr interfaceClassGuid,
           UInt32 memberIndex,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr hDevInfo,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           ref SP_DEVICE_INTERFACE_DETAIL_DATA_W deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           ref SP_DEVINFO_DATA deviceInfoData
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr hDevInfo,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           IntPtr deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           IntPtr deviceInfoData
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceInstanceId(
           IntPtr DeviceInfoSet,
           ref SP_DEVINFO_DATA DeviceInfoData,
           StringBuilder DeviceInstanceId,
           int DeviceInstanceIdSize,
           out int RequiredSize
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceInstanceId(
           IntPtr DeviceInfoSet,
           ref SP_DEVINFO_DATA DeviceInfoData,
           IntPtr DeviceInstanceId,
           int DeviceInstanceIdSize,
           out int RequiredSize
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        static extern int CM_Get_Device_ID(
           int dnDevInst,
           StringBuilder Buffer,
           int BufferLen,
           int ulFlags
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiSetClassInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref SP_PROPCHANGE_PARAMS ClassInstallParams, int ClassInstallParamsSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError=true)]
        static extern Boolean SetupDiCallClassInstaller(
                                                  UInt32 InstallFunction,
                                                  IntPtr DeviceInfoSet,
                                                  ref SP_DEVINFO_DATA DeviceInfoData
                                              );        

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
        public struct SP_DRVINFO_DATA
        {
            public int cbSize;
            public int DriverType;
            private IntPtr Reserved;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string MfgName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProviderName;
            public System.Runtime.InteropServices.ComTypes.FILETIME DriverDate;
            public long DriverVersion;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDriverInfoW(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, int driverType, int memberIndex, ref SP_DRVINFO_DATA driverInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiBuildDriverInfoList(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, int driverType);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiSetSelectedDevice(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupDiSetSelectedDriver(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref SP_DRVINFO_DATA DriverInfoData);

        [DllImport("newdev.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool InstallSelectedDriver(IntPtr hwndParent, IntPtr DeviceInfoSet, IntPtr reserved, bool backup, out int needReboot);

        public const int INSTALLFLAG_FORCE = 0x00000001;



        /// <summary>
        /// Driver media type
        /// </summary>
        public enum OemSourceMediaType
        {
            SPOST_NONE = 0,
            SPOST_PATH = 1,
            SPOST_URL = 2,
            SPOST_MAX = 3
        }

        /// <summary>
        /// Driver file copy style
        /// </summary>
        public enum OemCopyStyle
        {
            Default = 0,
            SP_COPY_DELETESOURCE = 0x0000001,   // delete source file on successful copy
            SP_COPY_REPLACEONLY = 0x0000002,   // copy only if target file already present
            SP_COPY_NEWER = 0x0000004,   // copy only if source newer than or same as target
            SP_COPY_NEWER_OR_SAME = SP_COPY_NEWER,
            SP_COPY_NOOVERWRITE = 0x0000008,   // copy only if target doesn't exist
            SP_COPY_NODECOMP = 0x0000010,   // don't decompress source file while copying
            SP_COPY_LANGUAGEAWARE = 0x0000020,   // don't overwrite file of different language
            SP_COPY_SOURCE_ABSOLUTE = 0x0000040,   // SourceFile is a full source path
            SP_COPY_SOURCEPATH_ABSOLUTE = 0x0000080,   // SourcePathRoot is the full path
            SP_COPY_IN_USE_NEEDS_REBOOT = 0x0000100,   // System needs reboot if file in use
            SP_COPY_FORCE_IN_USE = 0x0000200,   // Force target-in-use behavior
            SP_COPY_NOSKIP = 0x0000400,   // Skip is disallowed for this file or section
            SP_FLAG_CABINETCONTINUATION = 0x0000800,   // Used with need media notification
            SP_COPY_FORCE_NOOVERWRITE = 0x0001000,   // like NOOVERWRITE but no callback nofitication
            SP_COPY_FORCE_NEWER = 0x0002000,   // like NEWER but no callback nofitication
            SP_COPY_WARNIFSKIP = 0x0004000,   // system critical file: warn if user tries to skip
            SP_COPY_NOBROWSE = 0x0008000,   // Browsing is disallowed for this file or section
            SP_COPY_NEWER_ONLY = 0x0010000,   // copy only if source file newer than target
            SP_COPY_SOURCE_SIS_MASTER = 0x0020000,   // source is single-instance store master
            SP_COPY_OEMINF_CATALOG_ONLY = 0x0040000,   // (SetupCopyOEMInf only) don't copy INF--just catalog
            SP_COPY_REPLACE_BOOT_FILE = 0x0080000,   // file must be present upon reboot (i.e., it's needed by the loader), this flag implies a reboot
            SP_COPY_NOPRUNE = 0x0100000   // never prune this file
        }

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupCopyOEMInf(
            string SourceInfFileName,
            string OEMSourceMediaLocation,
            OemSourceMediaType OEMSourceMediaType,
            OemCopyStyle CopyStyle,
            string DestinationInfFileName,
            int DestinationInfFileNameSize,
            IntPtr RequiredSize,
            string DestinationInfFileNameComponent
            );

        public enum SetupUOInfFlags : uint { NONE = 0x0000, SUOI_FORCEDELETE = 0x0001 };

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupUninstallOEMInf(string InfFileName, SetupUOInfFlags Flags, IntPtr Reserved);

        #endregion

        IntPtr _HardwareDeviceInfo;

        public DeviceInformationSet()
        {
            _HardwareDeviceInfo = SetupDiGetClassDevs(
                                   IntPtr.Zero,
                                   null, // Define no enumerator (global)
                                   (IntPtr)0, // Define no
                                   (uint)(DIGCF.DIGCF_PRESENT | // Only Devices present
                                    DIGCF.DIGCF_ALLCLASSES)); // Function class devices.
        }

        public IEnumerable<DeviceInfo> GetAllDevices()
        {
            SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
            for (uint i = 0; ; i++)
            {
                deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);
                if (!SetupDiEnumDeviceInfo(_HardwareDeviceInfo,
                                             i,
                                             ref deviceInfoData))
                {
                    int Error = Marshal.GetLastWin32Error();
                    if (Error == 259) //ERROR_NO_MORE_ITEMS
                        yield break;
                }
                else
                {
                    int requiredSizeForID = 0;
                    SetupDiGetDeviceInstanceId(_HardwareDeviceInfo, ref deviceInfoData, IntPtr.Zero, 0, out requiredSizeForID);
                    StringBuilder deviceID = new StringBuilder(requiredSizeForID);
                    if (!SetupDiGetDeviceInstanceId(_HardwareDeviceInfo, ref deviceInfoData, deviceID, requiredSizeForID, out requiredSizeForID))
                        continue;

                    if(!deviceID.ToString().StartsWith("USB\\", StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    string friendlyName = QueryDeviceProperty(_HardwareDeviceInfo, ref deviceInfoData, SPDRP.SPDRP_DEVICEDESC);
                    string hardwareId = QueryDeviceProperty(_HardwareDeviceInfo, ref deviceInfoData, SPDRP.SPDRP_HARDWAREID);

                    if (friendlyName == null)
                        friendlyName = "USB device";

                    if (hardwareId == null || hardwareId.StartsWith(@"USB\ROOT_HUB", StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    string driver = QueryDeviceProperty(_HardwareDeviceInfo, ref deviceInfoData, SPDRP.SPDRP_DRIVER);


                    yield return new DeviceInfo(this, deviceInfoData, hardwareId, deviceID.ToString(), friendlyName) { Driver = driver };
                }
            }
        }

        private string QueryDeviceProperty(IntPtr hardwareDeviceInfo, ref SP_DEVINFO_DATA deviceInfoData, SPDRP propertyID)
        {
            uint type, nameSize;
            SetupDiGetDeviceRegistryProperty(hardwareDeviceInfo, ref deviceInfoData, propertyID, out type, null, 0, out nameSize);

            StringBuilder friendlyName = new StringBuilder((int)nameSize);
            if (!SetupDiGetDeviceRegistryProperty(hardwareDeviceInfo, ref deviceInfoData, propertyID, out type, friendlyName, nameSize, out nameSize))
                return null;

            return friendlyName.ToString();
        }

        public void Dispose()
        {
            if (_HardwareDeviceInfo != IntPtr.Zero)
                SetupDiDestroyDeviceInfoList(_HardwareDeviceInfo);
        }

        public DeviceInfo TryLookupDeviceById(string deviceId)
        {
            foreach (var devInfo in GetAllDevices())
            {
                if (devInfo.DeviceID == deviceId)
                    return devInfo;
            }
            return null;
        }

        public List<SP_DRVINFO_DATA> GetCompatibleDrivers(string deviceId)
        {
            DeviceInfo devInfo = TryLookupDeviceById(deviceId);
            if (devInfo == null)
                throw new Exception("Cannot locate device with ID " + deviceId);
            return GetCompatibleDrivers(devInfo);
        }

        public List<SP_DRVINFO_DATA> GetCompatibleDrivers(DeviceInfo devInfoFromTheSameSet)
        {
            List<SP_DRVINFO_DATA> result = new List<SP_DRVINFO_DATA>();
            if (!SetupDiBuildDriverInfoList(_HardwareDeviceInfo, ref devInfoFromTheSameSet.DevinfoData, 2))
                throw new Exception("Cannot build driver list: error " + Marshal.GetLastWin32Error());

            for (int i = 0; ; i++)
            {
                SP_DRVINFO_DATA driver = new SP_DRVINFO_DATA();
                driver.cbSize = Marshal.SizeOf(driver);
                if (!SetupDiEnumDriverInfoW(_HardwareDeviceInfo, ref devInfoFromTheSameSet.DevinfoData, 2 /* SPDIT_COMPATDRIVER */, i, ref driver))
                {
                    int Error = Marshal.GetLastWin32Error();
                    if (Error == 259) //ERROR_NO_MORE_ITEMS
                        break;
                }

                result.Add(driver);
            }

            return result;
        }

        public void InstallSpecificDriverForDevice(DeviceInfo deviceInstanceFromThisSet, SP_DRVINFO_DATA driverFromThisSet, IntPtr parentWindowHandle)
        {
            if (!SetupDiSetSelectedDevice(_HardwareDeviceInfo, ref deviceInstanceFromThisSet.DevinfoData))
                throw new LastWin32ErrorException("Cannot select the device for driver installation.");
            if (!SetupDiSetSelectedDriver(_HardwareDeviceInfo, ref deviceInstanceFromThisSet.DevinfoData, ref driverFromThisSet))
                throw new LastWin32ErrorException("Cannot select the driver for installation.");

            int needRestart;
            if (!InstallSelectedDriver(parentWindowHandle, _HardwareDeviceInfo, IntPtr.Zero, false, out needRestart))
                throw new LastWin32ErrorException("Cannot install the selected driver");
        }
    }

    class LastWin32ErrorException : Win32Exception
    {
        string _Message;

        public LastWin32ErrorException(string message)
            : base(Marshal.GetLastWin32Error())
        {
            _Message = message;
        }

        public override string Message
        {
            get
            {
                return _Message.TrimEnd('.') + ": " + base.Message;
            }
        }
    }
}
