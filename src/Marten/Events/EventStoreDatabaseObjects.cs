using System;
using System.IO;
using Baseline;
using Marten.Generation;
using Marten.Schema;
using Marten.Services;
using Marten.Util;

namespace Marten.Events
{
    public class EventStoreDatabaseObjects : IDocumentSchemaObjects
    {
        private readonly EventGraph _parent;
        private bool _checkedSchema;
        private readonly object _locker = new object();

        public EventStoreDatabaseObjects(EventGraph parent)
        {
            _parent = parent;
        }

        public void GenerateSchemaObjectsIfNecessary(AutoCreate autoCreateSchemaObjectsMode, IDocumentSchema schema, SchemaPatch patch)
        {
            if (_checkedSchema) return;

            _checkedSchema = true;

            var tableExists = schema.DbObjects.TableExists(_parent.Table);

            if (tableExists) return;

            if (autoCreateSchemaObjectsMode == AutoCreate.None)
            {
                throw new InvalidOperationException(
                    "The EventStore schema objects do not exist and the AutoCreateSchemaObjects is configured as " +
                    autoCreateSchemaObjectsMode);
            }

            lock (_locker)
            {
                if (!schema.DbObjects.TableExists(_parent.Table))
                {
                    var writer = new StringWriter();

                    writeBasicTables(schema, writer);

                    patch.Updates.Apply(this, writer.ToString());
                }
            }
        }

        public override string ToString()
        {
            return "Event Store";
        }

        private void writeBasicTables(IDocumentSchema schema, StringWriter writer)
        {
            var schemaName = _parent.DatabaseSchemaName;

            writer.WriteSql(schemaName, "mt_stream");
        }

        public void WriteSchemaObjects(IDocumentSchema schema, StringWriter writer)
        {
            writeBasicTables(schema, writer);

            if (schema.StoreOptions.OwnerName.IsNotEmpty())
            {
                var ddl = createOwnershipDDL(schema.StoreOptions);
                writer.WriteLine();
                writer.WriteLine(ddl);
            }
        }

        public void RemoveSchemaObjects(IManagedConnection connection)
        {
            var sql = $"drop table if exists {_parent.DatabaseSchemaName}.mt_streams cascade;drop table if exists {_parent.DatabaseSchemaName}.mt_events cascade;";

            connection.Execute(cmd => cmd.Sql(sql).ExecuteNonQuery());
        }

        public void ResetSchemaExistenceChecks()
        {
            _checkedSchema = false;
        }

        public TableDefinition StorageTable()
        {
            throw new NotSupportedException();
        }

        public void WritePatch(IDocumentSchema schema, SchemaPatch patch)
        {
            var tableExists = schema.DbObjects.TableExists(_parent.Table);

            if (tableExists) return;

            patch.Updates.Apply(this, SchemaBuilder.GetSqlScript(_parent.DatabaseSchemaName, "mt_stream"));

            patch.Rollbacks.Drop(this, new TableName(schema.Events.DatabaseSchemaName, "mt_streams"));

            writeOwnership(schema.StoreOptions, patch);
        }

        private void writeOwnership(StoreOptions options, SchemaPatch patch)
        {
            if (options.OwnerName.IsEmpty()) return;

            var toOwnershipDdl = createOwnershipDDL(options);
            patch.Updates.Apply(this, toOwnershipDdl);
        }

        private static string createOwnershipDDL(StoreOptions options)
        {
            var toOwnershipDdl =
                $@"
ALTER TABLE {options.Events.DatabaseSchemaName}.mt_streams OWNER TO ""{options.OwnerName}"";
ALTER TABLE {options.Events.DatabaseSchemaName}.mt_events OWNER TO ""{options.OwnerName}"";
ALTER TABLE {options.Events.DatabaseSchemaName}.mt_event_progression OWNER TO ""{options.OwnerName}"";
ALTER SEQUENCE {options.Events.DatabaseSchemaName}.mt_events_sequence OWNER TO ""{options.OwnerName}"";
ALTER FUNCTION {options.Events.DatabaseSchemaName}.mt_append_event(uuid, varchar, uuid[], varchar[], jsonb[]) OWNER TO ""{options.OwnerName}"";
ALTER FUNCTION {options.Events.DatabaseSchemaName}.mt_mark_event_progression(varchar, bigint) OWNER TO ""{options.OwnerName}"";
            ".Trim();
            return toOwnershipDdl;
        }

        public string Name { get; } = "eventstore";
    }
}