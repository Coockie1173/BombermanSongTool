using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BombermanSongTool
{
    internal class Program
    {
        struct Song
        {
            public UInt32[] Offsets;
            public UInt32 Unk01;
            public List<List<byte>> SongData;

            public UInt32 Size;
        };

        struct AudioHeaderEntry
        {
            public UInt32 Offset;
            public UInt32 Length;

            public static int GetSize()
            {
                return sizeof(UInt32) * 2;
            }
        };

        struct AudioHeader
        {
            public UInt16 Version; //5332
            public UInt16 EntryCount;
            public List<AudioHeaderEntry> Entries;

            public int GetSize()
            {
                return (AudioHeaderEntry.GetSize() * (EntryCount)) + (sizeof(UInt16) * 2);
            }
        };
        
        static void Main(string[] args)
        {
            List<Song> SongList = new List<Song>();
            AudioHeader MyHeader = new AudioHeader();

            string executablePath = Assembly.GetExecutingAssembly().Location;
            string executableFolder = Path.GetDirectoryName(executablePath);
            string WorkSpaceFolder = executableFolder + "\\WorkSpace";

            PrintColoredString($"Working in directory: ", ConsoleColor.Blue, ConsoleColor.Black);
            PrintColoredString($"{WorkSpaceFolder}\n", ConsoleColor.Green, ConsoleColor.Black);

            PrintColoredString($"Reading Order.txt...\n", ConsoleColor.Blue, ConsoleColor.Black);

            if(!File.Exists(WorkSpaceFolder + "\\Order.txt"))
            {
                PrintColoredString($"ERROR: CAN'T FIND ORDER.TXT, PLEASE ENSURE THIS FILE IS IN YOUR WORKSPACE FOLDER.\n", ConsoleColor.Red, ConsoleColor.Black);
                return;
            }

            string[] DirectoryOrder = File.ReadAllLines(WorkSpaceFolder + "\\Order.txt");

            foreach (string Directory in DirectoryOrder)
            {
                if(Directory == "")
                {
                    continue;
                }
                if(!Path.Exists(WorkSpaceFolder + "\\" + Directory))
                {
                    PrintColoredString($"Warning invalid directory: ", ConsoleColor.Yellow, ConsoleColor.Black);
                    PrintColoredString($"{WorkSpaceFolder + "\\" + Directory}\n", ConsoleColor.Red, ConsoleColor.Black);
                    continue;
                }
                if(!File.Exists(WorkSpaceFolder + "\\" + Directory + "\\Song.txt"))
                {
                    PrintColoredString($"Warning Song.txt not found: ", ConsoleColor.Yellow, ConsoleColor.Black);
                    PrintColoredString($"{WorkSpaceFolder + "\\" + Directory + "\\Song.txt"}\n", ConsoleColor.Red, ConsoleColor.Black);
                }

                //song found and present!
                PrintColoredString($"Parsing song {WorkSpaceFolder + "\\" + Directory + "\\Song.txt"}...\n", ConsoleColor.Blue, ConsoleColor.Black);
                SongList.Add(ParseSong(File.ReadAllLines(WorkSpaceFolder + "\\" + Directory + "\\Song.txt")));
            }

            PrintColoredString($"All songs parsed...\n", ConsoleColor.Green, ConsoleColor.Black);
            MyHeader.Version = 0x5332;
            MyHeader.EntryCount = (ushort)(SongList.Count - 1);
            MyHeader.Entries = new List<AudioHeaderEntry>();

            int Offs = MyHeader.GetSize();

            for (int i = 0; i < SongList.Count; i++)
            {
                AudioHeaderEntry AHE = new AudioHeaderEntry();
                AHE.Offset = (uint)Offs;
                AHE.Length = SongList[i].Size;

                MyHeader.Entries.Add(AHE);

                Offs += (int)SongList[i].Size;
            }


            List<byte> OutBytes = new List<byte>();

            OutBytes.AddRange(LittleEndianToBigEndian(BitConverter.GetBytes(MyHeader.Version)));
            OutBytes.AddRange(LittleEndianToBigEndian(BitConverter.GetBytes(MyHeader.EntryCount)));

            for (int i = 0; i < SongList.Count; i++)
            {
                OutBytes.AddRange(LittleEndianToBigEndian(BitConverter.GetBytes(MyHeader.Entries[i].Offset)));
                OutBytes.AddRange(LittleEndianToBigEndian(BitConverter.GetBytes(MyHeader.Entries[i].Length)));
            }

            for(int i = 0; i < SongList.Count; i++)
            {
                foreach(int SI in  SongList[i].Offsets)
                {
                    OutBytes.AddRange(LittleEndianToBigEndian(BitConverter.GetBytes(SI)));
                }

                OutBytes.AddRange(LittleEndianToBigEndian(BitConverter.GetBytes(SongList[i].Unk01)));

                foreach(List<byte> LB in SongList[i].SongData)
                {
                    OutBytes.AddRange(LB);
                }
            }

            File.WriteAllBytes(WorkSpaceFolder + "\\SongBank.bin", OutBytes.ToArray());
            PrintColoredString($"File written to: {WorkSpaceFolder + "\\SongBank.bin"}", ConsoleColor.Green, ConsoleColor.Black);
        }

        static Song ParseSong(string[] SongFile)
        {
            Song song = new Song();
            song.Offsets = new uint[16];
            song.SongData = new List<List<byte>>();

            for (int i = 0; i < 16; i++)
            {
                song.SongData.Add(new List<byte>());
            }

            for (int SongIndex = 0; SongIndex < SongFile.Length; SongIndex++)
            {
                string TrimLine = SongFile[SongIndex].ToLower().Trim();
                if (TrimLine == "")
                {
                    continue;
                }

                if (TrimLine.StartsWith("v"))
                {
                    string VVal = TrimLine.Split(' ')[1];
                    song.Unk01 = uint.Parse(VVal);
                    continue;
                }
                else if(TrimLine.StartsWith("track"))
                {
                    //start reading in all binary data
                    int TrackID = int.Parse(TrimLine.Replace("track",""));

                    PrintColoredString($"Parsing Track {TrackID}...\n", ConsoleColor.Blue, ConsoleColor.Black);

                    string HexText = "";

                    for(int TrackIndex = SongIndex + 1;  TrackIndex < SongFile.Length; TrackIndex++, SongIndex++)
                    {
                        HexText += SongFile[TrackIndex].Replace(" ", "").Trim().Replace("\n","");
                        if(HexText.EndsWith("FF2F"))
                        {
                            break;
                        }
                    }

                    byte[] byteArray = new byte[HexText.Length / 2];

                    for (int i = 0; i < byteArray.Length; i++)
                    {
                        string byteValue = HexText.Substring(i * 2, 2);
                        byteArray[i] = Convert.ToByte(byteValue, 16);
                    }

                    song.SongData[TrackID].AddRange(byteArray);
                }
            }

            uint CurrentOffset = 0x44;
            for(int i = 0; i < 16; i++)
            {
                song.Offsets[i] = CurrentOffset;
                if(song.SongData[i].Count != 0)
                {
                    CurrentOffset += (uint)song.SongData[i].Count;
                }
                else
                {
                    song.Offsets[i] = 0;
                }
                PrintColoredString($"Track {i} offset: {song.Offsets[i].ToString("X8")}\n", ConsoleColor.Blue, ConsoleColor.Black);
            }

            song.Size = CurrentOffset;

            while (song.Size % 4 != 0)
            {
                song.Size++;
                song.SongData[15].Add(0);
            }

            return song;
        }

        public static byte[] LittleEndianToBigEndian(byte[] littleEndianArray)
        {
            if (littleEndianArray == null)
            {
                throw new ArgumentNullException(nameof(littleEndianArray), "Input array cannot be null.");
            }

            byte[] bigEndianArray = new byte[littleEndianArray.Length];
            for (int i = 0; i < littleEndianArray.Length; i++)
            {
                bigEndianArray[i] = littleEndianArray[littleEndianArray.Length - 1 - i];
            }

            return bigEndianArray;
        }

        static void PrintColoredString(string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            ConsoleColor originalForeground = Console.ForegroundColor;
            ConsoleColor originalBackground = Console.BackgroundColor;

            try
            {
                Console.ForegroundColor = foregroundColor;
                Console.BackgroundColor = backgroundColor;

                Console.Write(text);
            }
            finally
            {
                Console.ForegroundColor = originalForeground;
                Console.BackgroundColor = originalBackground;
            }
        }
    }
}
