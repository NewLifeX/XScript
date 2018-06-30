using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.Net;
using NewLife.Reflection;

namespace NewLife.XScript
{
    class Program
    {
        /// <summary>脚本配置</summary>
        public static ScriptConfig Config { get; set; }

        /// <summary>标题</summary>
        public static String Title { get; set; }

        /// <summary>是否处理脚本文件</summary>
        private static Boolean _CodeFile;

        static void Main(String[] args)
        {
            var cfg = Config;
            // 分解参数
            try
            {
                cfg = Config = ScriptConfig.Parse(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.Read();
                return;
            }
            Script.Config = cfg;
            Host.Config = cfg;

            // 隐藏当前窗口
            if (cfg.Hide)
            {
                var ip = Process.GetCurrentProcess().MainWindowHandle;
                if (ip != IntPtr.Zero) ShowWindow(ip, 0);
            }

            // 修改标题
            Title = AssemblyX.Create(Assembly.GetExecutingAssembly()).Title;
            Console.Title = Title;

            if (cfg.Debug) XTrace.Debug = true;
            XTrace.UseConsole();

            _CodeFile = true;
            if (args == null || args.Length == 0 || args[0] == "?" || args[0] == "/?") _CodeFile = false;

            // 是否卸载流程
            if (args != null && "-uninstall".EqualIgnoreCase(args))
            {
                RegHelper.Uninstall(true);
                return;
            }

            // 是否安装流程
            var install = args != null && "-install".EqualIgnoreCase(args);

#if !DEBUG
            // 检查并写入注册表
            if (RegHelper.CheckVersion(install))
            {
                // 发送到菜单
                Task.Run(() => RegHelper.SetSendTo());
                if (IsAdministrator()) Task.Run(() => RegHelper.SetFileType());
                Task.Run(() => RegHelper.SetPath());

                if (install) Thread.Sleep(3000);
            }
            if (install) return;
#endif

            Task.Run(() => AutoUpdate());

            //if (cfg.Debug) Task.Run(() => Build.Builder.All);

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
                if (!cfg.NoLogo) ShowCopyright();

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
                else if (line.EqualIgnoreCase("-uninstall") || line.EqualIgnoreCase("-u"))
                {
                    RegHelper.Uninstall(false);
                    break;
                }
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
            var cfg = Config;
            // 加上源文件路径
            Console.Title = Title + " " + cfg.File;

            ScriptEngine se = null;
            while (true)
            {
                try
                {
                    if (se == null)
                    {
                        var file = cfg.File;
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
                    // 获取内部异常
                    if (ex is TargetInvocationException) ex = (ex as TargetInvocationException).InnerException;

                    XTrace.WriteException(ex);
                    //if (!Config.Debug) Console.WriteLine(ex.ToString());
                }
                finally
                {
                    // 处理文件已完成，自动更新任务下载完成后可马上执行更新
                    _CodeFile = false;
                }

                // 此时执行自动更新
                var up = _upgrade;
                if (up != null)
                {
                    _upgrade = null;
                    up.Update();
                }

                // 暂停，等待客户查看输出
                if (cfg.NoStop) return;

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

                // 是否卸载流程
                if (key.KeyChar == 'u')
                {
                    RegHelper.Uninstall(false);
                    return;
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
            Console.Write("v{0}\t", asmx.FileVersion);
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
                    var fi = typeof(ConsoleColor).GetFieldEx(name);
                    if (fi != null) Console.ForegroundColor = (ConsoleColor)typeof(ConsoleColor).GetValue(fi);
                }
                else if (item == "[Pause]")
                {
                    Console.ReadKey(true);
                }
                else
                    Console.WriteLine(item);
            }
        }

        static Upgrade _upgrade;
        static void AutoUpdate()
        {
            var set = Setting.Current;
            if (set.LastCheck.AddDays(set.UpdateDays) > DateTime.Now) return;

            set.LastCheck = DateTime.Now;
            set.Save();

            // 注意路径，避免写入到脚本文件所在路径
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var up = new Upgrade();
            if (Config.Debug) up.Log = XTrace.Log;
            up.Name = "XScript";
            //up.Server = "https://git.oschina.net/NewLifeX/XScript";
            //up.Server = "http://www.newlifex.com/showtopic-369.aspx";
            //up.Server = "http://x.newlifex.com";
            up.UpdatePath = root.CombinePath(up.UpdatePath);
            if (up.Check())
            {
                up.Log = XTrace.Log;

                // 从github.com下载需要处理Url
                if (up.Links.Length > 0)
                {
                    var url = up.Links[0].Url;
                    if ((url.Contains("github.com") || url.Contains("git."))
                        && url.Contains("/blob/")) up.Links[0].Url = url.Replace("/blob/", "/raw/");
                }

                up.Download();
                if (!_CodeFile)
                    up.Update();
                else
                    // 留到脚本执行完成以后自动更新
                    _upgrade = up;
            }
        }

        public static Boolean IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        /// <summary>  设置窗体的显示与隐藏  </summary>  
        /// <param name="hWnd"></param>  
        /// <param name="nCmdShow"></param>  
        /// <returns></returns>  
        [DllImport("user32.dll", SetLastError = true)]
        private static extern Boolean ShowWindow(IntPtr hWnd, UInt32 nCmdShow);
    }
}