// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2009.03.18

namespace Xtensive.Modelling
{
  /// <summary>
  /// Node with specified <see cref="Node.Parent"/> type.
  /// </summary>
  /// <typeparam name="TParent">The type of the parent.</typeparam>
  public interface INode<TParent> : INode
    where TParent : Node
  {
    /// <summary>
    /// Gets the parent node.
    /// </summary>
    new TParent Parent { get; }
  }
}