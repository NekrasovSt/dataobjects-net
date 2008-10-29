// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Nick Svetlov
// Created:    2008.06.25

using System;
using System.Diagnostics;
using System.Reflection;
using PostSharp.Extensibility;
using PostSharp.Laos;
using Xtensive.Core.Aspects.Helpers;
using Xtensive.Core.Disposable;
using Xtensive.Core.Helpers;

namespace Xtensive.Storage.Aspects
{
  /// <summary>
  /// Activates session on method boundary.
  /// </summary>
  [MulticastAttributeUsage(MulticastTargets.Method)]
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
  [Serializable]
  internal sealed class SessionBoundMethodAspect : ImplementFastMethodBoundaryAspect,
    ILaosWeavableAspect
  {
    int ILaosWeavableAspect.AspectPriority {
      get {
        return (int) StorageAspectPriority.SessionBound;
      }
    }

    public override bool CompileTimeValidate(MethodBase method)
    {
      if (!AspectHelper.ValidateContextBoundMethod<Session>(this, method))
        return false;

      if (!AspectHelper.ValidateNotInfrastructure(this, method))
        return false;

      return true;
    }

    /// <inheritdoc/>
    [DebuggerStepThrough]
    public override object OnEntry(object instance)
    {
      var sessionBound = (SessionBound)instance;
      var sessionScope = (SessionScope)sessionBound.ActivateContext();
      return sessionScope;
    }

    /// <inheritdoc/>
    [DebuggerStepThrough]
    public override void OnExit(object instance, object onEntryResult)
    {
      var d = (IDisposable)onEntryResult;
      d.DisposeSafely();
    }
  }
}