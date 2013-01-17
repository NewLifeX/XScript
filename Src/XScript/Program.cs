using System;
using System.IO;
using System.Reflection;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Exceptions;

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

            if (Config.Debug) XTrace.UseConsole();

            XTrace.TempPath = "XTemp";

            if (args == null || args.Length == 0 || args[0] == "?" || args[0] == "/?")
            {
                // 如果前面没有输出版权信息，这里输出
                ShowCopyright();

                // 显示帮助菜单
                ShowHelp();
                Console.ReadKey();
            }
            else
            {
                if (!Config.NoLogo) ShowCopyright();

                try
                {
                    var file = Config.File;
                    if (!File.Exists(file)) throw new FileNotFoundException(String.Format("文件{0}不存在！", file), file);

                    if (Config.Debug) Console.WriteLine("执行脚本：{0}", file);

                    var code = File.ReadAllText(file);
                    var sc = ScriptEngine.Create(code, false);
                    sc.Invoke();
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }

#if DEBUG
                Console.ReadKey();
#endif
            }

            // 发送到菜单
            SetSendTo();
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
            //Console.ForegroundColor = ConsoleColor.White;
            //Console.WriteLine("");
            //Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("温馨提醒：{0}会自己添加到资源管理器右键菜单中的“发送到”中，便于使用！", asmx.Name);
            Console.WriteLine();

            Console.ForegroundColor = oldcolor;
        }

        static void ShowHelp()
        {
            Console.WriteLine("使用方法：");
            Console.WriteLine("XScritp.exe [源文件] [/NoLogo] [/Debug]");
            Console.WriteLine("\t{0,-16}\t不显示版权信息", "/NoLogo");
            Console.WriteLine("\t{0,-16}\t调试模式", "/Debug (/D)");
            Console.WriteLine();

            Console.WriteLine("脚本格式：");
            Console.WriteLine("一、简易模式");
            Console.WriteLine("\t直接书写脚本代码行，不得使用函数。");
            Console.WriteLine("\t如：Console.WriteLine(\"Hello NewLife!\");");
            Console.WriteLine("二、完整模式");
            Console.WriteLine("\t代码必须写在方法之中，主函数必须是static void Main()");
            Console.WriteLine("\t自动添加命名空间和类名");
            Console.WriteLine("\t如：");
            Console.WriteLine("\tstatic void Main() {");
            Console.WriteLine("\t\tTest();");
            Console.WriteLine("\t}");
            Console.WriteLine();
            Console.WriteLine("\tstatic void Test() {");
            Console.WriteLine("\t\tConsole.WriteLine(\"Hello NewLife!\");");
            Console.WriteLine("\t}");
        }

        static void SetSendTo()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            if (!Directory.Exists(dir)) return;

            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());

            var file = Path.Combine(dir, asmx.Title + ".lnk");
            if (File.Exists(file)) return;

            XTrace.WriteLine("添加快捷方式到“发送到”菜单！");

            try
            {
                var sc = new Shortcut();
                sc.Path = Assembly.GetEntryAssembly().Location;
                //sc.Arguments = "启动参数";
                sc.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                sc.Description = asmx.Description;
                sc.Save(file);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine(ex.ToString());
            }
        }
    }
}