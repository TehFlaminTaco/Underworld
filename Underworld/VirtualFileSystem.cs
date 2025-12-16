using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Underworld;

public class VirtualFileSystem
{
    public Dictionary<string, byte[]> Files = new();

    public void AddFile(string path, byte[] data)
    {
        Files[path] = data;
    }

    // Get the File, case insensitive
    public byte[]? GetFile(string path)
    {
        foreach(var kvp in Files)
        {
            if (string.Equals(kvp.Key, path, System.StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        return null;
    }

    // Expects an <=8 character lump name
    // Accepts any file in the VFS of which the name matches (Case insensitive), or first 8 characters match
    public byte[]? GetLump(string lumpName)
    {
        foreach(var kvp in Files)
        {
            string fileName = Path.GetFileNameWithoutExtension(kvp.Key);
            fileName = fileName.Substring(0, System.Math.Min(8, fileName.Length));
            if (string.Equals(fileName, lumpName, System.StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
            
        }
        return null;
    }

    public List<byte[]> GetAllLumps(string lumpName)
    {
        List<byte[]> lumps = new();
        foreach(var kvp in Files)
        {
            string fileName = Path.GetFileNameWithoutExtension(kvp.Key);
            fileName = fileName.Substring(0, System.Math.Min(8, fileName.Length));
            if (string.Equals(fileName, lumpName, System.StringComparison.OrdinalIgnoreCase))
            {
                lumps.Add(kvp.Value);
            }
            
        }
        return lumps;
    }

    public bool TryGetFile(string path, [NotNullWhen(true)] out byte[]? data)
    {
        data = GetFile(path);
        return data != null;
    }

    public bool TryGetLump(string lumpName, [NotNullWhen(true)] out byte[]? data)
    {
        data = GetLump(lumpName);
        return data != null;
    }

    public void AddSubDirectory(string directoryPath, VirtualFileSystem subVFS)
    {
        foreach (var kvp in subVFS.Files)
        {
            string combinedPath = directoryPath.TrimEnd('/') + "/" + kvp.Key;
            Files[combinedPath] = kvp.Value;
        }
    }

    public static VirtualFileSystem CreateVFSFromPath(string wadPath)
    {
        // If it's a WAD, use WadVFS
        // If it's a Folder, use FolderVFS
        // If it's a PK3/ZIP, extract and use FolderVFS
        if (Directory.Exists(wadPath))
        {
            return CreateVFSFromFolder(wadPath);
        }
        else if (File.Exists(wadPath))
        {
            string extension = Path.GetExtension(wadPath).ToLowerInvariant();
            if (extension == ".wad")
            {
                return CreateVFSFromWAD(wadPath);
            }
            else if (extension == ".pk3" || extension == ".zip")
            {
                return CreateVFSFromZIP(wadPath);
            }
        }
        return new VirtualFileSystem();
    }

    public static VirtualFileSystem CreateVFSFromFolder(string folderPath)
    {
        VirtualFileSystem vfs = new();
        // Get all Directories and add them recursively
        foreach (var dirPath in Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly))
        {
            string dirName = Path.GetFileName(dirPath);
            var subVFS = CreateVFSFromFolder(dirPath);
            vfs.AddSubDirectory(dirName, subVFS);
        }
        // Get all Files and add them
        foreach (var filePath in Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(filePath);
            if(Path.GetExtension(fileName).ToLowerInvariant() == ".wad")
            {
                var wadVFS = CreateVFSFromWAD(filePath);
                vfs.AddSubDirectory(Path.GetFileNameWithoutExtension(fileName), wadVFS);
                continue;
            }
            byte[] data = File.ReadAllBytes(filePath);
            vfs.AddFile(fileName, data);
        }
        return vfs;
    }

    public static VirtualFileSystem CreateVFSFromZIP(string zipPath)
    {
        VirtualFileSystem vfs = new();
        using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    // It's a directory
                    continue;
                }
                using (var stream = entry.Open())
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    vfs.AddFile(entry.FullName.Replace('\\', '/'), ms.ToArray());
                }
            }
        }
        return vfs;
    }

    public static VirtualFileSystem CreateVFSFromWAD(string wadPath)
    {
        // Just fuckin, Parse the WAD
        VirtualFileSystem vfs = new();
        using BinaryReader reader = new BinaryReader(File.OpenRead(wadPath));

        reader.ReadChars(4);
        int numFiles = reader.ReadInt32();
        int offFAT = reader.ReadInt32();

        List<(string lumpName, int offset, int size)> lumpDictionary = new();
        reader.BaseStream.Seek(offFAT, SeekOrigin.Begin);

        for(int i = 0; i < numFiles; i++)
        {
            int offData = reader.ReadInt32();
            int lenData = reader.ReadInt32();
            char[] lumpNameChars = reader.ReadChars(8);
            string lumpName = new string(lumpNameChars).TrimEnd('\0');
            lumpDictionary.Add((lumpName, offData, lenData));
        }

        foreach(var lump in lumpDictionary)
        {
            reader.BaseStream.Seek(lump.offset, SeekOrigin.Begin);
            byte[] data = reader.ReadBytes(lump.size);
            vfs.AddFile(lump.lumpName, data);
        }

        return vfs;
    }
}