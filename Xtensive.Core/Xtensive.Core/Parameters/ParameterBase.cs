// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Kofman
// Created:    2008.08.14

using System;
using System.Diagnostics;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Core.Reflection;
using Xtensive.Core.Resources;

namespace Xtensive.Core.Parameters
{
  /// <summary>
  /// Base class for <see cref="Parameter{TValue}"/>.
  /// </summary>
  public abstract class ParameterBase
  {
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>    
    public string Name { get; private set;}

    /// <summary>
    /// Gets or sets the value of the parameter.
    /// </summary>
    public object Value {
      [DebuggerStepThrough]
      get { return GetValue(); }
      [DebuggerStepThrough]
      set { SetValue(value); }
    }

    /// <summary>
    /// Gets the value of the parameter.
    /// </summary>
    /// <returns>Parameter value.</returns>
    protected abstract object GetValue();

    /// <summary>
    /// Sets the value of the parameter.
    /// </summary>
    /// <param name="value">The new value.</param>
    protected abstract void SetValue(object value);

    /// <inheritdoc/>
    public override string ToString()
    {
      return Name;
    }

    
    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="name">The <see cref="Name"/> property value.</param>
    [DebuggerStepThrough]
    protected ParameterBase(string name)
    {
      ArgumentValidator.EnsureArgumentNotNull(name, "name");
      Name = name;
    }
  }
}