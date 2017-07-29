using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using NewLife.Log;

namespace NewLife.Build
{
    /// <summary>JLink操作封装</summary>
    public class JLink : DisposeBase
    {
        #region 构造
        static JLink()
        {
            var dll = FindDLL();
            if (!dll.IsNullOrEmpty())
            {
                var dir = Path.GetDirectoryName(dll);
                XTrace.WriteLine("发现JLink：{0}", dll);

                SetDllDirectory(dir);
            }
        }

        [DllImport("kernel32.dll")]
        static extern int SetDllDirectory(String pathName);

        /// <summary>实例化</summary>
        public JLink()
        {
        }

        private void DebugCom(String msg)
        {
            WriteLog(msg);
        }

        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void OnDispose(Boolean disposing)
        {
            base.OnDispose(disposing);

            try
            {
                Close();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
        #endregion

        #region 主要方法
        /// <summary>连接</summary>
        /// <returns></returns>
        public Boolean Connect()
        {
            //if (!IsConnected())
            //{
            //    WriteLog("未连接！");
            //    return false;
            //}

            if (IsOpen()) return true;

            Open();
            SetSpeed(50000);

            //EnableLogCom(this.GetType().GetMethodEx(nameof(DebugCom)).MethodHandle.Value);

            WriteLog(GetCompileDateTime());
            WriteLog(GetFirmwareString());
            //WriteLog(GetHardwareVersion());
            WriteLog(GetFeatureString());
            //WriteLog(GetId());

            ExecCommand("device=Cortex-M3");
            Select(1);
            //SetSpeed(1000);

            Reset();
            Halt();

            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(10);

                if (IsConnected()) return true;
            }

            WriteLog("未连接！");

            return false;
        }
        #endregion

        #region Flash操作
        const UInt32 WP = 0x01;
        const UInt32 SLB = 0x02;
        const UInt32 WPL = 0x03;
        const UInt32 CLB = 0x04;
        const UInt32 EA = 0x08;
        const UInt32 SGPB = 0x0B;
        const UInt32 CGPB = 0x0D;
        const UInt32 SSB = 0x0F;
        const UInt32 EFC_BASE = 0xFFFFFF00;
        const UInt32 PAGE_SIZE = 128;

        /// <summary>向Flash发送命令</summary>
        /// <param name="command"></param>
        /// <param name="page"></param>
        public void FlashCommand(UInt32 command, UInt32 page)
        {
            WriteU32(EFC_BASE + 0x64, (UInt32)((command & 0x0F) | (page & 0x3FF) << 8) | (0x5A << 24));

            while (true)
            {
                var status = ReadUInt32(EFC_BASE + 0x64);

                if ((status & 1) == 1) break;
            }
        }

        private void dumpMemory(UInt32 address, UInt32 size)
        {
            for (UInt32 i = 0; i < size; i += 16)
            {
                var buf = Read(address + i, 16);
                WriteLog("Dump 0x{0:X8} : {1}", address + i, buf.ToHex());
            }
        }

        /// <summary>写入Flash</summary>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <param name="buffer"></param>
        public void FlashWrite(UInt32 address, UInt32 size, Byte[] buffer)
        {
            address &= 0x000FFFFF;

            var p = 0;
            var remain = (size + 3) / 4;
            while (remain > 0)
            {
                var page = address / PAGE_SIZE;
                var count = ((page + 1) * PAGE_SIZE - address) / 4;
                if (count > remain) count = remain;

                // 写数据
                for (int i = 0; i < count; i++)
                {
                    WriteU32(address, buffer[p]);

                    address += 4;
                    p += 4;
                }
                FlashCommand(WP, page);

                remain -= count;
            }
        }

        /// <summary>擦除Flash</summary>
        public void EraseFlash()
        {
            dumpMemory(0, 16);
            FlashCommand(EA, 0);
            dumpMemory(0, 16);
        }

        void Init()
        {
            while (true)
            {
                var status = Halt();
                ClearError();
                if (status == 0) break;
            }

            ResetPullsRESET(1);
            ResetPullsTRST(1);
            Reset();
            WriteU32(0xFFFFFC20, 0x00000601);
            WriteU32(0xFFFFFC2C, 0x00191C05);
            WriteU32(0xFFFFFC30, 0x00000007);
        }

        void MapRam(Boolean flag)
        {
            WriteU32(0, 0);

            var dataBefore = ReadUInt32(0);

            var dataWrite = ~dataBefore;
            WriteU32(0, dataWrite);

            var dataAfter = ReadUInt32(0);
            if (flag)
            {
                if (dataAfter != dataWrite)
                {
                    WriteU32(0xffffff00, 0x00000001);
                }
            }
            else
            {
                if (dataAfter == dataWrite)
                {
                    WriteU32(0xffffff00, 0x00000001);
                }
            }

        }

        void InitFlash()
        {
            Init();

            WriteU32(EFC_BASE + 0x60, 0x00320180);

            MapRam(false);

            EraseFlash();
        }

        //void FlashDownload(ELF* elf)
        //{
        //    InitialiseForFlashing();


        //    printf("Downloading...");
        //    fflush(stdout);

        //    // Don't erase before writing   
        //    WriteU32(EFC_BASE + 0x60, ReadU32(EFC_BASE + 0x60) | (1 << 7));

        //    for (int i = 0; i < elf->header.e_phnum; i++)
        //    {
        //        Elf32_Phdr* hdr = &elf->programHeaders[i];
        //        // Don't put anything in low memory - it is mapped either to ram or flash, or something   
        //        // that has no size   
        //        if ((hdr->p_type == PT_LOAD) && (hdr->p_filesz > 0) && (hdr->p_paddr >= 0x00100000))
        //        {
        //            void* buffer = elf->ProgramSegment(i);
        //            //          SwapWords((unsigned char*)buffer, hdr->p_memsz);   
        //            FlashWrite(hdr->p_paddr, hdr->p_filesz, buffer);
        //            delete[] buffer;
        //        }
        //    }
        //    fprintf(stdout, "...Done\n");

        //}
        #endregion

        #region 连接
        /// <summary>是否连接</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_IsConnected", CallingConvention = CallingConvention.Cdecl)]
        public extern static Boolean IsConnected();

        ///// <summary>启用日志</summary>
        ///// <returns></returns>
        //[DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_EnableLog", CallingConvention = CallingConvention.Cdecl)]
        //public extern static Boolean EnableLog(Boolean flag);

        /// <summary>启用日志</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_EnableLogCom", CallingConvention = CallingConvention.Cdecl)]
        public extern static Boolean EnableLogCom(IntPtr debugCom);

        /// <summary>清除错误</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ClrError", CallingConvention = CallingConvention.Cdecl)]
        public extern static Boolean ClearError();

        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GetCompileDateTime", CallingConvention = CallingConvention.Cdecl)]
        extern static IntPtr GetCompileDateTime_();
        /// <summary>获取编译日期</summary>
        /// <returns></returns>
        public static String GetCompileDateTime()
        {
            var ip = GetCompileDateTime_();
            return Marshal.PtrToStringAnsi(ip);
        }

        ///// <summary>获取编译日期</summary>
        ///// <returns></returns>
        //[DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GetDebugInfo", CallingConvention = CallingConvention.Cdecl)]
        //public extern static String GetDebugInfo(Int32 param1, Int32 param2);

        ///// <summary>获取编译日期</summary>
        ///// <returns></returns>
        //[DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GetDeviceFamily", CallingConvention = CallingConvention.Cdecl)]
        //public extern static String GetDeviceFamily();

        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GetFeatureString", CallingConvention = CallingConvention.Cdecl)]
        extern static Int32 GetFeatureString_(StringBuilder sb);
        /// <summary>获取功能信息</summary>
        /// <returns></returns>
        public static String GetFeatureString()
        {
            var sb = new StringBuilder();
            GetFeatureString_(sb);
            return sb.ToString();
        }

        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GetFirmwareString", CallingConvention = CallingConvention.Cdecl)]
        extern static Int32 GetFirmwareStringg_(StringBuilder sb, Int32 count);
        /// <summary>获取功能信息</summary>
        /// <returns></returns>
        public static String GetFirmwareString()
        {
            var sb = new StringBuilder(256);
            GetFirmwareStringg_(sb, 256);
            return sb.ToString();
        }

        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GetHardwareVersion", CallingConvention = CallingConvention.Cdecl)]
        extern static IntPtr GetHardwareVersion_();
        /// <summary>获取编译日期</summary>
        /// <returns></returns>
        public static String GetHardwareVersion()
        {
            var ip = GetHardwareVersion_();
            return Marshal.PtrToStringAnsi(ip);
        }

        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GetId", CallingConvention = CallingConvention.Cdecl)]
        extern static IntPtr GetId_();
        /// <summary>获取编译日期</summary>
        /// <returns></returns>
        public static String GetId()
        {
            var ip = GetId_();
            return Marshal.PtrToStringAnsi(ip);
        }
        #endregion

        #region 打开关闭测试
        /// <summary>打开</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_Open", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 Open();

        /// <summary>是否打开</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_IsOpen", CallingConvention = CallingConvention.Cdecl)]
        public extern static Boolean IsOpen();

        /// <summary>关闭</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_Close", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 Close();

        /// <summary>测试</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_Test", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 Test();
        #endregion

        #region 执行挂起
        /// <summary>执行</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_Go", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 Go();

        /// <summary>执行</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GoEx", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 GoEx(Int32 param1, Int32 param2);

        /// <summary>执行</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GoHalt", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 GoHalt();

        /// <summary>挂起</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_Halt", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 Halt();

        /// <summary>是否挂起</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_IsHalted", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 IsHalted();
        #endregion

        #region 重置
        /// <summary>设置重置类型</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_SetResetType", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 SetResetType(Int32 type);

        /// <summary>设置重置延迟</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_SetResetDelay", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 SetResetDelay(Int32 delay);

        /// <summary>重置</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_Reset", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 Reset();

        /// <summary>重置</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ResetNoHalt", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 ResetNoHalt();

        /// <summary>重置</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ResetPullsRESET", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 ResetPullsRESET(Int32 param);

        /// <summary>重置</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ResetPullsTRST", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 ResetPullsTRST(Int32 param);
        #endregion

        #region 选择
        /// <summary>选择</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_TIF_Select", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 Select(Int32 is_swd_intf);

        /// <summary>获取可用</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_TIF_GetAvailable", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 GetAvailable(Int32 param);
        #endregion

        #region 速度
        /// <summary>设置速度</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_SetSpeed", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 SetSpeed(Int32 jlink_speed);

        /// <summary>设置最大速度</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_SetMaxSpeed", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 SetMaxSpeed();

        /// <summary>获取速度</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_GetSpeed", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 GetSpeed();
        #endregion

        #region 读取
        /// <summary>读取</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ReadMem", CallingConvention = CallingConvention.Cdecl)]
        extern static UInt32 ReadMem(UInt32 memaddr, UInt32 size, Byte[] buffer);
        ///// <summary>读取</summary>
        ///// <param name="addr"></param>
        ///// <param name="size"></param>
        ///// <returns></returns>
        //public static Byte[] Read(UInt32 addr, Int32 size)
        //{
        //    var buf = new Byte[size];
        //    var rs = ReadMem(addr, size, buf);
        //    if (rs == buf.Length) return buf;

        //    return buf.ReadBytes(0, rs);
        //}

        /// <summary>读取</summary>
        /// <param name="addr"></param>
        /// <param name="size"></param>
        /// <param name="blocksize"></param>
        /// <returns></returns>
        public Byte[] Read(UInt32 addr, UInt32 size, UInt32 blocksize = 1024)
        {
            WriteLog("Read 0x{0:X8} 0x{1:X8}({2:n0}k)", addr, size, size / 1024);

            var ms = new MemoryStream();
            var buf = new Byte[blocksize];

            // 头部对齐
            var bs = addr % blocksize;
            if (bs > 0)
            {
                bs = blocksize - bs;
                if (bs > size) bs = size;
                WriteLog("ReadBlock 0x{0:X8} 0x{1:X8}({1:n0})", addr, bs);
                var rs = ReadMem(addr, bs, buf);
                //if (rs > 0)
                {
                    ms.Write(buf, 0, (Int32)bs);
                    addr += bs;
                    size -= bs;
                }
            }
            // 循环读取
            while (size > 0)
            {
                bs = blocksize;
                if (bs > size) bs = size;
                WriteLog("ReadBlock 0x{0:X8} 0x{1:X8}({1:n0})", addr, bs);
                var rs = ReadMem(addr, bs, buf);
                //if (rs == 0) break;

                ms.Write(buf, 0, (Int32)bs);
                addr += bs;
                size -= bs;
            }

            return ms.ToArray();
        }

        /// <summary>读取</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ReadMemU8", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 ReadMemU8(UInt32 memaddr, Int32 size, Byte[] buffer, Int32 flag);

        /// <summary>读取</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ReadMemU16", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 ReadMemU16(UInt32 memaddr, Int32 size, UInt16[] buffer, Int32 flag);

        /// <summary>读取</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ReadMemU32", CallingConvention = CallingConvention.Cdecl)]
        extern static Int32 ReadMemU32(UInt32 memaddr, Int32 size, ref UInt32 data, Int32 flag);

        /// <summary>读取</summary>
        /// <param name="addr"></param>
        public static UInt32 ReadUInt32(UInt32 addr)
        {
            UInt32 rs = 0;
            ReadMemU32(addr, 1, ref rs, 0);

            return rs;
        }
        #endregion

        #region 写入
        /// <summary>写入</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_WriteMem", CallingConvention = CallingConvention.Cdecl)]
        extern static UInt32 WriteMem(UInt32 memaddr, UInt32 size, Byte[] buffer);
        /// <summary>写入</summary>
        /// <param name="addr"></param>
        /// <param name="buffer"></param>
        /// <param name="blocksize"></param>
        /// <returns></returns>
        public UInt32 Write(UInt32 addr, Byte[] buffer, UInt32 blocksize = 1024)
        {
            var size = (UInt32)buffer.Length;
            WriteLog("Write 0x{0:X8} 0x{1:X8}({2:n0}k)", addr, size, size / 1024);

            var ms = new MemoryStream(buffer);
            //var buf = new Byte[blocksize];

            // 头部对齐
            var bs = addr % blocksize;
            if (bs > 0)
            {
                bs = blocksize - bs;
                if (bs > size) bs = size;
                WriteLog("WriteBlock 0x{0:X8} 0x{1:X8}({1:n0})", addr, bs);
                var rs = WriteMem(addr, bs, ms.ReadBytes(bs));
                //if (rs > 0)
                {
                    addr += bs;
                    size -= bs;
                }
            }
            // 循环读取
            while (size > 0)
            {
                bs = blocksize;
                if (bs > size) bs = size;
                WriteLog("WriteBlock 0x{0:X8} 0x{1:X8}({1:n0})", addr, bs);
                var rs = WriteMem(addr, bs, ms.ReadBytes(bs));
                //if (rs == 0) break;

                addr += bs;
                size -= bs;
            }

            return size;
        }

        /// <summary>写入</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_WriteU8", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 WriteU8(UInt32 memaddr, Byte data);

        /// <summary>写入</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_WriteU16", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 WriteU16(UInt32 memaddr, UInt16 data);

        /// <summary>写入</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_WriteU32", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 WriteU32(UInt32 memaddr, UInt32 data);
        #endregion

        #region 读写寄存器
        /// <summary>读取寄存器</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ReadReg", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 ReadReg(Int32 RegIndex);

        /// <summary>写入寄存器</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_WriteReg", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 WriteReg(Int32 RegIndex, Int32 RegValue);
        #endregion

        #region 执行命令
        /// <summary>执行命令</summary>
        /// <returns></returns>
        [DllImport("JLinkARM.dll", EntryPoint = "JLINKARM_ExecCommand", CallingConvention = CallingConvention.Cdecl)]
        public extern static Int32 ExecCommand(String pbCommand, Int32 param1 = 0, Int32 param2 = 0);
        #endregion

        #region 辅助
        /// <summary>初始化RTL8710的SpiFlash</summary>
        public void RTL8710SpiInit()
        {
            // SpiFlash初始化
            JLink.WriteU32(0x40000230, 0x0000d3c4); // 打开SpiFlash时钟(0x300)
            JLink.WriteU32(0x40000210, 0x00200113); // 打开SpiFlash外设(0x10)
            JLink.WriteU32(0x400002C0, 0x00110001); // 选择SpiFlash输出引脚(0x01)

            // 初始化Spi
            JLink.WriteU32(0x40006008, 0);  // 禁用SpiFlash操作
            JLink.WriteU32(0x4000602C, 0);  // 禁用所有中断
            JLink.WriteU32(0x40006010, 1);  // 使用第一从选择引脚
            JLink.WriteU32(0x40006014, 2);  // 默认波特率
            JLink.WriteU32(0x40006018, 0);  // TX FIFO
            JLink.WriteU32(0x4000601C, 0);  // RX FIFO
            JLink.WriteU32(0x4000604C, 0);  // 禁用DMA

            // 系统时钟，0x11=166MHz，0x21=83MHz
            JLink.WriteU32(0x40000014, 0x01);

            Thread.Sleep(10);
        }

        /// <summary>系统初始化</summary>
        public void RTL8710SystemInit()
        {
            WriteU32(0x40000304, 0x1FC00002);
            WriteU32(0x40000250, 0x400);
            WriteU32(0x40000340, 0x0);
            WriteU32(0x40000230, 0xdcc4);
            WriteU32(0x40000210, 0x11117);
            WriteU32(0x40000210, 0x11157);
            WriteU32(0x400002c0, 0x110011);
            WriteU32(0x40000320, 0xffffffff);
        }

        /// <summary>设置系统时钟</summary>
        /// <param name="clock"></param>
        public void RTL8710SetClock(Int32 clock = 83)
        {
            // 系统时钟，0x11=166MHz，0x21=83MHz
            switch (clock)
            {
                case 83:
                    WriteU32(0x40000014, 0x21);
                    break;
                case 166:
                    WriteU32(0x40000014, 0x11);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private static String FindDLL()
        {
            // 本地检查
            var p = "JLinkARM.dll".GetFullPath();
            if (File.Exists(p)) return p;

            // 从注册表查找
            var root = Registry.LocalMachine;
            var reg = root.OpenSubKey(@"SOFTWARE\SEGGER\J-Link");
            if (reg == null) reg = root.OpenSubKey(@"SOFTWARE\Wow6432Node\SEGGER\J-Link");
            if (reg != null)
            {
                p = reg.GetValue("InstallPath") + "";
                if (!p.IsNullOrEmpty())
                {
                    p = p.CombinePath("JLinkARM.dll");
                    if (File.Exists(p)) return p;
                }
            }

            // 从默认安装目录查找
            p = Environment.SystemDirectory.CombinePath(@"..\..\Program Files\SEGGER\JLink");
            p = p.CombinePath("JLinkARM.dll");
            if (File.Exists(p)) return p;

            p = Environment.SystemDirectory.CombinePath(@"..\..\Program Files (x86)\SEGGER\JLink");
            p = p.CombinePath("JLinkARM.dll");
            if (File.Exists(p)) return p;

            return null;
        }
        #endregion

        #region 日志
        /// <summary>日志</summary>
        public ILog Log { get; set; } = Logger.Null;

        private void WriteLog(String format, params Object[] args)
        {
            Log?.Info(format, args);
        }
        #endregion
    }
}