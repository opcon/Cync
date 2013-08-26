using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TagLib;
using TagLib.Mpeg;
using File = System.IO.File;
using System.ComponentModel;

namespace Cync
{
    class Playlist
    {
        public string Name { get; set; }

        public int Count { get; set; }

        public bool Sync { get; set; }

        public bool Master { get; set; }

        public List<Song> Songs { get; set; }

        public List<string> InvalidFiles { get; set; }

        public float Size { get; set; }

        public static Playlist LoadPlaylist(string file, int index, BackgroundWorker worker = null)
        {
            bool reportProgress = (worker != null);
            int count = 0;

            Playlist p = new Playlist();

            p.Name = Path.GetFileNameWithoutExtension(file);

            var lines = File.ReadAllLines(file);

            List<string> invalidFiles = new List<string>();

            List<Song> songs = new List<Song>();

            for (int i = 0; i < lines.Length; i++)
            {
                count++;
                var s = lines[i];
                try
                {
                    if (reportProgress && count > 10) { worker.ReportProgress(1, 
                        new PlaylistWorkerState() { CurrentSong = i, PlaylistName = p.Name, PlaylistNumber = index, TotalSongs = lines.Length }); count = 0; }
                    if (File.Exists(s))
                    {
                        var song = new Song(s);
                        p.Size += (song.Size/1024);
                        songs.Add(song);
                    }
                }
                catch (Exception ex)
                {
                    invalidFiles.Add(s);
                }
            }

            p.Songs = songs;
            p.Count = songs.Count;
            p.Sync = true;
            p.InvalidFiles = invalidFiles;

            return p;
        }
    }

    class Song
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        
        public SongStatus SongStatus { get; set; }

        public bool Sync { get; set; }
        
        public string File { get; set; }
        public String DeviceFile { get; set; }

        public string RelativeFilePath { get; set; }

        public string Album { get; set; }
        public string Genre { get; set; }
        public uint Track { get; set; }

        public float Size { get; set; }

        public Destination SongDestination { get; set; }



        public Song(string f)
        {
            //TagLib.File file = new AudioFile(f);
            TagLib.File file = TagLib.File.Create(f); 
            var tag = file.Tag;
            Artist = String.IsNullOrWhiteSpace(tag.FirstAlbumArtist) ? tag.FirstArtist : tag.FirstAlbumArtist;
            Album = tag.Album;
            Genre = tag.FirstGenre;
            Title = tag.Title;
            Track = tag.Track;
            File = f;

            Size = new FileInfo(f).Length / 1024 / 1024;
        }

        public Song()
        {
        }

        public override bool Equals(object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Song return false.
            Song s = obj as Song;
            if (s == null)
            {
                return false;
            }

            Song song = s;

            // Return true if the fields match:
            return (Title == song.Title) && (Album == song.Album) && (Artist == song.Artist) && (Genre == song.Genre);
        }

    }

    enum SongStatus
    {
        DeviceAndLibrary,
        Library,
        Device
    }
}
