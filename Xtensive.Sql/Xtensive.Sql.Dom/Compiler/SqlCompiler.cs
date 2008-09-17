// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.

using System;
using System.Collections.Generic;
using Xtensive.Core;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Sql.Dom.Compiler.Internals;
using Xtensive.Sql.Dom.Database;
using Xtensive.Sql.Dom.Ddl;
using Xtensive.Sql.Dom.Dml;
using Xtensive.Sql.Dom.Exceptions;
using Xtensive.Sql.Dom.Resources;

namespace Xtensive.Sql.Dom.Compiler
{
  public class SqlCompiler : ISqlVisitor
  {
    protected readonly SqlTranslator translator;
    private readonly Formatter formatter = new Formatter();
    protected SqlCompilerOptions options;
    protected SqlCompilerContext context;

    public SqlCompilerResults Compile(ISqlCompileUnit unit)
    {
      return Compile(unit, SqlCompilerOptions.Default);
    }

    private SqlCompilerResults Compile(ISqlCompileUnit unit, SqlCompilerOptions options)
    {
      ArgumentValidator.EnsureArgumentNotNull(unit, "unit");
      this.options = options;
      OnBeginCompile();
      unit.AcceptVisitor(this);
      OnEndCompile();
      return new SqlCompilerResults(formatter.Format(context.Output));
    }

    protected virtual void OnBeginCompile()
    {
      context = new SqlCompilerContext();
    }

    protected virtual void OnEndCompile()
    {
      context.AliasProvider.Restore();
    }

    #region IVisitor Members

    public virtual void Visit(SqlAggregate node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Expression.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }
    
    public virtual void Visit(SqlAlterDomain node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, AlterDomainSection.Entry));
        if (node.Action is SqlAddConstraint) {
          DomainConstraint constraint = ((SqlAddConstraint)node.Action).Constraint as DomainConstraint;
          context.AppendText(translator.Translate(context, node, AlterDomainSection.AddConstraint));
          context.AppendText(translator.Translate(context, constraint, ConstraintSection.Entry));
          context.AppendText(translator.Translate(context, constraint, ConstraintSection.Check));
          constraint.Condition.AcceptVisitor(this);
          context.AppendText(translator.Translate(context, constraint, ConstraintSection.Exit));
        }
        else if (node.Action is SqlDropConstraint) {
          SqlDropConstraint action = node.Action as SqlDropConstraint;
          context.AppendText(translator.Translate(context, node, AlterDomainSection.DropConstraint));
          context.AppendText(translator.Translate(context, action.Constraint, ConstraintSection.Entry));
        }
        else if (node.Action is SqlSetDefault) {
          SqlSetDefault action = node.Action as SqlSetDefault;
          context.AppendText(translator.Translate(context, node, AlterDomainSection.SetDefault));
          action.DefaultValue.AcceptVisitor(this);
        }
        else if (node.Action is SqlDropDefault)
          context.AppendText(translator.Translate(context, node, AlterDomainSection.DropDefault));
        context.AppendText(translator.Translate(context, node, AlterDomainSection.Exit));
      }
    }

    public virtual void Visit(SqlAlterPartitionFunction node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlAlterPartitionScheme node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlAlterTable node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, AlterTableSection.Entry));
        if (node.Action is SqlAddColumn) {
          TableColumn column = ((SqlAddColumn)node.Action).Column;
          context.AppendText(translator.Translate(context, node, AlterTableSection.AddColumn));
          Visit(column);
        }
        else if (node.Action is SqlDropDefault) {
          TableColumn column = ((SqlDropDefault)node.Action).Column;
          context.AppendText(translator.Translate(context, node, AlterTableSection.AlterColumn));
          context.AppendText(translator.Translate(context, column, TableColumnSection.Entry));
          context.AppendText(translator.Translate(context, column, TableColumnSection.DropDefault));
        }
        else if (node.Action is SqlSetDefault) {
          SqlSetDefault action = node.Action as SqlSetDefault;
          context.AppendText(translator.Translate(context, node, AlterTableSection.AlterColumn));
          context.AppendText(translator.Translate(context, action.Column, TableColumnSection.Entry));
          context.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetDefault));
          action.DefaultValue.AcceptVisitor(this);
        }
        else if (node.Action is SqlDropColumn) {
          SqlDropColumn action = node.Action as SqlDropColumn;
          context.AppendText(translator.Translate(context, node, AlterTableSection.DropColumn));
          context.AppendText(translator.Translate(context, action.Column, TableColumnSection.Entry));
          context.AppendText(translator.Translate(context, action.Cascade, AlterTableSection.DropBehavior));
        }
        else if (node.Action is SqlAlterIdentityInfo) {
          SqlAlterIdentityInfo action = node.Action as SqlAlterIdentityInfo;
          context.AppendText(translator.Translate(context, node, AlterTableSection.AlterColumn));
          context.AppendText(translator.Translate(context, action.Column, TableColumnSection.Entry));
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.RestartWithOption)!=0)
            context.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.RestartValue));
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.IncrementByOption)!=0) {
            if (action.SequenceDescriptor.Increment.HasValue && action.SequenceDescriptor.Increment.Value==0)
              throw new SqlCompilerException("Increment must not be 0.");
            context.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetIdentityInfoElement));
            context.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.Increment));
          }
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.MaxValueOption)!=0) {
            context.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetIdentityInfoElement));
            context.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.AlterMaxValue));
          }
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.MinValueOption)!=0) {
            context.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetIdentityInfoElement));
            context.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.AlterMinValue));
          }
          if ((action.InfoOption & SqlAlterIdentityInfoOptions.CycleOption)!=0) {
            context.AppendText(translator.Translate(context, action.Column, TableColumnSection.SetIdentityInfoElement));
            context.AppendText(translator.Translate(context, action.SequenceDescriptor, SequenceDescriptorSection.IsCyclic));
          }
        }
        else if (node.Action is SqlAddConstraint) {
          TableConstraint constraint = ((SqlAddConstraint) node.Action).Constraint as TableConstraint;
          context.AppendText(translator.Translate(context, node, AlterTableSection.AddConstraint));
          Visit(constraint);
        }
        else if (node.Action is SqlDropConstraint) {
          SqlDropConstraint action = node.Action as SqlDropConstraint;
          TableConstraint constraint = action.Constraint as TableConstraint;
          context.AppendText(translator.Translate(context, node, AlterTableSection.DropConstraint));
          context.AppendText(translator.Translate(context, constraint, ConstraintSection.Entry));
          context.AppendText(translator.Translate(context, action.Cascade, AlterTableSection.DropBehavior));
        }
        context.AppendText(translator.Translate(context, node, AlterTableSection.Exit));
      }
    }

    public virtual void Visit(SqlAlterSequence node)
    {
      context.AppendText(translator.Translate(context, node, NodeSection.Entry));
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.RestartWithOption)!=0)
        context.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.RestartValue));
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.IncrementByOption)!=0) {
        if (node.SequenceDescriptor.Increment.HasValue && node.SequenceDescriptor.Increment.Value==0)
          throw new SqlCompilerException("Increment must not be 0.");
        context.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.Increment));
      }
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.MaxValueOption)!=0)
        context.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.AlterMaxValue));
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.MinValueOption)!=0)
        context.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.AlterMinValue));
      if ((node.InfoOption & SqlAlterIdentityInfoOptions.CycleOption)!=0)
        context.AppendText(translator.Translate(context, node.SequenceDescriptor, SequenceDescriptorSection.IsCyclic));
      context.AppendText(translator.Translate(context, node, NodeSection.Exit));
    }

    public virtual void Visit<T>(SqlArray<T> node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlAssignment node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Left.AcceptVisitor(this);
        context.AppendText(translator.Translate(node.NodeType));
        node.Right.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlBatch node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        using (context.EnterCollection()) {
          foreach (SqlStatement item in node) {
            item.AcceptVisitor(this);
            context.AppendDelimiter(translator.BatchStatementDelimiter, DelimiterType.Column);
          }
        }
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlBetween node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, BetweenSection.Entry));
        node.Expression.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, BetweenSection.Between));
        node.Left.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, BetweenSection.And));
        node.Right.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, BetweenSection.Exit));
      }
    }

    public virtual void Visit(SqlBinary node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Left.AcceptVisitor(this);
        context.AppendText(translator.Translate(node.NodeType));
        node.Right.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlBreak node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCase node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, CaseSection.Entry));

        if (!SqlExpression.IsNull(node.Value)) {
          context.AppendText(translator.Translate(context, node, CaseSection.Value));
          node.Value.AcceptVisitor(this);
        }

        using (context.EnterCollection()) {
          foreach (KeyValuePair<SqlExpression, SqlExpression> item in node) {
            if (!context.IsEmpty)
              context.AppendDelimiter(translator.WhenDelimiter);
            context.AppendText(translator.Translate(context, node, item.Key, CaseSection.When));
            item.Key.AcceptVisitor(this);
            context.AppendText(translator.Translate(context, node, item.Value, CaseSection.Then));
            item.Value.AcceptVisitor(this);
          }
        }

        if (!SqlExpression.IsNull(node.Else)) {
          context.AppendText(translator.Translate(context, node, CaseSection.Else));
          node.Else.AcceptVisitor(this);
        }

        context.AppendText(translator.Translate(context, node, CaseSection.Exit));
      }
    }

    public virtual void Visit(SqlCast node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Operand.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlCloseCursor node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCollate node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Operand.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlColumnRef node)
    {
      context.AppendText(translator.Translate(context, node, ColumnSection.Entry));
    }

    public virtual void Visit(SqlNative node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlNativeHint node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlContinue node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateAssertion node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Assertion.Condition.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlCreateCharacterSet node)
    {
//      ArgumentValidator.EnsureArgumentNotNull(node.CharacterSet.CharacterSetSource, "CharacterSetSource");
//      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateCollation node)
    {
//      ArgumentValidator.EnsureArgumentNotNull(node.Collation.CharacterSet, "CharacterSet");
//      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateDomain node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.Domain.DataType, "DataType");
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, CreateDomainSection.Entry));
        if (!SqlExpression.IsNull(node.Domain.DefaultValue)) {
          context.AppendText(translator.Translate(context, node, CreateDomainSection.DomainDefaultValue));
          node.Domain.DefaultValue.AcceptVisitor(this);
        }
        if (node.Domain.DomainConstraints.Count!=0) {
          using (context.EnterCollection())
            foreach (DomainConstraint constraint in node.Domain.DomainConstraints) {
              context.AppendText(translator.Translate(context, constraint, ConstraintSection.Entry));
              context.AppendText(translator.Translate(context, constraint, ConstraintSection.Check));
              constraint.Condition.AcceptVisitor(this);
              context.AppendText(translator.Translate(context, constraint, ConstraintSection.Exit));
            }
        }
        if (node.Domain.Collation!=null)
          context.AppendText(translator.Translate(context, node, CreateDomainSection.DomainCollate));
        context.AppendText(translator.Translate(context, node, CreateDomainSection.Exit));
      }
    }

    public virtual void Visit(SqlCreateIndex node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.Index.DataTable, "DataTable");
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreatePartitionFunction node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.PartitionFunction.DataType, "DataType");
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreatePartitionScheme node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.PartitionSchema.PartitionFunction, "PartitionFunction");
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateSchema node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
        if (node.Schema.Assertions.Count>0)
          using (context.EnterCollection())
            foreach (Assertion assertion in node.Schema.Assertions) {
              new SqlCreateAssertion(assertion).AcceptVisitor(this);
              context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
            }

        if (node.Schema.CharacterSets.Count>0)
          using (context.EnterCollection())
            foreach (CharacterSet characterSet in node.Schema.CharacterSets) {
              new SqlCreateCharacterSet(characterSet).AcceptVisitor(this);
              context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
            }

        if (node.Schema.Collations.Count>0)
          using (context.EnterCollection())
            foreach (Collation collation in node.Schema.Collations) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
              new SqlCreateCollation(collation).AcceptVisitor(this);
            }

        if (node.Schema.Domains.Count>0)
          using (context.EnterCollection())
            foreach (Domain domain in node.Schema.Domains) {
              new SqlCreateDomain(domain).AcceptVisitor(this);
              context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
            }

        if (node.Schema.Sequences.Count>0)
          using (context.EnterCollection())
            foreach (Sequence sequence in node.Schema.Sequences) {
              new SqlCreateSequence(sequence).AcceptVisitor(this);
              context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
            }

        if (node.Schema.Tables.Count>0)
          using (context.EnterCollection())
            foreach (Table table in node.Schema.Tables) {
              new SqlCreateTable(table).AcceptVisitor(this);
              context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
              if (table.Indexes.Count>0)
                using (context.EnterCollection())
                  foreach (Index index in table.Indexes) {
                    new SqlCreateIndex(index).AcceptVisitor(this);
                    context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
                  }
            }

        if (node.Schema.Translations.Count>0)
          using (context.EnterCollection())
            foreach (Translation translation in node.Schema.Translations) {
              new SqlCreateTranslation(translation).AcceptVisitor(this);
              context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
            }

        if (node.Schema.Views.Count>0)
          using (context.EnterCollection())
            foreach (View view in node.Schema.Views) {
              new SqlCreateView(view).AcceptVisitor(this);
              context.AppendDelimiter(translator.DdlStatementDelimiter, DelimiterType.Column);
            }

        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }
    
    public virtual void Visit(SqlCreateSequence node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.Sequence.DataType, "DataType");
      ArgumentValidator.EnsureArgumentNotNull(node.Sequence.SequenceDescriptor, "SequenceDescriptor");
      using (context.EnterNode(node)) {
        if (node.Sequence.DataType!=null &&
            (!SqlValueType.IsExactNumeric(node.Sequence.DataType) ||
             node.Sequence.DataType.Scale!=0))
          throw new SqlCompilerException("The data type must be exact numeric with scale 0.");
        if (node.Sequence.SequenceDescriptor.Increment.HasValue && node.Sequence.SequenceDescriptor.Increment.Value==0)
            throw new SqlCompilerException("Increment must not be 0.");
        if (String.IsNullOrEmpty(node.Sequence.Name))
          throw new SqlCompilerException("Name must be not null or empty.");
        if (node.Sequence.Schema==null)
          throw new SqlCompilerException("Schema must be not null.");
        if (node.Sequence.SequenceDescriptor.MaxValue.HasValue &&
            node.Sequence.SequenceDescriptor.MinValue.HasValue &&
            node.Sequence.SequenceDescriptor.MaxValue.Value<=node.Sequence.SequenceDescriptor.MinValue.Value)
          throw new SqlCompilerException("The maximum value must be greater than the minimum value.");
        if (node.Sequence.SequenceDescriptor.StartValue.HasValue &&
            (node.Sequence.SequenceDescriptor.MaxValue.HasValue &&
             node.Sequence.SequenceDescriptor.MaxValue.Value<node.Sequence.SequenceDescriptor.StartValue.Value ||
             node.Sequence.SequenceDescriptor.MinValue.HasValue &&
             node.Sequence.SequenceDescriptor.MinValue.Value>node.Sequence.SequenceDescriptor.StartValue.Value))
          throw new SqlCompilerException("The start value must lie between the minimum and maximum value.");
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        context.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.StartValue));
        context.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.Increment));
        context.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.MaxValue));
        context.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.MinValue));
        context.AppendText(translator.Translate(context, node.Sequence.SequenceDescriptor, SequenceDescriptorSection.IsCyclic));
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlCreateTable node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, CreateTableSection.Entry));
        context.AppendText(translator.Translate(context, node, CreateTableSection.TableElementsEntry));

        bool first = true;
        if (node.Table.Columns.Count>0)
          using (context.EnterCollection()) {
            foreach (TableColumn c in node.Table.Columns) {
              if (!context.IsEmpty) {
                context.AppendDelimiter(translator.ColumnDelimiter, DelimiterType.Column);
                first = false;
              }
              Visit(c);
            }
          }

        if (node.Table.TableConstraints.Count>0)
          using (context.EnterCollection()) {
            foreach (TableConstraint cs in node.Table.TableConstraints) {
              if (!context.IsEmpty || !first)
                context.AppendDelimiter(translator.ColumnDelimiter, DelimiterType.Column);
              Visit(cs);
            }
          }
        context.AppendText(translator.Translate(context, node, CreateTableSection.TableElementsExit));
        if (node.Table.PartitionDescriptor!=null) {
          context.AppendDelimiter(translator.ColumnDelimiter, DelimiterType.Column);
          context.AppendText(translator.Translate(context, node, CreateTableSection.Partition));
        }
        context.AppendText(translator.Translate(context, node, CreateTableSection.Exit));
      }
    }

    public virtual void Visit(SqlCreateTranslation node)
    {
//      ArgumentValidator.EnsureArgumentNotNull(node.Translation.SourceCharacterSet, "SourceCharacterSet");
//      ArgumentValidator.EnsureArgumentNotNull(node.Translation.TargetCharacterSet, "TargetCharacterSet");
//      ArgumentValidator.EnsureArgumentNotNull(node.Translation.TranslationSource, "TranslationSource");
//      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlCreateView node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.View.Definition.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlCursor node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDeclareCursor node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.Entry));
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.Sensivity));
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.Scrollability));
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.Cursor));
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.Holdability));
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.Returnability));
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.For));

        node.Cursor.Query.AcceptVisitor(this);
        if (node.Cursor.Columns.Count!=0) {
          foreach (SqlColumnRef item in node.Cursor.Columns) {
            item.AcceptVisitor(this);
          }
        }
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.Updatability));
        context.AppendText(translator.Translate(context, node, DeclareCursorSection.Exit));
      }
    }

    public virtual void Visit(SqlDeclareVariable node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDefaultValue node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDelete node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, DeleteSection.Entry));
        if (node.From==null)
          throw new SqlCompilerException(Strings.ExTablePropertyIsNotSet);
        node.From.AcceptVisitor(this);
        if (!SqlExpression.IsNull(node.Where)) {
          context.AppendText(translator.Translate(context, node, DeleteSection.Where));
          node.Where.AcceptVisitor(this);
        }
        context.AppendText(translator.Translate(context, node, DeleteSection.Exit));
      }
    }

    public virtual void Visit(SqlDropAssertion node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropCharacterSet node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropCollation node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropDomain node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropIndex node)
    {
      ArgumentValidator.EnsureArgumentNotNull(node.Index.DataTable, "DataTable");
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropPartitionFunction node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropPartitionScheme node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropSchema node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropSequence node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropTable node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropTranslation node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlDropView node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlFastFirstRowsHint node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlFetch node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, FetchSection.Entry));
        if (!SqlExpression.IsNull(node.RowCount))
          node.RowCount.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, FetchSection.Targets));
        foreach (ISqlCursorFetchTarget item in node.Targets) {
          item.AcceptVisitor(this);
        }
        context.AppendText(translator.Translate(context, node, FetchSection.Exit));
      }
    }

    public virtual void Visit(SqlForceJoinOrderHint node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlFunctionCall node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, FunctionCallSection.Entry, -1));
        if (node.Arguments.Count>0) {
          using (context.EnterCollection()) {
            int argumentPosition = 0;
            foreach (SqlExpression item in node.Arguments) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.Translate(context, node, FunctionCallSection.ArgumentDelimiter, argumentPosition));
              context.AppendText(translator.Translate(context, node, FunctionCallSection.ArgumentEntry, argumentPosition));
              item.AcceptVisitor(this);
              context.AppendText(translator.Translate(context, node, FunctionCallSection.ArgumentExit, argumentPosition));
              argumentPosition++;
            }
          }
        }
        context.AppendText(translator.Translate(context, node, FunctionCallSection.Exit, -1));
      }
    }

    public virtual void Visit(SqlIf node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, IfSection.Entry));

        node.Condition.AcceptVisitor(this);

        context.AppendText(translator.Translate(context, node, IfSection.True));
        node.True.AcceptVisitor(this);

        if (node.False!=null) {
          context.AppendText(translator.Translate(context, node, IfSection.False));
          node.False.AcceptVisitor(this);
        }

        context.AppendText(translator.Translate(context, node, IfSection.Exit));
      }
    }

    public virtual void Visit(SqlInsert node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, InsertSection.Entry));

        if (node.Into==null)
          throw new SqlCompilerException(Strings.ExTablePropertyIsNotSet);
        node.Into.AcceptVisitor(this);

        context.AppendText(translator.Translate(context, node, InsertSection.ColumnsEntry));
        if (node.Values.Keys.Count > 0)
          using (context.EnterCollection())
            foreach (SqlColumn item in node.Values.Keys) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.ColumnDelimiter);
              item.AcceptVisitor(this);
            }
        context.AppendText(translator.Translate(context, node, InsertSection.ColumnsExit));

        if (node.Values.Keys.Count == 0)
          context.AppendText(translator.Translate(context, node, InsertSection.DefaultValues));
        else {
          context.AppendText(translator.Translate(context, node, InsertSection.ValuesEntry));
          using (context.EnterCollection())
            foreach (SqlExpression item in node.Values.Values) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.ColumnDelimiter);
              item.AcceptVisitor(this);
            }
          context.AppendText(translator.Translate(context, node, InsertSection.ValuesExit));
        }
        context.AppendText(translator.Translate(context, node, InsertSection.Exit));
      }
    }

    public virtual void Visit(SqlJoinExpression node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, JoinSection.Entry));
        node.Left.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, JoinSection.Specification));
        node.Right.AcceptVisitor(this);
        if (!SqlExpression.IsNull(node.Expression)) {
          context.AppendText(translator.Translate(context, node, JoinSection.Condition));
          node.Expression.AcceptVisitor(this);
        }
        context.AppendText(translator.Translate(context, node, JoinSection.Exit));
      }
    }

    public virtual void Visit(SqlJoinHint node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlLike node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, LikeSection.Entry));
        node.Expression.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, LikeSection.Like));
        node.Pattern.AcceptVisitor(this);
        if (!SqlExpression.IsNull(node.Escape)) {
          context.AppendText(translator.Translate(context, node, LikeSection.Escape));
          node.Escape.AcceptVisitor(this);
        }
        context.AppendText(translator.Translate(context, node, LikeSection.Exit));
      }
    }

    public virtual void Visit<T>(SqlLiteral<T> node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlMatch node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, MatchSection.Entry));
        node.Value.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, MatchSection.Specification));
        node.SubQuery.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, MatchSection.Exit));
      }
    }

    public virtual void Visit(SqlNull node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlNextValue node)
    {
      context.AppendText(translator.Translate(context, node, NodeSection.Entry));
      context.AppendText(translator.Translate(node.Sequence));
      context.AppendText(translator.Translate(context, node, NodeSection.Exit));
    }

    public virtual void Visit(SqlOpenCursor node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlOrder node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        if (!SqlExpression.IsNull(node.Expression))
          node.Expression.AcceptVisitor(this);
        else if (node.Position > 0)
          context.AppendText(node.Position.ToString());
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlParameterRef node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlQueryRef node)
    {
      if (context.AliasProvider.Enabled)
        context.AliasProvider.Substitute(node);
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, TableSection.Entry));
        node.Query.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, TableSection.Exit));
        context.AppendText(translator.Translate(context, node, TableSection.AliasDeclaration));
      }
    }

    public virtual void Visit(SqlQueryExpression node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, QueryExpressionSection.Entry));
        context.AppendText("(");
        node.Left.AcceptVisitor(this);
        context.AppendText(")");
        context.AppendText(translator.Translate(node.NodeType));
        context.AppendText(translator.Translate(context, node, QueryExpressionSection.All));
        context.AppendText("(");
        node.Right.AcceptVisitor(this);
        context.AppendText(")");
        context.AppendText(translator.Translate(context, node, QueryExpressionSection.Exit));
      }
    }

    public virtual void Visit(SqlRow node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        using (context.EnterCollection()) {
          foreach (SqlExpression item in node) {
            if (!context.IsEmpty)
              context.AppendDelimiter(translator.RowItemDelimiter);
            item.AcceptVisitor(this);
          }
        }
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlSelect node)
    {
      if ((options & SqlCompilerOptions.ForcedAliasing) > 0)
        context.AliasProvider.Enabled = true;
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, SelectSection.Entry));

        if (node.Hints.Count>0) {
          using (context.EnterCollection()) {
            foreach (SqlHint hint in node.Hints) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.HintDelimiter);
              else
                context.AppendText(translator.Translate(context, node, SelectSection.HintsEntry));
              hint.AcceptVisitor(this);
            }
            context.AppendText(translator.Translate(context, node, SelectSection.HintsExit));
          }
        }

        if (node.Columns.Count>0) {
          using (context.EnterCollection()) {
            foreach (SqlColumn item in node.Columns) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.ColumnDelimiter);
              SqlColumnRef cr = item as SqlColumnRef;
              if (!SqlExpression.IsNull(cr)) {
                cr.SqlColumn.AcceptVisitor(this);
                context.AppendText(translator.Translate(context, cr, ColumnSection.AliasDeclaration));
              }
              else
                item.AcceptVisitor(this);
            }
          }
        }

        if (node.From!=null) {
          context.AppendText(translator.Translate(context, node, SelectSection.From));
          node.From.AcceptVisitor(this);
        }

        if (!SqlExpression.IsNull(node.Where)) {
          context.AppendText(translator.Translate(context, node, SelectSection.Where));
          node.Where.AcceptVisitor(this);
        }

        if (node.GroupBy.Count>0) {
          context.AppendText(translator.Translate(context, node, SelectSection.GroupBy));
          using (context.EnterCollection()) {
            foreach (SqlColumn item in node.GroupBy) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.ColumnDelimiter);
              SqlColumnRef cr = item as SqlColumnRef;
              if (!SqlExpression.IsNull(cr))
                cr.SqlColumn.AcceptVisitor(this);
              else
                item.AcceptVisitor(this);
            }
          }
          if (!SqlExpression.IsNull(node.Having)) {
            context.AppendText(translator.Translate(context, node, SelectSection.Having));
            node.Having.AcceptVisitor(this);
          }
        }

        if (node.OrderBy.Count>0) {
          context.AppendText(translator.Translate(context, node, SelectSection.OrderBy));
          using (context.EnterCollection()) {
            foreach (SqlOrder item in node.OrderBy) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.ColumnDelimiter);
              item.AcceptVisitor(this);
            }
          }
        }

        context.AppendText(translator.Translate(context, node, SelectSection.Exit));
      }
      if (context.AliasProvider.Enabled)
        context.AliasProvider.Enabled = false;
    }

    public virtual void Visit(SqlStatementBlock node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        using (context.EnterCollection()) {
          foreach (SqlStatement item in node) {
            item.AcceptVisitor(this);
            context.AppendDelimiter(translator.BatchStatementDelimiter, DelimiterType.Column);
          }
        }
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlSubQuery node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Query.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlTableColumn node)
    {
      if (context.AliasProvider.Enabled)
        context.AliasProvider.Substitute(node.SqlTable);
      context.AppendText(translator.Translate(context, node, NodeSection.Entry));
    }

    public virtual void Visit(SqlTableLockHint node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlTableRef node)
    {
      if (context.AliasProvider.Enabled)
        context.AliasProvider.Substitute(node);
      context.AppendText(
        translator.Translate(context, node, TableSection.Entry)+
        translator.Translate(context, node, TableSection.AliasDeclaration));
    }

    public virtual void Visit(SqlTrim node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, TrimSection.Entry));
        context.AppendText(translator.Translate(node.TrimType));
        if (!SqlExpression.IsNull(node.Pattern))
          node.Pattern.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, TrimSection.From));
        node.Expression.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, TrimSection.Exit));
      }
    }

    public virtual void Visit(SqlTableScanHint node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlUnary node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Operand.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlUpdate node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, UpdateSection.Entry));

        if (node.Update==null)
          throw new SqlCompilerException(Strings.ExTablePropertyIsNotSet);
        node.Update.AcceptVisitor(this);

        context.AppendText(translator.Translate(context, node, UpdateSection.Set));

        using (context.EnterCollection()) {
          foreach (ISqlLValue item in node.Values.Keys) {
            if (!context.IsEmpty)
              context.AppendDelimiter(translator.ColumnDelimiter);
            SqlTableColumn tc = item as SqlTableColumn;
            if (!SqlExpression.IsNull(tc) && tc.SqlTable!=node.Update)
              throw new SqlCompilerException(string.Format(Strings.ExUnboundColumn, tc.Name));
            item.AcceptVisitor(this);
            context.AppendText(translator.Translate(SqlNodeType.Equals));
            SqlExpression value = node.Values[item];
            value.AcceptVisitor(this);
          }
        }

        if (node.From!=null) {
          context.AppendText(translator.Translate(context, node, UpdateSection.From));
          node.From.AcceptVisitor(this);
        }

        if (!SqlExpression.IsNull(node.Where)) {
          context.AppendText(translator.Translate(context, node, UpdateSection.Where));
          node.Where.AcceptVisitor(this);
        }

        context.AppendText(translator.Translate(context, node, UpdateSection.Exit));
      }
    }

    public virtual void Visit(SqlUsePlanHint node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlUserColumn node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, NodeSection.Entry));
        node.Expression.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, NodeSection.Exit));
      }
    }

    public virtual void Visit(SqlUserFunctionCall node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, FunctionCallSection.Entry, -1));
        if (node.Arguments.Count>0) {
          using (context.EnterCollection()) {
            int argumentPosition = 0;
            foreach (SqlExpression item in node.Arguments) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.Translate(context, node, FunctionCallSection.ArgumentDelimiter, argumentPosition++));
              item.AcceptVisitor(this);
            }
          }
        }
        context.AppendText(translator.Translate(context, node, FunctionCallSection.Exit, -1));
      }
    }

    public virtual void Visit(SqlVariable node)
    {
      context.AppendText(translator.Translate(context, node));
    }

    public virtual void Visit(SqlWhile node)
    {
      using (context.EnterNode(node)) {
        context.AppendText(translator.Translate(context, node, WhileSection.Entry));
        node.Condition.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, WhileSection.Statement));
        node.Statement.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, node, WhileSection.Exit));
      }
    }

    private void Visit(TableColumn column)
    {
      context.AppendText(translator.Translate(context, column, TableColumnSection.Entry));
      if (SqlExpression.IsNull(column.Expression)) {
        if (column.Domain==null)
          ArgumentValidator.EnsureArgumentNotNull(column.DataType, "DataType");
        context.AppendText(translator.Translate(context, column, TableColumnSection.Type));
      }
      if (!SqlExpression.IsNull(column.DefaultValue)) {
        context.AppendText(translator.Translate(context, column, TableColumnSection.DefaultValue));
        column.DefaultValue.AcceptVisitor(this);
      }
      else if (column.SequenceDescriptor!=null) {
        context.AppendText(translator.Translate(context, column, TableColumnSection.GeneratedEntry));
        context.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.StartValue));
        context.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.Increment));
        context.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.MaxValue));
        context.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.MinValue));
        context.AppendText(translator.Translate(context, column.SequenceDescriptor, SequenceDescriptorSection.IsCyclic));
        context.AppendText(translator.Translate(context, column, TableColumnSection.GeneratedExit));
      }
      else if (!SqlExpression.IsNull(column.Expression)) {
        context.AppendText(translator.Translate(context, column, TableColumnSection.GenerationExpressionEntry));
        column.Expression.AcceptVisitor(this);
        context.AppendText(translator.Translate(context, column, TableColumnSection.GenerationExpressionExit));
      }
      if (!column.IsNullable)
        context.AppendText(translator.Translate(context, column, TableColumnSection.NotNull));
      context.AppendText(translator.Translate(context, column, TableColumnSection.Exit));
    }
    
    private void Visit(TableConstraint constraint)
    {
      context.AppendText(translator.Translate(context, constraint, ConstraintSection.Entry));
      if (constraint is CheckConstraint) {
        context.AppendText(translator.Translate(context, constraint, ConstraintSection.Check));
        ((CheckConstraint)constraint).Condition.AcceptVisitor(this);
      }
      else if (constraint is UniqueConstraint) {
        if (constraint is PrimaryKey)
          context.AppendText(translator.Translate(context, constraint, ConstraintSection.PrimaryKey));
        else
          context.AppendText(translator.Translate(context, constraint, ConstraintSection.Unique));
        if (((UniqueConstraint)constraint).Columns.Count>0)
          using (context.EnterCollection()) {
            foreach (TableColumn tc in ((UniqueConstraint)constraint).Columns) {
              if (!context.IsEmpty)
                context.AppendDelimiter(translator.ColumnDelimiter);
              context.AppendText(
                translator.Translate(context, tc, TableColumnSection.Entry));
            }
          }
      }
      else if (constraint is ForeignKey) {
        context.AppendText(translator.Translate(context, constraint, ConstraintSection.ForeignKey));
        ForeignKey fk = constraint as ForeignKey;
        if (fk.ReferencedColumns.Count==0)
          throw new SqlCompilerException(Strings.ExReferencedColumnsCountCantBeLessThenOne);
        if (fk.Columns.Count==0)
          throw new SqlCompilerException(Strings.ExReferencingColumnsCountCantBeLessThenOne);
        using (context.EnterCollection()) {
          foreach (TableColumn tc in fk.Columns) {
            if (!context.IsEmpty)
              context.AppendDelimiter(translator.ColumnDelimiter);
            context.AppendText(translator.Translate(context, tc, TableColumnSection.Entry));
          }
        }
        context.AppendText(translator.Translate(context, constraint, ConstraintSection.ReferencedColumns));
        using (context.EnterCollection()) {
          foreach (TableColumn tc in fk.ReferencedColumns) {
            if (!context.IsEmpty)
              context.AppendDelimiter(translator.ColumnDelimiter);
            context.AppendText(
              translator.Translate(context, tc, TableColumnSection.Entry));
          }
        }
      }
      context.AppendText(translator.Translate(context, constraint, ConstraintSection.Exit));
    }

    #endregion

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="driver">The driver.</param>
    protected internal SqlCompiler(SqlDriver driver)
    {
      ArgumentValidator.EnsureArgumentNotNull(driver, "driver");
      translator = driver.Translator;
    }
  }
}