using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Tools // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        private const string PLUGINS_DIRECTORY_NAME = "Plugins";

        private static string _rootPath = null;
        private static string _targetPath = null;
        private static string _junctionPath = null;

        private static Process _junctionProcess = new Process();

        private static List<string> _exclusiveDirectoryName = new List<string> { "Binaries", "DerivedDataCache", "Intermediate", "Saved", "Build" };

        private static List<DirectoryInfo> _paths = new List<DirectoryInfo>();

        private static void ProcessPluginsDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
                return;

            if (dirInfo.Name != PLUGINS_DIRECTORY_NAME)
                return;

            HashSet<DirectoryInfo> allPluginSet = new HashSet<DirectoryInfo>();
            HashSet<string> needJunctionPaths = new HashSet<string>();
            HashSet<string> needCopyPaths = new HashSet<string>();

            //Plugins subdirectory
            foreach (DirectoryInfo pluginCategoryDir in dirInfo.GetDirectories())
            {
                if (!pluginCategoryDir.Exists)
                    continue;

                GetAllPluginDirectories(pluginCategoryDir, allPluginSet, needJunctionPaths);
            }

            foreach (DirectoryInfo pluginDir in allPluginSet)
            {
                ProcessSinglePluginDirectory(pluginDir, needJunctionPaths);
            }

            foreach(string junctionPath in needJunctionPaths)
            {
                Console.WriteLine("Junction : " + junctionPath.Replace(_rootPath, _targetPath));
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

        private static void GetAllPluginDirectories(DirectoryInfo dir, HashSet<DirectoryInfo> dirSet, HashSet<string> needJunctionPaths)
        {
            if (IsValidPluginDirectory(dir))
            {
                dirSet.Add(dir);
                return;
            }

            if (dir.GetDirectories().Length == 0)
            {
                needJunctionPaths.Add(dir.FullName);
                return;
            }
            
            foreach (DirectoryInfo pluginDir in dir.GetDirectories())
            {
                GetAllPluginDirectories(pluginDir, dirSet, needJunctionPaths);
            }
        }

        private static void ProcessSinglePluginDirectory(DirectoryInfo pluginDir, HashSet<string> needJunctionPaths)
        {
            if (IsPluginDirectoryCanBeJunctioned(pluginDir))
            {
                //needJunctionPaths.Add(pluginDir.FullName);
                Junction(pluginDir.FullName, pluginDir.FullName.Replace(_rootPath, _targetPath));
                return;
            }
            else
            {
                CreatePluginDirectoryRecursively(pluginDir.FullName.Replace(_rootPath, _targetPath));
            }

            foreach(DirectoryInfo subDir in pluginDir.GetDirectories())
            {
                if (_exclusiveDirectoryName.Contains(subDir.Name))
                    continue;

                //needJunctionPaths.Add(subDir.FullName);
                Junction(subDir.FullName, subDir.FullName.Replace(_rootPath, _targetPath));
            }

            foreach(FileInfo file in pluginDir.GetFiles())
            {
                //needJunctionPaths.Add(file.FullName);
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

        private static void CreatePluginDirectoryRecursively(string path)
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

        private static void GetAllContentByRoot(string path, int currentDepth)
        {
            if (string.IsNullOrEmpty(path) || currentDepth < 0)
                return;

            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
                return;

            _paths.Add(dirInfo);

            foreach (var dir in dirInfo.GetDirectories())
            {
                if (_exclusiveDirectoryName.Contains(dir.Name))
                    continue;

                GetAllContentByRoot(dir.FullName, currentDepth - 1);
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
         
        static void Main(string[] args)
        {
            _rootPath = args[0];
            _targetPath = args[1];

            _junctionPath = args[2];

            InitJunctionProcess();

            _paths.Clear();
            ProcessPluginsDirectory(_rootPath);
        }
    }
}