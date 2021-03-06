﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using Utilities.Extensions;
using Utilities.SqlHelpers.Mapper;

namespace Utilities.SqlHelpers
{
    public class QueryBulk : IDisposable
    {
        private readonly string connectionString;
        private readonly string tableName;

        public QueryBulk(string connectionString, string tableName)
        {
            this.connectionString = connectionString;
            this.tableName = tableName;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void BulkInsert<T>(string connection, string tableName, IList<T> list)
        {
            Contract.Requires(list != null);

            using (var sqlConnexion = new SqlConnection(connectionString))
            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.BatchSize = list.Count;
                bulkCopy.DestinationTableName = tableName;

                if (sqlConnexion.State == ConnectionState.Closed) sqlConnexion.Open();

                DataTable table = CreateDataTable(list, bulkCopy);
                bulkCopy.WriteToServer(table);
            }
        }

        public DataTable CreateDataTable<T>(string tableName, IEnumerable<T> data, SqlBulkCopy bulkCopy)
        {
            Contract.Requires(data != null);
            Contract.Requires(bulkCopy != null && bulkCopy.ColumnMappings != null);

            var dataTable = new DataTable(tableName);
            IEnumerable<PropertyInfo> properties = typeof (T).GetPropertiesInfoWithInterfaces();

            foreach (PropertyInfo pi in properties)
            {
                dataTable.Columns.Add(pi.Name, pi.PropertyType);
                bulkCopy.ColumnMappings.Add(pi.Name, pi.Name);
            }

            if (data != null) foreach (T t in data) dataTable.Rows.Add(t);

            return dataTable;
        }

        public void DynamicInsertWithDataBaseTypes(IEnumerable<dynamic> data)
        {
            Contract.Requires(data != null);

            InsertWithDataBaseTypes(data.Cast<IDictionary<string, object>>());
        }

        public void FastInsert(IEnumerable<IDictionary<string, object>> data)
        {
            Contract.Requires(data != null);

            DataTable dataTable = null;

            using (var sqlConnexion = new SqlConnection(connectionString))
            using (var bulkCopy = new SqlBulkCopy(sqlConnexion))
            {
                bulkCopy.BatchSize = 5000;
                bulkCopy.DestinationTableName = tableName;

                if (sqlConnexion.State == ConnectionState.Closed)
                    sqlConnexion.Open();

                foreach (var record in data)
                {
                    if (dataTable == null && bulkCopy != null && bulkCopy.ColumnMappings != null)
                        dataTable = FastCreateDataTable(tableName, record.Keys, bulkCopy);
                    if (dataTable != null) dataTable.Rows.Add(record.Values.ToArray());
                }

                bulkCopy.WriteToServer(dataTable);
            }
        }

        public DataTable GetTable(string tableName)
        {
            Contract.Requires(!string.IsNullOrEmpty(tableName));

            var restrictions = new string[4];
            restrictions[3] = tableName;

            return GetSchema("TABLES", restrictions);
        }

        public void InsertWithDataBaseTypes(IEnumerable<IDictionary<string, object>> data)
        {
            Contract.Requires(data != null);

            int count = 0;
            DataTable dataTable = null;

            using (var sqlConnexion = new SqlConnection(connectionString))
            using (var bulkCopy = new SqlBulkCopy(sqlConnexion))
            {
                bulkCopy.DestinationTableName = tableName;

                if (sqlConnexion.State == ConnectionState.Closed) sqlConnexion.Open();

                foreach (var record in data)
                {
                    if (count == 0 && bulkCopy != null && bulkCopy.ColumnMappings != null && record != null)
                        dataTable = CreateDataTable(tableName, record.Keys, bulkCopy);
                    if (dataTable != null && dataTable.Rows != null && record != null)
                        dataTable.Rows.Add(record.Values.ToArray());

                    if (++count%5000 == 0)
                    {
                        bulkCopy.WriteToServer(dataTable);
                        if (dataTable != null) dataTable.Clear();
                    }
                }

                if (dataTable != null && dataTable.Rows.Count > 0)
                    bulkCopy.WriteToServer(dataTable);
            }
        }

        public void InsertWithReflectionDataReader<T>(IEnumerable<T> data)
        {
            Contract.Requires(data != null);

            using (var sqlConnexion = new SqlConnection(connectionString))
            using (var bulkCopy = new SqlBulkCopy(sqlConnexion))
            using (var dataReader = new ObjectDataReader<T>(data.ToArray()))
            {
                bulkCopy.BatchSize = 5000;
                bulkCopy.DestinationTableName = tableName;

                if (sqlConnexion.State == ConnectionState.Closed)
                    sqlConnexion.Open();

                bulkCopy.WriteToServer(dataReader);
            }
        }

        public void InsertWithReflectionDataTable<T>(IEnumerable<T> data)
        {
            Contract.Requires(data != null);

            using (var sqlConnexion = new SqlConnection(connectionString))
            using (var bulkCopy = new SqlBulkCopy(sqlConnexion))
            {
                bulkCopy.BatchSize = 5000;
                bulkCopy.DestinationTableName = tableName;

                if (sqlConnexion.State == ConnectionState.Closed)
                    sqlConnexion.Open();

                if (bulkCopy != null && bulkCopy.ColumnMappings != null)
                {
                    DataTable dataTable = CreateDataTable(tableName, data, bulkCopy);
                    bulkCopy.WriteToServer(dataTable);
                }
            }
        }

        protected void Dispose(bool disposing)
        {
            //if (disposing);
        }

        private static DataTable CreateDataTable<T>(IList<T> list, SqlBulkCopy bulkCopy)
        {
            var table = new DataTable();
            PropertyDescriptor[] props =
                typeof (T).GetPropertiesInfoWithInterfaces()
                    .Cast<PropertyDescriptor>()
                    .Where(propertyInfo => propertyInfo.PropertyType.Namespace.Equals("System"))
                    .ToArray();

            foreach (PropertyDescriptor propertyInfo in props)
            {
                bulkCopy.ColumnMappings.Add(propertyInfo.Name, propertyInfo.Name);
                table.Columns.Add(propertyInfo.Name,
                    Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType);
            }

            var values = new object[props.Length];
            foreach (T item in list)
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] = props[i].GetValue(item);
                table.Rows.Add(values);
            }

            return table;
        }

        private DataTable CreateDataTable(string tableName, ICollection<string> keys, SqlBulkCopy bulkCopy)
        {
            Contract.Requires(keys != null);
            Contract.Requires(bulkCopy != null && bulkCopy.ColumnMappings != null);

            IDictionary<string, Type> schema = GetColumns(tableName).ToDictionary(d => d.Key, d => d.Value);
            var dataTable = new DataTable(tableName);

            foreach (string key in keys)
            {
                if (schema.ContainsKey(key))
                {
                    dataTable.Columns.Add(key, schema.TryGetValue(key));
                    if (bulkCopy != null && bulkCopy.ColumnMappings != null) bulkCopy.ColumnMappings.Add(key, key);
                }
                else
                    dataTable.Columns.Add(Guid.NewGuid().ToString("N"));
            }

            return dataTable;
        }

        private DataTable FastCreateDataTable(string tableName, ICollection<string> keys, SqlBulkCopy bulkCopy)
        {
            Contract.Requires(keys != null);
            Contract.Requires(bulkCopy != null && bulkCopy.ColumnMappings != null);

            var dataTable = new DataTable(tableName);

            foreach (string key in keys)
            {
                dataTable.Columns.Add(key);
                bulkCopy.ColumnMappings.Add(key, key);
            }

            return dataTable;
        }

        private IEnumerable<KeyValuePair<string, Type>> GetColumns(string tableName)
        {
            DataTable dt = GetTable(tableName);

            foreach (DataRow row in dt.Rows)
                if ((row["TABLE_NAME"] as string) == tableName)
                    yield return
                        new KeyValuePair<string, Type>(row["COLUMN_NAME"] as string,
                            DbTypeLookup.GetSqlDbType(row["DATA_TYPE"] as string).ToClrType());
        }

        private DataTable GetSchema(string collectionName, params string[] constraints)
        {
            using (var sqlConnexion = new SqlConnection(connectionString))
            {
                if (sqlConnexion.State == ConnectionState.Closed)
                    sqlConnexion.Open();

                return sqlConnexion.GetSchema(collectionName, constraints);
            }
        }
    }
}