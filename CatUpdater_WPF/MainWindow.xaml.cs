using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CatUpdater_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        BackgroundWorker updateWorker = new BackgroundWorker();


        public MainWindow()
        {
            InitializeComponent();
            InitUpdateFeature();

        }

        private void InitUpdateFeature()
        {
            updateWorker.DoWork += UpdateWorker_DoWork;
            updateWorker.RunWorkerCompleted += UpdateWorker_RunWorkerCompleted;
            updateWorker.ProgressChanged += UpdateWorker_ProgressChanged;
            updateWorker.WorkerReportsProgress = true;
            updateWorker.WorkerSupportsCancellation = true;

            pgbar_updateProgress.Value = 0;
        }


        private void Btn_updateTrigger_Click(object sender, RoutedEventArgs e)
        {

            if (btn_updateTrigger.Content.ToString() == "Update")
            {
                btn_updateTrigger.Content = "Cancel";
                updateWorker.RunWorkerAsync();

            }
            else
            {
                btn_updateTrigger.Content = "Update";
                updateWorker.CancelAsync();
            }


        }

        private void UpdateWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pgbar_updateProgress.Value = e.ProgressPercentage;
            txtbox_updateLog.Text += ((FileInfo)e.UserState).Name + Environment.NewLine;
            txtbox_updateLog.ScrollToEnd();
        }

        private async void UpdateWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var worker = sender as BackgroundWorker;

            if (e.Cancelled && !updateWorker.CancellationPending)
            {
                txtblock_updateLog.Text = "Cancel !!";
            }
            else if (e.Error != null)
            {
                txtblock_updateLog.Text = "Error !!"  + e.Error.ToString();
            }
            else
            {
                var pvt = new WinPVT();
                if (pvt.canUpdate)
                {
                    btn_updateTrigger.IsEnabled = false;
                    if (pvt.isInstalled) { await pvt.uninstall(); txtblock_updateLog.Text = "Uninstalling WinPVT"; }
                    txtblock_updateLog.Text = "Installing WinPVT, waiting for restart !!";
                    await pvt.install();
                }
                else
                {
                    btn_updateTrigger.Content = "Update";
                    txtblock_updateLog.Text = "Done !!";
                }

            }
        }

        private void UpdateWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {

                var files = CopyFiles();
                var total = files.Count();
                var cnt = 0;

                foreach (var v in files)
                {
                    if (updateWorker.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }


                    var destiFile = v.Item2;
                    if (!destiFile.Directory.Exists) destiFile.Directory.Create();
                    v.Item1.CopyTo(destiFile.FullName, true);

                    var percentage = (cnt += 1)*100 / total;
                    updateWorker.ReportProgress(percentage, destiFile);

                }
            }
            catch(Exception s)
            {
                throw s;
            }
        }




        /* COPY FILES */
        private IEnumerable<Tuple<FileInfo, FileInfo>> CopyFiles()
        {
            var pvt = new WinPVT();
            var pws = new PwrStress();
            var cat = new CAT();

            IEnumerable<Tuple<FileInfo, FileInfo>> copyList = Enumerable.Empty<Tuple<FileInfo, FileInfo>>();

            if (pvt.canUpdate && !pvt.isDownloaded) copyList = copyList.Union(pvt.addCopyList());
            if (pws.canUpdate) copyList = copyList.Union(pws.addCopyList());
            if (cat.canUpdate) copyList = copyList.Union(cat.addCopyList());

            return copyList;
        }

        
        class WinPVT
        {
            string CPU_ARCH
            {
                get
                {
                    if (System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER").ToLower().Contains("arm")) return "arm";
                    else return "x64";
                }
            }

            public FileInfo getLocal;
            public FileInfo getServer;
            FileInfo getlocal
            {
                get
                {
                    DirectoryInfo d;
                    if(CPU_ARCH == "arm")
                    {
                        d = new DirectoryInfo(@"C:\Program Files (x86)\Hewlett-Packard");
                    }
                    else
                    {
                        d = new DirectoryInfo(@"C:\Program Files\Hewlett-Packard");
                    }
                    if (d!=null && d.Exists)
                    {
                        var f = d.EnumerateFiles("WinPVT.exe", SearchOption.AllDirectories).FirstOrDefault();

                        if (f != null)
                        {
                            localVersion = f.Directory.Name;
                            isInstalled = true;
                            return f;
                        }
                    }

                    return null;
                }
            }
            FileInfo getserver
            {
                get
                {
                    var d = new DirectoryInfo(@"\\lab_server\share");
                    if (d != null && d.Exists)
                    {
                        if (CPU_ARCH == "arm")
                        {
                            d = d.EnumerateDirectories("WinPVTarm").FirstOrDefault();
                        }
                        else
                        {
                            d = d.EnumerateDirectories("WinPVT").FirstOrDefault();
                        }
                        if (d != null && d != null)
                        {
                            var winpvt = d.EnumerateFiles().Aggregate((x, y) => x.LastWriteTime > y.LastWriteTime ? x : y);
                            if (winpvt != null)
                            {
                                serverVersion = winpvtGetRegex(winpvt.Name).Groups[1].Value;
                                return winpvt;
                            }
                        }
                    }
                    else throw new Exception($"Server not find {d.FullName}");
                    return null;
                }
            }
            public bool isInstalled;
            public bool isDownloaded
            {
                get
                {
                    var d = new DirectoryInfo(@"C:\Program Files (x86)\Cat\WinPVT");
                    if (d.Exists)
                    {
                        var f = d.EnumerateFiles(getServer.Name, SearchOption.AllDirectories).FirstOrDefault();
                        if(f != null && f.Exists)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        d.Create();
                    }
                    return false;
                }
            }
            public string localVersion;
            public string serverVersion;
            public bool canUpdate
            {
                get
                {
                    if(getServer != null && isInstalled)
                    {
                        //compare two version
                        var lV = winpvtGetRegex(localVersion).Groups;
                        // GroupCollection(7) { [WinPVT 10.0E (x64).exe], [WinPVT 10.0E], [10.0], [E], [(x64)], [x64], [.exe] }
                        var sV = winpvtGetRegex(serverVersion).Groups;
                        var localNumV = Version.Parse(lV[2].Value);
                        // ...............................................................[10.0]..............................}
                        var serverNumV = Version.Parse(sV[2].Value);
                        if (serverNumV > localNumV) return true;    //server version > local version => allow  install 
                        else if (serverNumV == localNumV)
                        {
                            var localAlphV = lV[3].Value;
                            var serverAlphV = sV[3].Value;
                            if (serverAlphV != "" && localAlphV == "") return true;
                            else if(serverAlphV != "" && localAlphV != "")
                            {
                                if (Convert.ToByte(serverAlphV[0]) > Convert.ToByte(localAlphV[0])) return true;
                            }
                        }
                        
                    }
                    else if(getServer != null && !isInstalled)
                    {
                        //allow download
                        return true;
                    }

                    return false;
                }
            }

            private Match winpvtGetRegex(string input)
            {
                return Regex.Match(input, @"(WinPVT\s?((?:\d+\.?)+)([A-Z]?))\s?(\((x64|x86)\))?(.exe)?");
            }
            public WinPVT()
            {
                getLocal = getlocal;
                getServer = getserver;
            }
            public Task<bool> install()
            {
                return Task.Factory.StartNew(() => executeProgram($@"C:\Program Files (x86)\Cat\WinPVT\{getServer.Name}", "/S /v/passive"));
            }
            public Task<bool> uninstall()
            {
                return Task.Factory.StartNew(() => uninstallProgram(localVersion));
            }

            public IEnumerable<Tuple<FileInfo, FileInfo>> addCopyList()
            {
                var s = getServer;
                if (isDownloaded) yield break;
                else yield return new Tuple<FileInfo, FileInfo>(s, new FileInfo($@"C:\Program Files (x86)\Cat\WinPVT\{s.Name}"));
            }
            #region Program

            bool uninstallProgram(string programName)
            {

                string searchProductID(string _programName)
                {
                    using (var keys = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {

                        foreach (string key_name in keys.GetSubKeyNames())
                        {
                            var v = (string)keys.OpenSubKey(key_name).GetValue("DisplayName");
                            if (v != null && v.Trim().Contains(_programName)) return key_name;
                        }

                    }

                    using (var keys = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {

                        foreach (string key_name in keys.GetSubKeyNames())
                        {
                            var v = (string)keys.OpenSubKey(key_name).GetValue("DisplayName");
                            if (v != null && v.Trim().Contains(_programName)) return key_name;
                        }

                    }
                    return null;
                }

                string pId = searchProductID(programName);
                if (pId!=null) return executeProgram(@"C:\Windows\System32\msiexec.exe", $@"/x {pId} /passive /norestart", out _);
                return false;
                
            }
            bool executeProgram(string exePath, string exeCommand)
            {
                string output = "";
                try
                {
                    executeProgram(exePath, exeCommand, out output);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            bool executeProgram(string exePath, string exeCommand, out string Output)
            {
                Output = "";
                try
                {
                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = exePath;
                    process.StartInfo.Arguments = exeCommand;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    StreamReader reader = process.StandardOutput;
                    Output = reader.ReadToEnd();
                    process.WaitForExit();
                    return true;

                }
                catch
                {
                    Output = "";
                    return false;
                }

            }
            #endregion

        }
        class PwrStress
        {
            public bool isInstalled;
            public FileInfo getLocal;
            public FileInfo getServer;

            FileInfo getlocal
            {
                get
                {
                    var d =  new DirectoryInfo(@"C:\Release");
                    if (d!= null && d.Exists)
                    {
                        var f = d.EnumerateFiles("PowerStressTest.exe",SearchOption.AllDirectories).FirstOrDefault();
                        if (f != null && f.Exists)
                        {
                            localVersion = FileVersionInfo.GetVersionInfo(f.FullName).ProductVersion;
                            isInstalled = true;
                            return f;
                        }
                    }
                    return null;

                }
            }
            FileInfo getserver
            {
                get
                {
                    var d = new DirectoryInfo(@"\\lab_server\share\PowerStressTest");
                    if (d.Exists)
                    {
                        var ds = d.EnumerateDirectories();
                        if (ds.Count() > 1)
                        {
                            d = ds.Aggregate((x, y) => Version.Parse(pwrstressGetRegex(x.Name).Groups[1].Value) >
                                                                       Version.Parse(pwrstressGetRegex(y.Name).Groups[1].Value) ? x : y);
                        }
                        else
                        {
                            d = ds.FirstOrDefault();
                        }

                        if (d != null)
                        {
                            var f = d.EnumerateFiles("PowerStressTest.exe", SearchOption.AllDirectories).FirstOrDefault();
                            if (f.Exists)
                            {
                                serverVersion = FileVersionInfo.GetVersionInfo(f.FullName).ProductVersion;
                                return f;
                            }
                        }
                    }
                    else throw new Exception($"Not find {d.FullName}");
                    return null;
                }
            }

            public string localVersion;
            public string serverVersion;
            public bool canUpdate
            {
                get
                {
                    if (!isInstalled) return true;
                    else if(localVersion !="" && serverVersion != "")
                    {
                        return Version.Parse(serverVersion) > Version.Parse(localVersion);
                    }
                    return false;
                }
            }

            public IEnumerable<Tuple<FileInfo, FileInfo>> addCopyList()
            {
                var s = getServer.Directory.EnumerateFiles("*", SearchOption.AllDirectories);
                foreach (var _s in s)
                {
                    yield return new Tuple<FileInfo, FileInfo>(_s, new FileInfo(Regex.Replace(_s.FullName, @"(.*)Release", @"C:\Release")));
                }
            }
            private Match pwrstressGetRegex(string input)
            {
                return Regex.Match(input, @"PowerStressTest-((?:\d+\.?)+)");
            }

            public PwrStress()
            {
                getLocal = getlocal;
                getServer = getserver;

            }
        }
        class CAT
        {
            public FileInfo getLocal;
            public FileInfo getServer;
            public bool isInstalled;
            public bool canUpdate
            {
                get
                {
                    if (!isInstalled) return true;
                    else if(localVersion != "" && serverVersion != "")
                    {
                        return Version.Parse(serverVersion) > Version.Parse(localVersion);
                    }
                    return false;
                }
            }
            public string localVersion;
            public string serverVersion;
            FileInfo getlocal
            {
                get
                {
                    var d = new DirectoryInfo(@"C:\Program Files (x86)\Cat");
                    if (d != null && d.Exists)
                    {
                        var f = d.EnumerateFiles("Cat Client.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (f != null && f.Exists)
                        {
                            localVersion = FileVersionInfo.GetVersionInfo(f.FullName).ProductVersion;
                            isInstalled = true;
                            return f;
                        }
                    }
                    else d.Create();
                    return null;
                }
            }
            FileInfo getserver
            {
                get
                {
                    var d = new DirectoryInfo(@"\\lab_server\share\CatClient3.0");
                    if (d != null && d.Exists)
                    {
                        var f = d.EnumerateFiles("Cat Client.exe").FirstOrDefault();
                        if (f != null && f.Exists)
                        {
                            serverVersion = FileVersionInfo.GetVersionInfo(f.FullName).ProductVersion;
                            return f;
                        }

                    }
                    else throw new Exception($"Server not find {d.FullName}");
                    return null;
                }
            }

            public CAT()
            {
                getLocal = getlocal;
                getServer = getserver;
            }
            public IEnumerable<Tuple<FileInfo, FileInfo>> addCopyList()
            {
                var s = getServer.Directory.EnumerateFiles("*", SearchOption.AllDirectories);
                foreach (var _s in s)
                {
                    yield return new Tuple<FileInfo, FileInfo>(_s, new FileInfo(Regex.Replace(_s.FullName, @"(.*)CatClient3.0", @"C:\Program Files (x86)\Cat")));
                }
            }
        }
        class UPDATER
        {

        }
    }
}
