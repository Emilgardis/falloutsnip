using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Linq;
using System.Drawing;
using System.Text;
using RTF;

namespace TESVSnip
{
    using TESVSnip.Data;
    public class TESParserException : Exception { public TESParserException(string msg) : base(msg) { } }

    #region class SelectionContext
    /// <summary>
    /// External state for holding single selection for use with evaluating descriptions and intelligent editors
    /// </summary>
    public class SelectionContext
    {
        private Plugin plugin;
        private Record record;
        private SubRecord subRecord;

        public Plugin Plugin
        {
            get { return plugin; }
            set 
            {
                if (this.plugin != value)
                {
                    this.plugin = value;
                    this.Groups.Clear();
                    this.Record = null;
                    if (this.PluginChanged != null) 
                        this.PluginChanged(this, EventArgs.Empty);
                }
            }
        }
        public Stack<GroupRecord> Groups = new Stack<GroupRecord>();
        public Record Record
        {
            get { return this.record; }
            set
            {
                if (this.record != value)
                {
                    this.record = value;
                    this.SubRecord = null;
                    this.Conditions.Clear();
                    if (this.RecordChanged != null) 
                        this.RecordChanged(this, EventArgs.Empty);
                }
            }
        }
        public SubRecord SubRecord
        {
            get { return this.subRecord; }
            set 
            {
                if (this.subRecord != value)
                {
                    this.subRecord = value; 
                    if (this.SubRecordChanged != null) 
                        this.SubRecordChanged(this, EventArgs.Empty);
                }
            }
        }
        internal Dictionary<int, Conditional> Conditions = new Dictionary<int, Conditional>();
        internal dFormIDLookupI formIDLookup = null;
        internal dLStringLookup strLookup = null;
        internal dFormIDLookupR formIDLookupR = null;

        public bool SelectedSubrecord
        {
            get { return this.SubRecord != null;  }
        }

        public void Reset()
        {
            this.Plugin = null;
        }

        public event EventHandler PluginChanged;
        public event EventHandler RecordChanged;
        public event EventHandler SubRecordChanged;

        public SelectionContext Clone()
        {
            var result = (SelectionContext)this.MemberwiseClone();
            result.PluginChanged = null;
            result.RecordChanged = null;
            result.SubRecordChanged = null;
            return result;
        }
    }
    #endregion

	[Persistable(Flags = PersistType.DeclaredOnly), Serializable]
    public abstract class BaseRecord : PersistObject, ICloneable, ISerializable
    {
        [Persistable]
        public virtual string Name { get; set; }

        public abstract long Size { get; }
        public abstract long Size2 { get; }

        private static byte[] input;
        private static byte[] output;
        private static MemoryStream ms;
        private static BinaryReader compReader;
        private static ICSharpCode.SharpZipLib.Zip.Compression.Inflater inf;
        protected static BinaryReader Decompress(BinaryReader br, int size, int outsize)
        {
            if (input.Length < size)
            {
                input = new byte[size];
            }
            if (output.Length < outsize)
            {
                output = new byte[outsize];
            }
            br.Read(input, 0, size);
            inf.SetInput(input, 0, size);
            inf.Inflate(output);
            inf.Reset();

            ms.Position = 0;
            ms.Write(output, 0, outsize);
            ms.Position = 0;

            return compReader;
        }
        protected static void InitDecompressor()
        {
            inf = new ICSharpCode.SharpZipLib.Zip.Compression.Inflater(false);
            ms = new MemoryStream();
            compReader = new BinaryReader(ms);
            input = new byte[0x1000];
            output = new byte[0x4000];
        }
        protected static void CloseDecompressor()
        {
            compReader.Close();
            compReader = null;
            inf = null;
            input = null;
            output = null;
            ms = null;
        }

        public abstract string GetDesc();
        public virtual void GetFormattedData(RTFBuilder rb, SelectionContext context) { rb.Append(GetDesc()); }
        public virtual void GetFormattedData(StringBuilder sb, SelectionContext context) { sb.Append(GetDesc()); }
        

        public abstract bool DeleteRecord(BaseRecord br);
        public abstract void AddRecord(BaseRecord br);
        public virtual void InsertRecord(int index, BaseRecord br) { AddRecord(br); }

        // internal iterators
        public virtual bool While(Predicate<BaseRecord> action) { return action(this); }
        public virtual void ForEach(Action<BaseRecord> action) { action(this); }

        internal abstract List<string> GetIDs(bool lower);
        internal abstract void SaveData(BinaryWriter bw);

        private static readonly byte[] RecByte = new byte[4];
        protected static string ReadRecName(BinaryReader br)
        {
            br.Read(RecByte, 0, 4);
            return "" + ((char)RecByte[0]) + ((char)RecByte[1]) + ((char)RecByte[2]) + ((char)RecByte[3]);
        }
        protected static void WriteString(BinaryWriter bw, string s)
        {
            byte[] b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            bw.Write(b, 0, s.Length);
        }

        protected BaseRecord() { }
        protected BaseRecord(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
		}


        public abstract BaseRecord Clone();

        object ICloneable.Clone() { return this.Clone(); }
    }

    [Persistable(Flags = PersistType.DeclaredOnly), Serializable]
    public sealed class Plugin : BaseRecord
    {
        [Persistable]
        public readonly List<Rec> Records = new List<Rec>();

        public bool StringsDirty { get; set; }
        public readonly LocalizedStringDict Strings = new LocalizedStringDict();
        public readonly LocalizedStringDict ILStrings = new LocalizedStringDict();
        public readonly LocalizedStringDict DLStrings = new LocalizedStringDict();

        // Hash tables for quick FormID lookups
        public readonly Dictionary<uint, Record> FormIDLookup = new Dictionary<uint, Record>();

        // Whether the file was filtered on load
        public bool Filtered = false;

        public override long Size
        {
            get { long size = 0; foreach (Rec rec in Records) size += rec.Size2; return size; }
        }
        public override long Size2 { get { return Size; } }

        public override bool DeleteRecord(BaseRecord br)
        {
            Rec r = br as Rec;
            if (r == null) return false;
            bool result = Records.Remove(r);
            InvalidateCache();
            return result;
        }
        
        public override void AddRecord(BaseRecord br)
        {
            Rec r = br as Rec;
            if (r == null) throw new TESParserException("Record to add was not of the correct type." +
                   Environment.NewLine + "Plugins can only hold Groups or Records.");
            Records.Add(r);
            InvalidateCache();
        }
        public override void InsertRecord(int idx, BaseRecord br)
        {
            Rec r = br as Rec;
            if (r == null) throw new TESParserException("Record to add was not of the correct type." +
                   Environment.NewLine + "Plugins can only hold Groups or Records.");
            Records.Insert(idx, r);
            InvalidateCache();
        }

        public override bool While(Predicate<BaseRecord> action)
        {
            if ( !base.While(action) ) return false;
            foreach (var r in this.Records) 
                if (!r.While(action))
                    return false;
            return true;
        }
        public override void ForEach(Action<BaseRecord> action)
        {
            base.ForEach(action);
            foreach (var r in this.Records) r.ForEach(action);
        }

        public bool TryGetRecordByID(uint key, out Record value)
        {
            RebuildCache();
            return this.FormIDLookup.TryGetValue(key, out value);
        }

        private void RebuildCache()
        {
            if (this.FormIDLookup.Count == 0)
            {
                this.ForEach(br => { Record r = br as Record; if (r != null) { this.FormIDLookup[r.FormID] = r; } });
            }
        }

        /// <summary>
        /// Invalidate the FormID Cache.
        /// </summary>
        public void InvalidateCache()
        {
            this.FormIDLookup.Clear();
        }

        private void LoadPluginData(BinaryReader br, bool headerOnly, string[] recFilter)
        {
            string s;
            uint recsize;
            bool IsOblivion = false;

            this.Filtered = (recFilter != null && recFilter.Length > 0);

            InitDecompressor();

            s = ReadRecName(br);
            if (s != "TES4") throw new Exception("File is not a valid TES4 plugin (Missing TES4 record)");
            br.BaseStream.Position = 20;
            s = ReadRecName(br);
            if (s == "HEDR")
            {
                IsOblivion = true;
            }
            else
            {
                s = ReadRecName(br);
                if (s != "HEDR") throw new Exception("File is not a valid TES4 plugin (Missing HEDR subrecord in the TES4 record)");
            }
            br.BaseStream.Position = 4;
            recsize = br.ReadUInt32();
            Records.Add(new Record("TES4", recsize, br, IsOblivion));
            if (!headerOnly)
            {
                while (br.PeekChar() != -1)
                {
                    s = ReadRecName(br);
                    recsize = br.ReadUInt32();

                    
                    if (s == "GRUP")
                    {
                        Records.Add(new GroupRecord(recsize, br, IsOblivion, recFilter, false));
                    }
                    else
                    {
                        bool skip = recFilter != null && Array.IndexOf(recFilter, s) >= 0;
                        if (skip)
                        {
                            long size = (recsize + (IsOblivion ? 8 : 12));
                            if ((br.ReadUInt32() & 0x00040000) > 0) size += 4;
                            br.BaseStream.Position += size;// just read past the data
                        }
                        else
                            Records.Add(new Record(s, recsize, br, IsOblivion));
                    }
                }
            }

            CloseDecompressor();
        }

        public static bool GetIsEsm(string FilePath)
        {
            BinaryReader br = new BinaryReader(File.OpenRead(FilePath));
            try
            {
                string s = ReadRecName(br);
                if (s != "TES4") return false;
                br.ReadInt32();
                return (br.ReadInt32() & 1) != 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                br.Close();
            }
        }

        Plugin(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
		}


        public Plugin(byte[] data, string name)
        {
            Name = name;
            BinaryReader br = new BinaryReader(new MemoryStream(data));
            try
            {
                LoadPluginData(br, false, null);
            }
            finally
            {
                br.Close();
            }
        }
        internal Plugin(string FilePath, bool headerOnly) : this(FilePath, headerOnly, null) {}

        internal Plugin(string FilePath, bool headerOnly, string[] recFilter)
        {
            Name = Path.GetFileName(FilePath);
            FileInfo fi = new FileInfo(FilePath);
            using (BinaryReader br = new BinaryReader(fi.OpenRead()))
            {
                LoadPluginData(br, headerOnly, recFilter);
            }
            if (!headerOnly)
            {
                string prefix = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FilePath), "Strings"), System.IO.Path.GetFileNameWithoutExtension(FilePath));
                prefix += "_" + global::TESVSnip.Properties.Settings.Default.LocalizationName;
                Strings = LoadPluginStrings(LocalizedStringFormat.Base, prefix + ".STRINGS");
                ILStrings = LoadPluginStrings(LocalizedStringFormat.IL, prefix + ".ILSTRINGS");
                DLStrings = LoadPluginStrings(LocalizedStringFormat.DL, prefix + ".DLSTRINGS");
            }
        }

        public Plugin()
        {
            Name = "New plugin";
        }

        public override string GetDesc()
        {
            return "[Skyrim plugin]" + Environment.NewLine +
                "Filename: " + Name + Environment.NewLine +
                "File size: " + Size + Environment.NewLine +
                "Records: " + Records.Count;
        }

        public byte[] Save()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            SaveData(bw);
            byte[] b = ms.ToArray();
            bw.Close();
            return b;
        }

        internal void Save(string FilePath)
        {
            bool existed = false;
            DateTime timestamp = DateTime.Now;
            if (File.Exists(FilePath))
            {
                timestamp = new FileInfo(FilePath).LastWriteTime;
                existed = true;
                File.Delete(FilePath);
            }
            BinaryWriter bw = new BinaryWriter(File.OpenWrite(FilePath));
            try
            {
                SaveData(bw);
                Name = Path.GetFileName(FilePath);
            }
            finally
            {
                bw.Close();
            }
            try
            {
                if (existed)
                {
                    new FileInfo(FilePath).LastWriteTime = timestamp;
                }
            }
            catch { }

            //if (StringsDirty)
            var tes4 = this.Records.OfType<Record>().FirstOrDefault(x => x.Name == "TES4");
            if (tes4 != null && (tes4.Flags1 & 0x80) != 0)
            {
                string prefix = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FilePath), "Strings"), System.IO.Path.GetFileNameWithoutExtension(FilePath));
                prefix += "_" + global::TESVSnip.Properties.Settings.Default.LocalizationName;
                SavePluginStrings(LocalizedStringFormat.Base, Strings, prefix + ".STRINGS");
                SavePluginStrings(LocalizedStringFormat.IL, ILStrings, prefix + ".ILSTRINGS");
                SavePluginStrings(LocalizedStringFormat.DL, DLStrings, prefix + ".DLSTRINGS");
            }
            StringsDirty = false;
        }

        internal override void SaveData(BinaryWriter bw)
        {
            foreach (Rec r in Records) r.SaveData(bw);
        }

        internal override List<string> GetIDs(bool lower)
        {
            List<string> list = new List<string>();
            foreach (Rec r in Records) list.AddRange(r.GetIDs(lower));
            return list;
        }

        public override BaseRecord Clone()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        private LocalizedStringDict LoadPluginStrings(LocalizedStringFormat format, string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
                        return LoadPluginStrings(format, reader);
                }
            }
            catch{}
            return new LocalizedStringDict();
        }

        private LocalizedStringDict LoadPluginStrings(LocalizedStringFormat format, BinaryReader reader)
        {
            LocalizedStringDict dict = new LocalizedStringDict();
            int length = reader.ReadInt32();
            int size = reader.ReadInt32(); // size of data section
            var list = new List<Pair<uint, uint>>();
            for (uint i = 0; i < length; ++i)
            {
                uint id = reader.ReadUInt32();
                uint off = reader.ReadUInt32();
                list.Add(new Pair<uint, uint>(id, off));
            }
            long offset = reader.BaseStream.Position;
            byte[] data = new byte[size];
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream(data,0,size,true,false))
            {
                byte[] buffer = new byte[65536];
                int left = size;
                while (left > 0)
                {
                    int read = Math.Min(left, (int)buffer.Length);
                    int nread = reader.BaseStream.Read(buffer, 0, read);
                    if (nread == 0) break;
                    stream.Write(buffer, 0, nread);
                    left -= nread;
                }
            }
            foreach ( var kvp in list )
            {
                int start = (int)kvp.Value;
                int len = 0;
                switch(format)
                {
                    case LocalizedStringFormat.Base:
                        while (data[start+len] != 0) ++len;
                        break;

                    case LocalizedStringFormat.DL:
                    case LocalizedStringFormat.IL:
                        len = BitConverter.ToInt32(data, start) - 1;
                        start = start + sizeof(int);
                        break;
                }
                string str = System.Text.ASCIIEncoding.ASCII.GetString(data, start, len);
                dict.Add(kvp.Key, str);
            }
            return dict;
        }

        private void SavePluginStrings(LocalizedStringFormat format, LocalizedStringDict strings, string path)
        {
            try
            {
                using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
                    SavePluginStrings(format, strings, writer);
            }
            catch { }
        }
        private void SavePluginStrings(LocalizedStringFormat format, LocalizedStringDict strings, BinaryWriter writer)
        {
            var list = new List<Pair<uint, uint>>();

            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            using (System.IO.BinaryWriter memWriter = new System.IO.BinaryWriter(stream))
            {
                foreach (KeyValuePair<uint, string> kvp in strings)
                {
                    list.Add(new Pair<uint, uint>(kvp.Key, (uint)stream.Position));
                    byte[] data = System.Text.ASCIIEncoding.ASCII.GetBytes(kvp.Value);
                    switch (format)
                    {
                        case LocalizedStringFormat.Base:
                            memWriter.Write(data, 0, data.Length);
                            memWriter.Write((byte)0);
                            break;

                        case LocalizedStringFormat.DL:
                        case LocalizedStringFormat.IL:
                            memWriter.Write(data.Length+1);
                            memWriter.Write(data, 0, data.Length);
                            memWriter.Write((byte)0);
                            break;
                    }
                }
                writer.Write(strings.Count);
                writer.Write((int)stream.Length);
                foreach (var item in list)
                {
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }

                stream.Position = 0;
                byte[] buffer = new byte[65536];
                int left = (int)stream.Length;
                while (left > 0)
                {
                    int read = Math.Min(left, (int)buffer.Length);
                    int nread = stream.Read(buffer, 0, read);
                    if (nread == 0) break;
                    writer.Write(buffer, 0, nread);
                    left -= nread;
                }
            }
        }

    }


    [Persistable(Flags = PersistType.DeclaredOnly), Serializable]
    public abstract class Rec : BaseRecord
    {
        protected Rec() { }

        protected Rec(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
		}

        [Persistable]
        private string descriptiveName;
        public virtual string DescriptiveName
        {
            get { return descriptiveName == null ? Name : (Name + descriptiveName); }
            set { descriptiveName = value; }
        }
    }

    [Persistable(Flags = PersistType.DeclaredOnly), Serializable]
    public sealed class GroupRecord : Rec
    {
        [Persistable]
        public readonly List<Rec> Records = new List<Rec>();
        [Persistable]
        private readonly byte[] data;
        [Persistable]
        public uint groupType;
        [Persistable]
        public uint dateStamp;
        [Persistable]
        public uint flags;

        public string ContentsType
        {
            get { return groupType == 0 ? "" + (char)data[0] + (char)data[1] + (char)data[2] + (char)data[3] : ""; }
        }

        public override long Size
        {
            get { long size = 24; foreach (Rec rec in Records) size += rec.Size2; return size; }
        }
        public override long Size2 { get { return Size; } }

        public override bool DeleteRecord(BaseRecord br)
        {
            Rec r = br as Rec;
            if (r == null) return false;
            return Records.Remove(r);
        }
        public override void AddRecord(BaseRecord br)
        {
            Rec r = br as Rec;
            if (r == null) throw new TESParserException("Record to add was not of the correct type." +
                   Environment.NewLine + "Groups can only hold records or other groups.");
            Records.Add(r);
        }
        public override void InsertRecord(int idx, BaseRecord br)
        {
            Rec r = br as Rec;
            if (r == null) throw new TESParserException("Record to add was not of the correct type." +
                   Environment.NewLine + "Groups can only hold records or other groups.");
            Records.Insert(idx, r);
        }

        public override bool While(Predicate<BaseRecord> action)
        {
            if (!base.While(action))
                return false;
            foreach (var r in this.Records)
                if (!r.While(action))
                    return false;
            return true;
        }

        public override void ForEach(Action<BaseRecord> action)
        {
            base.ForEach(action);
            foreach (var r in this.Records) r.ForEach(action);
        }

        GroupRecord() { }

        GroupRecord(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
		}


        internal GroupRecord(uint Size, BinaryReader br, bool Oblivion, string[] recFilter, bool filterAll)
        {
            Name = "GRUP";
            data = br.ReadBytes(4);
            groupType = br.ReadUInt32();
            dateStamp = br.ReadUInt32();
            string contentType = groupType == 0 ? System.Text.Encoding.ASCII.GetString(data) : "";
            if (!Oblivion) flags = br.ReadUInt32();
            uint AmountRead = 0;
            while (AmountRead < Size - (Oblivion ? 20 : 24))
            {
                string s = Plugin.ReadRecName(br);
                uint recsize = br.ReadUInt32();

                if (s == "GRUP")
                {
                    bool skip = filterAll || (recFilter != null && Array.IndexOf(recFilter, contentType) >= 0);
                    GroupRecord gr = new GroupRecord(recsize, br, Oblivion, recFilter, skip);
                    AmountRead += recsize;

                    if (!filterAll) Records.Add(gr);
                }
                else
                {
                    bool skip = filterAll || (recFilter != null && Array.IndexOf(recFilter, s) >= 0);
                    if (skip)
                    {
                        long size = (recsize + (Oblivion ? 12 : 16));
                        //if ((br.ReadUInt32() & 0x00040000) > 0) size += 4;
                        br.BaseStream.Position += size;// just read past the data
                        AmountRead += (uint)(recsize + (Oblivion ? 20 : 24));
                    }
                    else
                    {
                        Record r = new Record(s, recsize, br, Oblivion);
                        AmountRead += (uint)(recsize + (Oblivion ? 20 : 24));
                        Records.Add(r);
                    }
                }
            }
            if (AmountRead > (Size - (Oblivion ? 20 : 24)))
            {
                throw new TESParserException("Record block did not match the size specified in the group header");
            }
            if (groupType == 0)
            {
                string name = System.Text.Encoding.ASCII.GetString(data, 0, 4);
                string desc = string.Format(" ({0})", name);
                RecordStructure rec;
                if (RecordStructure.Records.TryGetValue(name, out rec))
                {
                    if (rec.description != name)
                        desc += " - " + rec.description;
                }
                DescriptiveName = desc;

            }
        }

        public GroupRecord(string data)
        {
            Name = "GRUP";
            this.data = new byte[4];
            for (int i = 0; i < 4; i++) this.data[i] = (byte)data[i];
            string desc = string.Format(" ({0})", data);
            if (groupType == 0)
            {
                RecordStructure rec;
                if (RecordStructure.Records.TryGetValue(data, out rec))
                {
                    if (rec.description != data)
                        desc += " - " + rec.description;
                }
            }
            DescriptiveName = desc;
        }

        private GroupRecord(GroupRecord gr)
        {
            Name = "GRUP";
            data = (byte[])gr.data.Clone();
            groupType = gr.groupType;
            dateStamp = gr.dateStamp;
            flags = gr.flags;
            Records = new List<Rec>(gr.Records.Count);
            for (int i = 0; i < gr.Records.Count; i++) Records.Add((Rec)gr.Records[i].Clone());
            Name = gr.Name;
            DescriptiveName = gr.DescriptiveName;
        }

        private string GetSubDesc()
        {
            switch (groupType)
            {
                case 0:
                    return "(Contains: " + (char)data[0] + (char)data[1] + (char)data[2] + (char)data[3] + ")";
                case 2:
                case 3:
                    return "(Block number: " + (data[0] + data[1] * 256 + data[2] * 256 * 256 + data[3] * 256 * 256 * 256).ToString() + ")";
                case 4:
                case 5:
                    return "(Coordinates: [" + (data[0] + data[1] * 256) + ", " + data[2] + data[3] * 256 + "])";
                case 1:
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                    return "(Parent FormID: 0x" + data[3].ToString("x2") + data[2].ToString("x2") + data[1].ToString("x2") + data[0].ToString("x2") + ")";
            }
            return null;
        }

        public override string GetDesc()
        {
            string desc = "[Record group]" + Environment.NewLine + "Record type: ";
            switch (groupType)
            {
                case 0:
                    desc += "Top " + GetSubDesc();
                    break;
                case 1:
                    desc += "World children " + GetSubDesc();
                    break;
                case 2:
                    desc += "Interior Cell Block " + GetSubDesc();
                    break;
                case 3:
                    desc += "Interior Cell Sub-Block " + GetSubDesc();
                    break;
                case 4:
                    desc += "Exterior Cell Block " + GetSubDesc();
                    break;
                case 5:
                    desc += "Exterior Cell Sub-Block " + GetSubDesc();
                    break;
                case 6:
                    desc += "Cell Children " + GetSubDesc();
                    break;
                case 7:
                    desc += "Topic Children " + GetSubDesc();
                    break;
                case 8:
                    desc += "Cell Persistent Children " + GetSubDesc();
                    break;
                case 9:
                    desc += "Cell Temporary Children " + GetSubDesc();
                    break;
                case 10:
                    desc += "Cell Visible Distant Children " + GetSubDesc();
                    break;
                default:
                    desc += "Unknown";
                    break;
            }
            return desc + Environment.NewLine +
                "Records: " + Records.Count.ToString() + Environment.NewLine +
                "Size: " + Size.ToString() + " bytes (including header)";
        }

        internal override void SaveData(BinaryWriter bw)
        {
            WriteString(bw, "GRUP");
            bw.Write((uint)Size);
            bw.Write(data);
            bw.Write(groupType);
            bw.Write(dateStamp);
            bw.Write(flags);
            foreach (Rec r in Records) r.SaveData(bw);
        }

        internal override List<string> GetIDs(bool lower)
        {
            List<string> list = new List<string>();
            foreach (Record r in Records) list.AddRange(r.GetIDs(lower));
            return list;
        }

        public override BaseRecord Clone()
        {
            return new GroupRecord(this);
        }

        public byte[] GetData() { return (byte[])data.Clone(); }
        internal byte[] GetReadonlyData() { return data; }
        public void SetData(byte[] data)
        {
            if (data.Length != 4) throw new ArgumentException("data length must be 4");
            for (int i = 0; i < 4; i++) this.data[i] = data[i];
        }
    }

    [Persistable(Flags = PersistType.DeclaredOnly), Serializable]
    public sealed class Record : Rec, ISerializable, IDeserializationCallback
    {
        public readonly TESVSnip.Collections.Generic.AdvancedList<SubRecord> SubRecords  ;
        [Persistable]
        public uint Flags1;
        [Persistable]
        public uint Flags2;
        [Persistable]
        public uint Flags3;
        [Persistable]
        public uint FormID;

        static Dictionary<string, Func<string>> overrideFunctionsByType = new Dictionary<string, Func<string>>();
        Func<string> descNameOverride;

        public override long Size
        {
            get
            {
                long size = 0;
                foreach (SubRecord rec in SubRecords) size += rec.Size2;
                return size;
            }
        }
        public override long Size2
        {
            get
            {
                long size = 24;
                foreach (SubRecord rec in SubRecords) size += rec.Size2;
                return size;
            }
        }

        public override bool DeleteRecord(BaseRecord br)
        {
            SubRecord sr = br as SubRecord;
            if (sr == null) return false;
            return SubRecords.Remove(sr);
        }

        public override void AddRecord(BaseRecord br)
        {
            SubRecord sr = br as SubRecord;
            if (sr == null) throw new TESParserException("Record to add was not of the correct type." +
                   Environment.NewLine + "Records can only hold Subrecords.");
            SubRecords.Add(sr);
        }
        public override void InsertRecord(int idx, BaseRecord br)
        {
            SubRecord sr = br as SubRecord;
            if (sr == null) throw new TESParserException("Record to add was not of the correct type." +
                   Environment.NewLine + "Records can only hold Subrecords.");
            SubRecords.Insert(idx, sr);
        }

        // due to weird 'bug' in serialization of arrays we do not have access to children yet.
        SubRecord[] serializationItems = null;
        Record(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
            serializationItems = info.GetValue("SubRecords", typeof(SubRecord[])) as SubRecord[];
            SubRecords = new Collections.Generic.AdvancedList<SubRecord>(1);
            descNameOverride = new Func<string>(DefaultDescriptiveName);
            UpdateShortDescription();
		}

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("SubRecords", SubRecords.ToArray());
            TESVSnip.Data.PersistAssist.Serialize(this, info, context);
        }
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (serializationItems != null)
                this.SubRecords.AddRange(serializationItems.OfType<SubRecord>().ToList());
            serializationItems = null;
        }

        internal Record(string name, uint Size, BinaryReader br, bool Oblivion)
        {
            SubRecords = new TESVSnip.Collections.Generic.AdvancedList<SubRecord>(1);
            SubRecords.AllowSorting = false;
            Name = name;
            Flags1 = br.ReadUInt32();
            FormID = br.ReadUInt32();
            Flags2 = br.ReadUInt32();
            if (!Oblivion) Flags3 = br.ReadUInt32();
            if ((Flags1 & 0x00040000) > 0)
            {
                Flags1 ^= 0x00040000;
                uint newSize = br.ReadUInt32();
                br = Decompress(br, (int)(Size - 4), (int)newSize);
                Size = newSize;
            }
            uint AmountRead = 0;
            while (AmountRead < Size)
            {
                string s = ReadRecName(br);
                uint i = 0;
                if (s == "XXXX")
                {
                    br.ReadUInt16();
                    i = br.ReadUInt32();
                    s = ReadRecName(br);
                }
                SubRecord r = new SubRecord(this, s, br, i);
                AmountRead += (uint)(r.Size2);
                SubRecords.Add(r);
            }
            if (AmountRead > Size)
            {
                throw new TESParserException("Subrecord block did not match the size specified in the record header");
            }
            descNameOverride = new Func<string>(DefaultDescriptiveName);
            UpdateShortDescription();
            //br.BaseStream.Position+=Size;
        }

        private Record(Record r)
        {
            SubRecords = new TESVSnip.Collections.Generic.AdvancedList<SubRecord>(r.SubRecords.Count);
            SubRecords.AllowSorting = false;
            foreach (var sr in r.SubRecords.OfType<SubRecord>())
                SubRecords.Add((SubRecord)sr.Clone());
            Flags1 = r.Flags1;
            Flags2 = r.Flags2;
            Flags3 = r.Flags3;
            FormID = r.FormID;
            Name = r.Name;
            DescriptiveName = r.DescriptiveName;
            descNameOverride = new Func<string>(DefaultDescriptiveName);
            UpdateShortDescription();
        }

        public Record()
        {
            Name = "NEW_";
            SubRecords = new TESVSnip.Collections.Generic.AdvancedList<SubRecord>();
            descNameOverride = new Func<string>(DefaultDescriptiveName);
            UpdateShortDescription();
        }

        public override BaseRecord Clone()
        {
            return new Record(this);
        }

        private string DefaultDescriptiveName() { return base.DescriptiveName; }

        public override string DescriptiveName
        {
            get { return descNameOverride(); }
            set { base.DescriptiveName = value; }
        }

        void UpdateShortDescription()
        {
            if (this.Name == "REFR") // temporary hack for references
            {
                var edid = SubRecords.FirstOrDefault( x => x.Name == "EDID" );
                string desc = (edid != null) ? string.Format(" ({0})", edid.GetStrData()) : "";
                //var name = SubRecords.FirstOrDefault( x => x.Name == "NAME" );
                var data = SubRecords.FirstOrDefault(x => x.Name == "DATA");
                if (data != null)
                {
                    desc = string.Format("{0} \t[{1:F0}, {2:F0}]", 
                        desc, data.GetValue<float>(0), data.GetValue<float>(4)
                        );
                }
                DescriptiveName = desc;
            }
            else if (this.Name == "ACHR") // temporary hack for references
            {
                var edid = SubRecords.FirstOrDefault(x => x.Name == "EDID");
                string desc = (edid != null) ? string.Format(" ({0})", edid.GetStrData()) : "";
                var data = SubRecords.FirstOrDefault(x => x.Name == "DATA");
                if (data != null)
                {
                    desc = string.Format("{0} \t[{1:F0}, {2:F0}]",
                        desc, data.GetValue<float>(0), data.GetValue<float>(4)
                        );
                }
                DescriptiveName = desc;
            }
            else if (this.Name == "CELL")
            {
                var edid = SubRecords.FirstOrDefault(x => x.Name == "EDID");
                string desc = (edid != null) ? desc = " (" + edid.GetStrData() + ")" : "";

                var xclc = SubRecords.FirstOrDefault(x => x.Name == "XCLC");
                if (xclc != null)
                {
                    desc = string.Format(" [{1:F0},{2:F0}]\t{0}",
                        desc, xclc.GetValue<int>(0), xclc.GetValue<int>(4)
                        );
                }
                else
                {
                    desc = string.Format(" [Interior]\t{0}", desc);
                }
                DescriptiveName = desc;
            }
            else
            {
                var edid = SubRecords.FirstOrDefault(x => x.Name == "EDID");
                if (edid != null) DescriptiveName = " (" + edid.GetStrData() + ")";
            }
        }

        private string GetBaseDesc()
        {
            return "Type: " + Name + Environment.NewLine +
                "FormID: " + FormID.ToString("x8") + Environment.NewLine +
                "Flags 1: " + Flags1.ToString("x8") +
                (Flags1 == 0 ? "" : " (" + FlagDefs.GetRecFlags1Desc(Flags1) + ")") +
                Environment.NewLine +
                "Flags 2: " + Flags2.ToString("x8") + Environment.NewLine +
                "Flags 3: " + Flags3.ToString("x8") + Environment.NewLine +
                "Subrecords: " + SubRecords.Count.ToString() + Environment.NewLine +
                "Size: " + Size.ToString() + " bytes (excluding header)";
        }

        private string GetLocalizedString(dLStringLookup strLookup)
        {
            return default(string);
        }

        public override string GetDesc()
        {
            return "[Record]" + Environment.NewLine + GetBaseDesc();
        }

        public override void GetFormattedData(RTFBuilder rb, SelectionContext context)
        {
            rb.FontStyle(FontStyle.Bold).FontSize(rb.DefaultFontSize + 2).AppendLine("[Record]");
            rb.AppendLineFormat("Type: \t {0}", Name);
            rb.AppendLineFormat("FormID: \t {0:X8}", FormID);
            rb.AppendLineFormat("Flags 1: \t {0:X8}", Flags1);
            if (Flags1 != 0) rb.AppendLineFormat(" ({0})", FlagDefs.GetRecFlags1Desc(Flags1));
            rb.AppendLineFormat("Flags 2: \t {0:X8}", Flags2);
            rb.AppendLineFormat("Flags 3: \t {0:X8}", Flags3);
            rb.AppendLineFormat("Subrecords: \t {0}", SubRecords.Count);
            rb.AppendLineFormat("Size: \t {0:N0}", Size);
            rb.AppendLine();

            try
            {
                rb.FontStyle(FontStyle.Bold).FontSize(rb.DefaultFontSize).AppendLine("[Formatted information]");

                context = context.Clone();
                context.Record = this;
                RecordStructure rec;
                if (!RecordStructure.Records.TryGetValue(Name, out rec))
                    return;
                rb.FontStyle(FontStyle.Bold).ForeColor(KnownColor.DarkBlue).FontSize(rb.DefaultFontSize+2).AppendLine(rec.description);
                foreach (var subrec in SubRecords)
                {
                    if (subrec.Structure == null || subrec.Structure.elements == null || subrec.Structure.notininfo)
                        continue;
                    context.SubRecord = subrec;
                    rb.AppendLine();
                    subrec.GetFormattedData(rb, context);
                }
            }
            catch
            {
                rb.ForeColor(KnownColor.Red).Append("Warning: An error occurred while processing the record. It may not conform to the structure defined in RecordStructure.xml");
            }
        }

        internal string GetDesc(SelectionContext context)
        {
            string start = "[Record]" + Environment.NewLine + GetBaseDesc();
            string end;
            try
            {
                end = GetExtendedDesc(context);
            }
            catch
            {
                end = "Warning: An error occurred while processing the record. It may not conform to the structure defined in RecordStructure.xml";
            }
            if (end == null) return start;
            else return start + Environment.NewLine + Environment.NewLine + "[Formatted information]" + Environment.NewLine + end;
        }

        public override string Name
        {
            get
            {
                return base.Name;
            }
            set
            {
                base.Name = value;
            }
        }

        #region Extended Description
        private string GetExtendedDesc(SelectionContext selectContext)
        {
            var context = selectContext.Clone();
            try
            {
                context.Record = this;
                RecordStructure rec;
                if (!RecordStructure.Records.TryGetValue(Name, out rec))
                    return "";
                var s = new System.Text.StringBuilder();
                s.AppendLine(rec.description);
                foreach (var subrec in SubRecords)
                {
                    if (subrec.Structure == null)
                        continue;
                    if (subrec.Structure.elements == null)
                        return s.ToString();
                    if (subrec.Structure.notininfo)
                        continue;

                    context.SubRecord = subrec;
                    s.AppendLine();
                    s.Append(subrec.GetFormattedData(context));
                }
                return s.ToString();
            }
            finally
            {
                context.Record = null;
                context.SubRecord = null;
                context.Conditions.Clear();
            }
        }

        #endregion


        internal override void SaveData(BinaryWriter bw)
        {
            WriteString(bw, Name);
            bw.Write((uint)Size);
            bw.Write(Flags1);
            bw.Write(FormID);
            bw.Write(Flags2);
            bw.Write(Flags3);
            foreach (SubRecord sr in SubRecords) sr.SaveData(bw);
        }

        internal override List<string> GetIDs(bool lower)
        {
            List<string> list = new List<string>();
            foreach (SubRecord sr in SubRecords) list.AddRange(sr.GetIDs(lower));
            return list;
        }
    }

    [Persistable(Flags = PersistType.DeclaredOnly), Serializable]
    public sealed class SubRecord : BaseRecord
    {
        private Record Owner;

        [Persistable]
        private byte[] Data;

        public override long Size { get { return Data.Length; } }
        public override long Size2 { get { return 6 + Data.Length + (Data.Length > ushort.MaxValue ? 10 : 0); } }

        public byte[] GetData()
        {
            return (byte[])Data.Clone();
        }
        internal byte[] GetReadonlyData() { return Data; }
        public void SetData(byte[] data)
        {
            Data = (byte[])data.Clone();
        }
        public void SetStrData(string s, bool nullTerminate)
        {
            if (nullTerminate) s += '\0';
            Data = System.Text.Encoding.Default.GetBytes(s);
        }

        SubRecord(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
		}

        internal SubRecord(Record rec, string name, BinaryReader br, uint size)
        {
            Owner = rec;
            Name = name;
            if (size == 0) size = br.ReadUInt16(); else br.BaseStream.Position += 2;
            Data = new byte[size];
            br.Read(Data, 0, Data.Length);
        }

        private SubRecord(SubRecord sr)
        {
            Owner = null;
            Name = sr.Name;
            Data = (byte[])sr.Data.Clone();
        }

        public override BaseRecord Clone()
        {
            return new SubRecord(this);
        }

        public SubRecord()
        {
            Name = "NEW_";
            Data = new byte[0];
            Owner = null;
        }

        internal override void SaveData(BinaryWriter bw)
        {
            if (Data.Length > ushort.MaxValue)
            {
                WriteString(bw, "XXXX");
                bw.Write((ushort)4);
                bw.Write(Data.Length);
                WriteString(bw, Name);
                bw.Write((ushort)0);
                bw.Write(Data, 0, Data.Length);
            }
            else
            {
                WriteString(bw, Name);
                bw.Write((ushort)Data.Length);
                bw.Write(Data, 0, Data.Length);
            }
        }

        public override string GetDesc()
        {
            return "[Subrecord]" + Environment.NewLine +
                "Name: " + Name + Environment.NewLine +
                "Size: " + Size.ToString() + " bytes (Excluding header)";
        }
        public override bool DeleteRecord(BaseRecord br) { return false; }
        public override void AddRecord(BaseRecord br)
        {
            throw new TESParserException("Subrecords cannot contain additional data.");
        }
        public string GetStrData()
        {
            string s = "";
            foreach (byte b in Data)
            {
                if (b == 0) break;
                s += (char)b;
            }
            return s;
        }
        public string GetStrData(int id)
        {
            string s = "";
            foreach (byte b in Data)
            {
                if (b == 0) break;
                s += (char)b;
            }
            return s;
        }
        public string GetHexData()
        {
            string s = "";
            foreach (byte b in Data) s += b.ToString("X").PadLeft(2, '0') + " ";
            return s;
        }

        public string Description
        {
            get { return this.Structure!= null ? this.Structure.desc : ""; }
        }

        public bool IsValid
        {
            get { return this.Structure != null && (this.Structure.size == 0 || this.Structure.size == this.Size); }
        }
        
        internal SubrecordStructure Structure { get; private set; }

        internal void AttachStructure(SubrecordStructure ss)
        {
            this.Structure = ss;
        }
        internal void DetachStructure()
        {
            this.Structure = null;
        }

        internal string GetFormattedData(SelectionContext context)
        {
            var sb = new System.Text.StringBuilder();
            GetFormattedData(sb, context);
            return sb.ToString();
        }

        #region Get Formatted Data
        public override void GetFormattedData(System.Text.StringBuilder s, SelectionContext context)
        {
            SubrecordStructure ss = this.Structure;
            if (ss == null)
                return;

            dFormIDLookupI formIDLookup = context.formIDLookup;
            dLStringLookup strLookup = context.strLookup;
            dFormIDLookupR formIDLookupR = context.formIDLookupR;

            int offset = 0;
            s.AppendFormat("{0} ({1})", ss.name, ss.desc);
            s.AppendLine();
            try
            {
                for (int eidx = 0, elen = 1; eidx < ss.elements.Length; eidx += elen)
                {
                    var sselem = ss.elements[eidx];
                    bool repeat = sselem.repeat > 0;
                    elen = sselem.repeat > 1 ? sselem.repeat : 1;
                    do
                    {
                        for (int eoff = 0; eoff < elen && offset < Data.Length; ++eoff)
                        {
                            sselem = ss.elements[eidx + eoff];

                            if (offset == Data.Length && eidx == ss.elements.Length - 1 && sselem.optional) break;
                            if (!sselem.notininfo) s.Append(sselem.name).Append(": ");

                            switch (sselem.type)
                            {
                                case ElementValueType.Int:
                                    {

                                        string tmps = TypeConverter.h2si(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]).ToString("X8"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1])
                                                        s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                uint val = TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]);
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0)
                                                    s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset += 4;
                                    } break;
                                case ElementValueType.UInt:
                                    {
                                        string tmps = TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append( TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]).ToString("X8") );
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1])
                                                        s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                uint val = TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]);
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0)
                                                    s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset += 4;
                                    }
                                    break;
                                case ElementValueType.Short:
                                    {
                                        string tmps = TypeConverter.h2ss(Data[offset], Data[offset + 1]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(TypeConverter.h2ss(Data[offset], Data[offset + 1]).ToString("X4"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1]) s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                uint val = TypeConverter.h2s(Data[offset], Data[offset + 1]);
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0)
                                                    s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset += 2;
                                    }
                                    break;
                                case ElementValueType.UShort:
                                    {
                                        string tmps = TypeConverter.h2s(Data[offset], Data[offset + 1]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(TypeConverter.h2s(Data[offset], Data[offset + 1]).ToString("X4"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1]) s.Append(" (").Append(sselem.options[k]).Append(")");
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                uint val = TypeConverter.h2s(Data[offset], Data[offset + 1]);
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0)
                                                    s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset += 2;
                                    }
                                    break;
                                case ElementValueType.Byte:
                                    {
                                        string tmps = Data[offset].ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(Data[offset].ToString("X2"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1]) 
                                                        s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                int val = Data[offset];
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0) s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset++;
                                    }
                                    break;
                                case ElementValueType.SByte:
                                    {
                                        string tmps = ((sbyte)Data[offset]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(Data[offset].ToString("X2"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1])
                                                        s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                int val = Data[offset];
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0) s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset++;
                                    }
                                    break;
                                case ElementValueType.FormID:
                                    {
                                        uint id = TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]);
                                        if (!sselem.notininfo) s.Append(id.ToString("X8"));
                                        if (id != 0 && formIDLookup != null) s.Append(": ").Append(formIDLookup(id));
                                        offset += 4;
                                    } break;
                                case ElementValueType.Float:
                                    if (!sselem.notininfo) s.Append(TypeConverter.h2f(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]));
                                    offset += 4;
                                    break;
                                case ElementValueType.String:
                                    if (!sselem.notininfo)
                                    {
                                        while (Data[offset] != 0) s.Append((char)Data[offset++]);
                                    }
                                    else
                                    {
                                        while (Data[offset] != 0) offset++;
                                    }
                                    offset++;
                                    break;
                                case ElementValueType.fstring:
                                    if (!sselem.notininfo) s.Append(GetStrData());
                                    offset += Data.Length - offset;
                                    break;
                                case ElementValueType.Blob:
                                    if (!sselem.notininfo) s.Append(TypeConverter.GetHexData(Data, offset, Data.Length - offset));
                                    offset += Data.Length - offset;
                                    break;
                                case ElementValueType.BString:
                                    {
                                        int len = TypeConverter.h2s(Data[offset], Data[offset + 1]);
                                        if (!sselem.notininfo)
                                            s.Append(System.Text.Encoding.ASCII.GetString(Data, offset + 2, len));
                                        offset += (2 + len);
                                    }
                                    break;
                                case ElementValueType.LString:
                                    {
                                        // Try to guess if string or string index.  Do not know if the external string checkbox is set or not in this code
                                        int left = Data.Length - offset;
                                        var data = new ArraySegment<byte>(Data, offset, left);
                                        bool isString = TypeConverter.IsLikelyString(data);
                                        uint id = TypeConverter.h2i(data);
                                        string lvalue = strLookup(id);
                                        if (!string.IsNullOrEmpty(lvalue) || !isString)
                                        {
                                            if (!sselem.notininfo) s.Append(id.ToString("X8"));
                                            if (strLookup != null) s.Append(": ").Append(lvalue);
                                            offset += 4;
                                        }
                                        else
                                        {
                                            if (!sselem.notininfo)
                                                while (Data[offset] != 0) s.Append((char)Data[offset++]);
                                            else
                                                while (Data[offset] != 0) offset++;
                                            offset++;
                                        }
                                    } break;
                                case ElementValueType.Str4:
                                    {
                                        if (!sselem.notininfo)
                                            s.Append(System.Text.Encoding.ASCII.GetString(Data, offset, 4));
                                        offset += 4;
                                    }
                                    break;
                                default:
                                    throw new ApplicationException();
                            }
                            if (!sselem.notininfo) s.AppendLine();
                        }
                    } while (repeat && offset < Data.Length);
                }

                if (offset < Data.Length)
                {
                    s.AppendLine();
                    s.AppendLine("Remaining Data: ");
                    s.Append(TypeConverter.GetHexData(Data, offset, Data.Length - offset));
                }
            }
            catch
            {
                s.AppendLine("Warning: Subrecord doesn't seem to match the expected structure");
            }
        }

        public static RTFBuilderbase AppendLink(RTFBuilderbase s, string text, string hyperlink)
        {
            if (global::TESVSnip.Properties.Settings.Default.DisableHyperlinks)
                s.Append(text);
            else
                s.AppendLink(text, hyperlink);
            return s;
        }

        public override void GetFormattedData(RTF.RTFBuilder s, SelectionContext context)
        {
            SubrecordStructure ss = this.Structure;
            if (ss == null)
                return;

            dFormIDLookupI formIDLookup = context.formIDLookup;
            dLStringLookup strLookup = context.strLookup;
            dFormIDLookupR formIDLookupR = context.formIDLookupR;

            int offset = 0;

            s.FontSize(s.DefaultFontSize+1).FontStyle(FontStyle.Bold).AppendFormat("{0} ({1})", ss.name, ss.desc);
            s.AppendLine();
            try
            {
                for (int eidx = 0, elen = 1; eidx < ss.elements.Length; eidx += elen)
                {
                    var sselem = ss.elements[eidx];
                    bool repeat = sselem.repeat > 0;
                    elen = sselem.repeat > 1 ? sselem.repeat : 1;
                    do
                    {
                        for (int eoff = 0; eoff < elen && offset < Data.Length; ++eoff)
                        {
                            sselem = ss.elements[eidx + eoff];

                            if (offset == Data.Length && eidx == ss.elements.Length - 1 && sselem.optional) break;
                            if (!sselem.notininfo) 
                                s.FontStyle(FontStyle.Bold).Append(sselem.name).Append(":\t");

                            switch (sselem.type)
                            {
                                case ElementValueType.Int:
                                    {

                                        string tmps = TypeConverter.h2si(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]).ToString("X8"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1])
                                                        s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                uint val = TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]);
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0)
                                                    s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset += 4;
                                    } break;
                                case ElementValueType.UInt:
                                    {
                                        string tmps = TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]).ToString("X8"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1])
                                                        s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                uint val = TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]);
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0)
                                                    s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset += 4;
                                    }
                                    break;
                                case ElementValueType.Short:
                                    {
                                        string tmps = TypeConverter.h2ss(Data[offset], Data[offset + 1]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(TypeConverter.h2ss(Data[offset], Data[offset + 1]).ToString("X4"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1]) s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                uint val = TypeConverter.h2s(Data[offset], Data[offset + 1]);
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0)
                                                    s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset += 2;
                                    }
                                    break;
                                case ElementValueType.UShort:
                                    {
                                        string tmps = TypeConverter.h2s(Data[offset], Data[offset + 1]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(TypeConverter.h2s(Data[offset], Data[offset + 1]).ToString("X4"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1]) s.Append(" (").Append(sselem.options[k]).Append(")");
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                uint val = TypeConverter.h2s(Data[offset], Data[offset + 1]);
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0)
                                                    s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset += 2;
                                    }
                                    break;
                                case ElementValueType.Byte:
                                    {
                                        string tmps = Data[offset].ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(Data[offset].ToString("X2"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1])
                                                        s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                int val = Data[offset];
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0) s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset++;
                                    }
                                    break;
                                case ElementValueType.SByte:
                                    {
                                        string tmps = ((sbyte)Data[offset]).ToString();
                                        if (!sselem.notininfo)
                                        {
                                            if (sselem.hexview) s.Append(Data[offset].ToString("X2"));
                                            else s.Append(tmps);
                                            if (sselem.options != null)
                                            {
                                                for (int k = 0; k < sselem.options.Length; k += 2)
                                                {
                                                    if (tmps == sselem.options[k + 1])
                                                        s.AppendFormat(" ({0})", sselem.options[k]);
                                                }
                                            }
                                            else if (sselem.flags != null)
                                            {
                                                int val = Data[offset];
                                                var tmp2 = new System.Text.StringBuilder();
                                                for (int k = 0; k < sselem.flags.Length; k++)
                                                {
                                                    if ((val & (1 << k)) != 0)
                                                    {
                                                        if (tmp2.Length > 0) tmp2.Append(", ");
                                                        tmp2.Append(sselem.flags[k]);
                                                    }
                                                }
                                                if (tmp2.Length > 0) s.AppendFormat(" ({0})", tmp2);
                                            }
                                        }
                                        offset++;
                                    }
                                    break;
                                case ElementValueType.FormID:
                                    {
                                        uint id = TypeConverter.h2i(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]);
                                        if (!sselem.notininfo)
                                        {
                                            var strid = id.ToString("X8");
                                            if (id != 0 && formIDLookupR != null)
                                            {
                                                var rec = formIDLookupR(id);
                                                if (rec != null)
                                                {
                                                    AppendLink(s, strid, string.Format("{0}:{1}", rec.Name, strid));
                                                    var strval = rec.DescriptiveName;
                                                    if (!string.IsNullOrEmpty(strval))
                                                        s.Append(":\t").Append(strval);
                                                    else
                                                        s.Append(":\t").Append(formIDLookup(id));
                                                    var full = rec.SubRecords.FirstOrDefault(x => x.Name == "FULL");
                                                    if (full != null)
                                                    {
                                                        var data = new ArraySegment<byte>(full.Data, 0, full.Data.Length);
                                                        bool isString = TypeConverter.IsLikelyString(data);
                                                        string lvalue = (isString) 
                                                            ? full.GetStrData()
                                                            : strLookup != null 
                                                            ? strLookup(TypeConverter.h2i(data))
                                                            : null;
                                                        if (string.IsNullOrEmpty(lvalue)) 
                                                            s.Append("\t").Append(lvalue);
                                                    }
                                                }
                                                else if (formIDLookup != null)
                                                {
                                                    var strval = formIDLookup(id);
                                                    AppendLink(s, strid, string.Format("XXXX:{0}", strid));
                                                    if (!string.IsNullOrEmpty(strval))
                                                        s.Append(":\t").Append(strval);
                                                    else
                                                        s.Append(":\t").Append(formIDLookup(id));
                                                }
                                                else
                                                {
                                                    AppendLink(s, strid, string.Format("XXXX:{0}", strid));
                                                }
                                            }
                                            else
                                            {
                                                AppendLink(s, strid, string.Format("XXXX:{0}", strid));
                                            }
                                        }
                                        offset += 4;
                                    } break;
                                case ElementValueType.Float:
                                    if (!sselem.notininfo) s.Append(TypeConverter.h2f(Data[offset], Data[offset + 1], Data[offset + 2], Data[offset + 3]));
                                    offset += 4;
                                    break;
                                case ElementValueType.String:
                                    if (!sselem.notininfo)
                                    {
                                        while (Data[offset] != 0) s.Append((char)Data[offset++]);
                                    }
                                    else
                                    {
                                        while (Data[offset] != 0) offset++;
                                    }
                                    offset++;
                                    break;
                                case ElementValueType.fstring:
                                    if (!sselem.notininfo) s.Append(GetStrData());
                                    offset += Data.Length - offset;
                                    break;
                                case ElementValueType.Blob:
                                    if (!sselem.notininfo) s.Append(TypeConverter.GetHexData(Data, offset, Data.Length - offset));
                                    offset += Data.Length - offset;
                                    break;
                                case ElementValueType.BString:
                                    {
                                        int len = TypeConverter.h2s(Data[offset], Data[offset + 1]);
                                        if (!sselem.notininfo)
                                            s.Append(System.Text.Encoding.ASCII.GetString(Data, offset + 2, len));
                                        offset += (2 + len);
                                    }
                                    break;
                                case ElementValueType.LString:
                                    {
                                        // Try to guess if string or string index.  Do not know if the external string checkbox is set or not in this code
                                        int left = Data.Length - offset;
                                        var data = new ArraySegment<byte>(Data, offset, left);
                                        bool isString = TypeConverter.IsLikelyString(data);
                                        uint id = TypeConverter.h2i(data);
                                        string lvalue = strLookup(id);
                                        if (!string.IsNullOrEmpty(lvalue) || !isString)
                                        {
                                            if (!sselem.notininfo) s.Append(id.ToString("X8"));
                                            if (strLookup != null) s.Append(":\t").Append(lvalue);
                                            offset += 4;
                                        }
                                        else
                                        {
                                            if (!sselem.notininfo)
                                                while (Data[offset] != 0) s.Append((char)Data[offset++]);
                                            else
                                                while (Data[offset] != 0) offset++;
                                            offset++;
                                        }
                                    } break;
                                case ElementValueType.Str4:
                                    {
                                        if (!sselem.notininfo)
                                            s.Append(System.Text.Encoding.ASCII.GetString(Data, offset, 4));
                                        offset += 4;
                                    }
                                    break;
                                default:
                                    throw new ApplicationException();
                            }
                            if (!sselem.notininfo) s.AppendLine();
                        }
                    } while (repeat && offset < Data.Length);
                }

                if (offset < Data.Length)
                {
                    s.AppendLine();
                    s.AppendLine("Remaining Data: ");
                    s.Append(TypeConverter.GetHexData(Data, offset, Data.Length - offset));
                }
            }
            catch
            {
                s.AppendLine("Warning: Subrecord doesn't seem to match the expected structure");
            }
        }
        #endregion

        public bool TryGetValue<T>(int offset, out T value)
        {
            value = (T)TypeConverter.GetObject<T>(this.Data, offset);
            return true;
        }

        public T GetValue<T>(int offset)
        {
            T value;
            if (!TryGetValue<T>(offset, out value))
                value = default(T);
            return value;
        }


        internal override List<string> GetIDs(bool lower)
        {
            List<string> list = new List<string>();
            if (Name == "EDID")
            {
                if (lower)
                {
                    list.Add(this.GetStrData().ToLower());
                }
                else
                {
                    list.Add(this.GetStrData());
                }
            }
            return list;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Description) && this.Description != this.Name)
                return this.Name;
            return string.Format("{0}: {1}", this.Name, this.Description);
        }
    }

    internal static class FlagDefs
    {
        public static readonly string[] RecFlags1 = {
            "ESM file",
            null,
            null,
            null,
            null,
            "Deleted",
            null,
            null,
            null,
            "Casts shadows",
            "Quest item / Persistent reference",
            "Initially disabled",
            "Ignored",
            null,
            null,
            "Visible when distant",
            null,
            "Dangerous / Off limits (Interior cell)",
            "Data is compressed",
            "Can't wait",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
        };

        public static string GetRecFlags1Desc(uint flags)
        {
            string desc = "";
            bool b = false;
            for (int i = 0; i < 32; i++)
            {
                if ((flags & (uint)(1 << i)) > 0)
                {
                    if (b) desc += ", ";
                    b = true;
                    desc += (RecFlags1[i] == null ? "Unknown (" + ((uint)(1 << i)).ToString("x") + ")" : RecFlags1[i]);
                }
            }
            return desc;
        }
    }
}
