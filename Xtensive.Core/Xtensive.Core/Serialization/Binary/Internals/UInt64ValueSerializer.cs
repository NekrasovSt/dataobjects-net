// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Gamzov
// Created:    2007.11.14

using System;
using System.IO;

namespace Xtensive.Core.Serialization.Binary
{
  [Serializable]
  internal sealed class UInt64ValueSerializer : BinaryValueSerializerBase<ulong>
  {
    public override ulong Deserialize(Stream stream) 
    {
      int length = OutputLength;
      EnsureThreadBufferIsInitialized(length);
      stream.Read(ThreadBuffer, 0, length);
      return BitConverter.ToUInt64(ThreadBuffer, 0);
    }

    public override void Serialize(Stream stream, ulong value) 
    {
      stream.Write(BitConverter.GetBytes(value), 0, OutputLength);
    }

    
    // Constructors

    public UInt64ValueSerializer(IValueSerializerProvider<Stream> provider)
      : base(provider)
    {
      OutputLength = sizeof (ulong);
    }
  }
}