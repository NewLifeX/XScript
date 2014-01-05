using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;
using NewLife.Log;
using NewLife.Reflection;

namespace NewLife.XScript
{
    class Program
    {
        private static ScriptConfig _Config;
        /// <summary>脚本配置</summary>
        public static ScriptConfig Config { get { return _Config; } set { _Config = value; } }

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

            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());
            Console.Title = asmx.Title;

            if (Config.Debug) XTrace.UseConsole();

            //XTrace.TempPath = "XTemp";

            // 发送到菜单
            ThreadPool.QueueUserWorkItem(s => SetSendTo());
            ThreadPool.QueueUserWorkItem(s => SetFileType(true));

            if (args == null || args.Length == 0 || args[0] == "?" || args[0] == "/?")
            {
                // 输出版权信息
                ShowCopyright();

                // 显示帮助菜单
                ShowHelp();

                Console.ReadKey(true);
            }
            else
            {
                // 加上源文件路径
                Console.Title += " " + Config.File;

                if (!Config.NoLogo) ShowCopyright();

                try
                {
                    var file = Config.File;
                    if (!File.Exists(file)) throw new FileNotFoundException(String.Format("文件{0}不存在！", file), file);

                    if (Config.Debug) Console.WriteLine("脚本：{0}", file);

                    // 增加源文件路径，便于调试纠错
                    if (!Path.IsPathRooted(file)) file = Path.Combine(Environment.CurrentDirectory, file);
                    file = file.GetFullPath();

                    Script.Process(file, Config);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                    if (!Config.Debug) Console.WriteLine(ex.ToString());
                }

                // 暂停，等待客户查看输出
                if (!Config.NoStop)
                {
                    Console.WriteLine("任意键退出……");
                    Console.ReadKey();
                }
            }
        }

        static void ShowCopyright()
        {
            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());

            var oldcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("欢迎使用{0}！", asmx.Title);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("本工具由新生命开发团队开发，{0}！", asmx.Description);
            Console.WriteLine("版权所有：{0}", asmx.Asm.GetCustomAttributeValue<AssemblyCopyrightAttribute, String>());
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(".Net版本：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("v{0}", Environment.Version);
            //Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("编译时间：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("{0:yyyy-MM-dd HH:mm:ss}", asmx.Compile);
            Console.WriteLine();

            Console.ForegroundColor = oldcolor;
        }

        static void ShowHelp()
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Program).Namespace + ".帮助.txt");
            var txt = stream.ToStr();
            //Console.WriteLine(txt);
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
                XTrace.WriteException(ex);
            }
        }

        static void SetFileType(Boolean force = false)
        {
            var root = Registry.ClassesRoot;
            var asm = Assembly.GetCallingAssembly();
            var name = asm.GetName().Name;

            // 修改.cs文件指向
            root.CreateSubKey(".cs").SetValue("", name);

            var reg = root.OpenSubKey(name);
            if (!force && reg != null) return;

            var xs = root.CreateSubKey(name);
            xs.SetValue("", name + "脚本文件");
            var shell = root.CreateSubKey("shell");

            reg = shell.CreateSubKey("Vs");
            reg.SetValue("", "用VisualStudio打开");
            reg = reg.CreateSubKey("Command");
            reg.SetValue("", String.Format("\"{0}\" \"%1\" /Vs", asm.Location));

            reg = shell.CreateSubKey("open");
            reg.SetValue("", "执行脚本(&O)");
            reg = reg.CreateSubKey("Command");
            reg.SetValue("", String.Format("\"{0}\" \"%1\"", asm.Location));
        }
    }
}