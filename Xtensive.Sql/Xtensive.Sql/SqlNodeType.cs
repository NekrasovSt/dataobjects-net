// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.

using System;

namespace Xtensive.Sql
{
  [Serializable]
  public enum SqlNodeType
  {
    Action,
    Add,
    All,
    Alter,
    And,
    Any,
    Array,
    Assign,
    Asterisk,
    Avg,
    Batch,
    BeginEndBlock,
    Between,
    BitAnd,
    BitNot,
    BitOr,
    BitXor,
    Break,
    Case,
    Cast,
    CloseCursor,
    Collate,
    Column,
    ColumnRef,
    Command,
    Concat,
    Conditional,
    Constant,
    Continue,
    Container,
    Count,
    Create,
    Cursor,
    DateTimePlusInterval,
    DateTimeMinusInterval,
    DateTimeMinusDateTime,
    DeclareCursor,
    DefaultValue,
    Delete,
    Divide,
    Drop,
    RawConcat,
    Equals,
    Except,
    Exists,
    Extract,
    Fetch,
    FunctionCall,
    Grant,
    GreaterThan,
    GreaterThanOrEquals,
    In,
    Insert,
    Intersect,
    IsNull,
    IsNotNull,
    Join,
    Hint,
    Placeholder,
    LessThan,
    LessThanOrEquals,
    Like,
    Literal,
    Match,
    Max,
    Min,
    Modulo,
    Multiply,
    NextValue,
    Not,
    NotBetween,
    NotEquals,
    NotIn,
    Negate,
    Null,
    OpenCursor,
    Or,
    Order,
    Overlaps,
    Parameter,
    Revoke,
    Rename,
    Row,
    RowNumber,
    Round,
    Select,
    SetDefault,
    Some,
    SubSelect,
    Subtract,
    Sum,
    Table,
    Trim,
    Union,
    Unique,
    Update,
    Variable,
    Variant,
    DeclareVariable,
    While,
  }
}