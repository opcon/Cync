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

        private List<Destination> DestinationList = new List<Destination>(); 
        private string _playlistDirectory;
        public string Name { get; set; }

        public string PlaylistDirectory
        {
            get { return _playlistDirectory; }
            set { 
                _playlistDirectory = value;
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
            if (DestinationList == null) return;
            Destinations = new ObservableCollection<Destination>(DestinationList);
        }

        [OnSerializing]
        public void FixDestinations(StreamingContext context)
        {
            DestinationList = Destinations.ToList();
        }

        //public void GetObjectData(SerializationInfo info, StreamingContext context)
        //{
        //    info.AddValue("PlaylistDirectory", _playlistDirectory);
        //    info.AddValue("Name", Name);
        //    var d = Destinations.ToList();
        //    info.AddValue("Destinations", d);
        //}

        protected DeviceSettings(SerializationInfo info, StreamingContext context)
        {
            this.Name = (string)info.GetValue("Name", typeof(string));
            this.PlaylistDirectory = (string)info.GetValue("PlaylistDirectory", typeof(string));
            this.DestinationList = (List<Destination>)info.GetValue("Destinations", typeof(List<Destination>));
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
            info.AddValue("Destinations", this.DestinationList);
            info.AddValue("PlaylistDirectory", this._playlistDirectory);
        }
    }

    [Serializable]
    class Destination : INotifyPropertyChanged, ISerializable
    {
        private string _fileNameTemplate;
        private string _exampleName;
        private string _destinationPath;
        private string _playlistRoot;
        private int _maxFiles;
        private int _minFreeSpace;

        public int NumberOfSongs=0;

        public string DestinationPath
        {
            get { return _destinationPath; }
            set { 
                _destinationPath = value; 
                CalculateFreeSpace();
                Name = Path.GetPathRoot(value);
                Drive = Path.GetPathRoot(value);
                OnPropertyChanged("DestinationPath");
            }
        }

        public int MaxFiles
        {
            get { return _maxFiles;}
            set {_maxFiles = value; OnPropertyChanged("MaxFiles") ;}
        }

        public int MinFreeSpace
        {
            get { return _minFreeSpace; }
            set {_minFreeSpace = value; OnPropertyChanged("MinFreeSpace");}
        }

        public string PlaylistRoot
        {
            get { return _playlistRoot; }
            set { _playlistRoot = value; OnPropertyChanged("PlaylistRoot"); }
        }

        public string FileNameTemplate
        {
            get { return _fileNameTemplate; }
            set { _fileNameTemplate = value; if (String.IsNullOrWhiteSpace(ParseFileTemplate(value))) throw new ApplicationException("The file template you entered was invalid");
                ExampleName = ParseFileTemplate(value) + ".mp3";
            }
        }

        public string ExampleName
        {
            get { return _exampleName; }
            set
            {
                _exampleName = value;
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
                        space = drive.TotalFreeSpace / 1024 / 1024 / 1024;
                    }
                }
            }
            catch (Exception)
            {
            }

            return space;
        }

        public override string ToString()
        {
            return Name;
        }

        //public void GetObjectData(SerializationInfo info, StreamingContext context)
        //{
        //    info.AddValue("FileNameTemplate", _fileNameTemplate);
        //    info.AddValue("DestinationPath", _destinationPath);
        //    info.AddValue("PlaylistRoot", _playlistRoot);
        //}

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
            result = "Valid Pattern (So Far)";
            string[] variables = p.Split('%');
            //if (p[0] == '%')
            //{
            //    for (int i = 0; i < markerChararcters * 0.5; i+=2)
            //    {
            //        variables[i] = GetVariableValue(s, variables[i]);
            //    }
                
            //}
            //for (int i = 0; i < markerChararcters * 0.5; i+=2)
            //{
            //    variables[i+1] = GetVariableValue(s, variables[i+1]);
            //}
            for (int i = 0; i < markerChararcters; i+=1)
            {
                variables[i] = GetVariableValue(s, variables[i]);
            }
            result = string.Join("", variables);
            return result;
        }

        public static string SanitizeVariables(string path, char replaceChar)
        {
            if (path == null) return path;
            foreach (char c in Path.GetInvalidPathChars())
                path = path.Replace(c, replaceChar);

            foreach (char c in Path.GetInvalidFileNameChars())
                path = path.Replace(c, replaceChar);

            return path;
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

                info.AddValue("FileNameTemplate", _fileNameTemplate);
                info.AddValue("DestinationPath", this._destinationPath);
                info.AddValue("PlaylistRoot", this._playlistRoot);
                info.AddValue("MaxFiles", this._maxFiles);
                info.AddValue("MinFreeSpace", this._minFreeSpace);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving current configuration!");
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
