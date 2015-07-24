using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;
using NewLife.Log;
using NewLife.Net;
using NewLife.Reflection;

namespace NewLife.XScript
{
    class Program
    {
        private static ScriptConfig _Config;
        /// <summary>脚本配置</summary>
        public static ScriptConfig Config { get { return _Config; } set { _Config = value; } }

        private static String _Title;
        /// <summary>标题</summary>
        public static String Title { get { return _Title; } set { _Title = value; } }

        /// <summary>是否处理脚本文件</summary>
        private static Boolean _CodeFile;

        static void Main(string[] args)
        {
            // 分解参数
            try
            {
                Config = ScriptConfig.Parse(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
                return;
            }
            Script.Config = Config;

            Title = AssemblyX.Create(Assembly.GetExecutingAssembly()).Title;
            Console.Title = Title;

            if (Config.Debug) XTrace.Debug = true;
            XTrace.UseConsole();

            _CodeFile = true;
            if (args == null || args.Length == 0 || args[0] == "?" || args[0] == "/?") _CodeFile = false;

            // 发送到菜单
            ThreadPool.QueueUserWorkItem(s => SetSendTo());
            ThreadPool.QueueUserWorkItem(s => SetFileType());
            ThreadPool.QueueUserWorkItem(s => SetPath());
            ThreadPool.QueueUserWorkItem(s => AutoUpdate());

            if (!_CodeFile)
            {
                // 输出版权信息
                ShowCopyright();

                // 显示帮助菜单
                ShowHelp();

                ProcessUser();
            }
            else
            {
                if (!Config.NoLogo) ShowCopyright();

                ProcessFile();
            }
        }

        /// <summary>处理用户脚本</summary>
        static void ProcessUser()
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("脚本：");
                Console.ForegroundColor = ConsoleColor.Gray;
                var line = Console.ReadLine();
                if (line.IsNullOrWhiteSpace()) continue;

                line = line.Trim();
                if (line == "?" || line.EqualIgnoreCase("help"))
                    ShowDetail();
                else if (line.EqualIgnoreCase("exit", "quit", "bye"))
                    break;
                else
                {
                    Console.Title = Title + " " + line;

                    try
                    {
                        // 判断是不是脚本
                        if (File.Exists(line))
                            Script.ProcessFile(line);
                        else
                            Script.ProcessCode(line);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        /// <summary>处理脚本文件</summary>
        static void ProcessFile()
        {
            // 加上源文件路径
            Console.Title = Title + " " + Config.File;

            ScriptEngine se = null;
            while (true)
            {
                try
                {
                    if (se == null)
                    {
                        var file = Config.File;
                        if (!File.Exists(file)) throw new FileNotFoundException(String.Format("文件{0}不存在！", file), file);

                        //if (Config.Debug) Console.WriteLine("脚本：{0}", file);

                        // 增加源文件路径，便于调试纠错
                        if (!Path.IsPathRooted(file)) file = Path.Combine(Environment.CurrentDirectory, file);
                        file = file.GetFullPath();

                        se = Script.ProcessFile(file);
                        if (se == null) return;
                    }
                    else
                    {
                        // 多次执行
                        Script.Run(se);
                    }
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                    if (!Config.Debug) Console.WriteLine(ex.ToString());
                }

                // 此时执行自动更新
                var up = _upgrade;
                if (up != null)
                {
                    _upgrade = null;
                    up.Update();
                }

                // 暂停，等待客户查看输出
                if (Config.NoStop) return;

                //Console.WriteLine("任意键退出……");
                var key = Console.ReadKey(true);
                // 如果按下m键，重新打开菜单
                if (key.KeyChar == 'm')
                {
                    //Main(new String[0]);
                    // 输出版权信息
                    ShowCopyright();

                    // 显示帮助菜单
                    ShowHelp();

                    ProcessUser();

                    // 处理用户输入本来就是一个循环，里面退出以后，这里也应该跟着退出
                    return;
                }

                // 再次执行
                if (key.KeyChar == 'c')
                {
                    continue;
                }

                break;
            }
        }

        static void ShowCopyright()
        {
            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());

            var oldcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(asmx.Title);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("由新生命开发团队开发，{0}！", asmx.Description);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("版权所有：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(asmx.Asm.GetCustomAttributeValue<AssemblyCopyrightAttribute, String>());
            //Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("程序版本：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("v{0}\t", asmx.Version);
            //Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("编译时间：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("{0:yyyy-MM-dd HH:mm:ss}", asmx.Compile);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(".Net版本：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("v{0}\t", Environment.Version);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("安装路径：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            Console.WriteLine();

            Console.ForegroundColor = oldcolor;
        }

        static void ShowHelp() { ShowText("帮助.txt"); }

        static void ShowDetail() { ShowText("详细.txt"); }

        static void ShowText(String txtName)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Program).Namespace + "." + txtName);
            var txt = stream.ToStr();
            foreach (var item in txt.Split(new String[] { Environment.NewLine }, StringSplitOptions.None))
            {
                // 改变颜色
                if (item.StartsWith("[Color:"))
                {
                    var name = item.Substring("[Color:".Length);
                    name = name.TrimEnd(']');
                    var fix = FieldInfoX.Create(typeof(ConsoleColor), name);
                    if (fix != null) Console.ForegroundColor = (ConsoleColor)fix.GetValue();
                }
                else if (item == "[Pause]")
                {
                    Console.ReadKey(true);
                }
                else
                    Console.WriteLine(item);
            }
        }

        static void SetSendTo()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            if (!Directory.Exists(dir)) return;

            // 每次更新，用于覆盖，避免错误
            //if (File.Exists(file)) return;

            //XTrace.WriteLine("添加快捷方式到“发送到”菜单！");

            try
            {
                Shortcut.Create(null, null);
                Shortcut.Create("调试", "/D");
                Shortcut.Create("生成Exe", "/Exe");
                Shortcut.Create("用VisualStudio打开", "/Vs");
            }
            catch (Exception ex)
            {
                if (Config.Debug) XTrace.WriteException(ex);
            }
        }

        static void SetFileType()
        {
            try
            {
                var root = Registry.ClassesRoot;
                var asm = Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version;
                var name = asm.GetName().Name;

                // 修改.cs文件指向
                root.CreateSubKey(".cs").SetValue("", name);

                var reg = root.OpenSubKey(name);
                if (reg != null)
                {
                    var verStr = reg.GetValue("Version") + "";
                    if (!String.IsNullOrEmpty(verStr))
                    {
                        var verReg = new Version(verStr);
                        // 如果注册表记录的版本更新，则不写入
                        if (verReg >= ver) return;
                    }
                    reg.Close();
                }

                var ico = "";
                for (int i = 20; i >= 8; i--)
                {
                    reg = root.OpenSubKey(String.Format("VisualStudio.cs.{0}.0", i));
                    if (reg != null)
                    {
                        reg = reg.OpenSubKey("DefaultIcon");
                        if (reg != null) ico = reg.GetValue("") + "";
                        reg.Close();
                        if (!ico.IsNullOrWhiteSpace()) break;
                    }
                }

                using (var xs = root.CreateSubKey(name))
                {
                    xs.SetValue("", name + "脚本文件");
                    // 写入版本
                    xs.SetValue("Version", ver.ToString());
                    if (!ico.IsNullOrWhiteSpace()) xs.CreateSubKey("DefaultIcon").SetValue("", ico);

                    using (var shell = xs.CreateSubKey("shell"))
                    {
                        reg = shell.CreateSubKey("Vs");
                        reg.SetValue("", "用VisualStudio打开");
                        reg.Flush();
                        reg = reg.CreateSubKey("Command");
                        reg.SetValue("", String.Format("\"{0}\" \"%1\" /Vs", asm.Location));
                        reg.Close();

                        reg = shell.CreateSubKey("open");
                        reg.SetValue("", "执行脚本(&O)");
                        reg.Flush();
                        reg = reg.CreateSubKey("Command");
                        // 后面多带几个参数，支持"Test.cs /NoStop"这种用法，这种写法虽然很暴力，但是简单直接
                        reg.SetValue("", String.Format("\"{0}\" \"%1\" /NoLogo %2 %3 %4 %5", asm.Location));
                        reg.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.Debug) XTrace.WriteException(ex);
            }
        }

        /// <summary>设置安装路径到环境变量Path里面</summary>
        static void SetPath()
        {
            var epath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
            var ps = epath.Split(";").OrderBy(e => e).ToList();
            var asm = Assembly.GetExecutingAssembly();
            var mypath = Path.GetDirectoryName(asm.Location);
            var flag = false;
            foreach (var item in ps)
            {
                //Console.WriteLine(item);
                if (mypath.EqualIgnoreCase(item))
                {
                    flag = true;
                    break;
                }
            }
            if (!flag)
            {
                XTrace.WriteLine("设置安装目录到全局Path路径");
                ps.Add(mypath);
            }
            ps = ps.OrderBy(e => e).ToList();
            var epath2 = String.Join(";", ps.ToArray());
            epath2 = Environment.ExpandEnvironmentVariables(epath2);
            if (!epath.EqualIgnoreCase(epath2))
                Environment.SetEnvironmentVariable("Path", epath2, EnvironmentVariableTarget.Machine);
        }

        static Upgrade _upgrade;
        static void AutoUpdate()
        {
            //// 稍微等待一下，等主程序执行完成
            //Thread.Sleep(2000);

            // 文件保存配置信息
            var file = "Update.config";
            // 注意路径，避免写入到脚本文件所在路径
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            file = root.CombinePath(file);
            if (File.Exists(file))
            {
                var last = File.ReadAllText(file).ToDateTime();
                // 每天只更新一次
                if (last >= DateTime.Now.Date) return;
            }
            File.WriteAllText(file, DateTime.Now.ToFullString());

            var up = new Upgrade();
            if (Config.Debug) up.Log = XTrace.Log;
            up.Name = "XScript";
            up.Server = "http://www.newlifex.com/showtopic-369.aspx";
            up.UpdatePath = root.CombinePath(up.UpdatePath);
            if (up.Check())
            {
                up.Download();
                if (!_CodeFile)
                    up.Update();
                else
                    // 留到脚本执行完成以后自动更新
                    _upgrade = up;
            }
        }
    }
}