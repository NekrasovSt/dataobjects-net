// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2009.03.17

using System;
using System.Diagnostics;
using Xtensive.Core.Internals.DocTemplates;

namespace Xtensive.Core.Security
{
  /// <summary>
  /// Delegate-based security token provider.
  /// </summary>
  [Serializable]
  public sealed class DelegateSecurityTokenProvider : ISecurityTokenProvider
  {
    private readonly Func<string, string, string> getSecurityToken;

    /// <inheritdoc/>
    public string GetSecurityToken(string userName, string options)
    {
      return getSecurityToken(userName, options);
    }

    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="getSecurityToken">The <see cref="GetSecurityToken"/> handler.</param>
    public DelegateSecurityTokenProvider(Func<string, string, string> getSecurityToken)
    {
      this.getSecurityToken = getSecurityToken;
    }
  }
}