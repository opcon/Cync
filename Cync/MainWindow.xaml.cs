using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using Ookii.Dialogs.Wpf;
using System.Collections.ObjectModel;
using Path = System.IO.Path;

namespace Cync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<Playlist> Playlists { get; set; }
        private BackgroundWorker worker = new BackgroundWorker();
        private DispatcherTimer timer = new DispatcherTimer();
        private string[] PlaylistFiles;

        private List<Playlist> PlaylistsToSync { get; set; }
        private ObservableCollection<Song> SongsToSync { get; set; }

        private ObservableCollection<DeviceSettings> Devices { get; set; }

        private DeviceSettings testDeviceSettings;
        private Destination fileTemplateDestination;

        private float MinimumFreeSpace = 10;

        private Serializer serializer;

        public MainWindow()
        {
            InitializeComponent();

            Playlists = new ObservableCollection<Playlist>();

            SongsToSync = new ObservableCollection<Song>();

            serializer = new Serializer();

            PlaylistGrid.DataContext = Playlists;

            worker.DoWork += LoadPlaylistDoWork;
            worker.RunWorkerCompleted += LoadPlaylistWorkerCompleted;
            worker.ProgressChanged += LoadPlaylistProgressChanged;
            worker.WorkerReportsProgress = true;

            timer.Tick += timer_Tick;
            timer.Interval = TimeSpan.FromMilliseconds(10);

            //PlaylistGrid.ItemsSource = Playlists;
            testDeviceSettings = new DeviceSettings();
            testDeviceSettings.Name = "Test";
            testDeviceSettings.PlaylistDirectory = "lol";
            

            fileTemplateDestination = new Destination(){ExampleName = "test", FileNameTemplate = @"%artist%\%album%\%title% - %artist%"};

            testDeviceSettings.AddDestination(fileTemplateDestination);

            Devices = new ObservableCollection<DeviceSettings>();
            //Devices.Add(testDeviceSettings);
            if (File.Exists("devices.cyn"))
                Devices = new ObservableCollection<DeviceSettings>(serializer.DeSerializeObject("devices.cyn").DeviceList);

            //Devices = dev;
            DeviceListBox.DataContext = Devices;
            DeviceComboPicker.DataContext = Devices;

            DeviceComboPicker.SelectedItem = Devices.Count > 0 ? Devices[0] : null;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            ProgressBar1.Value += 1;
            if (ProgressBar1.Value >= ProgressBar1.Maximum)
                ProgressBar1.Value = 0;
        }

        #region LoadPlaylists

        private void LoadPlaylistProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var report = (PlaylistWorkerState) e.UserState;
            StatusString.Content = string.Format("Processing {4} ({0}/{1}) | Processing song {2}/{3}",
                                                 report.PlaylistNumber + 1,
                                                 PlaylistFiles.Length, report.CurrentSong, report.TotalSongs,
                                                 report.PlaylistName);
        }

        private void LoadPlaylistWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var result = (PlaylistWorkerReturn) e.Result;
            Playlists = result.Playlists;
            PlaylistGrid.DataContext = Playlists;

            timer.Stop();
            ProgressBar1.Value = ProgressBar1.Maximum;
            StatusString.Content = "Ready";
            ReviewButton.IsEnabled = true;
        }

        private void LoadPlaylistDoWork(object sender, DoWorkEventArgs e)
        {
            string path = (string) e.Argument;

            List<string> invalidFiles = new List<string>();
            ObservableCollection<Playlist> play = new ObservableCollection<Playlist>();

            //Playlists.Clear();

            PlaylistFiles = Directory.GetFiles(path, "*.m3u8");

            for (int i = 0; i < PlaylistFiles.Length; i++)
            {

                var f = PlaylistFiles[i];
                var p = Playlist.LoadPlaylist(f, i, worker);
                play.Add(p);

                invalidFiles.AddRange(p.InvalidFiles);
            }

            string error = "";
            foreach (var s in invalidFiles)
            {
                error += s + "\n";
            }

            if (!string.IsNullOrWhiteSpace(error)) MessageBox.Show("There was an error loading these files: \n" + error);

            e.Result = new PlaylistWorkerReturn() {InvalidFiles = invalidFiles, Playlists = play};
        }

        #endregion



        #region BuildSongList

        private void BuildSongListWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            timer.Stop();
            ProgressBar1.Value = ProgressBar1.Maximum;

            var result = (SongListWorkerReturn)e.Result;
            SongsToSync = new ObservableCollection<Song>(result.Songs);

            SongDataGrid.DataContext = SongsToSync;

            StatusString.Content = "Ready";
        }

        private void BuildSongListProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var report = (PlaylistWorkerState)e.UserState;
            StatusString.Content = report.Status;
        }

        private void BuildSongListDoWork(object sender, DoWorkEventArgs e)
        {
            var ret = new SongListWorkerReturn();
            var selectedDevice = e.Argument as DeviceSettings;
            try
            {
                if (PlaylistsToSync.Any(p => p.Master))
                {
                    var master = PlaylistsToSync.First(p => p.Master);
                    ret.Songs = new ObservableCollection<Song>(master.Songs);
                }
                else
                {
                    ret.Songs = new ObservableCollection<Song>();
                    int i = 0;
                    for (int j = 0; j < PlaylistsToSync.Count; j++)
                    {
                        var playlist = PlaylistsToSync[j];
                        for (int index = 0; index < playlist.Songs.Count; index++)
                        {
                            var song = playlist.Songs[index];
                            i++;
                            if (!ret.Songs.Contains(song)) ret.Songs.Add(song);

                            if (i <= 10) continue;
                            i = 0;
                            worker.ReportProgress(0,
                                                  new PlaylistWorkerState()
                                                      {
                                                          Status =
                                                              string.Format(
                                                                  "Processing {4} ({0}/{1}) | Processing song {2}/{3}",
                                                                  j + 1,
                                                                  PlaylistFiles.Length, index, playlist.Songs.Count,
                                                                  playlist.Name)
                                                      });
                        }
                    }
                }

                var deviceSongList = new List<Song>();

                List<string> musicFiles = new List<string>();
                foreach (var d in selectedDevice.Destinations)
                {
                    var rangeToAdd = Directory.EnumerateFiles(d.DestinationPath, "*.*", SearchOption.AllDirectories).Where(
                       s => s.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                            || s.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)).ToList();
                    musicFiles.AddRange(rangeToAdd);

                    int count = 0;
                    for (int i = 0; i < musicFiles.Count; i++)
                    {
                        try
                        {
                            count++;
                            var file = musicFiles[i];
                            deviceSongList.Add(new Song(file) { SongDestination = d });
                            d.NumberOfSongs++;
                            if (count > 0)
                            {
                                count = 0;
                                worker.ReportProgress(0, new PlaylistWorkerState() { Status = string.Format("Reading destination {2} songs: {0}/{1}", i, musicFiles.Count, d) });
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error reading mp3 tag of " + musicFiles[i] + ", this file will be skipped.");
                        }
                    }

                    musicFiles = new List<string>();
                }

                for (int i = 0; i < ret.Songs.Count; i++)
                {
                    var s = ret.Songs[i];
                    if (deviceSongList.Contains(s))
                    {
                        var c = deviceSongList.Find(s.Equals);
                        s.SongStatus = SongStatus.DeviceAndLibrary;
                        s.DeviceFile = deviceSongList.Find(p => p.Equals(s)).File;
                        s.RelativeFilePath = s.DeviceFile.Replace(c.SongDestination.DestinationPath, "");
                        s.Sync = false;
                        deviceSongList.Remove(s);
                    }
                    else
                    {
                        s.SongStatus = SongStatus.Library;
                        s.Sync = true;
                    }
                }
                for (int i = 0; i < deviceSongList.Count; i++)
                {
                    var song = deviceSongList[i];
                    song.SongStatus = SongStatus.Device;
                    song.DeviceFile = song.File;
                    song.File = "";
                    ret.Songs.Add(song);

                    song.Sync = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);               
            }
            
            e.Result = ret;
        }
        #endregion

        #region Sync

        void WorkerSyncCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            StatusString.Content = "Sync Complete!";

            timer.Stop();
            ProgressBar1.Value = ProgressBar1.Maximum;

            SyncButton.Content = "Sync";
        }

        void SyncProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            StatusString.Content = ((PlaylistWorkerState) e.UserState).Status;
        }

        void AdvanceDestination(ref int currindex, ObservableCollection<Destination> dests)
        {
            if (currindex <= dests.Count - 1)
            {
                currindex += 1;
            }
            else
            {
                MessageBox.Show("All destinations are full or have reached their song limit");
                throw new Exception("All destinations are full or have reached their song limit");
            }
        }

        void DoSync(object sender, DoWorkEventArgs e)
        {
            int destinationIndex = 0;
            var device = e.Argument as DeviceSettings;

            var destinations = (e.Argument as DeviceSettings).Destinations;
            var currentDestination = destinations[destinationIndex];

            var songs = SongsToSync.Where(s => s.Sync);
            var songsToSync = songs as List<Song> ?? songs.ToList();
            var largeSongs = new List<Song>();

            //Do the initial sync
            for (int i = 0; i < songsToSync.Count; i++)
            {
                if ((currentDestination.MaxFiles != 0 && currentDestination.NumberOfSongs >= currentDestination.MaxFiles)
                    || (currentDestination.MinFreeSpace != 0 && currentDestination.FreeSpace >= currentDestination.MinFreeSpace))
                {
                    AdvanceDestination(ref destinationIndex, destinations);
                    currentDestination = destinations[destinationIndex];

                }
                if (worker.CancellationPending) return;
                var song = songsToSync[i];
                worker.ReportProgress(1,
                                      new PlaylistWorkerState()
                                          {
                                              Status =
                                                  string.Format("Copying song {0}/{1}, {2}", i, songsToSync.Count(),
                                                                System.IO.Path.GetFileName(song.File))
                                          });
                try
                {
                    song.RelativeFilePath = Destination.ParseFileTemplate(currentDestination.FileNameTemplate, song) + System.IO.Path.GetExtension(song.File);
                    string dest = System.IO.Path.Combine(currentDestination.DestinationPath, song.RelativeFilePath);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
                    File.Copy(song.File, dest);
                    song.DeviceFile = dest;
                    song.SongStatus = SongStatus.DeviceAndLibrary;
                    currentDestination.NumberOfSongs++;
                }
                catch (Exception ex)
                {
                    const long ERROR_HANDLE_DISK_FULL = 0x27;
                    const long ERROR_DISK_FULL = 0x70;

                    long win32ErrorCode = Marshal.GetHRForException(ex) & 0xFFFF;
                    if (win32ErrorCode == ERROR_HANDLE_DISK_FULL || win32ErrorCode == ERROR_DISK_FULL || true)
                    {
                        currentDestination.CalculateFreeSpace();
                        if (currentDestination.FreeSpace < (MinimumFreeSpace / 1024))
                        {
                            if (destinationIndex >= destinations.Count - 1)
                            {
                                largeSongs.Add(song);
                                i++;
                            }
                            else
                            {
                                destinationIndex += 1;
                                currentDestination = destinations[destinationIndex];
                            }

                            i--; //Attempt to sync the song again to another destination
                        }
                        else
                        {
                            largeSongs.Add(song);
                        }
                    }
                }
            }
            
            //Retry syncing the larger songs that failed
            for (int i = 0; i < largeSongs.Count; i++)
            {
                if (worker.CancellationPending) return;
                var song = largeSongs[i];
                worker.ReportProgress(1,
                                      new PlaylistWorkerState()
                                      {
                                          Status =
                                              string.Format("Copying song {0}/{1}, {2}", i, largeSongs.Count(),
                                                            System.IO.Path.GetFileName(song.File))
                                      });
                try
                {
                    song.RelativeFilePath = Destination.ParseFileTemplate(currentDestination.FileNameTemplate, song) + System.IO.Path.GetExtension(song.File);
                    string dest = System.IO.Path.Combine(currentDestination.DestinationPath, song.RelativeFilePath);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
                    File.Copy(song.File, dest);
                    song.DeviceFile = dest;
                    song.SongStatus = SongStatus.DeviceAndLibrary;
                }
                catch (Exception ex)
                {
                    const long ERROR_HANDLE_DISK_FULL = 0x27;
                    const long ERROR_DISK_FULL = 0x70;

                    long win32ErrorCode = Marshal.GetHRForException(ex) & 0xFFFF;
                    if (win32ErrorCode == ERROR_HANDLE_DISK_FULL || win32ErrorCode == ERROR_DISK_FULL)
                    {
                        currentDestination.CalculateFreeSpace();
                        if (currentDestination.FreeSpace < (MinimumFreeSpace / 1024))
                        {
                            if (destinationIndex >= destinations.Count - 1)
                            {
                                MessageBox.Show("Error: All destinations are full.");
                                break;
                            }
                            destinationIndex += 1;
                            currentDestination = destinations[destinationIndex];

                            i--;
                        }
                        else
                        {
                            largeSongs.Add(song);
                        }
                    }
                }
            }

            //Sync the playlists. In the event that there is no room for a playlist, we bail.
            foreach (var playlist in PlaylistsToSync)
            {
                string completePlaylist = "";
                foreach (var song in playlist.Songs.Where(s => s.SongStatus == SongStatus.Device || s.SongStatus == SongStatus.DeviceAndLibrary))
                {
                    try
                    {
                        var root = Path.GetPathRoot(song.DeviceFile);
                        Destination dest = destinations.First(d => song.DeviceFile.Contains(d.DestinationPath));
                        var trimmedpath = Path.GetFullPath(song.DeviceFile).Substring(3);
                        //string songRoot = Path.Combine(dest.PlaylistRoot, trimmedpath);
                        string songRoot = "";
                        if (!string.IsNullOrWhiteSpace(dest.PlaylistRoot))
                            songRoot = dest.PlaylistRoot + "\\" + trimmedpath;
                        else
                            songRoot = trimmedpath;
                        string songPath = dest.PlaylistRoot + song.RelativeFilePath;
                        completePlaylist += songPath + "\n";
                    }
                    catch (Exception ex)
                    {
                        completePlaylist += ex.Message;
                    }
                }

                System.IO.File.WriteAllText(device.PlaylistDirectory + "\\" + playlist.Name + ".m3u8", completePlaylist);
            }
        }
        #endregion


        #region WindowsFormsHandlers

        private void Choose_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg = new VistaFolderBrowserDialog();
            // Configure open file dialog box
            bool? result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // Open document 
                string filename = dlg.SelectedPath;

                PlaylistDir.Text = filename;
            }
        }

        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            if (!worker.IsBusy)
            {
                worker.RunWorkerAsync(PlaylistDir.Text);
                timer.Start();
                StatusString.Content = "Scanning";
                ((TabItem)MainTabControl.Items[1]).IsEnabled = true;
            }
            else MessageBox.Show("Playlist Scan is already running!");
        }

        private void Review_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 1;

            ProgressBar1.Value = 0;
            timer.Start();
            StatusString.Content = "Building list of songs...";

            worker.DoWork -= LoadPlaylistDoWork;
            worker.ProgressChanged -= LoadPlaylistProgressChanged;
            worker.RunWorkerCompleted -= LoadPlaylistWorkerCompleted;

            worker.DoWork += BuildSongListDoWork;
            worker.ProgressChanged += BuildSongListProgressChanged;
            worker.RunWorkerCompleted += BuildSongListWorkerCompleted;

            PlaylistsToSync = Playlists.Where(p => p.Sync).ToList();

            worker.RunWorkerAsync(DeviceComboPicker.SelectedItem as DeviceSettings);
        }

        private void Sync_Click(object sender, RoutedEventArgs e)
        {
            if (worker.IsBusy)
            {
                worker.CancelAsync();

                SyncButton.Content = "Sync";
                return;
            }
            ProgressBar1.Value = 0;
            timer.Start();
            StatusString.Content = "Syncing Songs";

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;

            worker.DoWork += DoSync;
            worker.ProgressChanged += SyncProgressChanged;
            worker.RunWorkerCompleted += WorkerSyncCompleted;


            worker.RunWorkerAsync(DeviceComboPicker.SelectedItem as DeviceSettings);

            SyncButton.Content = "Cancel";
        }
        
        private void AddDevice_Click(object sender, RoutedEventArgs e)
        {
            Devices.Add(new DeviceSettings());
            DeviceListBox.SelectedIndex = Devices.Count - 1;
            DeviceListBox.Focus();
        }

        private void RemoveDevice_Click(object sender, RoutedEventArgs e)
        {
            Devices.RemoveAt(DeviceListBox.SelectedIndex);
            DeviceListBox.Focus();
        }

        private void DeviceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceListBox.SelectedItem != null)
            {
                DestinationListBox.DataContext = null;
                DestinationListBox.DataContext = ((DeviceSettings) DeviceListBox.SelectedItem).Destinations;
                PlaylistDirectory.DataContext = (DeviceSettings) DeviceListBox.SelectedItem;
            }
            else DestinationListBox.DataContext = null;
        }

        private void ChooseDestinationPath_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg = new VistaFolderBrowserDialog();
            // Configure open file dialog box
            bool? result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // Open document 
                string filename = dlg.SelectedPath;

                ((Destination)DestinationListBox.SelectedItem).DestinationPath = filename;
            }

            DeviceListBox_SelectionChanged(null, null);
        }

        private void DestinationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DestinationPropertyPanel.DataContext = ((Destination) DestinationListBox.SelectedItem);
        }

        private void AddDestination_Click(object sender, RoutedEventArgs e)
        {
            var ds = DeviceListBox.SelectedItem as DeviceSettings;
            ds.Destinations.Add(new Destination());
            DestinationListBox.SelectedIndex = ds.Destinations.Count - 1;
            DestinationListBox.Focus();
        }

        private void RemoveDestination_Click(object sender, RoutedEventArgs e)
        {
            var ds = DeviceListBox.SelectedItem as DeviceSettings;
            ds.Destinations.RemoveAt(DestinationListBox.SelectedIndex);
            DestinationListBox.Focus();
        }

        private void DeviceComboPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var device = DeviceComboPicker.SelectedItem as DeviceSettings;
            float totalfreespace = 0;
            foreach (var d in device.Destinations)
            {
                d.CalculateFreeSpace();
                totalfreespace += d.FreeSpace;
            }

            FreeSpaceIndicator.Content = string.Format("{0:0.###}", totalfreespace) + "GB";
        }

        private void ChoosePlaylistDirectory_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg = new VistaFolderBrowserDialog();
            // Configure open file dialog box
            bool? result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // Open document 
                string filename = dlg.SelectedPath;

                ((DeviceSettings)DeviceListBox.SelectedItem).PlaylistDirectory = filename;
            }
        }

        private void Window_Closing_1(object sender, CancelEventArgs e)
        {
            var d = new DeviceCollection();
            d.DeviceList = Devices.ToList();
            //if (d.DeviceList.Count != 0)
                serializer.SerializeObject("devices.cyn", d);
        }

        private void RenameDevice_Click(object sender, RoutedEventArgs e)
        {
            var name = Microsoft.VisualBasic.Interaction.InputBox("Please enter a new name for the device", "Rename Device");

            if (!string.IsNullOrWhiteSpace(name))
            {
                (DeviceListBox.SelectedItem as DeviceSettings).Name = name;
            }
            DeviceListBox.DataContext = null;
            DeviceListBox.DataContext = Devices;
        } 
        
        private void FileTemplate_TextChanged(object sender, TextChangedEventArgs e)
        {
            //ExampleFileName.Content = fileTemplateDestination.ExampleName;
        }

        #endregion
    }

    struct SongListWorkerReturn
    {
        public ObservableCollection<Song> Songs;
    }

    struct PlaylistWorkerReturn
    {
        public ObservableCollection<Playlist> Playlists;
        public List<string> InvalidFiles;
    }

    struct PlaylistWorkerState
    {
        public string PlaylistName;
        public int PlaylistNumber;
        public int CurrentSong;
        public int TotalSongs;
        public String Status;
    }
}
