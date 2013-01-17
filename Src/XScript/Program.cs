using System;
using System.IO;
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

            #region 版权信息
            if (!Config.NoLogo)
            {
                var oldcolor = Console.ForegroundColor;
                Console.WriteLine("");
            }
            #endregion

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
    }
}