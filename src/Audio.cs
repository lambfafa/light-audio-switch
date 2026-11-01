using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace LightAudioSwitch
{
    public sealed class AudioDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public uint State { get; set; }
        public bool Active { get { return State == 1; } }
        public override string ToString() { return Name + (Active ? "" : "（未连接 / 已禁用）"); }
    }

    // All COM operations run on the application's STA thread. Notification callbacks
    // only set a flag: never enumerate or release devices inside a COM callback.
    public sealed class AudioService : IDisposable
    {
        private IMMDeviceEnumerator enumerator;
        private DeviceNotifications notifications;
        private int changed = 1;
        private bool registered;
        public AudioService()
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            notifications = new DeviceNotifications(delegate { Interlocked.Exchange(ref changed, 1); });
            int hr = enumerator.RegisterEndpointNotificationCallback(notifications);
            registered = hr >= 0;
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
        }
        public bool ConsumeChanged() { return Interlocked.Exchange(ref changed, 0) != 0; }
        public List<AudioDevice> Enumerate()
        {
            var devices = new List<AudioDevice>();
            IMMDeviceCollection collection = null;
            try
            {
                Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0, 15, out collection));
                uint count;
                Marshal.ThrowExceptionForHR(collection.GetCount(out count));
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    IPropertyStore store = null;
                    try
                    {
                        Marshal.ThrowExceptionForHR(collection.Item(i, out device));
                        string id;
                        uint state;
                        Marshal.ThrowExceptionForHR(device.GetId(out id));
                        Marshal.ThrowExceptionForHR(device.GetState(out state));
                        Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out store));
                        PropertyKey key = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
                        PropVariant value;
                        Marshal.ThrowExceptionForHR(store.GetValue(ref key, out value));
                        string name;
                        try { name = value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : id; }
                        finally { PropVariantClear(ref value); }
                        devices.Add(new AudioDevice { Id = id, Name = name ?? id, State = state });
                    }
                    catch (COMException) { /* A device can disappear during enumeration. */ }
                    finally { Release(store); Release(device); }
                }
            }
            finally { Release(collection); }
            devices.Sort(delegate(AudioDevice a, AudioDevice b) {
                int c = b.Active.CompareTo(a.Active);
                return c != 0 ? c : StringComparer.CurrentCulture.Compare(a.Name, b.Name);
            });
            return devices;
        }
        public string GetDefault(int role)
        {
            IMMDevice device = null;
            try
            {
                int hr = enumerator.GetDefaultAudioEndpoint(0, role, out device);
                if (hr == unchecked((int)0x80070490)) return null; // No active endpoint.
                Marshal.ThrowExceptionForHR(hr);
                string id;
                Marshal.ThrowExceptionForHR(device.GetId(out id));
                return id;
            }
            finally { Release(device); }
        }
        public string[] GetDefaults() { return new string[] { GetDefault(0), GetDefault(1), GetDefault(2) }; }
        public bool IsDefault(string id, bool communications)
        {
            return String.Equals(GetDefault(0), id, StringComparison.OrdinalIgnoreCase)
                && String.Equals(GetDefault(1), id, StringComparison.OrdinalIgnoreCase)
                && (!communications || String.Equals(GetDefault(2), id, StringComparison.OrdinalIgnoreCase));
        }
        public void SetDefault(string id, bool communications)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentException("没有选择输出设备。");
            bool active = Enumerate().Exists(delegate(AudioDevice d) { return d.Active && d.Id == id; });
            if (!active) throw new InvalidOperationException("目标设备未连接或不可用，已保留当前输出。");
            string[] old = GetDefaults();
            int count = communications ? 3 : 2;
            object instance = null;
            int completed = 0;
            try
            {
                instance = new PolicyConfigClient();
                var policy = (IPolicyConfig)instance;
                for (int role = 0; role < count; role++)
                {
                    Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, role));
                    completed++;
                }
                if (!IsDefault(id, communications)) throw new InvalidOperationException("Windows 未确认设备切换，请稍后重试。");
            }
            catch
            {
                var policy = instance as IPolicyConfig;
                if (policy != null)
                    for (int role = 0; role < completed; role++)
                        if (!String.IsNullOrEmpty(old[role])) policy.SetDefaultEndpoint(old[role], role);
                throw;
            }
            finally { Release(instance); }
        }
        // Used by the verification harness to restore every role exactly as it was.
        public void RestoreDefaults(string[] ids)
        {
            object instance = new PolicyConfigClient();
            try
            {
                var policy = (IPolicyConfig)instance;
                for (int role = 0; role < Math.Min(3, ids.Length); role++)
                    if (!String.IsNullOrEmpty(ids[role])) Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(ids[role], role));
            }
            finally { Release(instance); }
        }
        private static void Release(object o) { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
        public void Dispose()
        {
            if (enumerator == null) return;
            if (registered) enumerator.UnregisterEndpointNotificationCallback(notifications);
            Release(enumerator);
            enumerator = null;
            notifications = null;
        }
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid Format;
        public uint Id;
        public PropertyKey(Guid format, uint id) { Format = format; Id = id; }
    }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IDeviceNotifications client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IDeviceNotifications client);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDeviceNotifications
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class DeviceNotifications : IDeviceNotifications
    {
        private readonly Action changed;
        public DeviceNotifications(Action changed) { this.changed = changed; }
        public int OnDeviceStateChanged(string id, uint state) { changed(); return 0; }
        public int OnDeviceAdded(string id) { changed(); return 0; }
        public int OnDeviceRemoved(string id) { changed(); return 0; }
        public int OnDefaultDeviceChanged(int flow, int role, string id) { if (flow == 0) changed(); return 0; }
        public int OnPropertyValueChanged(string id, PropertyKey key) { changed(); return 0; }
    }

    // IPolicyConfig is an undocumented Windows interface. Keep its vtable layout
    // isolated here; verify it on each supported Windows build.
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] internal class PolicyConfigClient { }
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(IntPtr id, IntPtr format);
        [PreserveSig] int GetDeviceFormat(IntPtr id, int flag, IntPtr format);
        [PreserveSig] int ResetDeviceFormat(IntPtr id);
        [PreserveSig] int SetDeviceFormat(IntPtr id, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod(IntPtr id, int flag, IntPtr defaultPeriod, IntPtr minimumPeriod);
        [PreserveSig] int SetProcessingPeriod(IntPtr id, IntPtr period);
        [PreserveSig] int GetShareMode(IntPtr id, IntPtr mode);
        [PreserveSig] int SetShareMode(IntPtr id, IntPtr mode);
        [PreserveSig] int GetPropertyValue(IntPtr id, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue(IntPtr id, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility(IntPtr id, int visible);
    }
}
