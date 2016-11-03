using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using NewLife.Log;

namespace NewLife.Build
{
    /// <summary>ICC环境</summary>
    public class ICC : Builder
    {
        #region 属性
        #endregion

        #region 初始化
        private static ICCLocation location = new ICCLocation();

        /// <summary>初始化</summary>
        public ICC()
        {
            Name = "ICC";

            Version = location.Version;
            ToolPath = location.ToolPath;
        }
        #endregion

        /// <summary>初始化</summary>
        /// <param name="addlib"></param>
        /// <returns></returns>
        public override Boolean Init(Boolean addlib)
        {
            var basePath = ToolPath.CombinePath("arm\\bin").GetFullPath();

            Complier = basePath.CombinePath("iccarm.exe").GetFullPath();
            Asm = basePath.CombinePath("iasmarm.exe");
            Link = basePath.CombinePath("ilinkarm.exe");
            Ar = basePath.CombinePath("iarchive.exe");
            ObjCopy = basePath.CombinePath("ielftool.exe");

            IncPath = basePath.CombinePath(@"arm\include").GetFullPath();
            LibPath = basePath.CombinePath(@"arm\lib").GetFullPath();

            return base.Init(addlib);
        }

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        public override String GetCompileCommand(Boolean cpp)
        {
            // --debug --endian=little --cpu=Cortex-M3 --enum_is_int -e --char_is_signed --fpu=None 
            // -Ohz --use_c++_inline 
            // --dlib_config C:\Program Files (x86)\IAR Systems\Embedded Workbench 7.0\arm\INC\c\DLib_Config_Normal.h 
            var sb = new StringBuilder();
            if (cpp)
                //sb.Append("--c++ --no_exceptions");
                sb.Append("--eec++");
            else
                sb.Append("--use_c++_inline");
            // -e打开C++扩展
            sb.AppendFormat(" --endian=little --cpu={0} -e --silent", CPU);
            if (Cortex >= 4) sb.Append(" --fpu=None");
            //sb.Append(" --enable_multibytes");
            if (Debug) sb.Append(" --debug");
            // 默认低级优化，发行版-Ohz为代码大小优化，-Ohs为高速度优化
            if (!Debug) sb.Append(" -Ohz");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" -D {0}", item);
            }
            if (Tiny) sb.Append(" -D TINY");
            //var basePath = Complier.CombinePath(@"..\..\..\").GetFullPath();
            //sb.AppendFormat(" --dlib_config \"{0}\\arm\\INC\\c\\DLib_Config_Normal.h\"", basePath);

            if (!ExtCompiles.IsNullOrEmpty()) sb.AppendFormat(" {0}", ExtCompiles.Trim());

            return sb.ToString();
        }

        /// <summary>汇编程序</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected override String OnAssemble(String file)
        {
            /*
			* -s+ -M<> -w+ -r --cpu Cortex-M3 --fpu None
			* -s+	标记符大小写敏感
			* -r	调试输出
			*/
            var sb = new StringBuilder();
            sb.Append("-s+ -M<> -w+ -S");
            sb.AppendFormat(" --cpu {0}", CPU);
            if (Cortex >= 4) sb.Append(" --fpu=None");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" -D{0}", item);
            }
            if (Debug) sb.Append(" -r");
            if (Tiny) sb.Append(" -DTINY");

            return sb.ToString();
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected override String OnBuildLib(String lib)
        {
            var sb = new StringBuilder();
            sb.AppendFormat(" --create \"{0}\"", lib);

            return sb.ToString();
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected override String OnBuild(String name)
        {
            /*
             * -o \Exe\application.axf 
             * --redirect _Printf=_PrintfTiny 
             * --redirect _Scanf=_ScanfSmallNoMb 
             * --keep bootloader 
             * --keep gImage2EntryFun0 
             * --keep RAM_IMG2_VALID_PATTEN 
             * --image_input=\ram_1.r.bin,bootloader,LOADER,4
             * --map \List\application.map 
             * --log veneers 
             * --log_file \List\application.log 
             * --config \image2.icf 
             * --diag_suppress Lt009,Lp005,Lp006 
             * --entry Reset_Handler --no_exceptions --no_vfe
			 */
            var lstName = GetListPath(name);
            var objName = GetObjPath(name);

            var sb = new StringBuilder();
            //sb.AppendFormat("--cpu {0} --library_type=microlib --strict", CPU);
            var icf = Scatter;
            if (icf.IsNullOrEmpty()) icf = ".".AsDirectory().GetAllFiles("*.icf", false).FirstOrDefault()?.FullName;
            if (!icf.IsNullOrEmpty() && File.Exists(icf.GetFullPath()))
                sb.AppendFormat(" --config \"{0}\"", icf);
            //else
            //    sb.AppendFormat(" --ro-base 0x08000000 --rw-base 0x20000000 --first __Vectors");
            sb.Append(" --entry Reset_Handler --no_exceptions --no_vfe");

            if (!ExtBuilds.IsNullOrEmpty()) sb.AppendFormat(" {0}", ExtBuilds.Trim());

            var axf = objName.EnsureEnd(".axf");
            sb.AppendFormat(" --list \"{0}.map\" -o \"{1}\"", lstName, axf);

            return sb.ToString();
        }

        /// <summary>导出目标文件</summary>
        /// <param name="axf"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        protected override Boolean Dump(String axf, String target)
        {
            XTrace.WriteLine("生成：{0}", target);
            Console.WriteLine("");

            var cmd = "";
            if (target.EndsWithIgnoreCase(".bin"))
                cmd = "--bin  \"{0}\" \"{1}\"".F(axf, target);
            else
                cmd = "--ihex \"{0}\" \"{1}\"".F(axf, target);

            var rs = ObjCopy.Run(cmd, 3000, WriteLog);

            return rs != 0;
        }
        #endregion
    }

    class ICCLocation
    {
        #region 属性
        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        public ICCLocation()
        {
            #region 从注册表获取目录和版本
            if (String.IsNullOrEmpty(ToolPath))
            {
                var reg = Registry.LocalMachine.OpenSubKey(@"Software\IAR Systems\Embedded Workbench\5.0");
                if (reg == null) reg = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\IAR Systems\Embedded Workbench\5.0");
                if (reg != null)
                {
                    ToolPath = reg.GetValue("LastInstallPath") + "";
                    if (ToolPath.Contains(".")) Version = ToolPath.AsDirectory().Name.Split(" ").Last();

                    WriteLog("注册表 {0} {1}", ToolPath, Version);
                }
            }
            #endregion

            #region 扫描所有根目录，获取MDK安装目录
            foreach (var item in DriveInfo.GetDrives())
            {
                if (!item.IsReady) continue;

                //var p = Path.Combine(item.RootDirectory.FullName, @"Program Files (x86)\IAR Systems\Embedded Workbench 7.0");
                var p = Path.Combine(item.RootDirectory.FullName, @"Program Files\IAR Systems");
                if (!Directory.Exists(p)) p = Path.Combine(item.RootDirectory.FullName, @"Program Files (x86)\IAR Systems");
                if (Directory.Exists(p))
                {
                    var di = p.AsDirectory().GetDirectories().LastOrDefault();
                    if (di != null && di.Exists)
                    {
                        var f = di.FullName.CombinePath("arm\\bin\\iccarm.exe").AsFile();
                        if (f != null && f.Exists)
                        {
                            p = f.Directory.FullName.CombinePath(@"..\..\").GetFullPath();
                            var ver = GetVer(p);
                            if (ver.CompareTo(Version) > 0)
                            {
                                ToolPath = p;
                                Version = ver;

                                WriteLog("本地 {0} {1}", ToolPath, Version);
                            }
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取ICC安装目录！");
            #endregion
        }

        String GetVer(String path)
        {
            if (!path.Contains(".")) return "";

            return path.AsDirectory().Name.Split(" ").LastOrDefault();
        }

        void WriteLog(String format, params Object[] args)
        {
            if (XTrace.Debug) XTrace.WriteLine(format, args);
        }
    }
}
