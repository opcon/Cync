using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;

namespace Cync
{
    [Serializable]
    class DeviceCollection : ISerializable
    {
        private List<DeviceSettings> _deviceList;

        public List<DeviceSettings> DeviceList
        {
            get { return this._deviceList; }
            set { this._deviceList = value; }
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("DeviceList", this._deviceList);
        }

        protected DeviceCollection(SerializationInfo info, StreamingContext context)
        {
            this._deviceList = (List<DeviceSettings>) info.GetValue("DeviceList", typeof (List<DeviceSettings>));
        }

        public DeviceCollection()
        {
            
        }
    }

    [Serializable]
    class DeviceSettings : INotifyPropertyChanged, ISerializable
    {
        [NonSerialized]
        public ObservableCollection<Destination> Destinations = new ObservableCollection<Destination>();

        private List<Destination> destinationList = new List<Destination>(); 
        private string playlistDirectory;
        public string Name { get; set; }

        public string PlaylistDirectory
        {
            get { return playlistDirectory; }
            set { 
                playlistDirectory = value;
                OnPropertyChanged("PlaylistDirectory");
            }
        }

        public override string ToString()
        {
            return Name;
        }

        [OnDeserialized]
        public void RunOnceDeSerialized(StreamingContext context)
        {
            if (destinationList == null) return;
            Destinations = new ObservableCollection<Destination>(destinationList);
        }

        [OnSerializing]
        public void FixDestinations(StreamingContext context)
        {
            destinationList = Destinations.ToList();
        }


        protected DeviceSettings(SerializationInfo info, StreamingContext context)
        {
            this.Name = (string)info.GetValue("Name", typeof(string));
            this.PlaylistDirectory = (string)info.GetValue("PlaylistDirectory", typeof(string));
            this.destinationList = (List<Destination>)info.GetValue("Destinations", typeof(List<Destination>));
            //Destinations = new ObservableCollection<Destination>(dest);
        }

        public DeviceSettings()
        {
            Name = "";
            PlaylistDirectory = "";
        }

        public void AddDestination(Destination d)
        {
            Destinations.Add(d);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", this.Name);
            info.AddValue("Destinations", this.destinationList);
            info.AddValue("PlaylistDirectory", this.playlistDirectory);
        }
    }

    [Serializable]
    class Destination : INotifyPropertyChanged, ISerializable
    {
        private string fileNameTemplate;
        private string exampleName;
        private string destinationPath;
        private string playlistRoot;
        private int maxFiles;
        private int minFreeSpace;

        public int NumberOfSongs=0;

        public string DestinationPath
        {
            get { return destinationPath; }
            set { 
                destinationPath = value; 
                Name            = Path.GetPathRoot(value);
                Drive           = Path.GetPathRoot(value);
                OnPropertyChanged("DestinationPath");
                CalculateFreeSpace();
            }
        }

        public int MaxFiles
        {
            get { return maxFiles;}
            set {maxFiles = value; OnPropertyChanged("MaxFiles") ;}
        }

        public int MinFreeSpace
        {
            get { return minFreeSpace; }
            set {minFreeSpace = value; OnPropertyChanged("MinFreeSpace");}
        }

        public string PlaylistRoot
        {
            get { return playlistRoot; }
            set { playlistRoot = value; OnPropertyChanged("PlaylistRoot"); }
        }

        public string FileNameTemplate
        {
            get { return fileNameTemplate; }
            set { fileNameTemplate = value; if (String.IsNullOrWhiteSpace(ParseFileTemplate(value))) throw new ApplicationException("The file template you entered was invalid");
                ExampleName = ParseFileTemplate(value) + ".mp3";
            }
        }

        public string ExampleName
        {
            get { return exampleName; }
            set
            {
                exampleName = value;
                OnPropertyChanged("ExampleName");
            }
        }

        public float FreeSpace { get { return CalculateFreeSpace(); } }
        public float SpaceUsedMaxPercentage { get; set; }

        public string Name { get; set; }
        public string Drive { get; set; }

        public Destination()
        {
            Name = "Destination";

        }

        public float CalculateFreeSpace()
        {
            float space = -1;
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.Name == Path.GetPathRoot(DestinationPath))
                    {
                        space = drive.TotalFreeSpace / (1024.0f*1024.0f*1024.0f);
                    }
                }
            }
            catch
            {
            }

            return space;
        }

        public override string ToString()
        {
            return Name;
        }

        public static string ParseFileTemplate(string p, Song s = null)
        {
            if (string.IsNullOrWhiteSpace(p)) return "j";
            if (s == null) s = new Song();
            string result = "";
            int markerChararcters = p.Count(c => c == '%');

            if (markerChararcters % 2 == 1 || markerChararcters == 0)
            {
                return result;
            }

            string[] variables = p.Split('%');
            for (int i = 0; i < markerChararcters; i+=1)
            {
                variables[i] = GetVariableValue(s, variables[i]);
            }
            result = string.Join("", variables);
            return result;
        }

        public static string SanitizeVariables(string path, char replaceChar)
        {
            if (path == null) return null;
            path = Path.GetInvalidPathChars().Aggregate(path, (current, c) => current.Replace(c, replaceChar));

            return Path.GetInvalidFileNameChars().Aggregate(path, (current, c) => current.Replace(c, replaceChar));
        }

        public static string GetVariableValue(Song s, string variableName)
        {
            string result = "";
            Type songType = s.GetType();
            foreach (var propertyInfo in songType.GetProperties())
            {
                if (string.Equals(propertyInfo.Name, variableName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (propertyInfo.PropertyType == typeof(uint))
                        result = ((uint) propertyInfo.GetValue(s)).ToString();
                    else
                        result = SanitizeVariables((string) propertyInfo.GetValue(s), '_');
                    if (result == null)
                        result = propertyInfo.Name;
                }
            }
            if (result == "") result = variableName; 
            return result;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            try
            {

                info.AddValue("FileNameTemplate", fileNameTemplate);
                info.AddValue("DestinationPath", this.destinationPath);
                info.AddValue("PlaylistRoot", this.playlistRoot);
                info.AddValue("MaxFiles", this.maxFiles);
                info.AddValue("MinFreeSpace", this.minFreeSpace);
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Error saving current configuration: {0}", ex));
            }
        }

        protected Destination(SerializationInfo info, StreamingContext context)
        {
            Name = "Destination";
            try
            {
                this.DestinationPath = (string)info.GetValue("DestinationPath", typeof(string));
                this.FileNameTemplate = (string)info.GetValue("FileNameTemplate", typeof(string));
                this.PlaylistRoot = (string)info.GetValue("PlaylistRoot", typeof(string));
                this.MaxFiles = (int)info.GetValue("MaxFiles", typeof(int));
                this.MinFreeSpace = (int)info.GetValue("MinFreeSpace", typeof(int));
            }
            catch (Exception)
            {
                MessageBox.Show("Error loading previous configuration!");
            }
        }
    }

    public enum Tags
    {
        Artist,
        Album,
        Genre,
        Title,
        Number
    }
}
