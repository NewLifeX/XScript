// 自动选择最新的文件源
var srcs = new[] { @"..\Bin", @"C:\X\DLL", @"C:\X\Bin", @"D:\X\Bin", @"E:\X\DLL", @"E:\X\Bin" };
".".AsDirectory().CopyIfNewer(srcs, "*.dll;*.exe;*.xml;*.pdb;*.cs");