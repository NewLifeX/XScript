using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// 有关程序集的常规信息通过以下
// 特性集控制。更改这些特性值可修改
// 与程序集关联的信息。
[assembly: AssemblyProduct("XScript")]
[assembly: AssemblyTitle("新生命C#脚本引擎")]
[assembly: AssemblyDescription("用于编译执行C#文件脚本")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCulture("")]

// 将 ComVisible 设置为 false 使此程序集中的类型
// 对 COM 组件不可见。如果需要从 COM 访问此程序集中的类型，
// 则将该类型上的 ComVisible 特性设置为 true。
[assembly: ComVisible(false)]

// 如果此项目向 COM 公开，则下列 GUID 用于类型库的 ID
[assembly: Guid("3ef629c3-57a6-44fb-bf33-0edbb1d8bbb9")]

// 程序集的版本信息由下面四个值组成:
//
//      主版本
//      次版本 
//      内部版本号
//      修订号
//
// 可以指定所有这些值，也可以使用“内部版本号”和“修订号”的默认值，
// 方法是按如下所示使用“*”:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.7.*")]
[assembly: AssemblyFileVersion("1.7.2014.0704")]

/*
 * v1.7.2014.0704   输出XScript安装路径，提示C再次执行M回主界面
 * 
 * v1.6.2014.0329   支持按c键让脚本再次执行，跳过编译过程
 * 
 * v1.5.2014.0109   控制端可以直接输入用户代码或者脚本路径
 * 
 * v1.4.2014.0107   从代码中读取配置
 * 
 * v1.3.2014.0104   关联cs文件右键菜单
 * 
 * v1.2.2013.0129   支持源文件引用所在目录的程序集
 * 
 * v1.1.2013.0127   增加C#脚本生成Exe文件的功能
 * 
 * v1.0.2013.0117   创建XScript
 *
*/