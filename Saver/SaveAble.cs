//#define DEBUG
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.ComponentModel;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

//[assembly: Obfuscation(Feature = "Apply to member * when field and private: renaming", Exclude = true)]


namespace Saver
{
    /// <summary>
    /// Save / Load ability 
    /// </summary>
    [Obfuscation(Feature = "Apply to member *k__BackingField when field: renaming", Exclude = true)]
    public class SaveAble
    {
        static int exp_chach = new Func<int>(() =>
        {

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.IsTerminating)
                {
                    MessageBox.Show("!!! UnhandledException in Saver !!!");
                    MessageBox.Show(Environment.StackTrace + "");
                    MessageBox.Show(((Exception)e.ExceptionObject).Message);
                }

            };
            return 0;
        })();
        #region Static settings
        public class Settings
        {
            #region static settings

            /// <summary>
            /// مساوی بودن آبجکت های این کلاس ها با استفاده از آی دی چک نخواهد شد
            /// </summary>
            public static List<Type> IgnorUIDs = new List<Type>();
            /// <summary>
            /// این نوع ها ذخیره نمی شوند
            /// </summary>
            public static List<Type> IgnorTypes = new List<Type>();
            /// <summary>
            /// این آبجک ها ذخیره نمی شوند
            /// </summary>
            public static List<Object> IgnorSaveObjects = new List<Object>();
            /// <summary>
            /// for obfuscation
            /// </summary>
            public static bool use_nrmap = true;
            /// <summary>
            /// save xml file formatted?
            /// </summary>
            public static bool FormatedXML = true;
            [ThreadStatic]
            public static double LoadedVersion;
            /// <summary>
            /// saver version (Assembly)
            /// </summary>
            public static double Version
            {
                get
                {
                    var v = typeof(SaveAble).Assembly.GetName().Version;
                    return v.Major + Convert.ToDouble("0." + v.Minor);
                }
            }
            #endregion

            static object lock_obj = new object();
            /// <summary>
            /// path to the log file, for debug purpose 
            /// </summary>
            public static string debug_file
            {
                get { return Application.ExecutablePath + "." + System.Threading.Thread.CurrentThread.ManagedThreadId + ".saver"; }
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="type"></param>
            /// <param name="Loader">simple for string: (str) => Convert.ToInt32(str)</param>
            /// <param name="Saver">simple for string: (v) => v + ""</param>
            /// <param name="in_first_place"></param>
            /// <returns></returns>
            public static SaveAbleType AddSaveAbleType(Type type, LoadFuncSimple Loader, SaveFuncSimple Saver = null, bool in_first_place = true)
            {
                return AddSaveAbleType(
                    type,
                    (t, n, r, v) => Loader(Settings.GetNodeVal(n, r)),
                    (n, v, r) => Settings.AddNode(n, Saver(v), r),
                    in_first_place);
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="type"></param>
            /// <param name="Loader">simple for string: (t, n, r, v) => Convert.ToInt32(Settings.GetNodeVal(n, r))</param>
            /// <param name="Saver">simple for string: (n, v, r) => Settings.AddNode(n, v + "", r)</param>
            /// <param name="in_first_place"></param>
            /// <returns></returns>
            public static SaveAbleType AddSaveAbleType(Type type, LoadFunc Loader, SaveFunc Saver = null, bool in_first_place = true)
            {
                RegisterTypesIfNot();
                var st = new SaveAbleType() { type = type, Loader = Loader };
                if (Saver != null)
                    st.Saver = Saver;
                if (in_first_place)
                    Types.Insert(0, st);
                else
                    Types.Add(st);
                return st;
            }

            /// <summary>
            /// defined types for custom save/load
            /// </summary>
            public static List<SaveAbleType> Types = new List<SaveAbleType>();
            /// <summary>
            /// Save/Load for object
            /// </summary>
            public static SaveAbleType SaveAbleObjectType;

            public static bool use_waiting_jobs = true;

            #region NRMAP
            internal static Dictionary<Assembly, NRMAP> NRMAPs = new Dictionary<Assembly, NRMAP>();

            internal static NRMAP LoadNRMAP(Assembly assem)
            {
                lock (lock_obj)
                    if (!NRMAPs.ContainsKey(assem))
                    {
                        var file = Path.GetDirectoryName(assem.Location) + "\\" + assem.GetName().Name + ".lib";
                        var file2 = Path.GetTempFileName() + "-setup.rar";
                        try
                        {
                            if (File.Exists(file))
                            {
                                var pass = (typeof(FileInfo).FullName.ToLower()).Substring(0, 16);
                                Tools.DecryptFile(file, file2, pass);
                                try
                                {
                                    var map = new NRMAP(file2);
                                    NRMAPs.Add(assem, map);
                                }
                                catch { NRMAPs.Add(assem, null); }
                            }
                            else
                            {
                                //MessageBox.Show(f.Name + " " + file);
                                NRMAPs.Add(assem, null);
                            }
                        }
                        finally
                        {
                            try
                            { if (File.Exists(file2)) File.Delete(file2); }
                            catch
                            {
#if DEBUG
                                throw;
#endif
                            }
                        }
                    }
                return NRMAPs[assem];
            }
            internal static string NameFromMap(FieldInfo f)
            {
                if (use_nrmap)
                {
                    var map = LoadNRMAP(f.DeclaringType.Assembly);
                    if (map != null)
                        return map.DecryptFiledName(f);
                    else return f.Name;
                }
                return f.Name;
            }
            internal static string TypeFromMap(string T, bool decrypt)
            {
                if (use_nrmap && T != "")
                {
                    var map = LoadNRMAP(Assembly.GetExecutingAssembly());
                    if (map != null)
                    {
                        if (decrypt)
                            return map.Decrypt(T);
                        else
                            return map.Encrypt(T);
                    }
                    else return T;
                }
                return T;
            }
            public static string DecryptStackTrace(string stack, bool debug = false)
            {
                string dbg = "";
                try
                {
                    dbg += "0";
                    if (Settings.NRMAPs.Count == 0)
                    {
                        var asm = Assembly.GetCallingAssembly();
                        Settings.LoadNRMAP(asm);
                        foreach (var a in asm.GetReferencedAssemblies())
                            try
                            {
                                Settings.LoadNRMAP(Assembly.Load(a.FullName));
                            }
                            catch { }
                    }
                    dbg += "1";
                    var res = "";
                    int start = -1;
                    for (int i = 0; i < stack.Length; i++)
                    {
                        if (debug)
                            dbg += ":" + i;
                        if (i < stack.Length - 5 && stack.Substring(i, 4) == " at ")
                        {
                            start = i + 4;
                            res += " @ ";
                        }
                        else if (start > 0)
                        {
                            if ((stack[i] == '(' || stack[i] == '<'))
                            {
                                var res_ = stack.Substring(start, i - start);
                                foreach (var map in Settings.NRMAPs.Values)
                                    if (map != null)
                                    {
                                        res_ = map.Decrypt(res_);
                                        break;
                                    }
                                res += res_ + stack[i];
                                start = -1;
                            }
                        }
                        else
                            res += stack[i];
                    }
                    return res;
                }
                catch (Exception ex)
                {
                    if (debug)
                        MessageBox.Show(ex.Message + "\r\n---\r\n" + ex.StackTrace, dbg);
                    return stack.Replace(" at ", " @ ");
                }
            }

            internal class NRMAP
            {
                internal NRMAP(string fileName)
                { 
                }
                internal string Encrypt(string str)
                { 
                    return str;
                }
                internal string Decrypt(string str)
                { 
                    return str;
                }
                internal string DecryptFiledName(FieldInfo f)
                { 
                    return f.Name;
                }
                internal static bool equal(string str1, string str2)
                {
                    if (str1.Length != str2.Length) return false;
                    return str1.Replace("+", ".") == str2;//.Replace("+", ".");

                }
                internal List<TYPE> Types = new List<TYPE>();
                internal class TYPE
                {
                    public string name, name_;
                    public List<string> membrs = new List<string>();
                    public List<string> membrs_ = new List<string>();
                    public override string ToString()
                    {
                        return name + " (" + membrs.Count + ")";
                    }
                }
            }
            #endregion

            /// <summary>
            /// custom save/load for specific type
            /// </summary>
            public class SaveAbleType
            {
                public Type type;
                /// <summary>
                /// simple for string: (t, n, r, v) => Convert.ToInt32(Settings.GetNodeVal(n, r))
                /// </summary>
                public LoadFunc Loader;
                /// <summary>
                /// simple for string: (n, v, r) => Settings.AddNode(n, v + "", r);
                /// </summary>
                public SaveFunc Saver = (n, v, r) => AddNode(n, v + "", r);

                public bool CanBeUsedForDerivativedTypes = true;
                public static SaveAbleType Get(Type type)
                {
                    RegisterTypesIfNot();
                    foreach (var st in Types)
                        if (st.type == type)
                            return st;
                    if (type.IsSubclassOf(typeof(SaveAble)))
                    {
                        var sat = new SaveAbleType() { type = type, Saver = SaveAbleObjectType.Saver, Loader = SaveAbleObjectType.Loader, CanBeUsedForDerivativedTypes = false };
                        var m1 = type.GetMethod(nameof(CustomSaver));
                        if (m1 != null && m1.DeclaringType == type)
                            sat.Saver = (n, v, r) => Settings.AddNode(n, m1.Invoke(v, new object[] { }) + "", r);
                        var m2 = type.GetMethod(nameof(CustomLoader));
                        if (m2 != null && m2.DeclaringType == type)
                            sat.Loader = (t, n, r, v) =>
                            {
                                v = v ?? CreateInstance(t, n);
                                m2.Invoke(v, new object[] { Settings.GetNodeVal(n, r) }); return v;
                            };
                        Types.Add(sat);
                        return sat;
                    }
                    foreach (var st in Types)
                        if (st.Check(type))
                            return st;
                    Types.Add(new SaveAbleType() { type = type, Saver = SaveAbleObjectType.Saver, Loader = SaveAbleObjectType.Loader, CanBeUsedForDerivativedTypes = false });
                    return Types[Types.Count - 1];
                }
                internal bool Check(Type T)
                {
                    if (CanBeUsedForDerivativedTypes) return T == type || T.IsSubclassOf(type);
                    else return T == type;
                }
            }
            internal static List<FieldInfo> GetFields(Type type)
            {
                var bindingFlags = BindingFlags.NonPublic |
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.FlattenHierarchy;
                if (Settings.saveStaticFields != Settings.StaticFieldsBehavior.DontSaveAtAll)
                    bindingFlags |= BindingFlags.Static;

                var fieldInfos = type.GetFields(bindingFlags);

                // If this class doesn't have a base, don't waste any time
                if (type == typeof(object) || type.BaseType == typeof(object))
                    return Union(fieldInfos);
                else
                {   // Otherwise, collect all types up to the furthest base class
                    var currentType = type.BaseType;
                    var fieldInfoList = new List<FieldInfo>(fieldInfos);
                    fieldInfoList = Union(fieldInfoList);
                    do
                    {
                        fieldInfos = currentType.GetFields(bindingFlags);
                        Union(ref fieldInfoList, fieldInfos);
                        currentType = currentType.BaseType;
                    }
                    while (currentType != typeof(object));
                    return fieldInfoList;
                }
            }

            static bool FieldInfoEquals(FieldInfo x, FieldInfo y)
            {
                return x.DeclaringType == y.DeclaringType && x.Name == y.Name;
            }
            static List<FieldInfo> Union(ICollection<FieldInfo> list)
            {
                var list1 = new List<FieldInfo>() { Capacity = list.Count };
                foreach (var item in list)
                    if (!item.IsLiteral)
                    {
                        /*var found = false;
                        foreach (var item1 in list1)
                            if (FieldInfoEquals(item1, item))
                            {
                                found = true;
                                break;
                            }
                        if (!found)*/
                        list1.Add(item);
                    }
                return list1;
            }

            static void Union(ref List<FieldInfo> list1, FieldInfo[] list2)
            {
                foreach (var item2 in list2)
                    if (!item2.IsLiteral)
                    {
                        var found = false;
                        foreach (var item1 in list1)
                            if (FieldInfoEquals(item1, item2))
                            {
                                found = true;
                                break;
                            }
                        if (!found)
                            list1.Add(item2);
                    }
            }

            #region XML Tools
            public static XmlNode AddNode(string name, string value, XmlNode root, string atribName = "val")
            {
                name = ValidName(name);
                try
                {
                    XmlNode node = root.AppendChild(xml.CreateElement(name));
                    node.Attributes.Append(node.OwnerDocument.CreateAttribute(atribName)).Value = Tools.Encrypt(value);
                    return node;
                }
                catch (Exception ex) { Settings.HandleError(ex, ErrorCode.AddNode, ex.StackTrace + ""); return null; }
            }
            public static void SetAttrib(string name, string value, XmlNode node)
            {
                node.Attributes.Append(node.OwnerDocument.CreateAttribute(name)).Value = Tools.Encrypt(value);
            }
            public static XmlNode GetNode(string name, XmlNode root)
            {
                name = ValidName(name);
                foreach (XmlNode n in root.ChildNodes)
                    if (n.Name == name)
                        return n;
                return root.AppendChild(xml.CreateElement(name));
            }
            public static string GetNodeVal(string name, XmlNode root, string atribName = "val")
            {
                name = ValidName(name);
                foreach (XmlNode n in root.ChildNodes)
                    if (n.Name == name)
                    {
                        if (n.Attributes.GetNamedItem(atribName) != null)
                            return Tools.Decrypt(n.Attributes.GetNamedItem(atribName).Value);
                        else if (n.Attributes.GetNamedItem("null") != null && n.Attributes.GetNamedItem("null").Value == "1")
                            return null;
                    }
                return GetAttrib(name, root);
            }
            public static bool HasNode(string name, XmlNode root)
            {
                name = ValidName(name);
                foreach (XmlNode n in root.ChildNodes)
                    if (n.Name == name)
                        return true;
                return false;
            }
            public static bool HasAttrib(string name, XmlNode node)
            {
                return node.Attributes.GetNamedItem(name) != null;
            }

            public static string GetAttrib(string name, XmlNode node)
            {
                var res = node.Attributes.GetNamedItem(name);
                if (res != null)
                    return Tools.Decrypt(res.Value);
                else
                    return null;
            }
            public static Type GetTpyeFromNode(XmlNode node, Type Default)
            {
                var type = TypeFromMap(GetAttrib("Type", node) + "", false);
                if (type != "")
                {
                    try
                    {
                        var T = Assembly.GetEntryAssembly().GetType(type);
                        if (T != null) return T;
                    }
                    catch { }
                    try
                    {
                        var T = Default.Assembly.GetType(type);
                        if (T != null) return T;
                    }
                    catch { }
                    try
                    {
                        var T = Assembly.GetExecutingAssembly().GetType(type);
                        if (T != null) return T;
                    }
                    catch { }
                    try
                    {
                        var T = Assembly.GetCallingAssembly().GetType(type);
                        if (T != null) return T;
                    }
                    catch { }
                }
                return Default;
            }

            #endregion
            #region Definitions
            public delegate XmlNode SaveFunc(string name, object v, XmlNode root);
            public delegate object LoadFunc(Type type, string name, XmlNode root, object v);
            public delegate string SaveFuncSimple(object value);
            public delegate object LoadFuncSimple(string str);
            #endregion
            #region OPtions
            /// <summary>
            /// when an exception occurs 
            /// </summary>
            [ThreadStatic, DontSave]
#if DEBUG
            public static ExceptionBehavior exceptionBehavior = ExceptionBehavior.ThrowException;
#else
            public static ExceptionBehavior exceptionBehavior = ExceptionBehavior.MessageBox;
#endif
            /// <summary>
            /// Encryption names?
            /// </summary>
            public static bool Encryption = false;
            /// <summary>
            /// Save static fields?
            /// </summary>
            public static StaticFieldsBehavior saveStaticFields = StaticFieldsBehavior.DontSaveAtAll;
            /// <summary>
            /// save array in binary format?
            /// </summary>
            public static bool SerializeArrays = false;
            #region definition
            /// <summary>
            /// when an exception occurs 
            /// </summary>
            public enum ExceptionBehavior
            {
                Hide, MessageBox, ThrowException
            }
            /// <summary>
            /// Save static fields?
            /// </summary>
            public enum StaticFieldsBehavior
            {
                DontSaveAtAll, SaveIfRequested, SaveAll
            }
            #endregion
            #endregion
            public enum ErrorCode
            {
                None = 0,
                AddNode = 1,
                LoadArray = 2,
                LoadGenericType = 3,
                LoadArray2 = 4,
                CloneArray1 = 5,
                CloneArray3 = 6,
                CloneArray2 = 7,
                CloneArray10 = 8,
                CloneGenericType1 = 9,
                LoadGenericType1 = 10,
                CloneGenericType2 = 11,
                CloneGenericType10 = 12,
                CloneNew = 13,
                CloneFields = 14,
                SaveNew = 15,
                LoadFields = 16,
                CloneFields2 = 17,
                SaveArray10 = 18,
                SaveGenericType10 = 19,
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns>aborted?</returns>
            internal static bool HandleError(Exception ex, ErrorCode error_code, string comment = "")
            {
                var code = "";
                if (error_code != ErrorCode.None)
                    code += " [error:" + (int)error_code + "] ";
                if (exceptionBehavior == ExceptionBehavior.ThrowException) throw new Exception(code + ex.Message + "\r\n" + comment);
                else if (exceptionBehavior == ExceptionBehavior.MessageBox)
                {
                    var res = MessageBox.Show(code + ex.Message + "\r\n" + comment +
                        "\r\nYes: Continue\r\nNo: Continue, don't show errors any more\r\nCancel: Abort", "Error in saver", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (res == DialogResult.No)
                        exceptionBehavior = ExceptionBehavior.Hide;
                    else if (res == DialogResult.Cancel)
                        return abort = true;
                }
                return false;
            }
            /// <summary>
            /// will be set at Error()
            /// </summary>
            [ThreadStatic]
            internal static bool abort = false;

        }
        #endregion

        #region Defined Types
        [DontSave]
        static int reg = RegisterTypesIfNot();
        static int RegisterTypesIfNot()
        {
            if (Settings.Types.Count > 0) return 0;

            #region Object
            Settings.SaveAbleObjectType = new Settings.SaveAbleType()
            {
                type = typeof(object),
                Saver = ObjectSaver,
                Loader = ObjectLoader,
            };
            #endregion

            #region base types
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Enum),
                Saver = (n, v, r) => { try { return Settings.AddNode(n, ((int)v).ToString(), r); } catch { return Settings.AddNode(n, "0", r); } },
                Loader = (t, n, r, v) => Convert.ToInt32(Settings.GetNodeVal(n, r))
            });
            Settings.AddSaveAbleType(typeof(System.Drawing.Color),
                (t, n, r, v) =>
                {
                    try
                    {
                        var s = Settings.GetNodeVal(n, r);
                        return System.Drawing.Color.FromArgb(
                            byte.Parse(s.Substring(1, 2), System.Globalization.NumberStyles.HexNumber),
                            byte.Parse(s.Substring(3, 2), System.Globalization.NumberStyles.HexNumber),
                            byte.Parse(s.Substring(5, 2), System.Globalization.NumberStyles.HexNumber),
                            byte.Parse(s.Substring(7, 2), System.Globalization.NumberStyles.HexNumber));
                    }
                    catch
                    {
                        return ObjectLoader(t, n, r, v);
                    }
                },
                (n, v, r) =>
                {
                    var c = (System.Drawing.Color)v;
                    return Settings.AddNode(n, "#" + c.A.ToString("X2") + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"), r);
                },
                false);
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(UInt16),
                Loader = (t, n, r, v) => Convert.ToUInt16(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(uint),
                Loader = (t, n, r, v) => Convert.ToUInt32(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(UInt64),
                Loader = (t, n, r, v) => Convert.ToUInt64(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Int16),
                Loader = (t, n, r, v) => Convert.ToInt16(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Int32),
                Loader = (t, n, r, v) => Convert.ToInt32(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Int64),
                Loader = (t, n, r, v) => Convert.ToInt64(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(String),
                Loader = (t, n, r, v) => (Settings.GetNodeVal(n, r)),
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Double),
                Loader = (t, n, r, v) =>
                {
                    var _v = Settings.GetNodeVal(n, r);
                    if (_v == "NaN") return double.NaN;
                    if (_v == "ناعدد") return double.NaN;
                    return Convert.ToDouble(_v);
                }
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Single),
                Loader = (t, n, r, v) =>
                {
                    var _v = Settings.GetNodeVal(n, r);
                    if (_v == "NaN") return Single.NaN;
                    if (_v == "ناعدد") return Single.NaN;
                    return Convert.ToSingle(_v);
                }
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Boolean),
                Loader = (t, n, r, v) => Convert.ToBoolean(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Byte),
                Loader = (t, n, r, v) => Convert.ToByte(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(SByte),
                Loader = (t, n, r, v) => Convert.ToSByte(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Char),
                Loader = (t, n, r, v) => Convert.ToChar(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Decimal),
                Loader = (t, n, r, v) => Convert.ToDecimal(Settings.GetNodeVal(n, r))
            });
            #endregion

            #region Array

            var ArraySaver = new Settings.SaveAbleType()
            {
                Saver = (n, v, r) =>
                {
                    if (Settings.SerializeArrays)
                    {
                        using (var str = new MemoryStream())
                        {
                            new BinaryFormatter().Serialize(str, v);
                            if (str.Length > 50)
                            {
                                var n_ = Settings.AddNode(n, Convert.ToBase64String(str.ToArray()), r);
                                Settings.SetAttrib("serialized", "1", n_);
                                return n_;
                            }
                        }
                    }
                    {
                        var val = new StringBuilder();
                        foreach (var a in (System.Collections.IEnumerable)v)
                            val.Append(a + ",");
                        var n_ = Settings.AddNode(n, val.ToString(), r);
                        Settings.SetAttrib("array", "1", n_);
                        return n_;
                    }
                    {
                        //return Settings.SaveAbleObjectType.Saver(n, v, r);
                    }
                },
                Loader = (t, n, r, v) =>
                {
                    {
                        var serialized = false;
                        if (Settings.LoadedVersion < 2.0) serialized = true;
                        var n_ = Settings.GetNode(n, r);
                        if (n_ != null && Settings.GetAttrib("serialized", n_) == "1") serialized = true;
                        if (serialized)
                        {
                            var s = Settings.GetNodeVal(n, r);
                            if (s == null) return null;
                            var bytes = Convert.FromBase64String(s);
                            using (var str = new MemoryStream(bytes))
                            {
                                return new BinaryFormatter().Deserialize(str);
                            }
                        }
                    }
                    {
                        var n_ = Settings.GetNode(n, r);
                        if (n_ != null && Settings.GetAttrib("array", n_) == "1")
                        {
                            var V = Settings.GetNodeVal(n, r).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (t.IsArray)
                            {
                                var t_ = t.GetElementType();
                                v = Array.CreateInstance(t_, V.Length);
                                for (int i = 0; i < V.Length; i++)
                                    try
                                    {
                                        if (Settings.abort) return null;
                                        ((Array)v).SetValue(ConvertBaseTypes(t_, V[i]), i);
                                    }
                                    catch (Exception ex)
                                    {
                                        Settings.HandleError(ex, Settings.ErrorCode.LoadArray, $"Error in set value for: { Tools.GetNodeFullName(r) }.{ n }[{ i }] (Type:{t_},Value:{V[i]})");
                                    }
                            }
                            if (t.IsGenericType)
                            {
                                var t_ = t.GetGenericArguments()[0];
                                var T_ = typeof(List<>).MakeGenericType(t_);
                                v = CreateInstance(T_, n);
                                for (int i = 0; i < V.Length; i++)
                                    try
                                    {
                                        if (Settings.abort) return null;
                                        T_.InvokeMember("Add", BindingFlags.InvokeMethod, null, v, new object[] { ConvertBaseTypes(t_, V[i]) });
                                    }
                                    catch (Exception ex)
                                    {
                                        Settings.HandleError(ex, Settings.ErrorCode.LoadGenericType, $"Error in set value for: { Tools.GetNodeFullName(r) }.{ n }[{ i }] (Type:{t_},Value:{V[i]})");
                                    }
                            }
                            return v;
                        }
                    }
                    {
                        return Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    }
                }
            };
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(double[]),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Byte[]),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Boolean[]),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(int[]),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(List<double>),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(List<int>),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(List<Byte>),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(List<Boolean>),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });

            #endregion

            #region Controls
            Settings.IgnorUIDs.Add(typeof(TextBox));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(TextBox),
                Loader = (t, n, r, v) =>
                {
                    v = Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    ((TextBox)v).Text = Settings.GetAttrib("text", node);
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    Settings.SaveAbleObjectType.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("text", node))
                        Settings.SetAttrib("text", ((TextBox)v).Text, node);
                    return node;
                }
            });
            Settings.IgnorUIDs.Add(typeof(ProgressBar));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(ProgressBar),
                Loader = (t, n, r, v) =>
                {
                    v = Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("value", node))
                        ((ProgressBar)v).Value = Convert.ToInt32(Settings.GetAttrib("value", node));
                    ((ProgressBar)v).Value++;
                    ((ProgressBar)v).Value--;
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    Settings.SaveAbleObjectType.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    Settings.SetAttrib("value", ((ProgressBar)v).Value + "", node);
                    return node;
                }
            });
            Settings.IgnorUIDs.Add(typeof(Form));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Form),
                Loader = (t, n, r, v) =>
                {
                    v = Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("Width", node))
                        ((Form)v).Width = Convert.ToInt32(Settings.GetAttrib("Width", node));
                    if (Settings.HasAttrib("Height", node))
                        ((Form)v).Height = Convert.ToInt32(Settings.GetAttrib("Height", node));
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    Settings.SaveAbleObjectType.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    Settings.SetAttrib("Width", ((Form)v).Width + "", node);
                    Settings.SetAttrib("Height", ((Form)v).Height + "", node);
                    return node;
                }
            });
            Settings.IgnorUIDs.Add(typeof(TabControl));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(TabControl),
                Loader = (t, n, r, v) =>
                {
                    v = Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("index", node))
                        ((TabControl)v).SelectedIndex = Convert.ToInt32(Settings.GetAttrib("index", node));
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    Settings.SaveAbleObjectType.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    Settings.SetAttrib("index", ((TabControl)v).SelectedIndex + "", node);
                    return node;
                }
            });
            Settings.IgnorUIDs.Add(typeof(ComboBox));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(ComboBox),
                Loader = (t, n, r, v) =>
                {
                    //v = SaveAbleObject.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("index", node))
                        ((ComboBox)v).SelectedIndex = Convert.ToInt32(Settings.GetAttrib("index", node));
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    //SaveAbleObject.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    Settings.SetAttrib("index", ((ComboBox)v).SelectedIndex + "", node);
                    return node;
                }
            });
            #endregion

            Settings.IgnorTypes.Add(typeof(Pointer));
            Settings.IgnorTypes.Add(typeof(Button));
            Settings.IgnorTypes.Add(typeof(TabControl.TabPageCollection));
            Settings.IgnorUIDs.Add(typeof(System.Drawing.Point));
            Settings.IgnorUIDs.Add(typeof(System.Drawing.Size));
            Settings.IgnorUIDs.Add(typeof(System.Diagnostics.Process));
            return 1;
        }
        static object ConvertBaseTypes(Type t, string val)
        {
            if (t == typeof(Int16)) return Convert.ToInt16(val);
            if (t == typeof(Int32)) return Convert.ToInt32(val);
            if (t == typeof(Int64)) return Convert.ToInt64(val);
            if (t == typeof(UInt16)) return Convert.ToUInt16(val);
            if (t == typeof(uint)) return Convert.ToUInt32(val);
            if (t == typeof(UInt64)) return Convert.ToUInt64(val);
            if (t == typeof(Double)) return (val == "NaN" || val == "ناعدد" ? double.NaN : Convert.ToDouble(val));
            if (t == typeof(Single)) return (val == "NaN" || val == "ناعدد" ? Single.NaN : Convert.ToSingle(val));
            if (t == typeof(Decimal)) return Convert.ToDecimal(val);
            if (t == typeof(Char)) return Convert.ToChar(val);
            if (t == typeof(Boolean)) return Convert.ToBoolean(val);
            if (t == typeof(Byte)) return Convert.ToByte(val);
            if (t == typeof(SByte)) return Convert.ToSByte(val);
            if (t == typeof(String)) return val;
            return null;
        }
        static XmlNode get_node_ref(XmlNode node, string name, string id)
        {
            if (Settings.abort) return null;
            foreach (XmlNode n in node.ChildNodes)
            {
                if (n.Name == name && Settings.GetAttrib("id", n) + "" == id && Settings.GetAttrib("ref", n) == null)
                    return n;
                var n_ = get_node_ref(n, name, id);
                if (n_ != null) return n_;
            }
            return null;
        }
        [ThreadStatic, DontSave]
        static List<Action<int>> waiting_jobs = new List<Action<int>>();
        static void RunWaiting_Jobs()
        {
            while (waiting_jobs.Count > 0)
            {
                for (int j = waiting_jobs.Count - 1; j >= 0; j--)
                {
                    waiting_jobs[j](j);
                    waiting_jobs.RemoveAt(j);
                }
            }
        }
        static object ObjectLoader(Type t, string n, XmlNode r, object val)
        {
#if DEBUG
            File.AppendAllText(Settings.debug_file, "Load: " + n + " - " + t + " - " + r.Name + " - " + val + "\r\n");
#endif
            if (Settings.abort) return null;
            n = ValidName(n);
            foreach (XmlNode node_ in r.ChildNodes)
                if (node_.Name == n)
                {
                    var node = node_;
                    if (Settings.abort) return null;
                    if (Settings.GetAttrib("null", node) == "1") return null;
                    var id = Settings.GetAttrib("id", node) + "";
                    if (id != "")
                        if (!Settings.IgnorUIDs.Contains(t))
                            if (UIDs.ContainsKey(id))
                                return UIDs[id];
                    if (Settings.GetAttrib("ref", node) != null) // is ref but origin value is not loaded yet
                    {
                        t = Settings.GetTpyeFromNode(node, t);
                        node = get_node_ref(xml.ChildNodes[0], Settings.GetAttrib("ref", node), id + "");
                        if (Settings.GetAttrib("null", node) == "1") return null;
                    }
                    object v = val;
                    // برای دیکشنری و آرایه دو بعدی باید مطابق کلون اصلاح شود ?????????????
                    if (t.IsArray)
                    {
                        var t_ = t.GetElementType();
                        v = Array.CreateInstance(t_, node.ChildNodes.Count);
                        if (!Settings.IgnorUIDs.Contains(t))
                        {
                            if (!UIDs.ContainsKey(id)) UIDs.Add(id, v);
                            else UIDs[id] = v;
                        }
                        for (int i = 0; i < node.ChildNodes.Count; i++)
                            try
                            {
                                if (Settings.abort) return null;
                                var fT = Settings.GetTpyeFromNode(node.ChildNodes[i], t_);
                                var st = Settings.SaveAbleType.Get(fT);
                                ((Array)v).SetValue(st.Loader(fT, Tools.Decrypt(node.ChildNodes[i].Name), node, null), i);
                            }
                            catch (Exception ex)
                            {
                                Settings.HandleError(ex, Settings.ErrorCode.LoadArray2, "Error in set value for: " + Tools.GetNodeFullName(r) + "." + n + "[" + i + "]");
                            }
                        return v;
                    }
                    if (t.IsGenericType && t.GetGenericArguments().Length == 1)
                    {
                        {
                            var t_ = t.GetGenericArguments()[0];
                            var T_ = typeof(List<>).MakeGenericType(t_);
                            if (t == T_)
                            {
                                v = CreateInstance(T_, n);
                                if (!Settings.IgnorUIDs.Contains(t))
                                {
                                    if (!UIDs.ContainsKey(id)) UIDs.Add(id, v);
                                    else UIDs[id] = v;
                                }
                                for (int i = 0; i < node.ChildNodes.Count; i++)
                                {
                                    object item = null;
                                    try
                                    {
                                        if (Settings.abort) return null;
                                        var fT = Settings.GetTpyeFromNode(node.ChildNodes[i], t_);
                                        var st = Settings.SaveAbleType.Get(fT);
                                        if (st != null)
                                            item = st.Loader(fT, Tools.Decrypt(node.ChildNodes[i].Name), node, null);
                                        var met = T_.GetMethod("Add");
                                        met.Invoke(v, new object[] { item });
                                    }
                                    catch (Exception ex)
                                    {
                                        Settings.HandleError(ex, Settings.ErrorCode.LoadGenericType1, "Error in set value for: " + Tools.GetNodeFullName(r) + "." + n + "[" + i + "] id:" + GetUID(item));
                                        T_.InvokeMember("Add", BindingFlags.InvokeMethod, null, v, new object[] { null });
                                    }
                                }
                                return v;
                            }
                        }
                    }
                    {
                        if (!t.IsSubclassOf(typeof(Control)) && v == null)
                            v = CreateInstance(t, n);
                        if (v != null && v is SaveAble)
                            (v as SaveAble).BeforeLoad();
                        if (!Settings.IgnorUIDs.Contains(t))
                        {
                            if (!UIDs.ContainsKey(id)) UIDs.Add(id, v);
                            else UIDs[id] = v;
                        }

                        if (Settings.use_waiting_jobs)
                        {
                            var n__ = n;
                            var node__ = node;
                            var v__ = v;
                            waiting_jobs.Add(int_ => LoadFields(n__, v__, node__));
                        }
                        else
                            LoadFields(n, v, node);
                        if (v != null && v is SaveAble)
                            (v as SaveAble).AfterLoad();
                    }
                    return v;
                }
            return null;
        }
        static object CreateInstance(Type t, string name = null)
        {
            object v = null;
            try
            {
                v = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
            }
            catch
            {
                try
                {
                    v = Activator.CreateInstance(t);
                }
                catch (Exception ex)
                {
                    Settings.HandleError(ex, Settings.ErrorCode.SaveNew, name + " , " + (t != null ? t.FullName : "!!" + t.FullName) + ", ");
                }
            }
            return v;
        }
        static object ObjectCloner(object v, object clone_to = null)
        {
#if DEBUG
            File.AppendAllText(Settings.debug_file, "Clone: " + v + "\r\n");
#endif
            if (v == null)
                return null;
            var t = v.GetType();
            if (t.IsValueType) return v;
            if (v is string) return v;
            if (Settings.IgnorTypes.Contains(t) || !DontSave.Save(v) || !SaveCondition.Save(v))
                return null;
            var id = SaveAble.GetUID(v);
            {
                if (UIDs.ContainsKey(id))
                {
                    var o = UIDs[id];
                    return o;
                }
            }
            if (t.IsArray)
            {
                var t_ = t.GetElementType();
                if (t.GetArrayRank() == 1)
                {
                    var v2 = clone_to ?? Array.CreateInstance(t_, ((Array)v).Length);

                    if (!Settings.IgnorUIDs.Contains(t))
                    {
                        if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                        else UIDs[id] = v2;
                    }
                    for (int i = 0; i < ((Array)v).Length; i++)
                        try
                        {
                            if (Settings.abort) return null;
                            ((Array)v2).SetValue(ObjectCloner(((Array)v).GetValue(i)), i);
                        }
                        catch (Exception ex)
                        {
                            Settings.HandleError(ex, Settings.ErrorCode.CloneArray1, "Error in set value for: " + v + "." + i);
                        }
                    return v2;
                }
                if (t.GetArrayRank() == 2)
                {
                    var L1 = ((Array)v).GetLength(0);
                    var L2 = ((Array)v).GetLength(1);
                    var v2 = clone_to ?? Array.CreateInstance(t_, L1, L2);

                    if (!Settings.IgnorUIDs.Contains(t))
                    {
                        if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                        else UIDs[id] = v2;
                    }
                    for (int i = 0; i < L1; i++)
                        for (int j = 0; j < L2; j++)
                            try
                            {
                                if (Settings.abort) return null;
                                ((Array)v2).SetValue(ObjectCloner(((Array)v).GetValue(i, j)), i, j);
                            }
                            catch (Exception ex)
                            {
                                Settings.HandleError(ex, Settings.ErrorCode.CloneArray2, "Error in set value for: " + v + "." + i + "," + j);
                            }
                    return v2;
                }
                if (t.GetArrayRank() == 3)
                {
                    var L1 = ((Array)v).GetLength(0);
                    var L2 = ((Array)v).GetLength(1);
                    var L3 = ((Array)v).GetLength(2);
                    var v2 = clone_to ?? Array.CreateInstance(t_, L1, L2, L3);

                    if (!Settings.IgnorUIDs.Contains(t))
                    {
                        if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                        else UIDs[id] = v2;
                    }
                    for (int i = 0; i < L1; i++)
                        for (int j = 0; j < L2; j++)
                            for (int k = 0; k < L3; k++)
                                try
                                {
                                    if (Settings.abort) return null;
                                    ((Array)v2).SetValue(ObjectCloner(((Array)v).GetValue(i, j, k)), i, j, k);
                                }
                                catch (Exception ex)
                                {
                                    Settings.HandleError(ex, Settings.ErrorCode.CloneArray3, "Error in set value for: " + v + "." + i + "," + j + "," + k);
                                }
                    return v2;
                }
                Settings.HandleError(new Exception("Can't clone type: " + t), error_code: Settings.ErrorCode.CloneArray10);
            }
            else if (t.IsGenericType)
            {
                if (t.GetGenericArguments().Length == 1)
                {
                    var t_ = t.GetGenericArguments()[0];
                    var T_ = typeof(List<>).MakeGenericType(t_);
                    if (t == T_)
                    {
                        var v2 = clone_to ?? CreateInstance(t);
                        if (!Settings.IgnorUIDs.Contains(t))
                        {
                            if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                            else UIDs[id] = v2;
                        }
                        foreach (var a in (System.Collections.IEnumerable)v)
                        {
                            object item = null;
                            try
                            {
                                if (Settings.abort) return null;
                                item = ObjectCloner(a);
                                var met = T_.GetMethod("Add");
                                met.Invoke(v2, new object[] { item });
                            }
                            catch (Exception ex)
                            {
                                Settings.HandleError(ex, Settings.ErrorCode.CloneGenericType1, "Error in set value for: " + v + ".");
                                T_.InvokeMember("Add", BindingFlags.InvokeMethod, null, v2, new object[] { null });
                            }
                        }
                        return v2;
                    }
                }
                else if (t.GetGenericArguments().Length == 2)
                {
                    var t1_ = t.GetGenericArguments()[0];
                    var t2_ = t.GetGenericArguments()[1];
                    var T_ = typeof(Dictionary<,>).MakeGenericType(t1_, t2_);
                    if (t == T_)
                    {
                        var v2 = clone_to ?? CreateInstance(t);
                        if (!Settings.IgnorUIDs.Contains(t))
                        {
                            if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                            else UIDs[id] = v2;
                        }
                        var key = t.GetProperty("Keys").GetValue(v, null);
                        var val = t.GetProperty("Values").GetValue(v, null);
                        var tmp_vals_array = new List<object>();
                        foreach (var v_ in (System.Collections.IEnumerable)val)
                            tmp_vals_array.Add(v_);
                        var tmp_keys_array = new List<object>();
                        foreach (var k_ in (System.Collections.IEnumerable)key)
                            tmp_keys_array.Add(k_);
                        for (int i = 0; i < tmp_keys_array.Count; i++)
                        {
                            try
                            {
                                if (Settings.abort) return null;
                                var val_ = ObjectCloner(tmp_vals_array[i]);
                                var key_ = ObjectCloner(tmp_keys_array[i]);
                                var met = T_.GetMethod("Add");
                                met.Invoke(v2, new object[] { key_, val_ });
                            }
                            catch (Exception ex)
                            {
                                Settings.HandleError(ex, Settings.ErrorCode.CloneGenericType2, "Error in set value for: " + v + ".");
                                T_.InvokeMember("Add", BindingFlags.InvokeMethod, null, v2, new object[] { null, null });
                            }
                        }
                        return v2;
                    }
                }
                Settings.HandleError(new Exception("Can't clone type: " + t), Settings.ErrorCode.CloneGenericType10);
            }

            {
                object v2 = null;
                try
                {
                    v2 = clone_to ?? CreateInstance(t);
                }
                catch (Exception ex)
                {
                    Settings.HandleError(ex, Settings.ErrorCode.CloneNew, "Clone: (new) " + (t != null ? t.FullName : "!!") + ", " + v);
                }
                try
                {
                    if (v2 != null && v2 is SaveAble)
                        (v2 as SaveAble).BeforeClone();
                    UIDs.Add(id, v2);
                    if (Settings.use_waiting_jobs)
                    {
                        var v__ = v;
                        var v2__ = v2;
                        waiting_jobs.Add(int_ => CloneFields(v__, v2__));
                    }
                    else
                        CloneFields(v, v2);
                    if (v2 != null && v2 is SaveAble)
                        (v2 as SaveAble).AfterClone();
                }
                catch (Exception ex)
                {
                    Settings.HandleError(ex, Settings.ErrorCode.CloneFields, "Clone: " + (t != null ? t.FullName : "!!" + v));

                }
                return v2;
            }
        }
        static XmlNode ObjectSaver(string n, object v, XmlNode r)
        {
#if DEBUG
            File.AppendAllText(Settings.debug_file, "Save: " + n + " - " + (v != null ? v.GetType() + "" : "null") + " - " + r.Name + " - " + v + "\r\n");
#endif
            if (Settings.abort) return null;
            n = ValidName(n);
            var node = r.AppendChild(xml.CreateElement(n));
            if (v == null)
            {
                Settings.SetAttrib("null", "1", node);
                return null;
            }
            if (Settings.IgnorTypes.Contains(v.GetType()) || !DontSave.Save(v) || !SaveCondition.Save(v))
                return null;
            {
                var id = SaveAble.GetUID(v);
                Settings.SetAttrib("id", id + "", node);
                if (UIDs.ContainsKey(id))
                {
                    var o = UIDs[id];
                    Settings.SetAttrib("ref", UIDs_node_name[id], node);
                    //if (v.GetType() != o.GetType())
                    // Settings.SetAttrib("Type", Settings.TypeFromMap(o.GetType() + "", true), node);
                    return node;
                }
                UIDs.Add(id, v);
                UIDs_node_name.Add(id, n);
            }
            // برای دیکشنری و آرایه دو بعدی باید مطابق کلون اصلاح شود ?????????????
            var t = v.GetType();
            if (v is System.Collections.IEnumerable && !(v is string) && (t.IsArray || (t.IsGenericType && t.GetGenericArguments().Length == 1)))
            {
                if (t.IsArray && t.GetArrayRank() != 1)
                    Settings.HandleError(new Exception("Can't save type: (" + n + ")" + t), Settings.ErrorCode.SaveArray10);
                if (t.IsGenericType && t.GetGenericArguments().Length != 1)
                    Settings.HandleError(new Exception("Can't save type: " + t), Settings.ErrorCode.SaveGenericType10);
                int i = 0;
                var vt_ = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
                try
                {
                    var object_node = new Dictionary<object, XmlNode>();
                    foreach (var a in (System.Collections.IEnumerable)v)
                    {
                        if (Settings.abort) break;
                        if (a == null)
                            Settings.AddNode("_" + (i++), "1", node, "null");
                        else
                        {
                            var fT = a != null ? a.GetType() : vt_;
                            var st = Settings.SaveAbleType.Get(fT);
                            if (st == Settings.SaveAbleObjectType && a is SaveAble)
                                ignor_save_fields = true;
                            var n_ = st.Saver("_" + (i++), a, node);
                            if (n_ != null && ignor_save_fields)
                                object_node[a] = n_;
                            ignor_save_fields = false;
                            if (n_ != null && vt_ != fT)
                                Settings.SetAttrib("Type", Settings.TypeFromMap(fT + "", true), n_);
                        }
                    }
                    foreach (var a in object_node)
                    {
                        if (Settings.abort) break;
                        ignor_save_fields = false;
                        SaveFields(a.Key, a.Value);
                    }
                }
                finally { ignor_save_fields = false; }
            }
            else
            {
                if (v != null && v is SaveAble)
                    (v as SaveAble).BeforeSave();
                if (Settings.use_waiting_jobs)
                {
                    var node__ = node;
                    var v__ = v;
                    waiting_jobs.Add(int_ => SaveFields(v__, node__));
                }
                else
                    SaveFields(v, node);
                if (v != null && v is SaveAble)
                    (v as SaveAble).AfterSave();
            }
            if (Settings.abort) return null;
            return node;
        }
        [DontSave, ThreadStatic]
        static bool ignor_save_fields = false;
        static void LoadFields(string n, object v, XmlNode node)
        {
            var F = Settings.GetFields(v.GetType());
            foreach (var f in F)
                if (Settings.HasNode(GetFieldName(f), node))
                {
                    Type fT = null;
                    object val = null;
                    try
                    {
                        if (Settings.abort) return;
                        var n_ = Settings.GetNode(GetFieldName(f), node);
                        if (Settings.GetAttrib("null", n_) + "" != "1")
                        {
                            fT = Settings.GetTpyeFromNode(n_, f.FieldType);
                            var st = Settings.SaveAbleType.Get(fT);
                            if (st != null)
                            {
                                val = st.Loader(fT, GetFieldName(f), node, f.GetValue(v));
                                f.SetValue(v, val);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Settings.HandleError(ex, Settings.ErrorCode.LoadFields, $"Error in set value for: {Tools.GetNodeFullName(node) }.{ GetFieldName(f)} (Type:'{fT?.Name + ""}',Value:'{val + ""}')");
                    }
                }
        }
        static void SaveFields(object v, XmlNode node)
        {
            if (ignor_save_fields) return;
            var F = Settings.GetFields(v.GetType());
            foreach (var f in F)
            {
                if (Settings.abort) break;
                if (f.IsStatic && Settings.saveStaticFields != Settings.StaticFieldsBehavior.SaveAll)
                {
                    if (Settings.saveStaticFields == Settings.StaticFieldsBehavior.DontSaveAtAll)
                        continue;
                    if (!SaveStaticFields.Save(f) && !SaveStaticFields.Save(v))
                        continue;
                }
                var fval = f.GetValue(v);
                var fT = fval != null ? fval.GetType() : f.FieldType;
                if (Settings.IgnorTypes.Contains(fT) || !DontSave.Save(f) || !SaveCondition.Save(f))
                    continue;
                if (fval == null)
                    Settings.AddNode(GetFieldName(f), "1", node, "null");
                else
                {
                    var st = Settings.SaveAbleType.Get(fT);
                    if (st != null)
                    {
                        var n_ = st.Saver(GetFieldName(f), fval, node);
                        // if (!(fval.GetType() + "").Contains("["))
                        if (n_ != null && f.FieldType != fT)
                            Settings.SetAttrib("Type", Settings.TypeFromMap(fT + "", true), n_);
                    }
                }
            }
        }
        static void CloneFields(object v, object v2)
        {
            if (ignor_save_fields) return;
            var F = Settings.GetFields(v.GetType());
            foreach (var f in F)
                try
                {
                    if (Settings.abort) break;
                    if (f.IsStatic)
                        continue;
                    var fval = f.GetValue(v);
                    if (!DontSave.Save(f) || !SaveCondition.Save(f))
                        continue;
                    f.SetValue(v2, ObjectCloner(fval));
                }
                catch (Exception ex)
                {
                    Settings.HandleError(ex, Settings.ErrorCode.CloneFields2, "Error in field: " + v.GetType().FullName + " > " + f.Name);
                }
        }
        #endregion

        #region unique id
        /*[DontSave, ThreadStatic]
        static uint counter = 0;
        [DontSave]
        string _uid_ = Get_next_uid_();
        static string Get_next_uid_()
        {
            counter++;
            //if (counter++ > 10000)
            //    counter = 1;
            return System.Threading.Thread.CurrentThread.ManagedThreadId + "." + counter;
        }*/
        /// <summary>
        /// لیستی از آبجکت های ذخیره یا لود شده بر اساس آی دی
        /// </summary>
        [DontSave, ThreadStatic]
        static Dictionary<string, object> UIDs;
        /// <summary>
        /// for save
        /// </summary>
        [DontSave, ThreadStatic]
        static Dictionary<string, string> UIDs_node_name;
        static System.Runtime.Serialization.ObjectIDGenerator id_gen = new System.Runtime.Serialization.ObjectIDGenerator();
        public static string GetUID(object obj)
        {
            try
            {
                var first = false;
                return id_gen.GetId(obj, out first) + "";
                /*
                if (obj == null) return "";
                if (obj is SaveAble)
                {
                    if (((SaveAble)obj)._uid_ + "" == "")
                        ((SaveAble)obj)._uid_ = Get_next_uid_();
                    return ((SaveAble)obj)._uid_;
                }
                else
                {
                    var f = obj.GetType().GetField("_uid_");
                    if (f != null)
                    {
                        var uid = f.GetValue(obj) + "";
                        if (uid == "")
                            f.SetValue(obj, uid = Get_next_uid_());
                        return uid;
                    }
                }
                var t = obj.GetType();
                var ext = t.Namespace + "." + t.Name + "=";
                if (t.IsArray)
                {
                    var t_ = t.GetElementType();
                    ext = "1-" + t_?.Namespace + "." + t_?.Name;
                }
                else if (t.IsGenericType)
                {
                    ext = 2 + "";
                    for (int i = 0; i < t.GetGenericArguments().Length; i++)
                    {
                        var t_ = t.GetGenericArguments()[i];
                        ext = "-" + t_?.Namespace + "." + t_?.Name;
                    }
                }
#if DEBUG
                return id_gen.GetId(obj, out first)  + "-" + (ext);
#else
                return id_gen.GetId(obj, out first) + "-" + Tools.Hash(ext);
#endif */
            }
            catch
            {
                return obj.GetHashCode() + "!";
            }
        }
        #endregion

        #region Not Public
        /// <summary>
        /// before save done, (internally)
        /// </summary>
        /// <returns></returns>
        protected static void BeginSave()
        {
            xml = new XmlDocument();
            Settings.abort = false;
            waiting_jobs = new List<Action<int>>();
            UIDs = new Dictionary<string, object>();
            UIDs_node_name = new Dictionary<string, string>();
            root = xml.AppendChild(xml.CreateElement("Root"));
            Settings.SetAttrib("ver", Settings.Version + "", root);
            var d = DateTime.Now;
            Settings.SetAttrib("time", (d.Month + "/" + d.Day + "/" + d.Year + "-" + d.ToLongTimeString() + "     "), root);
        }
        [DontSave, ThreadStatic]
        static XmlDocument xml;
        [DontSave, ThreadStatic]
        static XmlNode root;
        /// <summary>
        /// after save done, (internally)
        /// </summary>
        /// <returns></returns>
        protected static string EndSave()
        {
            try
            {
                if (!Settings.FormatedXML)
                    return xml.OuterXml;
                var sb = new StringBuilder();
                using (var sw = new StringWriter(sb))
                {
                    xml.Save(sw);
                }
                var str = sb + "";
                var p1 = str.IndexOf("\n");
                return str.Substring(p1 + 1);
            }
            finally
            {
                EndLoad();
            }
        }
        /// <summary>
        /// before load done, (internally)
        /// </summary>
        /// <returns></returns>
        protected static void BeginLoad(string str, bool IsFile)
        {
            xml = new XmlDocument();
            Settings.abort = false;
            waiting_jobs = new List<Action<int>>();
            UIDs = new Dictionary<string, object>();
            if (IsFile)
                xml.Load(str);
            else
                xml.LoadXml(str);
            root = xml.ChildNodes[0];
            Settings.LoadedVersion = -1;
            double.TryParse(Settings.GetAttrib("ver", root), out Settings.LoadedVersion);
        }
        protected static void EndLoad()
        {
            xml = null;
            root = null;
            UIDs = null;
            UIDs_node_name = null;
            GC.Collect();
        }
        static string GetFieldName(FieldInfo f)
        {
            var name = SaveAs.Name(f);
            try
            {
                XmlConvert.VerifyName(name);
                return name;
            }
            catch
            {
                return Tools.Base64Encode(Tools.Base64Encode(name));
            }
        }

        static string ValidName(string name)
        {
            return Tools.Encrypt(name.Replace('<', '_').Replace('>', '_').Replace(' ', '_'));
        }

        static string GetFileHash(string fileName)
        {
            return GetStrHash(File.ReadAllText(fileName));
        }
        [DontSave]
        static Regex _remove_id_Regex = new Regex("id=\"[^\"]+\"", RegexOptions.Compiled);
        static string GetStrHash(string str)
        {
            using (var md5Hash = MD5.Create())
            {
                str = _remove_id_Regex.Replace(str.Substring(50), "$");
                var data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(str));
                var sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                    sBuilder.Append(data[i].ToString("x2"));
                return sBuilder.ToString();
            }
        }
        /// <summary>
        /// will not update current hash
        /// </summary>
        /// <returns>Hash Code</returns>
        string Save4Hash()
        {
            return Run(() =>
            {
                BeginSave();
                Settings.SaveAbleType.Get(this.GetType()).Saver("Data", this, root);
                RunWaiting_Jobs();
                return GetStrHash(EndSave());
            });
        }
        [DontSave, ThreadStatic]
        string FileHash = "";

        static string Run(Act2 act)
        {
            if (xml != null) // قبلا در همین ترد در حال ذخیره یا لود است - از داخل تایمر ها ممکن است رخ دهد
            {
                var res = "";
                var th = new System.Threading.Thread(new System.Threading.ThreadStart(() => { res = act(); }));
                th.Start();
                for (int i = 0; i < 2000 && th.IsAlive; i++)
                    System.Threading.Thread.Sleep(100);
                return res;
            }
            else
                return act();
        }
        static void Run(Act1 act)
        {
            if (xml != null) // قبلا در همین ترد در حال ذخیره یا لود است - از داخل تایمر ها ممکن است رخ دهد
            {
                var th = new System.Threading.Thread(new System.Threading.ThreadStart(act));
                th.Start();
                for (int i = 0; i < 2000 && th.IsAlive; i++)
                    System.Threading.Thread.Sleep(100);
            }
            else
                act();
        }
        delegate void Act1();
        delegate string Act2();
        #endregion

        #region Save/Load methods

        /// <summary>
        /// باز تعریف نحوه ذخیره شده
        /// حواست به رفرنس ها باشه!
        /// </summary> 
        /// <returns></returns>
        public virtual string CustomSaver()
        {
            throw new NotImplementedException("CustomSaver");
        }
        /// <summary>
        /// باز تعریف نحوه لود شدن 
        /// حواست به رفرنس ها باشه!
        /// </summary>
        /// <param name="content"></param> 
        public virtual void CustomLoader(string content)
        {
            throw new NotImplementedException("CustomLoader");
        }

        /// <summary>
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        /// <param name="IgnorObjects"></param>
        /// <returns></returns>
        public virtual object Clone(params object[] IgnorObjects)
        {
            BeforeClone();
            var res = Clone(this, IgnorObjects);
            AfterClone();
            return res;
        }
        /// <summary>
        /// clone an abject to this
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="IgnorObjects"></param>
        public virtual void LoadFromObject(object obj, params object[] IgnorObjects)
        {
            var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
            try
            {
                BeforeLoad();
                if (IgnorObjects != null)
                    Settings.IgnorSaveObjects.AddRange(IgnorObjects);
                Settings.abort = false;
                waiting_jobs = new List<Action<int>>();
                UIDs = new Dictionary<string, object>();
                UIDs_node_name = new Dictionary<string, string>();

                ObjectCloner(obj, this);
                RunWaiting_Jobs();
            }
            finally
            {
                UIDs = null;
                UIDs_node_name = null;
                while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                    Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
                AfterLoad();
            }
        }
        public static T Clone<T>(T obj, params object[] IgnorObjects)
        {
            return (T)Clone_(obj, IgnorObjects);
        }
        static object Clone_(object obj, params object[] IgnorObjects)
        {
            var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
            try
            {
                if (IgnorObjects != null)
                    Settings.IgnorSaveObjects.AddRange(IgnorObjects);
                Settings.abort = false;
                UIDs = new Dictionary<string, object>();
                waiting_jobs = new List<Action<int>>();
                UIDs_node_name = new Dictionary<string, string>();

                var res = ObjectCloner(obj);
                RunWaiting_Jobs();
                return res;
            }
            finally
            {
                UIDs = null;
                UIDs_node_name = null;
                while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                    Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
            }
        }
        /// <summary>
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="IgnorObjects">List of objects that will not be saved</param>
        /// <returns>Hash Code</returns>
        public virtual string Save_NoID(string fileName, params object[] IgnorObjects)
        {
            var res = Save(fileName, IgnorObjects: IgnorObjects);
            var str = File.ReadAllText(fileName);
            str = _remove_id_Regex.Replace(str, "");
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 10; j++)
                    str = str.Replace(" <", "<");
                if (!str.Contains(" <"))
                    break;
            }
            File.WriteAllText(fileName, str);
            return res;
        }
        /// <summary>
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="IgnorObjects">List of objects that will not be saved</param>
        /// <returns>Hash Code</returns>
        public virtual string Save(string fileName, params object[] IgnorObjects)
        {
            return Run(() =>
            {
                var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
                try
                {
                    BeforeSave();
                    if (IgnorObjects != null)
                        Settings.IgnorSaveObjects.AddRange(IgnorObjects);
                    BeginSave();
                    this.FileHash = "";
                    Settings.SaveAbleType.Get(this.GetType()).Saver("Data", this, root);
                    RunWaiting_Jobs();
                    var str = EndSave();
                    File.WriteAllText(fileName, str, encoding: Encoding.Unicode);
                    FileHash = GetStrHash(str);
                    return FileHash;
                }
                finally
                {
                    while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                        Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
                    AfterSave();
                }
            });
        }
        /// <summary>
        /// Load from file
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        /// <param name="fileName"></param>
        public virtual void Load(string fileName)
        {
            Run(() =>
            {
                BeforeLoad();
                BeginLoad(fileName, true);
                Settings.SaveAbleType.Get(this.GetType()).Loader(this.GetType(), "Data", root, this);
                RunWaiting_Jobs();
                FileHash = GetFileHash(fileName);
                EndLoad();
                AfterLoad();
            });
        }
        /// <summary>
        /// Save an object to file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="obj">objact that will be saved in file</param>
        /// <returns>Hash Code</returns>
        public static string Save(string fileName, object obj)
        {
            return Run(() =>
            {
                BeginSave();
                if (obj is SaveAble)
                    ((SaveAble)obj).FileHash = "";
                Settings.SaveAbleType.Get(obj.GetType()).Saver("Data", obj, root);
                RunWaiting_Jobs();
                var str = EndSave();
                File.WriteAllText(fileName, str, Encoding.Unicode);
                var LastFileHash = GetStrHash(str);
                if (obj is SaveAble)
                    ((SaveAble)obj).FileHash = LastFileHash;
                return LastFileHash;
            });
        }
        /// <summary>
        /// load from file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T Load<T>(string fileName)
        {
            BeginLoad(fileName, true);
            var res = Settings.SaveAbleType.Get(typeof(T)).Loader(typeof(T), "Data", root, null);
            RunWaiting_Jobs();
            if (res is SaveAble)
                ((SaveAble)res).FileHash = GetFileHash(fileName);
            return (T)res;
        }
        /// <summary>
        /// Load from file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static object Load(string fileName, object obj)
        {
            BeginLoad(fileName, true);
            var res = Settings.SaveAbleType.Get(obj.GetType()).Loader(obj.GetType(), "Data", root, obj);
            RunWaiting_Jobs();
            if (res is SaveAble)
                ((SaveAble)res).FileHash = GetFileHash(fileName);
            return res;
        }
        /// <summary>
        /// Load from file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object Load_(string fileName, Type type)
        {
            BeginLoad(fileName, true);
            object v = CreateInstance(type);

            var res = Settings.SaveAbleType.Get(type).Loader(type, "Data", root, v);
            RunWaiting_Jobs();
            if (res is SaveAble)
                ((SaveAble)res).FileHash = GetFileHash(fileName);
            return res;
        }

        /// <summary> 
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        public virtual void BeforeClone()
        {
        }
        /// <summary> 
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        public virtual void AfterClone()
        {
        }
        /// <summary> 
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        public virtual void BeforeLoad()
        {
        }
        /// <summary> 
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        public virtual void AfterLoad()
        {
        }
        /// <summary> 
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        public virtual void BeforeSave()
        {
        }
        /// <summary> 
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید
        /// </summary>
        public virtual void AfterSave()
        {
        }

        /// <summary>
        /// Save object as a string instead a file 
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید 
        /// </summary>
        /// <param name="IgnorObjects"></param>
        /// <returns></returns>
        public virtual string SaveString(params object[] IgnorObjects)
        {
            return Run(() =>
            {
                var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
                try
                {
                    BeforeSave();
                    if (IgnorObjects != null)
                        Settings.IgnorSaveObjects.AddRange(IgnorObjects);
                    BeginSave();
                    Settings.SaveAbleType.Get(this.GetType()).Saver("Data", this, root);
                    RunWaiting_Jobs();
                    return EndSave();
                }
                finally
                {
                    while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                        Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
                    AfterSave();
                }
            });
        }
        /// <summary>
        /// Save object as a string instead a file
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string SaveString(object obj)
        {
            return Run(() =>
            {
                var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
                try
                {
                    BeginSave();
                    Settings.SaveAbleType.Get(obj.GetType()).Saver("Data", obj, root);
                    RunWaiting_Jobs();
                    return EndSave();
                }
                finally
                {
                    while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                        Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
                }
            });
        }
        /// <summary>
        /// Load object from a string instead an file
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید 
        /// </summary>
        /// <param name="str"></param>
        public virtual void LoadString(string str)
        {
            Run(() =>
            {
                BeforeLoad();
                BeginLoad(str, false);
                Settings.SaveAbleType.Get(this.GetType()).Loader(this.GetType(), "Data", root, this);
                RunWaiting_Jobs();
                FileHash = GetStrHash(str);
                EndLoad();
                AfterLoad();
            });
        }
        /// <summary>
        /// Load object from a string instead an file
        /// </summary>
        /// <param name="str"></param>
        public static object LoadString(string str, object obj)
        {
            var f = Path.GetTempFileName() + "-LS";
            try
            {
                File.WriteAllText(f, str);
                return Load(f, obj);
            }
            finally
            {
                try
                {
                    File.Delete(f);
                }
                catch
                {
#if DEBUG
                    throw;
#endif
                }
            }
        }
        public static object LoadString_(string str, Type type)
        {
            var f = Path.GetTempFileName() + "-LS";
            try
            {
                File.WriteAllText(f, str);
                return Load_(f, type);
            }
            finally
            {
                try
                {
                    File.Delete(f);
                }
                catch
                {
#if DEBUG
                    throw;
#endif
                }
            }
        }
        /// <summary>
        /// is saved in a file
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید 
        /// </summary>
        [Browsable(false)]
        public virtual bool IsSaved
        {
            get { return !IsChanged; }
            set { IsChanged = !value; }
        }
        /// <summary>
        /// is changed after last save in a file or load from a file
        /// هشدار:
        /// اگر اطلاعات کافی ندارید این متد را باز تعریف استفاده نکنید 
        /// </summary>
        [Browsable(false)]
        public virtual bool IsChanged
        {
            get
            {
                return Save4Hash() != FileHash;
            }
            set
            {
                if (value)
                    FileHash = "!";
                else
                    FileHash = Save4Hash();
            }
        }
        #endregion
    }

    #region Attributes
    /// <summary>
    /// this field will not be saved
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Property)]
    public class DontSave : Attribute
    {
        /// <summary>
        /// DontSave Attribute will not work at all
        /// </summary>
        public static bool DisableGlobaly = false;
        /// <summary>
        /// save or not?
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static bool Save(FieldInfo f)
        {
            //if (f.Name == "_uid_") return false;
            if (DisableGlobaly) return true;
            {
                var AT = f.FieldType.GetCustomAttributes(typeof(DontSave), true);
                if (AT != null && AT.Length > 0)
                    return false;
            }
            var fName = SaveAble.Settings.NameFromMap(f);
            if (fName.Contains(">k__BackingField"))
            {
                var name = fName.Replace(">k__BackingField", "").Substring(1);
                var p = f.DeclaringType.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (p != null)
                {
                    var AT = p.GetCustomAttributes(typeof(DontSave), true);
                    if (AT != null && AT.Length > 0)
                        return false;
                }
                return true;
            }
            else
            {
                var AT = f.GetCustomAttributes(typeof(DontSave), true);
                if (AT != null && AT.Length > 0)
                    return false;
            }
            return true;
        }
        /// <summary>
        /// save or not?
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool Save(object v)
        {
            if (DisableGlobaly) return true;
            if (v != null)
            {
                var AT = v.GetType().GetCustomAttributes(typeof(DontSave), true);
                return (AT == null || AT.Length == 0);
            }
            return true;
        }

    }
    /// <summary>
    /// will be saved if SaveIf.AllowSave is true
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Property)]
    public class SaveCondition : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="save_if_this_tag_exist"></param>
        /// <param name="dont_save_if_this_tag_exist"></param>
        public SaveCondition(object save_if_this_tag_exist = null, object dont_save_if_this_tag_exist = null)
        {
            this.save_if_this_tag_exist = save_if_this_tag_exist;
            this.dont_save_if_this_tag_exist = dont_save_if_this_tag_exist;
        }
        public static bool SaveAll = false;
        object save_if_this_tag_exist, dont_save_if_this_tag_exist;
        public static List<object> Tags = new List<object>();
        /// <summary>
        /// save or not
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static bool Save(FieldInfo f)
        {
            if (SaveAll) return true;
            var fName = SaveAble.Settings.NameFromMap(f);
            if (fName.Contains(">k__BackingField"))
            {
                var name = fName.Replace(">k__BackingField", "").Substring(1);
                var p = f.DeclaringType.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (p != null)
                {
                    var AT = p.GetCustomAttributes(typeof(SaveCondition), true);
                    if (AT != null && AT.Length > 0)
                        return (AT[0] as SaveCondition).Save();
                }
                return true;
            }
            else
            {
                var AT = f.GetCustomAttributes(typeof(SaveCondition), true);
                if (AT != null && AT.Length > 0)
                    return (AT[0] as SaveCondition).Save();
            }
            {
                var AT = f.FieldType.GetCustomAttributes(typeof(SaveCondition), true);
                if (AT != null && AT.Length > 0)
                    return (AT[0] as SaveCondition).Save();
            }
            return true;
        }
        /// <summary>
        /// save or not
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool Save(object v)
        {
            if (SaveAll) return true;
            if (v == null) return true;
            var AT = v.GetType().GetCustomAttributes(typeof(SaveCondition), true);
            if (AT == null || AT.Length == 0)
                return true;
            var at = AT[0] as SaveCondition;
            return at.Save();
        }
        bool Save()
        {
            if (dont_save_if_this_tag_exist != null && Tags.Contains(dont_save_if_this_tag_exist)) return false;
            if (save_if_this_tag_exist != null && !Tags.Contains(save_if_this_tag_exist)) return false;
            return true;
        }
    }
    /// <summary>
    /// define a name for the field
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Property)]
    public class SaveAs : Attribute
    {
        /// <summary>
        /// SaveAs Attribute will not work at all
        /// </summary>
        public static bool DisableGlobaly = false;
        /// <summary>
        /// name for save
        /// </summary>
        public string name;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">given name for save and load</param>
        public SaveAs(string name)
        {
            this.name = name;
        }
        /// <summary>
        /// given name for save and load
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static string Name(FieldInfo f)
        {
            var fName = SaveAble.Settings.NameFromMap(f);
            if (fName.Contains(">k__BackingField"))
            {
                var name = fName.Replace(">k__BackingField", "").Substring(1);
                if (DisableGlobaly) return name;
                var p = f.DeclaringType.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (p != null)
                {
                    var A = p.GetCustomAttributes(typeof(SaveAs), true);
                    if (A.Length > 0)
                        return (A[0] as SaveAs).name;
                }
                return name;
            }
            else
            {
                if (DisableGlobaly) return fName;
                var A = f.GetCustomAttributes(typeof(SaveAs), true);
                if (A.Length > 0)
                    return (A[0] as SaveAs).name;
            }
            return fName;
        }

    }
    /// <summary>
    /// Static Fields will be saved for this class | field
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
    public class SaveStaticFields : Attribute
    {
        /// <summary>
        /// SaveStaticFields Attribute will not work at all
        /// </summary>
        public static bool DisableGlobaly = false;
        /// <summary>
        /// save or not?
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static bool Save(FieldInfo f)
        {
            if (DisableGlobaly) return false;
            var AT = f.FieldType.GetCustomAttributes(typeof(SaveStaticFields), true);
            return (AT != null && AT.Length > 0);
        }
        /// <summary>
        /// save or not?
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool Save(object v)
        {
            if (DisableGlobaly) return false;
            if (v != null)
            {
                var AT = v.GetType().GetCustomAttributes(typeof(SaveStaticFields), true);
                return (AT != null && AT.Length > 0);
            }
            return false;
        }

    }
    #endregion

    #region Tools
    /// <summary>
    /// Settings
    /// </summary>
    public class Settings : SaveAble.Settings { }
    internal class Tools
    {
        public static string Hash(string str)
        {
            return _Decrypt(str);
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes).Replace("=", "");
        }
        static string XXXX = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz",
                     YYYY = "w9abc2FGHRSTUVWXYixyz1mnoBCDEldefghvpqrstuZ0jk3456AIJKLMNOPQ78",
                     XXXX2 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz#$%^&*()_-+=!.~`;:<>{}",
                     YYYY2 = "w9^ghab)z1f>ST}FGH:<(n.6AI&*JKYixy%vpR#LMNOP-+UVWm{c2~oBCDElde$qrst_`;uZ0jk345=!XQ78";
        public static string Encrypt(string str)
        {
            if (!Settings.Encryption)
                return str;
            return _Encrypt(str);
        }
        static string _Encrypt(string str)
        {
            var res = new StringBuilder();
            foreach (var c in str)
            {
                var i = XXXX.IndexOf(c);
                if (i >= 0)
                    res.Append(YYYY[i]);
                else
                    res.Append(c);
            }
            return "_" + res;
        }
        static string _Encrypt2(string str)
        {
            var res = new StringBuilder();
            foreach (var c in str)
            {
                var i = XXXX2.IndexOf(c);
                if (i >= 0)
                    res.Append(YYYY2[i]);
                else
                    res.Append(c);
            }
            return res.ToString();
        }
        public static string Decrypt(string str)
        {
            if (!Settings.Encryption)
                return str;
            return _Decrypt(str);
        }
        static string _Decrypt(string str)
        {
            str = str.Substring(1);
            var res = new StringBuilder();
            foreach (var c in str)
            {
                var i = YYYY.IndexOf(c);
                if (i >= 0)
                    res.Append(XXXX[i]);
                else
                    res.Append(c);
            }
            return res.ToString();
        }
        static string _Decrypt2(string str)
        {
            var res = new StringBuilder();
            foreach (var c in str)
            {
                var i = YYYY2.IndexOf(c);
                if (i >= 0)
                    res.Append(XXXX2[i]);
                else
                    res.Append(c);
            }
            return res.ToString();
        }
        ///<summary>
        /// Steve Lydford - 12/05/2008.
        ///
        /// Encrypts a file using Rijndael algorithm.
        ///</summary>
        ///<param name="inputFile"></param>
        ///<param name="outputFile"></param>
        ///<param name="password"></param>
        public static void EncryptFile(string inputFile, string outputFile, string password)
        {
            using (RijndaelManaged aes = new RijndaelManaged())
            {
                byte[] key = ASCIIEncoding.UTF8.GetBytes(password);

                /* This is for demostrating purposes only. 
                 * Ideally you will want the IV key to be different from your key and you should always generate a new one for each encryption in other to achieve maximum security*/
                byte[] IV = ASCIIEncoding.UTF8.GetBytes(password);

                using (FileStream fsCrypt = new FileStream(outputFile, FileMode.Create))
                {
                    using (ICryptoTransform encryptor = aes.CreateEncryptor(key, IV))
                    {
                        using (CryptoStream cs = new CryptoStream(fsCrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (FileStream fsIn = new FileStream(inputFile, FileMode.Open))
                            {
                                int data;
                                while ((data = fsIn.ReadByte()) != -1)
                                    cs.WriteByte((byte)data);
                            }
                        }
                    }
                }
            }
        }
        ///<summary>
        /// Steve Lydford - 12/05/2008.
        ///
        /// Decrypts a file using Rijndael algorithm.
        ///</summary>
        ///<param name="inputFile"></param>
        ///<param name="outputFile"></param>
        ///<param name="password"></param>
        public static void DecryptFile(string inputFile, string outputFile, string password)
        {
            using (RijndaelManaged aes = new RijndaelManaged())
            {
                byte[] key = ASCIIEncoding.UTF8.GetBytes(password);

                /* This is for demostrating purposes only. 
                 * Ideally you will want the IV key to be different from your key and you should always generate a new one for each encryption in other to achieve maximum security*/
                byte[] IV = ASCIIEncoding.UTF8.GetBytes(password);

                using (FileStream fsCrypt = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                {
                    if (File.Exists(outputFile)) File.Delete(outputFile);
                    using (FileStream fsOut = new FileStream(outputFile, FileMode.Create))
                    {
                        using (ICryptoTransform decryptor = aes.CreateDecryptor(key, IV))
                        {
                            using (CryptoStream cs = new CryptoStream(fsCrypt, decryptor, CryptoStreamMode.Read))
                            {
                                int data;
                                while ((data = cs.ReadByte()) != -1)
                                {
                                    fsOut.WriteByte((byte)data);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static string GetNodeFullName(XmlNode n)
        {
            var res = n.Name;
            while (n.ParentNode != null)
            {
                res = n.ParentNode.Name + "." + res;
                n = n.ParentNode;
            }
            var reg = new Regex(@"\._(\d+)\.");
            res = reg.Replace(res, (m) =>
            {
                return m.Value.Replace("._", "[").Replace(".", "].");
            });
            return res.Replace("#document.Root.", "");
        }
    }
    #endregion
}