﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18408
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TESVSnip.Domain.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "11.0.0.0")]
    public sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("English")]
        public string LocalizationName {
            get {
                return ((string)(this["LocalizationName"]));
            }
            set {
                this["LocalizationName"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MonitorStringsFolderForChanges {
            get {
                return ((bool)(this["MonitorStringsFolderForChanges"]));
            }
            set {
                this["MonitorStringsFolderForChanges"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool SaveStringsFiles {
            get {
                return ((bool)(this["SaveStringsFiles"]));
            }
            set {
                this["SaveStringsFiles"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoUpdateNextFormID {
            get {
                return ((bool)(this["AutoUpdateNextFormID"]));
            }
            set {
                this["AutoUpdateNextFormID"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool UseDefaultRecordCompression {
            get {
                return ((bool)(this["UseDefaultRecordCompression"]));
            }
            set {
                this["UseDefaultRecordCompression"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool EnableAutoCompress {
            get {
                return ((bool)(this["EnableAutoCompress"]));
            }
            set {
                this["EnableAutoCompress"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        public uint CompressionLimit {
            get {
                return ((uint)(this["CompressionLimit"]));
            }
            set {
                this["CompressionLimit"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("CELL;DIAL;IMAD;LAND;NAVI;NAVM;NPC_;REGN;WRLD;WTHR")]
        public string AutoCompressRecords {
            get {
                return ((string)(this["AutoCompressRecords"]));
            }
            set {
                this["AutoCompressRecords"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool EnableCompressionLimit {
            get {
                return ((bool)(this["EnableCompressionLimit"]));
            }
            set {
                this["EnableCompressionLimit"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UsePluginRecordCompression {
            get {
                return ((bool)(this["UsePluginRecordCompression"]));
            }
            set {
                this["UsePluginRecordCompression"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("conf")]
        public string SettingsDirectory {
            get {
                return ((string)(this["SettingsDirectory"]));
            }
            set {
                this["SettingsDirectory"] = value;
            }
        }
    }
}
