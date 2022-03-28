using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Tools // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        private static string _rootPath = null;
        private static string _targetPath = null;
        private static string _junctionPath = null;

        private static Process _junctionProcess = new Process();

        private static readonly List<string> _exclusiveDirectoryName = new List<string> 
        { 
            "Binaries", 
            "DerivedDataCache", 
            "Intermediate", 
            "Saved", 
            "Build" 
        };

        private static List<string> _firstCreateDirs = new List<string>
        {
            @"Engine",
            @"Engine\Plugins",
            @"Project",
            @"Project\Plugins"
        };

        private static List<string> _ignoredJunctionDirs = new List<string>
        {
            @".idea",
            @".vs",
            @"Binaries",

            @"Engine",
            @"Engine\Build",
            @"Engine\Binaries",
            @"Engine\DerivedDataCache",
            @"Engine\Intermediate",
            @"Engine\Plugins",
            @"Engine\Saved",

            @"Project",
            @"Project\.idea",
            @"Project\.vs",
            @"Project\Binaries",
            @"Project\Build",
            @"Project\DerivedDataCache",
            @"Project\Intermediate",
            @"Project\Plugins",
            @"Project\Saved",
        };

        private static List<string> _extraCopyDirs = new List<string>
        {
            @"Engine\Build",
            @"Engine\Plugins\Media\AjaMedia\Binaries\ThirdParty",
            @"Engine\Plugins\Media\BlackmagicMedia\Binaries\ThirdParty",
        };

        private static void ProcessPluginsDir(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
                return;

            HashSet<DirectoryInfo> allPluginSet = new HashSet<DirectoryInfo>();
            HashSet<string> needJunctionPaths = new HashSet<string>();

            //Plugins subdirectory
            foreach (DirectoryInfo pluginCategoryDir in dirInfo.GetDirectories())
            {
                if (!pluginCategoryDir.Exists)
                    continue;

                GetAllPluginDirectories(pluginCategoryDir, allPluginSet);
            }

            foreach (DirectoryInfo pluginDir in allPluginSet)
            {
                ProcessSinglePluginDirectory(pluginDir);
            }
        }

        private static bool IsValidPluginDirectory(DirectoryInfo dir)
        {
            FileInfo[]? pluginFile = dir.GetFiles("*.uplugin");
            if (pluginFile == null || pluginFile.Length != 1)
            {
                return false;
            }

            return true;
        }

        private static void GetAllPluginDirectories(DirectoryInfo dir, HashSet<DirectoryInfo> dirSet)
        {
            if (IsValidPluginDirectory(dir))
            {
                dirSet.Add(dir);
                return;
            }

            if (dir.GetDirectories().Length == 0)
            {
                return;
            }
            
            foreach (DirectoryInfo pluginDir in dir.GetDirectories())
            {
                GetAllPluginDirectories(pluginDir, dirSet);
            }
        }

        private static void ProcessSinglePluginDirectory(DirectoryInfo pluginDir)
        {
            if (IsPluginDirectoryCanBeJunctioned(pluginDir))
            {
                Junction(pluginDir.FullName, pluginDir.FullName.Replace(_rootPath, _targetPath));
                return;
            }
            else
            {
                CreateDirectoryRecursively(pluginDir.FullName.Replace(_rootPath, _targetPath));
            }

            foreach(DirectoryInfo subDir in pluginDir.GetDirectories())
            {
                if (_exclusiveDirectoryName.Contains(subDir.Name))
                    continue;

                Junction(subDir.FullName, subDir.FullName.Replace(_rootPath, _targetPath));
            }

            foreach(FileInfo file in pluginDir.GetFiles())
            {
                File.Copy(file.FullName, file.FullName.Replace(_rootPath, _targetPath), true);
            }
        }

        private static bool IsPluginDirectoryCanBeJunctioned(DirectoryInfo dir)
        {
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                if (_exclusiveDirectoryName.Contains(subDir.Name))
                    return false;
            }

            return true;
        }

        private static void CreateDirectoryRecursively(string path)
        {
            if (Directory.Exists(path))
                return;

            Stack<string> stack = new Stack<string>();
            string currentPath = path;
            while (!Directory.Exists(currentPath))
            {
                stack.Push(currentPath);
                currentPath = Directory.GetParent(currentPath).FullName;
            }

            foreach (string p in stack)
            {
                Directory.CreateDirectory(p);
            }
        }

        private static void Junction(string source, string target)
        {
            _junctionProcess.StartInfo.Arguments = string.Format("/c {0} {1} {2}", _junctionPath, target, source);
            _junctionProcess.Start();
            _junctionProcess.StandardOutput.ReadToEnd();
            _junctionProcess.WaitForExit();
            _junctionProcess.Close();
        }

        private static void InitJunctionProcess()
        {
            _junctionProcess.StartInfo.FileName = "cmd.exe";
            _junctionProcess.StartInfo.UseShellExecute = false;
            _junctionProcess.StartInfo.RedirectStandardOutput = true;
            _junctionProcess.StartInfo.RedirectStandardError = true;
            _junctionProcess.StartInfo.CreateNoWindow = true;
        }

        private static void FullDirectoryCopy(string sourceDir, string targetDir)
		{
            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine($"Failed to copy dir : {sourceDir}");
                return;
            }

            CreateDirectoryRecursively(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
			{
                File.Copy(file, file.Replace(sourceDir, targetDir), true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
			{
                FullDirectoryCopy(subDir, subDir.Replace(sourceDir, targetDir));
			}
		}
         
        static void Main(string[] args)
        {
            _junctionPath = args[0];
            _rootPath = args[1];
            _targetPath = args[2];

            InitJunctionProcess();

            CreateDefaultFilesAndDirs();

            JunctionDefaultDirs(_rootPath);
            JunctionDefaultDirs($@"{_rootPath}\Engine");
            JunctionDefaultDirs($@"{_rootPath}\Project");

            ProcessPluginsDir($@"{_rootPath}\Engine\Plugins");
            ProcessPluginsDir($@"{_rootPath}\Project\Plugins");

            CopyExtraDirs();
        }

        private static void CreateDefaultFilesAndDirs()
		{
            foreach (string dir in _firstCreateDirs)
			{
                Directory.CreateDirectory(@$"{_targetPath}\{dir}");
			}

            foreach (string file in Directory.GetFiles(_rootPath))
			{
                File.Copy(file, file.Replace(_rootPath, _targetPath), true);
            }

            foreach (string file in Directory.GetFiles($@"{_rootPath}\Project"))
            {
                File.Copy(file, file.Replace(_rootPath, _targetPath), true);
            }
        }

        private static void JunctionDefaultDirs(string path)
		{
            foreach(string dir in Directory.GetDirectories(path))
			{
                if (_ignoredJunctionDirs.Contains(dir.Replace(_rootPath + @"\", "")))
                    continue;

                Junction(dir, dir.Replace(_rootPath, _targetPath));
			}
		}

        private static void CopyExtraDirs()
		{
            foreach (string dir in _extraCopyDirs)
			{
                FullDirectoryCopy($@"{_rootPath}\{dir}", $@"{_targetPath}\{dir}");
			}
		}
    }
}