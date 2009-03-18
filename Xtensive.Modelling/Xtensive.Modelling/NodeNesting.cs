// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2009.03.18

using System;
using System.Reflection;
using System.Runtime.Serialization;
using Xtensive.Core;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Core.Reflection;
using Xtensive.Modelling.Resources;
using Xtensive.Core.Helpers;

namespace Xtensive.Modelling
{
  /// <summary>
  /// <see cref="INodeNesting{TParent,TProperty}"/> implementation.
  /// </summary>
  /// <typeparam name="TNode">The type of the node.</typeparam>
  /// <typeparam name="TParent">The type of the parent.</typeparam>
  /// <typeparam name="TProperty">The type of the property.</typeparam>
  [Serializable]
  public sealed class NodeNesting<TNode, TParent, TProperty> : INodeNesting<TParent, TProperty>,
    IDeserializationCallback
    where TNode: Node
    where TParent: Node
    where TProperty: IPathNode
  {
    [NonSerialized]
    private PropertyInfo propertyInfo;
    [NonSerialized]
    private bool isCollectionProperty;
    [NonSerialized]
    private Func<Node, TProperty> propertyAccessor;
    [NonSerialized]
    private Func<Node, IPathNode> untypedPropertyAccessor;

    /// <inheritdoc/>
    public string PropertyName { get; private set; }

    /// <inheritdoc/>
    public TNode Node { get; private set; }

    /// <inheritdoc/>
    public PropertyInfo PropertyInfo {
      get { return propertyInfo; }
    }

    /// <inheritdoc/>
    public Func<Node, TProperty> PropertyAccessor {
      get { return propertyAccessor; }
    }

    /// <inheritdoc/>
    public TProperty PropertyValue {
      get { return PropertyAccessor(Node); }
    }

    /// <inheritdoc/>
    public bool IsCollectionProperty {
      get { return isCollectionProperty; }
    }

    #region INodeNesting members

    Node INodeNesting.Node
    {
      get { return Node; }
    }

    Func<Node, IPathNode> INodeNesting.PropertyAccessor { 
      get { return untypedPropertyAccessor; }
    }

    IPathNode INodeNesting.PropertyValue {
      get { return PropertyValue; }
    }

    #endregion

    /// <exception cref="InvalidOperationException">Invalid property type.</exception>
    private void Initialize()
    {
      var tNode = typeof (TNode);
      var tProperty = typeof (TProperty);
      if (PropertyName.IsNullOrEmpty()) {
        PropertyName = null;
        propertyInfo = null;
        isCollectionProperty = false;
        propertyAccessor = null;
        untypedPropertyAccessor = null;
        return;
      }

      propertyInfo = tNode.GetProperty(PropertyName);
      if (propertyInfo.PropertyType!=tProperty)
        throw new InvalidOperationException(String.Format(
          Strings.ExTypeOfXPropertyMustBeY, 
          propertyInfo.GetShortName(true), tProperty.GetShortName()));
      isCollectionProperty = typeof (INodeCollection).IsAssignableFrom(tProperty);
      var typedAccessor = DelegateHelper.CreateGetMemberDelegate<TNode, TProperty>(PropertyName);
      if (typedAccessor==null)
        throw new InvalidOperationException(string.Format(
          Strings.ExBindingFailedForX, propertyInfo.GetShortName(true)));
      propertyAccessor = 
        n => typedAccessor.Invoke((TNode) n);
      untypedPropertyAccessor = 
        n => typedAccessor.Invoke((TNode) n);
    }


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="node"><see cref="Node"/> property value.</param>
    /// <param name="propertyName"><see cref="PropertyName"/> property value.</param>
    public NodeNesting(TNode node, string propertyName)
    {
      ArgumentValidator.EnsureArgumentNotNull(node, "node");
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(propertyName, "propertyName");
      Node = node;
      PropertyName = propertyName;
      Initialize();
    }

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="node"><see cref="Node"/> property value.</param>
    internal NodeNesting(TNode node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node, "node");
      Node = node;
      Initialize();
    }

    // Deserialization

    /// <inheritdoc/>
    public void OnDeserialization(object sender)
    {
      Initialize();
    }
  }
}