using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Godmist.Editor
{
    /// <summary>Preserve authored pixel icons instead of Unity's interpolated intermediate sizes.</summary>
    public static class GodmistWindowsIconPostprocess
    {
        const string IconPath = "Assets/_Project/Art/Icons/Application/Ghorn/Godmist-Ghorn.ico";

        [PostProcessBuild(1000)]
        public static void Apply(BuildTarget target, string executable)
        {
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64) return;
            if (!File.Exists(IconPath)) return;
#if UNITY_EDITOR_WIN
            var bytes = File.ReadAllBytes(IconPath);
            using var input = new BinaryReader(new MemoryStream(bytes));
            if (input.ReadUInt16() != 0 || input.ReadUInt16() != 1) throw new InvalidDataException("Invalid application ICO.");
            var count = input.ReadUInt16();
            var entries = new byte[count][];
            var images = new byte[count][];
            for (var i = 0; i < count; i++)
            {
                entries[i] = input.ReadBytes(12);
                var offset = input.ReadUInt32();
                var length = BitConverter.ToUInt32(entries[i], 8);
                images[i] = new byte[length];
                Array.Copy(bytes, offset, images[i], 0, length);
            }
            // Find the icon group already supplied by Unity; preserve its ID and language.
            var module = LoadLibraryEx(executable, IntPtr.Zero, 2 | 32);
            if (module == IntPtr.Zero) throw new Win32Exception();
            var groups = new System.Collections.Generic.List<(IntPtr id, ushort language)>();
            try
            {
                NameCallback names = (handle, type, name, parameter) =>
                {
                    if (name.ToInt64() > ushort.MaxValue) throw new InvalidDataException("Expected numeric Windows icon group ID.");
                    LanguageCallback languages = (h, t, n, language, p) => { groups.Add((name, language)); return true; };
                    if (!EnumResourceLanguages(handle, (IntPtr)14, name, languages, IntPtr.Zero)) throw new Win32Exception();
                    return true;
                };
                if (!EnumResourceNames(module, (IntPtr)14, names, IntPtr.Zero)) throw new Win32Exception();
            }
            finally { FreeLibrary(module); }
            var update = BeginUpdateResource(executable, false);
            if (update == IntPtr.Zero) throw new Win32Exception();
            var committed = false;
            try
            {
                foreach (var group in groups)
                {
                    using var stream = new MemoryStream();
                    using var writer = new BinaryWriter(stream);
                    writer.Write((ushort)0); writer.Write((ushort)1); writer.Write(count);
                    for (var i = 0; i < count; i++)
                    {
                        var id = (ushort)(5000 + i);
                        if (!UpdateResource(update, (IntPtr)3, (IntPtr)id, group.language, images[i], (uint)images[i].Length)) throw new Win32Exception();
                        writer.Write(entries[i]); writer.Write(id);
                    }
                    var data = stream.ToArray();
                    if (!UpdateResource(update, (IntPtr)14, group.id, group.language, data, (uint)data.Length)) throw new Win32Exception();
                }
                if (!EndUpdateResource(update, false)) throw new Win32Exception();
                committed = true;
            }
            finally { if (!committed) EndUpdateResource(update, true); }
#endif
        }

#if UNITY_EDITOR_WIN
        delegate bool NameCallback(IntPtr module, IntPtr type, IntPtr name, IntPtr parameter);
        delegate bool LanguageCallback(IntPtr module, IntPtr type, IntPtr name, ushort language, IntPtr parameter);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        [DllImport("kernel32.dll", EntryPoint = "EnumResourceNamesW", SetLastError = true)] static extern bool EnumResourceNames(IntPtr module, IntPtr type, NameCallback callback, IntPtr parameter);
        [DllImport("kernel32.dll", EntryPoint = "EnumResourceLanguagesW", SetLastError = true)] static extern bool EnumResourceLanguages(IntPtr module, IntPtr type, IntPtr name, LanguageCallback callback, IntPtr parameter);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr BeginUpdateResource(string path, bool deleteExisting);
        [DllImport("kernel32.dll", EntryPoint = "UpdateResourceW", SetLastError = true)] static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool EndUpdateResource(IntPtr update, bool discard);
#endif
    }
}
