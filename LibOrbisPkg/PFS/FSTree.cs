using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// Base class for directories and files in a PFS image builder.
  /// </summary>
  public abstract class FSNode
  {
    /// <summary>
    /// The parent directory of this node. This should only be null for the root directory.
    /// </summary>
    public FSDir Parent = null;

    /// <summary>
    /// The name of this node.
    /// </summary>
    public string name;

    /// <summary>
    /// The inode describing this node.
    /// </summary>
    public inode ino;

    /// <summary>
    /// The actual size on disk of this node (i.e. the filesize or size of the dirent block)
    /// </summary>
    public virtual long Size { get; protected set; }

    /// <summary>
    /// The logical size of this file. Should only differ from Size in the case of a compressed (PFSC) file.
    /// </summary>
    public virtual long CompressedSize => Size;

    /// <summary>
    /// Get the full path of this file within the image.
    /// </summary>
    /// <param name="suffix">Optional suffix to append to the result</param>
    /// <returns></returns>
    public string FullPath(string suffix = "")
    {
      // Parent == null implies this is the root directory, which doesn't really have a name.
      if (Parent == null) return suffix;
      return Parent.FullPath("/" + name + suffix);
    }
  }

  /// <summary>
  /// Represents a directory in a PFS image builder.
  /// </summary>
  public class FSDir : FSNode
  {
    /// <summary>
    /// The directories in this directory.
    /// </summary>
    public List<FSDir> Dirs = new List<FSDir>();
    /// <summary>
    /// The files in this directory.
    /// </summary>
    public List<FSFile> Files = new List<FSFile>();

    /// <summary>
    /// The dirents describing the nodes in this directory.
    /// </summary>
    public List<PfsDirent> Dirents = new List<PfsDirent>();

    public override long Size
    {
      get { return Dirents.Sum(d => d.EntSize); }
    }

    /// <summary>
    /// Gets all the dirs and files in this directory and subdirectories.
    /// </summary>
    /// <returns>all the dirs and files in this directory and subdirectories</returns>
    public List<FSNode> GetAllChildren()
    {
      var ret = new List<FSNode>(GetAllChildrenDirs());
      ret.AddRange(GetAllChildrenFiles());
      return ret;
    }

    /// <summary>
    /// Gets all the dirs in this directory and subdirectories.
    /// </summary>
    /// <returns>all the dirs in this directory and subdirectories</returns>
    public List<FSDir> GetAllChildrenDirs()
    {
      var ret = new List<FSDir>(Dirs);
      foreach (var dir in Dirs)
        foreach (var child in dir.GetAllChildrenDirs())
          ret.Add(child);
      return ret;
    }

    /// <summary>
    /// Gets all the files in this directory and subdirectories.
    /// </summary>
    /// <returns>all the files in this directory and subdirectories</returns>
    public List<FSFile> GetAllChildrenFiles()
    {
      var ret = new List<FSFile>(Files);
      foreach (var dir in GetAllChildrenDirs())
        foreach (var f in dir.Files)
          ret.Add(f);
      return ret;
    }

    /// <summary>
    /// Gets the file at the given path relative to this directory.
    /// 
    /// For example, to get a file named "b" in a directory called "a" in this directory,
    /// you'd pass in "a/b".
    /// </summary>
    /// <param name="path">Relative path to the desired file</param>
    /// <returns>The file, or null if it can't be found.</returns>
    public FSFile GetFile(string path)
    {
      var breadcrumbs = path.Split('/');
      if(breadcrumbs.Length == 1)
      {
        return Files.Find(f => f.name == path);
      }
      var dir = Dirs.Find(d => d.name == breadcrumbs[0]);
      return dir?.GetFile(path.Substring(path.IndexOf('/') + 1));
    }
  }

  /// <summary>
  /// Represents a File in a PFS image builder.
  /// </summary>
  public class FSFile : FSNode
  {
    /// <summary>
    /// Creates an FSFile from a real on-disk file.
    /// You need to set the name, inode, and parent.
    /// </summary>
    /// <param name="origFileName">Real path to the file.</param>
    public FSFile(string origFileName)
    {
      Write = s => { using (var f = File.OpenRead(origFileName)) f.CopyTo(s); };
      Size = new FileInfo(origFileName).Length;
      _compressedSize = Size;
    }

    /// <summary>
    /// Creates an FSFile that represents the PFS image that will be created by the given PfsBuilder.
    /// Useful for creating the pfs_image.dat file within an outer PFS.
    /// You need to set the inode and parent.
    /// </summary>
    /// <param name="b">the PfsBuilder that this file represents</param>
    public FSFile(PfsBuilder b)
    {
      var pfsc = new PFSCWriter(b.CalculatePfsSize());
      Write = s =>
      {
        pfsc.WritePFSCHeader(s);
        b.WriteImage(new Util.OffsetStream(s, s.Position));
      };
      _compressedSize = b.CalculatePfsSize();
      Size = _compressedSize + pfsc.HeaderSize;
      name = "pfs_image.dat";
      Compress = true;
    }

    /// <summary>
    /// A generic constructor for anything that can be written to a stream.
    /// Don't forget to set the inode and parent.
    /// </summary>
    /// <param name="writer">A function that takes a Stream and writes this file's data to it.</param>
    /// <param name="name">This file's name</param>
    /// <param name="size">The total size in bytes that will be written by writer</param>
    public FSFile(Action<Stream> writer, string name, long size)
    {
      Write = writer;
      this.name = name;
      Size = size;
      _compressedSize = Size;
    }
    private long _compressedSize;
    public override long CompressedSize => _compressedSize;
    /// <summary>
    /// Call this to write the file to a stream.
    /// </summary>
    public readonly Action<Stream> Write;
    /// <summary>
    /// Flag for PFSC encoded files
    /// </summary>
    public bool Compress = false;
  }
}
