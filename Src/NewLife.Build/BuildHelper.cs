using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace NewLife.Build
{
    /// <summary>编译助手</summary>
    public static class BuildHelper
    {
        static Int32 Main2(String[] args)
        {
            // 双击启动有两个参数，第一个是脚本自身，第二个是/NoLogo
            if (args == null || args.Length <= 2)
            {
                SetProjectAfterMake();
            }
            else
            {
                var axf = GetAxf(args);
                if (!String.IsNullOrEmpty(axf))
                {
                    // 修改编译信息
                    if (WriteBuildInfo(axf)) MakeBin(axf);
                }
            }

            // 清理垃圾文件
            Clear();

            // 更新脚本自己
            //UpdateSelf();

            // 编译SmartOS
            /*var path = ".".GetFullPath().ToUpper();
            if(path.Contains("STM32F0"))
                "XScript".Run("..\\..\\SmartOS\\Tool\\Build_SmartOS_F0.cs /NoLogo /NoStop");
            else if(path.Contains("STM32F1"))
                "XScript".Run("..\\SmartOS\\Tool\\Build_SmartOS_F1.cs /NoLogo /NoStop");
            else if(path.Contains("STM32F4"))
                "XScript".Run("..\\SmartOS\\Tool\\Build_SmartOS_F4.cs /NoLogo /NoStop");*/

            "完成".SpeakAsync();
            System.Threading.Thread.Sleep(250);

            return 0;
        }

        /// <summary>获取项目文件名</summary>
        /// <returns></returns>
        public static String GetProjectFile()
        {
            var fs = Directory.GetFiles(".".GetBasePath(), "*.uvprojx");
            if (fs.Length == 0) Directory.GetFiles(".".GetBasePath(), "*.uvproj");
            if (fs.Length == 0)
            {
                Console.WriteLine("找不到项目文件！");
                return null;
            }
            if (fs.Length > 1)
            {
                //Console.WriteLine("找到项目文件{0}个，无法定夺采用哪一个！", fs.Length);
                //return null;
                Console.WriteLine("找到项目文件{0}个，选择第一个{1}！", fs.Length, fs[0]);
            }

            return Path.GetFileName(fs[0]);
        }

        /// <summary>设置项目的编译后脚本</summary>
        public static void SetProjectAfterMake()
        {
            Console.WriteLine("设置项目的编译脚本");

            /*
             * 找到项目文件
             * 查找<AfterMake>，开始处理
             * 设置RunUserProg1为1
             * 设置UserProg1Name为XScript.exe Build.cs /NoLogo /NoTime /NoStop
             * 循环查找<AfterMake>，连续处理
             */

            var file = GetProjectFile();
            if (file.IsNullOrEmpty()) return;

            Console.WriteLine("加载项目：{0}", file);
            file = file.GetBasePath();

            var doc = new XmlDocument();
            doc.Load(file);

            var nodes = doc.DocumentElement.SelectNodes("//AfterMake");
            Console.WriteLine("发现{0}个编译目标", nodes.Count);
            var flag = false;
            foreach (XmlNode node in nodes)
            {
                var xn = node.SelectSingleNode("../../../TargetName");
                Console.WriteLine("编译目标：{0}", xn.InnerText);

                xn = node.SelectSingleNode("RunUserProg1");
                xn.InnerText = "1";
                xn = node.SelectSingleNode("UserProg1Name");

                var bat = "XScript.exe Build.cs /NoLogo /NoTime /NoStop /Hide";
                if (xn.InnerText != bat)
                {
                    xn.InnerText = bat;
                    flag = true;
                }
            }

            if (flag)
            {
                Console.WriteLine("保存修改！");
                //doc.Save(file);
                var set = new XmlWriterSettings();
                set.Indent = true;
                // Keil实在烂，XML文件头指明utf-8编码，却不能添加BOM头
                set.Encoding = new UTF8Encoding(false);
                using (var writer = XmlWriter.Create(file, set))
                {
                    doc.Save(writer);
                }
            }
        }

        /// <summary>查找axf文件</summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static String GetAxf(String[] args)
        {
            var axf = args.FirstOrDefault(e => e.EndsWithIgnoreCase(".axf"));
            if (!String.IsNullOrEmpty(axf)) return axf.GetBasePath();

            // 搜索所有axf文件，找到最新的那一个
            var fis = Directory.GetFiles(".", "*.axf", SearchOption.AllDirectories);
            if (fis != null && fis.Length > 0)
            {
                // 按照修改时间降序的第一个
                return fis.OrderByDescending(e => e.AsFile().LastWriteTime).First().GetBasePath();
            }

            Console.WriteLine("未能从参数中找到输出文件.axf，请在命令行中使用参数#L");
            return null;
        }

        /// <summary>写入编译信息</summary>
        /// <param name="axf"></param>
        /// <returns></returns>
        public static Boolean WriteBuildInfo(String axf)
        {
            // 修改编译时间
            var ft = "yyyy-MM-dd HH:mm:ss";
            var sys = axf.GetBasePath();
            if (!File.Exists(sys)) return false;

            var dt = ft.GetBytes();
            var company = "NewLife_Embedded_Team";
            //var company = "NewLife_Team";
            var name = String.Format("{0}_{1}", Environment.MachineName, Environment.UserName);
            if (name.GetBytes().Length > company.Length)
                name = name.Cut(company.Length);

            var rs = false;
            // 查找时间字符串，写入真实时间
            using (var fs = File.Open(sys, FileMode.Open, FileAccess.ReadWrite))
            {
                if (fs.IndexOf(dt) > 0)
                {
                    fs.Position -= dt.Length;
                    var now = DateTime.Now.ToString(ft);
                    Console.WriteLine("编译时间：{0}", now);
                    fs.Write(now.GetBytes());

                    rs = true;
                }
                fs.Position = 0;
                var ct = company.GetBytes();
                if (fs.IndexOf(ct) > 0)
                {
                    fs.Position -= ct.Length;
                    Console.WriteLine("编译机器：{0}", name);
                    fs.Write(name.GetBytes());
                    // 多写一个0以截断字符串
                    fs.Write((Byte)0);

                    rs = true;
                }
            }

            return rs;
        }

        /// <summary>获取Keil目录</summary>
        /// <returns></returns>
        public static String GetKeil()
        {
            var reg = Registry.LocalMachine.OpenSubKey("Software\\Keil\\Products\\MDK");
            if (reg == null) reg = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Keil\\Products\\MDK");
            if (reg == null) return null;

            return reg.GetValue("Path") + "";
        }

        /// <summary>生产Bin文件</summary>
        public static void MakeBin(String axf)
        {
            // 修改成功说明axf文件有更新，需要重新生成bin
            // 必须先找到Keil目录，否则没得玩
            var mdk = GetKeil();
            if (!String.IsNullOrEmpty(mdk) && Directory.Exists(mdk))
            {
                var fromelf = mdk.CombinePath("ARMCC\\bin\\fromelf.exe");
                //var bin = Path.GetFileNameWithoutExtension(axf) + ".bin";
                var prj = Path.GetFileNameWithoutExtension(GetProjectFile());
                if (Path.GetFileNameWithoutExtension(axf).EndsWithIgnoreCase("D"))
                    prj += "D";
                var bin = prj + ".bin";
                var bin2 = bin.GetBasePath();
                //Process.Start(fromelf, String.Format("--bin {0} -o {1}", axf, bin2));
                var p = new Process();
                p.StartInfo.FileName = fromelf;
                p.StartInfo.Arguments = String.Format("--bin {0} -o {1}", axf, bin2);
                //p.StartInfo.CreateNoWindow = false;
                p.StartInfo.UseShellExecute = false;
                p.Start();
                p.WaitForExit(5000);
                var len = bin2.AsFile().Length;
                Console.WriteLine("生成固件：{0} 共{1:n0}字节/{2:n2}KB", bin, len, (Double)len / 1024);
            }
        }

        /// <summary>清理无用文件</summary>
        public static void Clear()
        {
            // 清理bak
            // 清理dep
            // 清理 用户名后缀
            // 清理txt/ini

            Console.WriteLine();
            Console.WriteLine("清理无用文件");

            var ss = new String[] { "bak", "dep", "txt", "ini", "htm" };
            var list = new List<String>(ss);
            //list.Add(Environment.UserName);

            foreach (var item in list)
            {
                var fs = Directory.GetFiles(".".GetBasePath(), "*." + item);
                if (fs.Length > 0)
                {
                    foreach (var elm in fs)
                    {
                        Console.WriteLine("删除 {0}", elm);
                        try
                        {
                            File.Delete(elm);
                        }
                        catch { }
                    }
                }
            }
        }

        /// <summary>更新脚本自己</summary>
        public static void UpdateSelf()
        {
            var deep = 1;
            // 找到SmartOS目录，里面的脚本可用于覆盖自己
            var di = "../SmartOS".GetBasePath();
            if (!Directory.Exists(di)) { deep++; di = "../../SmartOS".GetBasePath(); }
            if (!Directory.Exists(di)) { deep++; di = "../../../SmartOS".GetBasePath(); }
            if (!Directory.Exists(di)) return;

            var fi = di.CombinePath("Tool/Build.cs");
            switch (deep)
            {
                case 2: fi = di.CombinePath("Tool/Build2.cs"); break;
                case 3: fi = di.CombinePath("Tool/Build3.cs"); break;
                default: break;
            }

            if (!File.Exists(fi)) return;

            var my = "Build.cs".GetBasePath();
            if (my.AsFile().LastWriteTime >= fi.AsFile().LastWriteTime) return;

            try
            {
                File.Copy(fi, my, true);
            }
            catch { }
        }

        /// <summary>从当前目录向上查找指定名称目录</summary>
        /// <param name="dir"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static String FindUp(this String dir, String name)
        {
            var di = dir.AsDirectory();
            while (di != null)
            {
                if (di.Name.EqualIgnoreCase(name)) return di.FullName;

                // 复杂目录拼接，常用于相对目录
                if (Directory.Exists(di.FullName.CombinePath(name))) return di.FullName;

                if (di == di.Parent) return null;
                di = di.Parent;
            }

            return null;
        }
    }
}