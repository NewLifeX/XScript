using System;
using System.IO;
using NewLife.Log;
using NewLife.Reflection;
using System.Reflection;

namespace NewLife.XScript
{
    class Program
    {
        private static ScriptConfig _Config;
        /// <summary>脚本配置</summary>
        public static ScriptConfig Config { get { return _Config; } set { _Config = value; } }

        static void Main(string[] args)
        {
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
            Console.WriteLine("©");

            #region 版权信息
            if (!Config.NoLogo)
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
                Console.WriteLine("XGZip采用GZip压缩算法，所压缩的单文件使用WinRar等工具可以直接解压，所压缩的多文件因为采用特别的格式，必须由XGZip或者调用核心库NewLife.Core.dll的IOHelper来解压，WinRar解压只会得到一个文件包。");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("温馨提醒：{0}会自己添加到资源管理器右键菜单中的\"发送到\"中，便于使用！", asmx.Name);
                Console.WriteLine();

                Console.ForegroundColor = oldcolor;
            }
            #endregion

            SetSendTo();

            try
            {
                if (args == null || args.Length == 0 || args[0] == "?" || args[0] == "/?")
                {
                    // TODO: 显示帮助菜单
                    Console.WriteLine("显示帮助信息");
                    Console.ReadKey();
                }
                else
                {
                    var file = Config.File;
#if DEBUG
                    Console.WriteLine("执行脚本：{0}", file);
#endif

                    var code = File.ReadAllText(file);
                    var sc = ScriptEngine.Create(code, false);
                    sc.Invoke();
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

#if DEBUG
            Console.ReadKey();
#endif
        }

        static void SetSendTo()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            if (!Directory.Exists(dir)) return;

            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());

            var file = Path.Combine(dir, asmx.Title + ".lnk");

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