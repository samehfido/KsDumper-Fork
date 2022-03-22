using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using KsDumperClient.Driver;
using KsDumperClient.PE;
using KsDumperClient.Utility;

namespace KsDumperClient
{
    public partial class Dumper : Form
    {
        private readonly DriverInterface driver;
        private readonly ProcessDumper dumper;

        public Dumper()
        {
            InitializeComponent();

            driver = new DriverInterface("\\\\.\\KsDumper");
            dumper = new ProcessDumper(driver);
            LoadProcessList();
        }

        private void Dumper_Load(object sender, EventArgs e)
        {
            Logger.OnLog += Logger_OnLog;
            Logger.Log("KsDumper v1.1 - By EquiFox");
        }

        private void LoadProcessList()
        {
            if (driver.HasValidHandle())
            {
                if (driver.GetProcessSummaryList(out ProcessSummary[] result))
                {
                    processList.LoadProcesses(result);
                }
                else
                {
                    MessageBox.Show("Unable to retrieve process list !", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void dumpMainModuleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (driver.HasValidHandle())
            {
                ProcessSummary targetProcess = processList.SelectedItems[0].Tag as ProcessSummary;

                Task.Run(() =>
                {

                    if (dumper.DumpProcess(targetProcess, out PEFile peFile))
                    {
                        Invoke(new Action(() =>
                        {
                            using (SaveFileDialog sfd = new SaveFileDialog())
                            {
                                sfd.FileName = targetProcess.ProcessName.Replace(".exe", "_dump.exe");
                                sfd.Filter = "Executable File (.exe)|*.exe";

                                if (sfd.ShowDialog() == DialogResult.OK)
                                {
                                    peFile.SaveToDisk(sfd.FileName);
                                    Logger.Log("Saved at '{0}' !", sfd.FileName);
                                }
                            }
                        }));
                    }
                    else
                    {
                        Invoke(new Action(() =>
                        {
                            MessageBox.Show("Unable to dump target process !", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                });
            }
            else
            {
                MessageBox.Show("Unable to communicate with driver ! Make sure it is loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Logger_OnLog(string message)
        {
            logsTextBox.Invoke(new Action(() => logsTextBox.AppendText(message)));
        }

        private void refreshMenuBtn_Click(object sender, EventArgs e)
        {
            LoadProcessList();
        }

        private void hideSystemProcessMenuBtn_Click(object sender, EventArgs e)
        {
            if (!processList.SystemProcessesHidden)
            {
                processList.HideSystemProcesses();
                hideSystemProcessMenuBtn.Text = "Show System Processes";
            }
            else
            {
                processList.ShowSystemProcesses();
                hideSystemProcessMenuBtn.Text = "Hide System Processes";
            }
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            e.Cancel = processList.SelectedItems.Count == 0;           
        }

        private void logsTextBox_TextChanged(object sender, EventArgs e)
        {
            logsTextBox.SelectionStart = logsTextBox.Text.Length;
            logsTextBox.ScrollToCaret();
        }

        private void openInExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProcessSummary targetProcess = processList.SelectedItems[0].Tag as ProcessSummary;
            Process.Start("explorer.exe", Path.GetDirectoryName(targetProcess.MainModuleFileName));
        }
        
        
        private void moduleToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            if (driver.HasValidHandle())
            {
                var targetProcess = processList.SelectedItems[0].Tag as ProcessSummary;
                var proc = Process.GetProcessById(targetProcess.ProcessId);
                if (proc != null)
                {
                    var modulesList = new List<ProcessModule>();
                    foreach (ProcessModule module in proc.Modules)
                    {
                        modulesList.Add(module);
                    }
                    
                    modulesList.Sort(new Compare());
                    moduleToolStripMenuItem.DropDownItems.Clear();

                    foreach (var module in modulesList)
                    {
                        var menuItem = new ToolStripMenuItem(module.ModuleName);
                        menuItem.Tag = module;
                        menuItem.Click += MenuItem_Click;
                        moduleToolStripMenuItem.DropDownItems.Add(menuItem);
                    }
                }
            }

        }

        private void MenuItem_Click(object sender, EventArgs e)
        {
            var targetProcess = processList.SelectedItems[0].Tag as ProcessSummary;
            var module = (sender as ToolStripMenuItem).Tag as ProcessModule;
            if (targetProcess is null || module is null)
            {
                MessageBox.Show("targetProcess Is Null!!");
                return; 
            }
            targetProcess.MainModuleImageSize = (uint)module.ModuleMemorySize;
            targetProcess.MainModuleBase = (ulong)module.BaseAddress.ToInt64();
            targetProcess.MainModuleEntryPoint = (ulong)module.EntryPointAddress.ToInt64();
            targetProcess.MainModuleFileName = module.FileName;

            if (dumper.DumpProcess(targetProcess, out PEFile peFile))
            {
                Invoke(new Action(() =>
                {
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.FileName = module.FileName.Replace(".dll", "_dump.dll");
                        sfd.Filter = "Executable File (.dll)|*.dll";

                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            peFile.SaveToDisk(sfd.FileName);
                            Logger.Log("Saved at '{0}' !", sfd.FileName);
                        }
                    }
                }));
            }
            else
            {
                Invoke(new Action(() =>
                {
                    MessageBox.Show("Unable to dump target process !", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
        }

        class Compare : IComparer<ProcessModule>, IEqualityComparer<ProcessModule>
        {
            public bool Equals(ProcessModule x, ProcessModule y)
            {
                return x.ModuleName == y.ModuleName;
            }

            public int GetHashCode(ProcessModule obj)
            {
                throw new NotImplementedException();
            }

            int IComparer<ProcessModule>.Compare(ProcessModule x, ProcessModule y)
            {
                if (x == null || y == null)
                    return 0;
                return x.ModuleName.CompareTo(y.ModuleName);
            }
        }
    }
}
