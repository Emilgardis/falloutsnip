 // This source code was auto-generated by xsd, Version=4.0.30319.1.

using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

namespace TESVSnip.Domain.Data.RecordStructure.Xml
{
    /// <summary>
    /// The group.
    /// </summary>
    /// <remarks>
    /// </remarks>
    [GeneratedCode("xsd", "4.0.30319.1")]
    [Serializable]
    [DebuggerStepThrough]
    [DesignerCategory("code")]
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false)]
    public class Group
    {
        ///// <remarks/>
        // [System.Xml.Serialization.XmlElementAttribute("Subrecord")]
        // public List<Subrecord> Subrecords = new List<Subrecord>();
        ///// <remarks/>
        // [System.Xml.Serialization.XmlElementAttribute("Group")]
        // public List<Group> Groups = new List<Group>();
        [XmlElement("Group", typeof (Group))] 
        [XmlElement("Subrecord", typeof (Subrecord))] 
        public ArrayList Items = new ArrayList();

        /// <summary>
        /// The desc.
        /// </summary>
        /// <remarks>
        /// </remarks>
        [XmlAttribute] 
        [DefaultValue("")] public string 
        desc = string.Empty;

        /// <summary>
        /// The id.
        /// </summary>
        /// <remarks>
        /// </remarks>
        [XmlAttribute]
        [DefaultValue("")]
        public string id = string.Empty;

        /// <summary>
        /// The name.
        /// </summary>
        /// <remarks>
        /// </remarks>
        [XmlAttribute]
        [DefaultValue("")]
        public string name = string.Empty;

        /// <summary>
        /// The optional.
        /// </summary>
        /// <remarks>
        /// </remarks>
        [XmlAttribute]
        [DefaultValue(0)]
        public int optional;

        /// <summary>
        /// The repeat.
        /// </summary>
        /// <remarks>
        /// </remarks>
        [XmlAttribute]
        [DefaultValue(0)]
        public int repeat;

        [XmlIgnore]
        public IEnumerable<Group> Groups
        {
            get
            {
                return this.Items.OfType<Group>();
            }
        }

        [XmlIgnore]
        public IEnumerable<Subrecord> Subrecords
        {
            get
            {
                return this.Items.OfType<Subrecord>();
            }
        }

    }
}
