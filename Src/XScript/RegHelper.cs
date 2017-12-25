using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using NewLife.Log;
using NewLife.Reflection;

namespace NewLife.XScript
{
    static class RegHelper
    {
        /// <summary>检查当前版本是否更新</summary>
        /// <returns></returns>
        //[RegistryPermission(SecurityAction.PermitOnly, Read = @"HKEY_LOCAL_MACHINE\SOFTWARE\NewLife")]
        public static Boolean CheckVersion(Boolean isadmin)
        {
            var cur = AssemblyX.Entry.Version;
            var key = @"Software\NewLife\XScript";

            var root = Registry.LocalMachine;
            var reg = root.OpenSubKey(key);
            if (reg != null)
            {
                var v = reg.GetValue("Version") + "";
                reg.Close();
                if (v.CompareTo(cur) >= 0) return false;
            }

            XTrace.WriteLine("更新注册版本为 v{0}", cur);

            try
            {
                reg = root.CreateSubKey(key);

                reg.SetValue("Version", cur, RegistryValueKind.String);
                reg.SetValue("Path", AppDomain.CurrentDomain.BaseDirectory, RegistryValueKind.String);
                reg.Close();
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("失败！可能需要管理员权限运行！" + ex.Message);

                if (!isadmin)
                {
                    var pi = new ProcessStartInfo(Assembly.GetExecutingAssembly().CodeBase)
                    {
                        Arguments = "-install",

                        // 以管理员启动
                        UseShellExecute = true,
                        Verb = "runas",
                        //WindowStyle = ProcessWindowStyle.Hidden
                    };
                    //Task.Run(() => Process.Start(pi));
                    Process.Start(pi);
                }
            }
            root.Close();

            return true;
        }

        public static void SetFileType()
        {
            XTrace.WriteLine("设置.cs文件打开方式");

            try
            {
                var root = Registry.ClassesRoot;
                var asm = Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version;
                var name = asm.GetName().Name;

                // 修改.cs文件指向
                var reg = root.CreateSubKey(".cs");
                reg.SetValue("", name);
                reg.SetValue("Content Type", "text/plain");
                reg.SetValue("PerceivedType", "text");
                reg = reg.CreateSubKey("OpenWithProgids");
                reg.SetValue("XScript", "", RegistryValueKind.String);

                reg = root.CreateSubKey(".xs");
                reg.SetValue("", name);
                reg = reg.CreateSubKey("OpenWithProgids");
                reg.SetValue("XScript", "", RegistryValueKind.String);

                reg = root.OpenSubKey(name);
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
                //var vcs = root.GetSubKeyNames().LastOrDefault(e => e.StartsWithIgnoreCase("VisualStudio.cs."));
                //if (!vcs.IsNullOrEmpty())
                //{
                //    reg = root.OpenSubKey(vcs);
                //    if (reg != null)
                //    {
                //        reg = reg.OpenSubKey("DefaultIcon");
                //        if (reg != null) ico = reg.GetValue("") + "";
                //        reg.Close();
                //    }
                //}
                if (ico.IsNullOrEmpty()) ico = "\"{0}\",0".F(asm.Location);

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
            //catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        public static void SetSendTo()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            if (!Directory.Exists(dir)) return;

            // 每次更新，用于覆盖，避免错误
            //if (File.Exists(file)) return;

            XTrace.WriteLine("添加快捷方式到“发送到”菜单！");

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

        /// <summary>设置安装路径到环境变量Path里面</summary>
        public static void SetPath()
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
            {
                try
                {
                    Environment.SetEnvironmentVariable("Path", epath2, EnvironmentVariableTarget.Machine);
                }
                catch { }
            }
        }

        public static void Uninstall(Boolean isadmin)
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetName().Name;

            try
            {
                XTrace.WriteLine("删除“发送到”菜单");
                Shortcut.Delete(null);
                Shortcut.Delete("调试");
                Shortcut.Delete("生成Exe");
                Shortcut.Delete("用VisualStudio打开");

                var epath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
                var ps = epath.Split(";").OrderBy(e => e).ToList();
                var mypath = Path.GetDirectoryName(asm.Location);
                var ps2 = ps.Where(e => !e.EqualIgnoreCase(mypath)).ToList();
                if (ps2.Count < ps.Count)
                {
                    XTrace.WriteLine("删除环境变量Path [{0}]", mypath);
                    var epath2 = String.Join(";", ps2.ToArray());
                    epath2 = Environment.ExpandEnvironmentVariables(epath2);

                    try
                    {
                        Environment.SetEnvironmentVariable("Path", epath2, EnvironmentVariableTarget.Machine);
                    }
                    catch { }
                }

                var key = @"Software\NewLife\XScript";
                XTrace.WriteLine("删除 {0}", key);
                var root = Registry.LocalMachine;
                root.DeleteSubKey(key, false);
                root.Close();

                root = Registry.ClassesRoot;
                var vcs = root.GetSubKeyNames().LastOrDefault(e => e.StartsWithIgnoreCase("VisualStudio.cs."));

                XTrace.WriteLine("删除 .cs [XScript]");
                var reg = root.OpenSubKey(".cs", true);
                if (!vcs.IsNullOrEmpty())
                    reg.SetValue("", vcs);
                else
                    reg.SetValue("", "");
                reg = reg.OpenSubKey("OpenWithProgids", true);
                reg.DeleteValue("XScript", false);
                reg.Close();

                XTrace.WriteLine("删除 .xs");
                root.DeleteSubKeyTree(".xs", false);

                XTrace.WriteLine("删除 {0}", name);
                root.DeleteSubKeyTree(name, false);
                root.Close();

                XTrace.WriteLine("卸载完成！");
                //Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("卸载失败！可能需要管理员权限运行！" + ex);

                if (!isadmin)
                {
                    var pi = new ProcessStartInfo(asm.CodeBase)
                    {
                        Arguments = "-uninstall",

                        // 以管理员启动
                        UseShellExecute = true,
                        Verb = "runas",
#if !DEBUG
                        //WindowStyle = ProcessWindowStyle.Hidden
#endif
                    };
                    Task.Run(() => Process.Start(pi));
                }
                //Console.ReadKey(true);
            }

            Thread.Sleep(3000);
        }
    }
}