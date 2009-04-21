// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Kochetov
// Created:    2008.12.24

using System.Collections.Generic;
using Xtensive.Core;
using Xtensive.Storage.Linq.Rewriters;

namespace Xtensive.Storage.Linq.Expressions.Mappings
{
  internal interface IMapping
  {
    IMapping CreateShifted(int offset);
    List<int> GetColumns(bool entityAsKey);
    Segment<int> GetMemberSegment(MemberPath fieldPath);
    IMapping GetMemberMapping(MemberPath fieldPath);
    IMapping RewriteColumnIndexes(ItemProjectorRewriter rewriter);
  }
}