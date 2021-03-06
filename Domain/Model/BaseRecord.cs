using FalloutSnip.Domain.Services;

namespace FalloutSnip.Domain.Model
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using FalloutSnip.Framework.Persistence;

    [Persistable(Flags = PersistType.DeclaredOnly)]
    [Serializable]
    public abstract class BaseRecord : PersistObject, ICloneable, ISerializable, IRecord
    {
        private static readonly BaseRecord[] emptyList = new BaseRecord[0];

        protected BaseRecord()
        {
        }

        protected BaseRecord(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        ///   Shared Record Change Notifications
        /// </summary>
        public static event EventHandler<RecordChangeEventArgs> ChildListChanged;

        /// <summary>
        ///   Shared Record Change Notifications
        /// </summary>
        public static event EventHandler<RecordChangeEventArgs> RecordDeleted;

        /// <summary>
        ///   Shared Record Change Notifications
        /// </summary>
        public static event EventHandler<RecordChangeEventArgs> RecordDescChanged;

        public static bool HoldUpdates { get; set; }

        public virtual string DescriptiveName
        {
            get { return this.Name; }
        }

        [Persistable]
        public virtual string Name { get; set; }

        [Persistable]
        public virtual string PluginPath { get; set; }

        public abstract BaseRecord Parent { get; internal set; }

        public virtual IList Records
        {
            get { return emptyList; }
        }

        /// <summary>
        ///   Size of Data portion
        /// </summary>
        public abstract long Size { get; }

        /// <summary>
        ///   Size of Data + Headers
        /// </summary>
        public abstract long Size2 { get; }

        public abstract void AddRecord(BaseRecord br);

        public virtual void AddRecords(IEnumerable<BaseRecord> br)
        {
            foreach (var r in br)
            {
                this.AddRecord(r);
            }
        }

        public abstract BaseRecord Clone();

        public virtual BaseRecord Clone(bool recursive)
        {
            return this.Clone();
        }

        public abstract bool DeleteRecord(BaseRecord br);

        public virtual bool DeleteRecords(IEnumerable<BaseRecord> br)
        {
            return br.Aggregate(false, (current, r) => current | this.DeleteRecord(r));
        }

        public virtual IEnumerable<BaseRecord> Enumerate()
        {
            return this.Enumerate(x => true);
        }

        public virtual IEnumerable<BaseRecord> Enumerate(Predicate<BaseRecord> match)
        {
            if (match(this))
            {
                yield return this;
            }
        }

        public virtual void ForEach(Action<BaseRecord> action)
        {
            action(this);
        }

        public virtual int IndexOf(BaseRecord br)
        {
            return -1;
        }

        public virtual void InsertRecord(int index, BaseRecord br)
        {
            this.AddRecord(br);
        }

        public virtual void InsertRecords(int index, IEnumerable<BaseRecord> br)
        {
            foreach (var r in br.Reverse())
            {
                this.InsertRecord(index, r);
            }
        }

        public virtual void SetDescription(string value)
        {
        }

        public override string ToString()
        {
            return this.DescriptiveName;
        }

        public virtual void UpdateShortDescription()
        {
        }

        // internal iterators
        public virtual bool While(Predicate<BaseRecord> action)
        {
            return action(this);
        }

        object ICloneable.Clone()
        {
            return this.Clone(recursive: true);
        }

        internal abstract List<string> GetIDs(bool lower);

        internal abstract void SaveData(BinaryWriter writer);

        protected static void FireRecordChangeUpdate(object sender, BaseRecord rec)
        {
            if (!HoldUpdates && RecordDescChanged != null)
            {
                RecordDescChanged(sender, new RecordChangeEventArgs(rec));
            }
        }

        protected static void FireRecordDeleted(object sender, BaseRecord rec)
        {
            if (!HoldUpdates && RecordDeleted != null)
            {
                RecordDeleted(sender, new RecordChangeEventArgs(rec));
            }
        }

        protected static void FireRecordListUpdate(object sender, BaseRecord rec)
        {
            if (!HoldUpdates && ChildListChanged != null)
            {
                ChildListChanged(sender, new RecordChangeEventArgs(rec));
            }
        }

        protected static string ReadRecName(BinaryReader br)
        {
            try
            {
                return Encoding.Default.GetString(br.ReadBytes(4));
            }
            catch (Exception ex)
            {
                throw new TESParserException("BaseRecord.ReadRecName: " + Environment.NewLine + ex.Message);
            }
        }

        protected static string ReadRecName(byte[] rec)
        {
            try
            {
                return Encoding.Default.GetString(rec,0,4);
            }
            catch (Exception ex)
            {
                throw new TESParserException("BaseRecord.ReadRecName: " + Environment.NewLine + ex.Message);
            }
        }

        protected static void WriteString(BinaryWriter bw, string s)
        {
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                b[i] = (byte)s[i];
            }

            bw.Write(b, 0, s.Length);
        }
    }
}
