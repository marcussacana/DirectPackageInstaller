using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// Represents the flat_path_table file, which is a mapping of filename hash to inode number
  /// that the Orbis OS can use to speed up lookups.
  /// </summary>
  public class FlatPathTable
  {
    public static bool HasCollision(List<FSNode> nodes)
    {
      var hashSet = new HashSet<uint>();
      foreach(var n in nodes)
      {
        var hash = HashFunction(n.FullPath());
        if (hashSet.Contains(hash))
          return true;
        hashSet.Add(hash);
      }
      return false;
    }
    public static Tuple<FlatPathTable,CollisionResolver> Create(List<FSNode> nodes)
    {
      var hashMap = new SortedDictionary<uint, uint>();
      var nodeMap = new Dictionary<uint, List<FSNode>>();
      bool collision = false;
      foreach (var n in nodes)
      {
        var hash = HashFunction(n.FullPath());
        if (hashMap.ContainsKey(hash))
        {
          hashMap[hash] = 0x80000000;
          nodeMap[hash].Add(n);
          collision = true;
        }
        else
        {
          hashMap[hash] = n.ino.Number | (n is FSDir ? 0x20000000u : 0u);
          nodeMap[hash] = new List<FSNode>();
          nodeMap[hash].Add(n);
        }
      }
      if(!collision)
      {
        return Tuple.Create(new FlatPathTable(hashMap), (CollisionResolver)null);
      }

      uint offset = 0;
      var colEnts = new List<List<PfsDirent>>();
      foreach(var kv in hashMap.Where(kv => kv.Value == 0x80000000).ToList())
      {
        hashMap[kv.Key] = 0x80000000 | offset;
        var entList = new List<PfsDirent>();
        colEnts.Add(entList);
        foreach(var node in nodeMap[kv.Key])
        {
          var d = new PfsDirent()
          {
            InodeNumber = node.ino.Number,
            Type = node is FSDir ? DirentType.Directory : DirentType.File,
            Name = node.FullPath(),
          };
          entList.Add(d);
          offset += (uint)d.EntSize;
        }
        offset += 0x18;
      }
      return Tuple.Create(new FlatPathTable(hashMap), new CollisionResolver(colEnts));
    }

    private SortedDictionary<uint, uint> hashMap;

    public int Size => hashMap.Count * 8;

    /// <summary>
    /// Construct a flat_path_table out of the given filesystem nodes.
    /// </summary>
    /// <param name="nodes"></param>
    public FlatPathTable(SortedDictionary<uint, uint> hashMap)
    {
      this.hashMap = hashMap;
    }

    /// <summary>
    /// Write this file to the stream.
    /// </summary>
    /// <param name="s"></param>
    public void WriteToStream(Stream s)
    {
      foreach (var hash in hashMap.Keys)
      {
        s.WriteUInt32LE(hash);
        s.WriteUInt32LE(hashMap[hash]);
      }
    }

    /// <summary>
    /// Hashes the given name for the table.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static uint HashFunction(string name)
    {
      uint hash = 0;
      foreach (var c in name)
        hash = char.ToUpper(c) + (31 * hash);
      return hash;
    }
  }

  public class CollisionResolver
  {
    public int Size { get; }

    public CollisionResolver(List<List<PfsDirent>> ents)
    {
      Entries = ents;
      var size = 0;
      foreach(var l in ents)
      {
        foreach(var e in l)
        {
          size += e.EntSize;
        }
        size += 0x18;
      }
      Size = size;
    }

    private List<List<PfsDirent>> Entries;
    public void WriteToStream(Stream s)
    {
      foreach(var d in Entries)
      {
        foreach(var e in d)
        {
          e.WriteToStream(s);
        }
        s.Position += 0x18;
      }
    }
  }
}