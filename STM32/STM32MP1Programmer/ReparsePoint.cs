using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Collections.Generic;

namespace STM32MP1Programmer
{
    public static class ReparsePoint
    {
        private const int FSCTL_GET_REPARSE_POINT = 0x000900A8;

        #region CreateFile() flags
        [Flags]
        private enum EFileAccess : uint
        {
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,
        }

        [Flags]
        private enum EFileShare : uint
        {
            None = 0x00000000,
            Read = 0x00000001,
            Write = 0x00000002,
            Delete = 0x00000004,
        }

        private enum ECreationDisposition : uint
        {
            New = 1,
            CreateAlways = 2,
            OpenExisting = 3,
            OpenAlways = 4,
            TruncateExisting = 5,
        }

        [Flags]
        private enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }
        #endregion

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct REPARSE_DATA_BUFFER
        {
            public uint ReparseTag;
            public ushort ReparseDataLength;
            public ushort Reserved;
            public ushort SubstituteNameOffset;
            public ushort SubstituteNameLength;
            public ushort PrintNameOffset;
            public ushort PrintNameLength;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16 * 1024)]
            public string DataBuffer;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct REPARSE_DATA_BUFFER_SYMLINK
        {
            public uint ReparseTag;
            public ushort ReparseDataLength;
            public ushort Reserved;
            public ushort SubstituteNameOffset;
            public ushort SubstituteNameLength;
            public ushort PrintNameOffset;
            public ushort PrintNameLength;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16 * 1024)]
            public string DataBuffer;
        }


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode,
        IntPtr InBuffer,
        int nInBufferSize,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] OutBuffer, int nOutBufferSize,
        out int pBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            EFileAccess dwDesiredAccess,
            EFileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            ECreationDisposition dwCreationDisposition,
            EFileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        public static KeyValuePair<string, bool> Read(string dir)
        {
            using (SafeFileHandle handle = (CreateFile(dir, EFileAccess.GenericRead,
                EFileShare.Read | EFileShare.Write | EFileShare.Delete,
                IntPtr.Zero, ECreationDisposition.OpenExisting,
                EFileAttributes.BackupSemantics | EFileAttributes.OpenReparsePoint, IntPtr.Zero)))
            {
                if (handle.IsInvalid)
                    throw new Win32Exception();

                byte[] buf = new byte[65536];

                int bytesReturned;
                if (!DeviceIoControl(handle, FSCTL_GET_REPARSE_POINT, IntPtr.Zero, 0, buf, buf.Length, out bytesReturned, IntPtr.Zero))
                    throw new Win32Exception();
                uint tag = BitConverter.ToUInt32(buf, 0);
                if (tag == 0xA000000C) //IO_REPARSE_TAG_SYMLINK
                {
                    int pathBufferOffset = 8 + 12;
                    int nameOffset = BitConverter.ToInt16(buf, 8);
                    int nameLen = BitConverter.ToInt16(buf, 10);
                    int flags = BitConverter.ToInt32(buf, 16);
                    string target = Encoding.Unicode.GetString(buf, pathBufferOffset + nameOffset, nameLen);
                    if (target.StartsWith(@"\??\"))
                        target = target.Substring(4);

                    return new KeyValuePair<string, bool>(target, (flags & 1) != 0);
                }
                else if (tag == 0xA0000003) //IO_REPARSE_TAG_MOUNT_POINT
                {
                    int pathBufferOffset = 8 + 8;
                    int nameOffset = BitConverter.ToInt16(buf, 8);
                    int nameLen = BitConverter.ToInt16(buf, 10);
                    string target = Encoding.Unicode.GetString(buf, pathBufferOffset + nameOffset, nameLen);
                    if (target.StartsWith(@"\??\"))
                        target = target.Substring(4);

                    return new KeyValuePair<string, bool>(target, false);
                }
                else
                    throw new Exception(string.Format("Unknown reparse tag: 0x{0:x8}", tag));
            }
        }

    }
}
